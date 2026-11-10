namespace OlcuAtolyesi.Core.Geometry;

/// <summary>Görünüm (ekran) koordinatlarındaki bir nokta. Ölçüm matematiğinde kullanılmaz.</summary>
public readonly record struct ViewPoint(double X, double Y)
{
    public double DistanceTo(ViewPoint other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct ViewSize(double Width, double Height)
{
    public bool IsValid => double.IsFinite(Width) && double.IsFinite(Height) && Width > 0d && Height > 0d;
}

/// <summary>
/// Fotoğraf (piksel) koordinatları ile görünüm koordinatları arasındaki yakınlaştırma/kaydırma dönüşümü.
/// Yalnızca görüntüleme içindir; ölçüm noktaları her zaman fotoğraf piksel koordinatlarında saklanır.
/// </summary>
public readonly record struct ImageViewportTransform(double Scale, double OffsetX, double OffsetY)
{
    public static ImageViewportTransform Fit(ImageSize image, ViewSize view, double padding = 0d)
    {
        if (!image.IsValid || !view.IsValid)
        {
            return new ImageViewportTransform(1d, 0d, 0d);
        }

        var availableWidth = Math.Max(1d, view.Width - (2d * padding));
        var availableHeight = Math.Max(1d, view.Height - (2d * padding));
        var scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
        return new ImageViewportTransform(
            scale,
            (view.Width - (image.Width * scale)) / 2d,
            (view.Height - (image.Height * scale)) / 2d);
    }

    public ViewPoint ImageToView(ImagePoint point) =>
        new((point.X * Scale) + OffsetX, (point.Y * Scale) + OffsetY);

    public ImagePoint ViewToImage(ViewPoint point) =>
        new((point.X - OffsetX) / Scale, (point.Y - OffsetY) / Scale);

    public ImageViewportTransform PanBy(double deltaX, double deltaY) =>
        this with { OffsetX = OffsetX + deltaX, OffsetY = OffsetY + deltaY };

    /// <summary>Verilen görünüm noktasının altındaki fotoğraf noktası sabit kalacak şekilde ölçeği değiştirir.</summary>
    public ImageViewportTransform ZoomAt(ViewPoint anchor, double factor, double minScale, double maxScale)
    {
        if (!double.IsFinite(factor) || factor <= 0d)
        {
            return this;
        }

        var upper = Math.Max(minScale, maxScale);
        var newScale = Math.Clamp(Scale * factor, minScale, upper);
        var ratio = newScale / Scale;
        return new ImageViewportTransform(
            newScale,
            anchor.X - ((anchor.X - OffsetX) * ratio),
            anchor.Y - ((anchor.Y - OffsetY) * ratio));
    }

    /// <summary>Fotoğrafın görünüm alanından tamamen çıkmasını engeller (en az <paramref name="padding"/> kadar görünür kalır).</summary>
    public ImageViewportTransform ClampToViewport(ImageSize image, ViewSize view, double padding = 0d)
    {
        if (!image.IsValid || !view.IsValid)
        {
            return this;
        }

        var scaledWidth = image.Width * Scale;
        var scaledHeight = image.Height * Scale;
        return this with
        {
            OffsetX = ClampAxis(OffsetX, scaledWidth, view.Width, padding),
            OffsetY = ClampAxis(OffsetY, scaledHeight, view.Height, padding)
        };
    }

    private static double ClampAxis(double offset, double scaledLength, double viewLength, double padding)
    {
        var a = padding;
        var b = viewLength - padding - scaledLength;
        return Math.Clamp(offset, Math.Min(a, b), Math.Max(a, b));
    }
}
