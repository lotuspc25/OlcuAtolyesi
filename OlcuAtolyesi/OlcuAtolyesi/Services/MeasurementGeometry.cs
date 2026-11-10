using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

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
