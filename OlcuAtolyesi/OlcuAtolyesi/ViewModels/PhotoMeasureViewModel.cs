using OlcuAtolyesi.Models;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.ViewModels;

public sealed class PhotoMeasureViewModel : ObservableObject
{
    public const int PointM1 = 0;
    public const int PointM2 = 1;
    public const int PointR1 = 2;
    public const int PointR2 = 3;
    private const int MaxUndoDepth = 30;

    public static readonly ImageSize DemoImageSize = new(1000, 600);
    public static readonly ImageSegment DemoReferenceSegment =
        new(new ImagePoint(200, 500), new ImagePoint(800, 500));
    public static readonly ImageSegment DefaultMeasurementSegment =
        new(new ImagePoint(250, 205), new ImagePoint(700, 205));

    private readonly record struct UndoSnapshot(
        ImageSegment ReferenceSegment,
        ImageSegment Measurement,
        LengthValue? KnownLength);

    private readonly LinkedList<UndoSnapshot> _history = new();
    private readonly ImageSegment _defaultReference;
    private readonly ImageSegment _defaultMeasurement;
    private ImageSegment _referenceSegment;
    private ImageSegment _measurement;
    private LengthValue? _knownLength;
    private ReferenceScale? _referenceScale;
    private LengthUnit _unit;

    public PhotoMeasureViewModel(LengthUnit unit)
        : this(unit, DemoImageSize, isRealPhoto: false)
    {
    }

    public PhotoMeasureViewModel(LengthUnit unit, ImageSize imageSize, bool isRealPhoto = true)
    {
        _unit = unit;
        ImageSize = imageSize;
        IsRealPhoto = isRealPhoto;

        if (!isRealPhoto && imageSize.Equals(DemoImageSize))
        {
            _defaultReference = DemoReferenceSegment;
            _defaultMeasurement = DefaultMeasurementSegment;
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

        _referenceSegment = _defaultReference;
        _measurement = _defaultMeasurement;
    }

    public ImageSize ImageSize { get; }

    public bool IsRealPhoto { get; }

    public ImageSegment ReferenceSegment
    {
        get => _referenceSegment;
        private set
        {
            if (SetProperty(ref _referenceSegment, value))
            {
                OnPropertyChanged(nameof(R1));
                OnPropertyChanged(nameof(R2));
                RebuildReferenceScale();
            }
        }
    }

    public ImagePoint R1 => _referenceSegment.Start;

    public ImagePoint R2 => _referenceSegment.End;

    public ImageSegment Measurement
    {
        get => _measurement;
        private set
        {
            if (SetProperty(ref _measurement, value))
            {
                OnPropertyChanged(nameof(M1));
                OnPropertyChanged(nameof(M2));
                OnPropertyChanged(nameof(Result));
                OnPropertyChanged(nameof(ResultText));
            }
        }
    }

    public ImagePoint M1 => _measurement.Start;

    public ImagePoint M2 => _measurement.End;

    public LengthValue? KnownLength => _knownLength;

    public ReferenceScale? ReferenceScale
    {
        get => _referenceScale;
        private set
        {
            if (SetProperty(ref _referenceScale, value))
            {
                OnPropertyChanged(nameof(HasReference));
                OnPropertyChanged(nameof(ReferenceText));
                OnPropertyChanged(nameof(Result));
                OnPropertyChanged(nameof(ResultText));
            }
        }
    }

    public LengthUnit Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                if (_knownLength is { } known)
                {
                    _knownLength = known.ConvertTo(value);
                    RebuildReferenceScale();
                }

                OnPropertyChanged(nameof(KnownLength));
                OnPropertyChanged(nameof(ReferenceText));
                OnPropertyChanged(nameof(Result));
                OnPropertyChanged(nameof(ResultText));
            }
        }
    }

    public bool HasReference => ReferenceScale is not null;

    public string ReferenceText => ReferenceScale is null
        ? "REFERANS (R1–R2)"
        : $"REF  {ReferenceScale.KnownLength.Format()}";

    public LengthValue? Result => ReferenceScale is null
        ? null
        : MeasurementGeometry.MeasureLength(Measurement, ReferenceScale, Unit);

    public string ResultText => Result?.Format() ?? "—";

    public ReferenceLengthParseResult ApplyReference(string? text)
    {
        var parse = ReferenceLengthParser.ParsePositive(text, Unit);
        if (!parse.IsValid || parse.Length is not { } length)
        {
            return parse;
        }

        if (_referenceSegment.ImageLength <= 0.0001d)
        {
            ReferenceScale = null;
            return ReferenceLengthParseResult.Failure("Referans noktaları R1 ve R2 birbirinden farklı olmalıdır.");
        }

        _knownLength = length;
        OnPropertyChanged(nameof(KnownLength));
        ReferenceScale = new ReferenceScale(_referenceSegment, length);
        return parse;
    }

    public void CaptureUndoState()
    {
        if (_history.Count >= MaxUndoDepth)
        {
            _history.RemoveFirst();
        }

        _history.AddLast(new UndoSnapshot(_referenceSegment, _measurement, _knownLength));
    }

    public void CancelLastUndoCapture()
    {
        if (_history.Last is { } last)
        {
            _history.RemoveLast();
            RestoreSnapshot(last.Value);
        }
    }

    public bool Undo()
    {
        if (_history.Last is not { } last)
        {
            return false;
        }

        _history.RemoveLast();
        RestoreSnapshot(last.Value);
        return true;
    }

    public void MovePoint(int pointIndex, ImagePoint point)
    {
        var clamped = ClampPoint(point);

        switch (pointIndex)
        {
            case PointM1:
                Measurement = new ImageSegment(clamped, Measurement.End);
                break;
            case PointM2:
                Measurement = new ImageSegment(Measurement.Start, clamped);
                break;
            case PointR1:
                ReferenceSegment = new ImageSegment(clamped, ReferenceSegment.End);
                break;
            case PointR2:
                ReferenceSegment = new ImageSegment(ReferenceSegment.Start, clamped);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
        }
    }

    public void MoveReferencePoint(int referenceEndpointIndex, ImagePoint point) =>
        MovePoint(referenceEndpointIndex == 0 ? PointR1 : PointR2, point);

    public void MoveMeasurementPoint(int measurementEndpointIndex, ImagePoint point) =>
        MovePoint(measurementEndpointIndex == 0 ? PointM1 : PointM2, point);

    public void LoadSavedState(
        ImageSegment? referenceSegment,
        LengthValue? referenceLength,
        ImageSegment? measurementSegment,
        LengthUnit? displayUnit = null)
    {
        _history.Clear();

        if (displayUnit is { } unit)
        {
            _unit = unit;
        }

        if (referenceSegment is { } refSeg)
        {
            _referenceSegment = new ImageSegment(ClampPoint(refSeg.Start), ClampPoint(refSeg.End));
        }

        if (measurementSegment is { } measSeg)
        {
            _measurement = new ImageSegment(ClampPoint(measSeg.Start), ClampPoint(measSeg.End));
        }

        if (referenceLength is { } known && double.IsFinite(known.Value) && known.Value > 0d)
        {
            _knownLength = known.ConvertTo(_unit);
        }
        else
        {
            _knownLength = null;
        }

        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(KnownLength));
        OnPropertyChanged(nameof(ReferenceSegment));
        OnPropertyChanged(nameof(R1));
        OnPropertyChanged(nameof(R2));
        OnPropertyChanged(nameof(Measurement));
        OnPropertyChanged(nameof(M1));
        OnPropertyChanged(nameof(M2));
        RebuildReferenceScale();
    }

    public void Reset()
    {
        _history.Clear();
        _referenceSegment = _defaultReference;
        _measurement = _defaultMeasurement;
        OnPropertyChanged(nameof(ReferenceSegment));
        OnPropertyChanged(nameof(R1));
        OnPropertyChanged(nameof(R2));
        OnPropertyChanged(nameof(Measurement));
        OnPropertyChanged(nameof(M1));
        OnPropertyChanged(nameof(M2));
        RebuildReferenceScale();
    }

    private ImagePoint ClampPoint(ImagePoint point) =>
        new(
            Math.Clamp(point.X, 0d, ImageSize.Width),
            Math.Clamp(point.Y, 0d, ImageSize.Height));

    private void RebuildReferenceScale()
    {
        if (_knownLength is { } length && _referenceSegment.ImageLength > 0.0001d)
        {
            ReferenceScale = new ReferenceScale(_referenceSegment, length.ConvertTo(_unit));
        }
        else
        {
            ReferenceScale = null;
        }
    }

    private void RestoreSnapshot(UndoSnapshot snapshot)
    {
        _knownLength = snapshot.KnownLength?.ConvertTo(_unit);
        _referenceSegment = snapshot.ReferenceSegment;
        _measurement = snapshot.Measurement;
        OnPropertyChanged(nameof(KnownLength));
        OnPropertyChanged(nameof(ReferenceSegment));
        OnPropertyChanged(nameof(R1));
        OnPropertyChanged(nameof(R2));
        OnPropertyChanged(nameof(Measurement));
        OnPropertyChanged(nameof(M1));
        OnPropertyChanged(nameof(M2));
        RebuildReferenceScale();
    }
}
