using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OlcuAtolyesi.PC.Media;

/// <summary>Görüntüleme ayarları. Ölçümü ETKİLEMEZ; yalnızca görüntüyü işler.</summary>
public sealed record PhotoFilterSettings(
    double Brightness = 0d,     // -100..+100
    double Contrast = 0d,       // -100..+100
    double Sharpen = 0d,        // 0..100
    bool Grayscale = false,
    bool Negative = false,
    bool EdgeHighlight = false)
{
    public bool IsIdentity =>
        Math.Abs(Brightness) < 0.5 && Math.Abs(Contrast) < 0.5 &&
        Sharpen < 0.5 && !Grayscale && !Negative && !EdgeHighlight;
}

/// <summary>
/// Fotoğrafa görüntüleme amaçlı filtreler uygular. Çıktı ekranda gösterilir; DXF ve ölçüm
/// hesapları her zaman orijinal görüntüden yapılır.
/// </summary>
public static class PhotoFilterPipeline
{
    public static BitmapSource Apply(BitmapSource source, PhotoFilterSettings settings)
    {
        if (source is null || settings.IsIdentity)
        {
            return source!;
        }

        var formatted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0d);

        var width = formatted.PixelWidth;
        var height = formatted.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            return source;
        }

        var stride = width * 4;
        var pixels = new byte[stride * height];
        formatted.CopyPixels(pixels, stride, 0);

        var brightnessOffset = settings.Brightness * 1.27d;   // -100..100 -> -127..127
        var contrastFactor = (259d * (settings.Contrast + 255d)) / (255d * (259d - settings.Contrast));

        for (var i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i];
            double g = pixels[i + 1];
            double r = pixels[i + 2];

            if (Math.Abs(settings.Brightness) > 0.01)
            {
                r += brightnessOffset;
                g += brightnessOffset;
                b += brightnessOffset;
            }

            if (Math.Abs(settings.Contrast) > 0.01)
            {
                r = (contrastFactor * (r - 128d)) + 128d;
                g = (contrastFactor * (g - 128d)) + 128d;
                b = (contrastFactor * (b - 128d)) + 128d;
            }

            if (settings.Grayscale || settings.EdgeHighlight)
            {
                var gray = (0.299d * r) + (0.587d * g) + (0.114d * b);
                r = gray;
                g = gray;
                b = gray;
            }

            if (settings.Negative)
            {
                r = 255d - r;
                g = 255d - g;
                b = 255d - b;
            }

            pixels[i] = Clamp(b);
            pixels[i + 1] = Clamp(g);
            pixels[i + 2] = Clamp(r);
        }

        BitmapSource result = BitmapSource.Create(
            width, height, 96d, 96d, PixelFormats.Bgra32, null, pixels, stride);

        if (settings.Sharpen > 0.5)
        {
            result = ApplySharpen(result, settings.Sharpen / 100d);
        }

        if (settings.EdgeHighlight)
        {
            result = ApplySobel(result);
        }

        result.Freeze();
        return result;
    }

    private static byte Clamp(double value) =>
        value <= 0d ? (byte)0 : value >= 255d ? (byte)255 : (byte)Math.Round(value);

    private static BitmapSource ApplySharpen(BitmapSource source, double amount)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var sourcePixels = new byte[stride * height];
        source.CopyPixels(sourcePixels, stride, 0);

        var output = new byte[sourcePixels.Length];
        Array.Copy(sourcePixels, output, sourcePixels.Length);

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var index = (y * stride) + (x * 4);
                for (var channel = 0; channel < 3; channel++)
                {
                    var center = sourcePixels[index + channel];
                    var sum = 0d;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var neighbour = sourcePixels[((y + dy) * stride) + ((x + dx) * 4) + channel];
                            var weight = dx == 0 && dy == 0 ? 8d : -1d;
                            sum += neighbour * weight;
                        }
                    }

                    var sharpened = center + (amount * sum);
                    output[index + channel] = Clamp(sharpened);
                }
            }
        }

        var result = BitmapSource.Create(width, height, 96d, 96d, PixelFormats.Bgra32, null, output, stride);
        result.Freeze();
        return result;
    }

    private static BitmapSource ApplySobel(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var gray = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * stride) + (x * 4);
                gray[(y * width) + x] = (0.299d * pixels[i + 2]) + (0.587d * pixels[i + 1]) + (0.114d * pixels[i]);
            }
        }

        var output = new byte[pixels.Length];
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                double gx = 0d, gy = 0d;
                gx += gray[((y - 1) * width) + (x - 1)] * -1 + gray[((y - 1) * width) + (x + 1)] * 1;
                gx += gray[((y + 1) * width) + (x - 1)] * -1 + gray[((y + 1) * width) + (x + 1)] * 1;
                gx += gray[(y * width) + (x - 1)] * -2 + gray[(y * width) + (x + 1)] * 2;
                gy += gray[((y - 1) * width) + (x - 1)] * -1 + gray[((y + 1) * width) + (x - 1)] * 1;
                gy += gray[((y - 1) * width) + (x + 1)] * -1 + gray[((y + 1) * width) + (x + 1)] * 1;
                gy += gray[(y * width) + x] * -2 + gray[(y * width) + x] * 2;

                var magnitude = Clamp(Math.Sqrt((gx * gx) + (gy * gy)));
                var i = (y * stride) + (x * 4);
                output[i] = magnitude;
                output[i + 1] = magnitude;
                output[i + 2] = magnitude;
                output[i + 3] = 255;
            }
        }

        var result = BitmapSource.Create(width, height, 96d, 96d, PixelFormats.Bgra32, null, output, stride);
        result.Freeze();
        return result;
    }
}
