using System.Globalization;

namespace OlcuAtolyesi.Core.Geometry;

public sealed record ReferenceScale
{
    public ReferenceScale(ImageSegment segment, LengthValue knownLength)
    {
        if (!segment.IsFinite || segment.ImageLength <= 0.0001d)
        {
            throw new ArgumentException("Referans noktaları R1 ve R2 birbirinden farklı olmalıdır.", nameof(segment));
        }

        if (!knownLength.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(knownLength), "Referans uzunluğu sıfırdan büyük olmalıdır.");
        }

        Segment = segment;
        KnownLength = knownLength;
    }

    public ImageSegment Segment { get; }

    public LengthValue KnownLength { get; }

    public double MillimetersPerPixel => KnownLength.Millimeters / Segment.ImageLength;

    public LengthValue Measure(ImageSegment measurementSegment, LengthUnit resultUnit)
    {
        if (!measurementSegment.IsFinite)
        {
            throw new ArgumentException("Ölçüm noktaları sonlu koordinatlar içermelidir.", nameof(measurementSegment));
        }

        var millimeters = measurementSegment.ImageLength * MillimetersPerPixel;
        return new LengthValue(millimeters / resultUnit.MillimetersPerUnit(), resultUnit);
    }
}

public readonly record struct ReferenceLengthParseResult(
    bool IsValid,
    LengthValue? Length,
    string? ErrorMessage)
{
    public static ReferenceLengthParseResult Success(LengthValue length) => new(true, length, null);

    public static ReferenceLengthParseResult Failure(string errorMessage) => new(false, null, errorMessage);
}

public static class ReferenceLengthParser
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static ReferenceLengthParseResult ParsePositive(string? rawText, LengthUnit unit)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return ReferenceLengthParseResult.Failure("Önce sıfırdan büyük bir referans uzunluğu gir.");
        }

        var normalized = rawText.Trim().Replace('.', ',');
        if (!double.TryParse(normalized, NumberStyles.Float, TurkishCulture, out var value) ||
            !double.IsFinite(value) ||
            value <= 0d)
        {
            return ReferenceLengthParseResult.Failure("Sıfırdan büyük, geçerli bir sayı gir (örn. 120 veya 26,15).");
        }

        return ReferenceLengthParseResult.Success(new LengthValue(value, unit));
    }
}

public static class MeasurementGeometry
{
    public static LengthValue MeasureLength(
        ImageSegment measurement,
        ReferenceScale reference,
        LengthUnit resultUnit) => reference.Measure(measurement, resultUnit);

    public static double MeasureAngleDegrees(ImagePoint first, ImagePoint vertex, ImagePoint second)
    {
        if (!first.IsFinite || !vertex.IsFinite || !second.IsFinite)
        {
            throw new ArgumentException("Açı noktaları sonlu koordinatlar içermelidir.");
        }

        var firstX = first.X - vertex.X;
        var firstY = first.Y - vertex.Y;
        var secondX = second.X - vertex.X;
        var secondY = second.Y - vertex.Y;
        var firstLength = Math.Sqrt((firstX * firstX) + (firstY * firstY));
        var secondLength = Math.Sqrt((secondX * secondX) + (secondY * secondY));

        if (firstLength <= 0d || secondLength <= 0d)
        {
            throw new ArgumentException("Açı kollarının uzunluğu sıfır olamaz.");
        }

        var cosine = ((firstX * secondX) + (firstY * secondY)) / (firstLength * secondLength);
        return Math.Acos(Math.Clamp(cosine, -1d, 1d)) * 180d / Math.PI;
    }
}
