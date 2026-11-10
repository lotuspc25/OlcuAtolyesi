namespace OlcuAtolyesi.Core.Geometry;

public readonly record struct ImagePoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public double DistanceTo(ImagePoint other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct ImageSegment(ImagePoint Start, ImagePoint End)
{
    public bool IsFinite => Start.IsFinite && End.IsFinite;

    public double ImageLength => Start.DistanceTo(End);

    public ImagePoint Midpoint => new((Start.X + End.X) / 2d, (Start.Y + End.Y) / 2d);
}

public readonly record struct ImageSize(double Width, double Height)
{
    public bool IsValid => double.IsFinite(Width) && double.IsFinite(Height) && Width > 0d && Height > 0d;
}
