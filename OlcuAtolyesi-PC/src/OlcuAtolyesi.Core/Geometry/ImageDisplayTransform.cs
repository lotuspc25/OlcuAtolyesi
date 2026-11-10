namespace OlcuAtolyesi.Core.Geometry;

/// <summary>Görüntü üzerinde uygulanan kırpma dikdörtgeni (orijinal piksel koordinatında).</summary>
public readonly record struct ImageRect(double X, double Y, double Width, double Height)
{
    public bool IsValid =>
        double.IsFinite(X) && double.IsFinite(Y) &&
        double.IsFinite(Width) && double.IsFinite(Height) &&
        Width > 1d && Height > 1d;
}

/// <summary>
/// Görüntü düzenleme ayarları. Uygulama sırası: KIRP -> AYNALA -> DÖNDÜR.
/// </summary>
public sealed record ImageDisplaySettings(
    int QuarterTurns = 0,
    bool FlipHorizontal = false,
    bool FlipVertical = false,
    ImageRect? CropRect = null)
{
    public int NormalizedTurns => ((QuarterTurns % 4) + 4) % 4;

    public bool IsIdentity => NormalizedTurns == 0 && !FlipHorizontal && !FlipVertical && CropRect is null;
}

/// <summary>
/// Orijinal fotoğraf koordinatları ile ekranda gösterilen (kırpılmış/döndürülmüş/aynalanmış)
/// görüntü koordinatları arasında iki yönlü dönüşüm. <see cref="ToOriginal"/>,
/// <see cref="ToDisplay"/>'in tersidir; bu özellik otomatik testlerle doğrulanır.
/// </summary>
public sealed class ImageDisplayTransform
{
    private readonly ImageDisplaySettings _settings;
    private readonly ImageSize _cropped;

    public ImageDisplayTransform(ImageSize original, ImageDisplaySettings? settings)
    {
        OriginalSize = original;
        _settings = settings ?? new ImageDisplaySettings();

        var crop = _settings.CropRect;
        _cropped = crop is { } c && c.IsValid
            ? new ImageSize(
                Math.Min(c.Width, original.Width),
                Math.Min(c.Height, original.Height))
            : original;

        DisplaySize = _settings.NormalizedTurns % 2 == 1
            ? new ImageSize(_cropped.Height, _cropped.Width)
            : _cropped;
    }

    public ImageSize OriginalSize { get; }

    public ImageSize DisplaySize { get; }

    public ImageDisplaySettings Settings => _settings;

    /// <summary>Orijinal koordinatı ekran (gösterim) koordinatına çevirir.</summary>
    public ImagePoint ToDisplay(ImagePoint original)
    {
        var crop = _settings.CropRect;
        var x = original.X - (crop?.X ?? 0d);
        var y = original.Y - (crop?.Y ?? 0d);

        if (_settings.FlipHorizontal) x = _cropped.Width - x;
        if (_settings.FlipVertical) y = _cropped.Height - y;

        var w = _cropped.Width;
        var h = _cropped.Height;

        return _settings.NormalizedTurns switch
        {
            1 => new ImagePoint(h - y, x),
            2 => new ImagePoint(w - x, h - y),
            3 => new ImagePoint(y, w - x),
            _ => new ImagePoint(x, y)
        };
    }

    /// <summary>Ekran (gösterim) koordinatını orijinal koordinata çevirir.</summary>
    public ImagePoint ToOriginal(ImagePoint displayed)
    {
        var w = _cropped.Width;
        var h = _cropped.Height;

        var (x, y) = _settings.NormalizedTurns switch
        {
            1 => (displayed.Y, h - displayed.X),
            2 => (w - displayed.X, h - displayed.Y),
            3 => (w - displayed.Y, displayed.X),
            _ => (displayed.X, displayed.Y)
        };

        if (_settings.FlipHorizontal) x = w - x;
        if (_settings.FlipVertical) y = h - y;

        var crop = _settings.CropRect;
        return new ImagePoint(x + (crop?.X ?? 0d), y + (crop?.Y ?? 0d));
    }

    public ImageSegment ToDisplaySegment(ImageSegment original) =>
        new(ToDisplay(original.Start), ToDisplay(original.End));
}
