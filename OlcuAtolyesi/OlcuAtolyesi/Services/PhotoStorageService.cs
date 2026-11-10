using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OlcuAtolyesi.Diagnostics;

namespace OlcuAtolyesi.Services;

public sealed class SavedMeasurementPhoto
{
    public string PhotoId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsTransferredToPc { get; set; }
}

public sealed record PhotoStorageUsageInfo(int TotalPhotosCount, long TotalBytesOccupied, string FormattedSize);

/// <summary>
/// Telefonda çekilen yüksek çözünürlüklü ölçüm fotoğraflarını uygulamanın özel depolama alanında
/// orijinal haliyle saklayan, SHA-256 bütünlüğünü koruyan ve depolama yönetimini sağlayan servis.
/// </summary>
public sealed class PhotoStorageService
{
    private const long MinimumRequiredFreeSpaceBytes = 20L * 1024 * 1024; // 20 MB
    private readonly string _storageDirectory;
    private readonly string _indexFilePath;
    private readonly object _lock = new();
    private readonly Func<string, long> _freeSpaceProvider;

    public PhotoStorageService(string? customStorageDir = null, Func<string, long>? freeSpaceBytesProvider = null)
    {
        _storageDirectory = !string.IsNullOrWhiteSpace(customStorageDir)
            ? customStorageDir
            : Path.Combine(GetDefaultBaseDirectory(), "Captures");

        Directory.CreateDirectory(_storageDirectory);
        _indexFilePath = Path.Combine(_storageDirectory, "captures_index.json");
        _freeSpaceProvider = freeSpaceBytesProvider ?? GetAvailableFreeSpaceBytes;
    }

    public string StorageDirectory => _storageDirectory;

    private static string GetDefaultBaseDirectory()
    {
        try
        {
            var mauiFsType = Type.GetType("Microsoft.Maui.Storage.FileSystem, Microsoft.Maui.Essentials");
            if (mauiFsType?.GetProperty("AppDataDirectory")?.GetValue(null) is string mauiPath && !string.IsNullOrWhiteSpace(mauiPath))
            {
                return mauiPath;
            }
        }
        catch { }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OlcuAtolyesi");
    }

    /// <summary>
    /// Orijinal JPEG akışını telefona kaydeder, SHA-256 özetini hesaplar ve doğrular.
    /// </summary>
    public async Task<SavedMeasurementPhoto> SavePhotoAsync(
        Stream rawJpegStream,
        string? suggestedFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawJpegStream);

        // 1. Disk alanı denetimi
        CheckDiskSpace();

        var timestamp = DateTimeOffset.UtcNow;
        var photoId = $"capt_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        var fileName = !string.IsNullOrWhiteSpace(suggestedFileName)
            ? suggestedFileName.Trim()
            : $"{photoId}.jpg";

        if (!fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".jpg";
        }

        var destinationPath = Path.Combine(_storageDirectory, fileName);
        var tempPath = destinationPath + ".tmp";

        long totalBytesWritten;
        string sha256Hex;

        // 2. Akışı doğrudan diske yazarken eşzamanlı SHA-256 hesapla (re-encoding yok)
        using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
        {
            var buffer = new byte[65536];
            totalBytesWritten = 0;
            int bytesRead;

            while ((bytesRead = await rawJpegStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                sha256.AppendData(buffer, 0, bytesRead);
                totalBytesWritten += bytesRead;
            }

            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            sha256Hex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
        }

        if (totalBytesWritten == 0)
        {
            try { File.Delete(tempPath); } catch { }
            throw new InvalidOperationException("Kamera fotoğraf akışı boş; dosya oluşturulamadı.");
        }

        // 3. Geçici dosyayı kalıcı konuma taşı
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }
        File.Move(tempPath, destinationPath);

        // 4. JPEG başlıklarından piksel çözünürlüğünü oku
        var (width, height) = TryReadJpegDimensions(destinationPath);

        var record = new SavedMeasurementPhoto
        {
            PhotoId = photoId,
            FileName = fileName,
            FilePath = destinationPath,
            FileSizeBytes = totalBytesWritten,
            Sha256 = sha256Hex,
            Width = width,
            Height = height,
            CreatedAt = timestamp,
            IsTransferredToPc = false
        };

        // 5. İndekse kaydet
        lock (_lock)
        {
            var list = LoadIndexInternal();
            list.RemoveAll(p => p.PhotoId == photoId || p.FileName == fileName);
            list.Insert(0, record);
            SaveIndexInternal(list);
        }

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "PhotoStorage",
            "PHOTO_SAVED",
            $"Fotoğraf telefona kaydedildi: {fileName} ({totalBytesWritten} bayt, {width}x{height}, SHA-256: {sha256Hex[..8]}...)");

        return record;
    }

    /// <summary>
    /// Kayıtlı tüm fotoğrafları listeler.
    /// </summary>
    public IReadOnlyList<SavedMeasurementPhoto> GetPhotos()
    {
        lock (_lock)
        {
            return LoadIndexInternal();
        }
    }

    /// <summary>
    /// Belirtilen kimlikteki fotoğrafı getirir.
    /// </summary>
    public SavedMeasurementPhoto? GetPhoto(string photoId)
    {
        if (string.IsNullOrWhiteSpace(photoId)) return null;

        lock (_lock)
        {
            return LoadIndexInternal().FirstOrDefault(p =>
                string.Equals(p.PhotoId, photoId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.FileName, photoId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Belirtilen fotoğraf için okuma akışı açar.
    /// </summary>
    public Stream? OpenPhotoReadStream(string photoId)
    {
        var photo = GetPhoto(photoId);
        if (photo == null || !File.Exists(photo.FilePath))
        {
            return null;
        }

        return new FileStream(photo.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Fotoğrafı PC'ye başarıyla aktarıldı olarak işaretler.
    /// </summary>
    public void MarkAsTransferred(string photoId)
    {
        if (string.IsNullOrWhiteSpace(photoId)) return;

        lock (_lock)
        {
            var list = LoadIndexInternal();
            var target = list.FirstOrDefault(p => p.PhotoId == photoId);
            if (target != null)
            {
                target.IsTransferredToPc = true;
                SaveIndexInternal(list);
            }
        }
    }

    /// <summary>
    /// Fotoğrafı siler. Kullanıcı onayı (userConfirmed = true) zorunludur!
    /// </summary>
    public bool DeletePhoto(string photoId, bool userConfirmed)
    {
        if (!userConfirmed)
        {
            throw new InvalidOperationException("Kullanıcı onayı olmadan fotoğraflar silinemez.");
        }

        lock (_lock)
        {
            var list = LoadIndexInternal();
            var target = list.FirstOrDefault(p => p.PhotoId == photoId);
            if (target == null)
            {
                return false;
            }

            try
            {
                if (File.Exists(target.FilePath))
                {
                    File.Delete(target.FilePath);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Warn,
                    "PhotoStorage",
                    "PHOTO_DELETE_FILE_ERR",
                    $"Dosya diskten silinirken hata: {ex.Message}");
            }

            list.Remove(target);
            SaveIndexInternal(list);

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "PhotoStorage",
                "PHOTO_DELETED",
                $"Fotoğraf kullanıcı onayıyla silindi: {target.FileName}");

            return true;
        }
    }

    /// <summary>
    /// Saklanan fotoğrafların kapladığı toplam disk alanını hesaplar.
    /// </summary>
    public PhotoStorageUsageInfo GetStorageUsage()
    {
        lock (_lock)
        {
            var list = LoadIndexInternal();
            long totalBytes = 0;
            var validPhotos = 0;

            foreach (var photo in list)
            {
                if (File.Exists(photo.FilePath))
                {
                    var len = new FileInfo(photo.FilePath).Length;
                    totalBytes += len;
                    validPhotos++;
                }
            }

            var formatted = totalBytes switch
            {
                < 1024 => $"{totalBytes} B",
                < 1024 * 1024 => $"{totalBytes / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{totalBytes / (1024.0 * 1024):F1} MB",
                _ => $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB"
            };

            return new PhotoStorageUsageInfo(validPhotos, totalBytes, formatted);
        }
    }

    private void CheckDiskSpace()
    {
        var availableBytes = _freeSpaceProvider(_storageDirectory);
        if (availableBytes < MinimumRequiredFreeSpaceBytes)
        {
            throw new InvalidOperationException(
                $"Yetersiz disk alanı. Çekim için en az {MinimumRequiredFreeSpaceBytes / (1024 * 1024)} MB boş alan gereklidir " +
                $"(Mevcut: {availableBytes / (1024 * 1024)} MB).");
        }
    }

    /// <summary>
    /// Verilen yolun bulunduğu bölümdeki kullanılabilir boş bayt miktarını döndürür.
    /// Android'de uygulama verisi <c>/data</c> bölümündedir; bu yüzden kök dizin (<c>/</c>) yerine
    /// doğrudan hedef yolun bölümü ölçülür (<c>Java.IO.File.UsableSpace</c>).
    /// Ölçüm yapılamazsa engelleme yapılmaz (long.MaxValue döner).
    /// </summary>
    public static long GetAvailableFreeSpaceBytes(string path)
    {
#if ANDROID
        try
        {
            return new Java.IO.File(path).UsableSpace;
        }
        catch
        {
            return long.MaxValue;
        }
#else
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return long.MaxValue;
        }
#endif
    }

    private List<SavedMeasurementPhoto> LoadIndexInternal()
    {
        if (!File.Exists(_indexFilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_indexFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<SavedMeasurementPhoto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveIndexInternal(List<SavedMeasurementPhoto> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_indexFilePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "PhotoStorage",
                "INDEX_SAVE_ERR",
                $"Fotoğraf indeksi kaydedilemedi: {ex.Message}");
        }
    }

    /// <summary>
    /// JPEG SOF (Start of Frame) belirteçlerini ayrıştırarak piksel genişlik ve yüksekliğini elde eder.
    /// </summary>
    public static (int Width, int Height) TryReadJpegDimensions(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryReadJpegDimensions(fs);
        }
        catch
        {
            return (0, 0);
        }
    }

    public static (int Width, int Height) TryReadJpegDimensions(Stream stream)
    {
        try
        {
            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();
            if (b1 != 0xFF || b2 != 0xD8) return (0, 0); // Not a valid JPEG

            while (stream.Position < stream.Length)
            {
                var markerPrefix = stream.ReadByte();
                if (markerPrefix != 0xFF) break;

                var marker = stream.ReadByte();
                while (marker == 0xFF) marker = stream.ReadByte(); // Padding bytes

                if (marker is 0xD9 or 0xDA) break; // EOI or SOS

                // 2 bytes length
                var lenH = stream.ReadByte();
                var lenL = stream.ReadByte();
                if (lenH < 0 || lenL < 0) break;
                var length = (lenH << 8) | lenL;
                if (length < 2) break;

                // SOF0 (0xC0), SOF1 (0xC1), SOF2 (0xC2)
                if (marker is 0xC0 or 0xC1 or 0xC2)
                {
                    var precision = stream.ReadByte(); // precision
                    var hH = stream.ReadByte();
                    var hL = stream.ReadByte();
                    var wH = stream.ReadByte();
                    var wL = stream.ReadByte();
                    if (hH >= 0 && hL >= 0 && wH >= 0 && wL >= 0)
                    {
                        var height = (hH << 8) | hL;
                        var width = (wH << 8) | wL;
                        return (width, height);
                    }
                    break;
                }

                // Skip marker payload
                stream.Seek(length - 2, SeekOrigin.Current);
            }
        }
        catch
        {
        }

        return (0, 0);
    }
}
