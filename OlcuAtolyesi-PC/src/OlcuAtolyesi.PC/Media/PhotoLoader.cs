using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OlcuAtolyesi.Core.Geometry;

namespace OlcuAtolyesi.PC.Media;

public sealed record PhotoLoadResult(
    bool Success,
    BitmapSource? Image,
    ImageSize OriginalSize,
    string? FileName,
    string? ErrorMessage)
{
    public static PhotoLoadResult Failure(string message) => new(false, null, default, null, message);
}

/// <summary>
/// JPG/JPEG/PNG/BMP dosyalarını güvenli biçimde yükler. Ölçüm koordinatları EXIF yönü uygulanmış
/// özgün piksel boyutunda tutulur; çok büyük görseller yalnızca görüntüleme için küçültülerek çözülür.
/// </summary>
public static class PhotoLoader
{
    public const long MaxFileBytes = 100L * 1024 * 1024;
    public const int MaxSideLength = 40_000;
    public const long MaxPixelCount = 250_000_000;
    public const int MaxDisplayLongSide = 6_000;

    public static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    public static PhotoLoadResult Load(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return PhotoLoadResult.Failure("Dosya yolu boş.");
            }

            var extension = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return PhotoLoadResult.Failure("Desteklenmeyen dosya türü. JPG, JPEG, PNG veya BMP seçin.");
            }

            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return PhotoLoadResult.Failure("Dosya bulunamadı.");
            }

            if (info.Length == 0)
            {
                return PhotoLoadResult.Failure("Dosya boş.");
            }

            if (info.Length > MaxFileBytes)
            {
                return PhotoLoadResult.Failure("Dosya çok büyük (en fazla 100 MB desteklenir).");
            }

            int width;
            int height;
            int orientation;
            using (var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = BitmapDecoder.Create(
                    probe,
                    BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.None);
                if (decoder.Frames.Count == 0)
                {
                    return PhotoLoadResult.Failure("Görüntü karesi okunamadı.");
                }

                var frame = decoder.Frames[0];
                width = frame.PixelWidth;
                height = frame.PixelHeight;
                orientation = ReadOrientation(frame);
            }

            if (width <= 0 || height <= 0)
            {
                return PhotoLoadResult.Failure("Görüntü boyutu okunamadı.");
            }

            if (width > MaxSideLength || height > MaxSideLength || (long)width * height > MaxPixelCount)
            {
                return PhotoLoadResult.Failure("Görüntü çok büyük (en fazla 250 megapiksel ve 40.000 piksel kenar desteklenir).");
            }

            BitmapSource source;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                if (Math.Max(width, height) > MaxDisplayLongSide)
                {
                    if (width >= height)
                    {
                        bitmap.DecodePixelWidth = MaxDisplayLongSide;
                    }
                    else
                    {
                        bitmap.DecodePixelHeight = MaxDisplayLongSide;
                    }
                }

                bitmap.EndInit();
                bitmap.Freeze();
                source = bitmap;
            }

            source = ApplyOrientation(source, orientation);
            var swapped = orientation is >= 5 and <= 8;
            var originalSize = swapped
                ? new ImageSize(height, width)
                : new ImageSize(width, height);
            return new PhotoLoadResult(true, source, originalSize, Path.GetFileName(path), null);
        }
        catch (OutOfMemoryException)
        {
            return PhotoLoadResult.Failure("Fotoğraf yüklemek için yeterli bellek yok.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PhotoLoadResult.Failure($"Dosya okunamadı: {ex.Message}");
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException
                                       or System.Runtime.InteropServices.COMException)
        {
            return PhotoLoadResult.Failure("Dosya geçerli bir görüntü değil veya bozuk.");
        }
        catch (Exception ex) when (ex.GetType().Name == "FileFormatException")
        {
            return PhotoLoadResult.Failure("Dosya geçerli bir görüntü değil veya bozuk.");
        }
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata &&
                metadata.ContainsQuery("System.Photo.Orientation") &&
                metadata.GetQuery("System.Photo.Orientation") is ushort value &&
                value is >= 1 and <= 8)
            {
                return value;
            }
        }
        catch
        {
            // Yön bilgisi okunamazsa görüntü olduğu gibi kullanılır.
        }

        return 1;
    }

    private static BitmapSource ApplyOrientation(BitmapSource source, int orientation)
    {
        Transform? transform = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => Group(new ScaleTransform(-1, 1), new RotateTransform(270)),
            6 => new RotateTransform(90),
            7 => Group(new ScaleTransform(-1, 1), new RotateTransform(90)),
            8 => new RotateTransform(270),
            _ => null
        };

        if (transform is null)
        {
            return source;
        }

        var transformed = new TransformedBitmap(source, transform);
        transformed.Freeze();
        return transformed;
    }

    private static TransformGroup Group(params Transform[] transforms)
    {
        var group = new TransformGroup();
        foreach (var transform in transforms)
        {
            group.Children.Add(transform);
        }

        return group;
    }
}
