namespace OlcuAtolyesi.Models;

public enum MeasurementKind
{
    Length,
    Angle,
    Diameter,
    Radius,
    Perimeter,
    Area,
    CameraDistance,
    Height
}

/// <summary>
/// Görsel üzerindeki bilinen fiziksel uzunluğu tanımlar. Görsel koordinatı,
/// fiziksel birim ve ekranda gösterilecek etiketten bağımsızdır.
/// </summary>
public sealed record ReferenceScale
{
    public ReferenceScale(ImageSegment imageSegment, LengthValue knownLength)
    {
        if (!imageSegment.IsFinite || imageSegment.ImageLength <= 0d)
        {
            throw new ArgumentException("Referans çizgisi iki farklı, geçerli görsel noktası içermelidir.", nameof(imageSegment));
        }

        if (!knownLength.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(knownLength), "Referans uzunluğu sıfırdan büyük olmalıdır.");
        }

        ImageSegment = imageSegment;
        KnownLength = knownLength;
    }

    public ImageSegment ImageSegment { get; }

    public LengthValue KnownLength { get; }

    public double MillimetersPerImageUnit => KnownLength.Millimeters / ImageSegment.ImageLength;

    public LengthValue Measure(ImageSegment segment, LengthUnit resultUnit)
    {
        if (!segment.IsFinite)
        {
            throw new ArgumentException("Ölçüm çizgisi geçerli görsel noktaları içermelidir.", nameof(segment));
        }

        var millimeters = segment.ImageLength * MillimetersPerImageUnit;
        return new LengthValue(millimeters, LengthUnit.Millimeter).ConvertTo(resultUnit);
    }
}

public sealed record ProjectMeasurement
{
    public ProjectMeasurement(
        Guid id,
        string name,
        MeasurementKind kind,
        LengthValue? length = null,
        double? angleDegrees = null,
        ImageSegment? imageSegment = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Ölçüm kimliği boş olamaz.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Ölçüm adı boş olamaz.", nameof(name));
        }

        if (angleDegrees is not null && (!double.IsFinite(angleDegrees.Value) || angleDegrees.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        Length = length;
        AngleDegrees = angleDegrees;
        ImageSegment = imageSegment;
    }

    public Guid Id { get; }

    public string Name { get; }

    public MeasurementKind Kind { get; }

    public LengthValue? Length { get; }

    public double? AngleDegrees { get; }

    public ImageSegment? ImageSegment { get; }
}
