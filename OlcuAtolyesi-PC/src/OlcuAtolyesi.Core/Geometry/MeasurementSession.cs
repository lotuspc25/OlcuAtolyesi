namespace OlcuAtolyesi.Core.Geometry;

public enum MeasurePoint
{
    M1 = 0,
    M2 = 1,
    R1 = 2,
    R2 = 3
}

/// <summary>
/// Bir fotoğraf üzerindeki R1/R2 referans ve M1/M2 ölçüm noktalarını fotoğraf piksel koordinatlarında tutar;
/// referans, birim, geri alma ve nokta seçim (araç) durumunu yönetir. Görünüm dönüşümlerinden bağımsızdır.
/// </summary>
public sealed class MeasurementSession
{
    public static readonly ImageSize DemoImageSize = new(1000d, 600d);
    private const int MaxUndoDepth = 50;
    private const double MinimumReferencePixels = 0.0001d;

    private readonly record struct Snapshot(ImageSegment Reference, ImageSegment Measurement);

    private readonly LinkedList<Snapshot> _history = new();
    private readonly ImageSegment _defaultReference;
    private readonly ImageSegment _defaultMeasurement;
    private ImageSegment _reference;
    private ImageSegment _measurement;
    private LengthValue? _knownLength;
    private LengthUnit _unit;
    private MeasurePoint? _armedPoint;
    private MeasurePoint? _chainNext;

    public MeasurementSession(ImageSize imageSize, LengthUnit unit, bool useDemoDefaults = false)
    {
        if (!imageSize.IsValid)
        {
            throw new ArgumentException("Fotoğraf boyutu sıfırdan büyük ve sonlu olmalıdır.", nameof(imageSize));
        }

        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }

        ImageSize = imageSize;
        _unit = unit;

        if (useDemoDefaults && imageSize == DemoImageSize)
        {
            _defaultReference = new ImageSegment(new ImagePoint(200d, 500d), new ImagePoint(800d, 500d));
            _defaultMeasurement = new ImageSegment(new ImagePoint(250d, 205d), new ImagePoint(700d, 205d));
        }
        else
        {
            _defaultReference = new ImageSegment(
                new ImagePoint(Math.Round(imageSize.Width * 0.20d, 1), Math.Round(imageSize.Height * 0.70d, 1)),
                new ImagePoint(Math.Round(imageSize.Width * 0.80d, 1), Math.Round(imageSize.Height * 0.70d, 1)));
            _defaultMeasurement = new ImageSegment(
                new ImagePoint(Math.Round(imageSize.Width * 0.25d, 1), Math.Round(imageSize.Height * 0.35d, 1)),
                new ImagePoint(Math.Round(imageSize.Width * 0.75d, 1), Math.Round(imageSize.Height * 0.35d, 1)));
        }

        _reference = _defaultReference;
        _measurement = _defaultMeasurement;
    }

    public event EventHandler? Changed;

    public ImageSize ImageSize { get; }

    public ImageSegment ReferenceSegment => _reference;

    public ImageSegment Measurement => _measurement;

    public ImagePoint R1 => _reference.Start;

    public ImagePoint R2 => _reference.End;

    public ImagePoint M1 => _measurement.Start;

    public ImagePoint M2 => _measurement.End;

    public LengthValue? KnownLength => _knownLength;

    public MeasurePoint? ArmedPoint => _armedPoint;

    public LengthUnit Unit
    {
        get => _unit;
        set
        {
            if (!Enum.IsDefined(value) || value == _unit)
            {
                return;
            }

            _unit = value;
            _knownLength = _knownLength?.ConvertTo(value);
            RaiseChanged();
        }
    }

    public ReferenceScale? ReferenceScale =>
        _knownLength is { IsPositive: true } known && _reference.IsFinite && _reference.ImageLength > MinimumReferencePixels
            ? new ReferenceScale(_reference, known)
            : null;

    public LengthValue? Result => ReferenceScale?.Measure(_measurement, _unit);

    public bool CanUndo => _history.Any(snapshot => snapshot != CurrentSnapshot());

    public ImagePoint GetPoint(MeasurePoint point) => point switch
    {
        MeasurePoint.M1 => _measurement.Start,
        MeasurePoint.M2 => _measurement.End,
        MeasurePoint.R1 => _reference.Start,
        MeasurePoint.R2 => _reference.End,
        _ => throw new ArgumentOutOfRangeException(nameof(point))
    };

    /// <summary>Geçerli (sıfırdan büyük, sonlu) bir uzunluk verilmezse referans uzunluğu temizlenir.</summary>
    public void SetKnownLength(LengthValue? length)
    {
        var normalized = length is { IsPositive: true } value ? value.ConvertTo(_unit) : (LengthValue?)null;
        if (Nullable.Equals(normalized, _knownLength))
        {
            return;
        }

        _knownLength = normalized;
        RaiseChanged();
    }

    /// <summary>Noktayı fotoğraf sınırları içine kırparak taşır. Geri alma durumu yakalamaz.</summary>
    public bool MovePoint(MeasurePoint point, ImagePoint target)
    {
        if (!target.IsFinite)
        {
            return false;
        }

        var clamped = new ImagePoint(
            Math.Clamp(target.X, 0d, ImageSize.Width),
            Math.Clamp(target.Y, 0d, ImageSize.Height));

        switch (point)
        {
            case MeasurePoint.M1:
                _measurement = _measurement with { Start = clamped };
                break;
            case MeasurePoint.M2:
                _measurement = _measurement with { End = clamped };
                break;
            case MeasurePoint.R1:
                _reference = _reference with { Start = clamped };
                break;
            case MeasurePoint.R2:
                _reference = _reference with { End = clamped };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(point));
        }

        RaiseChanged();
        return true;
    }

    public void CaptureUndo()
    {
        if (_history.Count >= MaxUndoDepth)
        {
            _history.RemoveFirst();
        }

        _history.AddLast(CurrentSnapshot());
    }

    /// <summary>Geçerli durumdan farklı en son yakalanan duruma döner.</summary>
    public bool Undo()
    {
        while (_history.Last is { } last)
        {
            _history.RemoveLast();
            if (last.Value == CurrentSnapshot())
            {
                continue;
            }

            _reference = last.Value.Reference;
            _measurement = last.Value.Measurement;
            RaiseChanged();
            return true;
        }

        return false;
    }

    /// <summary>Noktaları varsayılan konumlara döndürür; işlem geri alınabilir.</summary>
    public void Reset()
    {
        CaptureUndo();
        _reference = _defaultReference;
        _measurement = _defaultMeasurement;
        _armedPoint = null;
        _chainNext = null;
        RaiseChanged();
    }

    /// <summary>Kaydedilmiş bir projenin R1–R2 ve M1–M2 piksel koordinatlarını oturuma yükler.</summary>
    public void RestoreSegments(ImageSegment reference, ImageSegment measurement, bool clearHistory = true)
    {
        if (!reference.IsFinite || !measurement.IsFinite)
        {
            throw new ArgumentException("Referans ve ölçüm segmentleri sonlu koordinatlar içermelidir.");
        }

        _reference = new ImageSegment(
            new ImagePoint(Math.Clamp(reference.Start.X, 0d, ImageSize.Width), Math.Clamp(reference.Start.Y, 0d, ImageSize.Height)),
            new ImagePoint(Math.Clamp(reference.End.X, 0d, ImageSize.Width), Math.Clamp(reference.End.Y, 0d, ImageSize.Height)));
        _measurement = new ImageSegment(
            new ImagePoint(Math.Clamp(measurement.Start.X, 0d, ImageSize.Width), Math.Clamp(measurement.Start.Y, 0d, ImageSize.Height)),
            new ImagePoint(Math.Clamp(measurement.End.X, 0d, ImageSize.Width), Math.Clamp(measurement.End.Y, 0d, ImageSize.Height)));
        _armedPoint = null;
        _chainNext = null;

        if (clearHistory)
        {
            _history.Clear();
        }

        RaiseChanged();
    }

    public void ArmReference() => Arm(MeasurePoint.R1, MeasurePoint.R2);

    public void ArmMeasure() => Arm(MeasurePoint.M1, MeasurePoint.M2);

    public void ArmPoint(MeasurePoint point) => Arm(point, null);

    public void CancelArm()
    {
        if (_armedPoint is null && _chainNext is null)
        {
            return;
        }

        _armedPoint = null;
        _chainNext = null;
        RaiseChanged();
    }

    /// <summary>Seçili (armlı) noktayı verilen konuma yerleştirir. Seçili nokta yoksa false döner.</summary>
    public bool BeginPlace(ImagePoint target)
    {
        if (_armedPoint is not { } armed)
        {
            return false;
        }

        CaptureUndo();
        return MovePoint(armed, target);
    }

    /// <summary>Zincirdeki bir sonraki noktaya (R1→R2, M1→M2) geçer; zincir bittiyse seçimi kapatır.</summary>
    public void AdvanceArm()
    {
        if (_armedPoint is null)
        {
            return;
        }

        _armedPoint = _chainNext;
        _chainNext = null;
        RaiseChanged();
    }

    private void Arm(MeasurePoint first, MeasurePoint? next)
    {
        _armedPoint = first;
        _chainNext = next;
        RaiseChanged();
    }

    private Snapshot CurrentSnapshot() => new(_reference, _measurement);

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
