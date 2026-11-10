namespace OlcuAtolyesi.Media;

/// <summary>Only application-owned temporary files; originals and personal filenames never retained.</summary>
public sealed class TemporaryPhoto : IDisposable
{
    private static readonly SemaphoreSlim importGate = new(1, 1);
    public string Path { get; }
    private TemporaryPhoto(string path) => Path = path;
    public void Dispose() { try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    public static async Task<TemporaryPhoto> ImportAsync(Stream input, CancellationToken token)
    {
        await importGate.WaitAsync(token);
        try { return await ImportCoreAsync(input, token); }
        finally { importGate.Release(); }
    }

    private static async Task<TemporaryPhoto> ImportCoreAsync(Stream input, CancellationToken token)
    {
#if ANDROID
        var root = System.IO.Path.Combine(FileSystem.CacheDirectory, "photo-session");
        Directory.CreateDirectory(root);
        var id = Guid.NewGuid().ToString("N");
        var raw = System.IO.Path.Combine(root, id + ".source");
        var output = System.IO.Path.Combine(root, id + ".jpg");
        try {
            await using (var file = new FileStream(raw, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) {
                var buffer = new byte[65536]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0) {
                    total += read;
                    if (total > 40 * 1024 * 1024) throw new InvalidOperationException("Fotoğraf çok büyük. En fazla 40 MB dosya seçin.");
                    await file.WriteAsync(buffer.AsMemory(0, read), token);
                }
            }
            await Task.Run(() => Normalize(raw, output, token), token);
            return new(output);
        } finally {
            File.Delete(raw);
            if (token.IsCancellationRequested && File.Exists(output)) File.Delete(output);
        }
#else
        await Task.CompletedTask;
        throw new NotSupportedException("Gerçek fotoğraf edinme bu sürümde Android'de desteklenir.");
#endif
    }

#if ANDROID
    private static void Normalize(string raw, string output, CancellationToken token)
    {
        try {
            using var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            Android.Graphics.BitmapFactory.DecodeFile(raw, bounds);
            if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0 || bounds.OutWidth > 40000 || bounds.OutHeight > 40000 ||
                (long)bounds.OutWidth * bounds.OutHeight > 200_000_000)
                throw new InvalidOperationException("Fotoğraf biçimi veya boyutu desteklenmiyor. JPEG veya PNG bir fotoğraf deneyin.");
            var sample = 1;
            while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > 2048) sample *= 2;
            using var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
            token.ThrowIfCancellationRequested();
            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(raw, options)
                ?? throw new InvalidOperationException("Fotoğraf açılamadı.");
            using var exif = new Android.Media.ExifInterface(raw);
            var orientation = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1);
            Android.Graphics.Bitmap? transformed = null;
            try
            {
                var target = bitmap;
                if (orientation is >= 2 and <= 8)
                {
                    using var matrix = new Android.Graphics.Matrix();
                    switch (orientation)
                    {
                        case 2: matrix.SetScale(-1, 1); break;
                        case 3: matrix.SetRotate(180); break;
                        case 4: matrix.SetScale(1, -1); break;
                        case 5: matrix.SetRotate(90); matrix.PostScale(-1, 1); break;
                        case 6: matrix.SetRotate(90); break;
                        case 7: matrix.SetRotate(-90); matrix.PostScale(-1, 1); break;
                        case 8: matrix.SetRotate(-90); break;
                    }

                    transformed = Android.Graphics.Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true)
                        ?? throw new InvalidOperationException("Fotoğraf yönü uygulanamadı.");
                    target = transformed;
                }

                token.ThrowIfCancellationRequested();
                using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                if (!target.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, destination))
                    throw new IOException("Geçici fotoğraf yazılamadı.");
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (transformed is not null && !ReferenceEquals(transformed, bitmap))
                {
                    transformed.Dispose();
                }
            }
        } catch {
            if (File.Exists(output)) File.Delete(output);
            throw;
        }
    }
#endif

    // Only stale application-owned copies. Never enumerate gallery or other app directories.
    public static void RemoveStaleCopies()
    {
        var root = System.IO.Path.Combine(FileSystem.CacheDirectory, "photo-session");
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root)) {
            if (Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(file), "N", out _) &&
                File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1)) {
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
