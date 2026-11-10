namespace OlcuAtolyesi.Models;

/// <summary>Kaynak görselin kendi koordinat uzayındaki nokta.</summary>
public readonly record struct ImagePoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public double DistanceTo(ImagePoint other)
    {
        var deltaX = other.X - X;
        var deltaY = other.Y - Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

/// <summary>Ekran/görünüm koordinat uzayındaki nokta (DIP).</summary>
public readonly record struct ViewPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public double DistanceTo(ViewPoint other)
    {
        var deltaX = other.X - X;
        var deltaY = other.Y - Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

public readonly record struct ImageSize
{
    public ImageSize(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!double.IsFinite(height) || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}

public readonly record struct ViewSize
{
    public ViewSize(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!double.IsFinite(height) || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}

public readonly record struct ImageSegment(ImagePoint Start, ImagePoint End)
{
    public bool IsFinite => Start.IsFinite && End.IsFinite;

    public double ImageLength => Start.DistanceTo(End);
}

public readonly record struct ViewSegment(ViewPoint Start, ViewPoint End)
{
    public bool IsFinite => Start.IsFinite && End.IsFinite;

    public double ViewLength => Start.DistanceTo(End);
}
