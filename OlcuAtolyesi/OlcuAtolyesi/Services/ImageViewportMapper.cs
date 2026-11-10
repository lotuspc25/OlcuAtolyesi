using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

/// <summary>
/// Kaynak görsel koordinatlarını görünüm koordinatlarına dönüştürür. Scale toplam
/// büyütme oranıdır; Translation değerleri görünüm DIP birimindedir.
/// </summary>
public readonly record struct ImageViewportTransform
{
    public ImageViewportTransform(double scale, double translationX, double translationY)
    {
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Ölçek sıfırdan büyük olmalıdır.");
        }

        if (!double.IsFinite(translationX) || !double.IsFinite(translationY))
        {
            throw new ArgumentOutOfRangeException(nameof(translationX), "Kaydırma değerleri sonlu olmalıdır.");
        }

        Scale = scale;
        TranslationX = translationX;
        TranslationY = translationY;
    }

    public double Scale { get; }

    public double TranslationX { get; }

    public double TranslationY { get; }

    public static ImageViewportTransform Fit(ImageSize image, ViewSize viewport, double padding = 0d)
    {
        if (!double.IsFinite(padding) || padding < 0d || padding * 2d >= viewport.Width || padding * 2d >= viewport.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(padding));
        }

        var availableWidth = viewport.Width - (padding * 2d);
        var availableHeight = viewport.Height - (padding * 2d);
        var scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
        var translationX = (viewport.Width - (image.Width * scale)) / 2d;
        var translationY = (viewport.Height - (image.Height * scale)) / 2d;
        return new ImageViewportTransform(scale, translationX, translationY);
    }

    public ViewPoint ImageToView(ImagePoint point)
    {
        EnsureFinite(point.IsFinite, nameof(point));
        return new ViewPoint(
            (point.X * Scale) + TranslationX,
            (point.Y * Scale) + TranslationY);
    }

    public ImagePoint ViewToImage(ViewPoint point)
    {
        EnsureFinite(point.IsFinite, nameof(point));
        return new ImagePoint(
            (point.X - TranslationX) / Scale,
            (point.Y - TranslationY) / Scale);
    }

    public ViewSegment ImageToView(ImageSegment segment) =>
        new(ImageToView(segment.Start), ImageToView(segment.End));

    public ImageSegment ViewToImage(ViewSegment segment) =>
        new(ViewToImage(segment.Start), ViewToImage(segment.End));

    public ImageViewportTransform PanBy(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        }

        return new ImageViewportTransform(Scale, TranslationX + deltaX, TranslationY + deltaY);
    }

    /// <summary>Büyütme sırasında parmak altındaki görsel noktasını sabit tutar.</summary>
    public ImageViewportTransform ZoomAt(ViewPoint anchor, double scaleFactor, double minimumScale = 0.05d, double maximumScale = 20d)
    {
        EnsureFinite(anchor.IsFinite, nameof(anchor));

        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleFactor));
        }

        if (!double.IsFinite(minimumScale) || !double.IsFinite(maximumScale) || minimumScale <= 0d || maximumScale < minimumScale)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScale));
        }

        var imageAnchor = ViewToImage(anchor);
        var newScale = Math.Clamp(Scale * scaleFactor, minimumScale, maximumScale);
        return new ImageViewportTransform(
            newScale,
            anchor.X - (imageAnchor.X * newScale),
            anchor.Y - (imageAnchor.Y * newScale));
    }

    /// <summary>Görselin görünüm alanından tamamen çıkmasını önleyecek şekilde kaydırmayı sınırlar.</summary>
    public ImageViewportTransform ClampToViewport(ImageSize image, ViewSize viewport, double padding = 0d)
    {
        if (!double.IsFinite(padding) || padding < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(padding));
        }

        var effectivePaddingX = padding * 2d < viewport.Width ? padding : 0d;
        var effectivePaddingY = padding * 2d < viewport.Height ? padding : 0d;

        var scaledWidth = image.Width * Scale;
        var scaledHeight = image.Height * Scale;

        var boundX1 = effectivePaddingX;
        var boundX2 = viewport.Width - effectivePaddingX - scaledWidth;
        var minX = Math.Min(boundX1, boundX2);
        var maxX = Math.Max(boundX1, boundX2);

        var boundY1 = effectivePaddingY;
        var boundY2 = viewport.Height - effectivePaddingY - scaledHeight;
        var minY = Math.Min(boundY1, boundY2);
        var maxY = Math.Max(boundY1, boundY2);

        return new ImageViewportTransform(
            Scale,
            Math.Clamp(TranslationX, minX, maxX),
            Math.Clamp(TranslationY, minY, maxY));
    }

    private static void EnsureFinite(bool isFinite, string parameterName)
    {
        if (!isFinite)
        {
            throw new ArgumentException("Koordinatlar sonlu olmalıdır.", parameterName);
        }
    }
}
