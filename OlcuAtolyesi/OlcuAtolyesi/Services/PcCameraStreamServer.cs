using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using OlcuAtolyesi.Diagnostics;

namespace OlcuAtolyesi.Services;

/// <summary>
/// Android telefondan PC'ye yerel ağ üzerinden güvenli ve düşük gecikmeli
/// JPEG / MJPEG kamera yayını, yüksek çözünürlüklü uzaktan fotoğraf çekimi ve
/// güvenli dosya aktarımı sağlayan bağımsız HTTP sunucusu.
/// </summary>
public sealed class PcCameraStreamServer : IDisposable
{
    private readonly object _gate = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _clients = new();
    private int _activeClientCount;
    private string? _lastClientIp;

    private string? _sessionAuthToken;
    private DateTimeOffset _sessionTokenExpiresAt;

    // --- PIN kaba kuvvet koruması (v0.6.0) ---
    private const int MaxFailedPinAttempts = 5;
    private static readonly TimeSpan PinLockoutDuration = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, PinAttemptState> _pinAttempts = new();

    private sealed class PinAttemptState
    {
        public int FailedCount;
        public DateTimeOffset LockedUntilUtc;
    }

    public bool IsRunning { get; private set; }
    public int Port { get; private set; } = 8088;
    public string SessionPin { get; private set; } = string.Empty;
    public int ActiveClientCount => _activeClientCount;
    public string? LastClientIp => _lastClientIp;

    public bool IsForeground { get; set; } = true;
    public PhotoStorageService PhotoStorage { get; }
    public ContinuousPreviewPipeline Pipeline { get; } = new();
    public Func<CancellationToken, Task<Stream?>>? OnStillCaptureRequested { get; set; }

    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<string>? LogMessage;

    public static string GenerateRandomPin() =>
        RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    public PcCameraStreamServer(PhotoStorageService? photoStorage = null)
    {
        PhotoStorage = photoStorage ?? new PhotoStorageService();
    }

    /// <summary>
    /// Kamera yayın sunucusunu başlatır.
    /// </summary>
    public void Start(int port = 8088, string? pin = null)
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                Stop();
            }

            Port = port;
            SessionPin = !string.IsNullOrWhiteSpace(pin) ? pin.Trim() : GenerateRandomPin();

            // Yeni oturum yetkilendirme belirteci üret (1 saat geçerli)
            _sessionAuthToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _sessionTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

            Pipeline.Reset();

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();

            // Tüm yerel ağ arayüzlerinden erişim için dinleme prefix'i
            try
            {
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Start();
            }
            catch
            {
                // Standart wildcard yetkisi yoksa localhost ve yerel IP fallback
                _listener.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{Port}/");
                try
                {
                    _listener.Start();
                }
                catch
                {
                    _listener.Close();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{Port}/");
                    _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                    _listener.Start();
                }
            }

            IsRunning = true;
            _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraServer",
                "SERVER_START",
                $"Kamera yayın sunucusu başlatıldı. Port: {Port}");
            Log($"Kamera yayın sunucusu başlatıldı. Port: {Port}, PIN: {DiagnosticDataMasker.Mask(SessionPin)}");
        }
    }

    /// <summary>
    /// Kamera yayın sunucusunu durdurur ve bağlı istemcileri sonlandırır.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            DiagnosticLogHub.Instance.Log(DiagnosticLevel.Info, "CameraServer", "SERVER_STOP", "Kamera yayın sunucusu durduruldu.");
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
            }
            finally
            {
                _listener = null;
            }

            // Bağlı tüm istemci kanallarını kapat
            foreach (var client in _clients)
            {
                client.Value.Writer.TryComplete();
            }
            _clients.Clear();
            _activeClientCount = 0;

            _sessionAuthToken = null;
            _pinAttempts.Clear();
            Pipeline.Reset();

            _cts?.Dispose();
            _cts = null;

            Log("Kamera yayın sunucusu durduruldu.");
        }
    }

    /// <summary>
    /// Kameradan yakalanan en güncel JPEG karesini bağlı tüm istemcilere yayınlar.
    /// Düşük gecikme için eski kareler tamponda tutulmaz, gerektiğinde atılır.
    /// </summary>
    public void PublishFrame(byte[] jpegBytes)
    {
        if (!IsRunning || jpegBytes == null || jpegBytes.Length == 0 || _clients.IsEmpty)
        {
            return;
        }

        foreach (var client in _clients)
        {
            // BoundedChannelFullMode.DropOldest sayesinde istemci yetişemezse eski kare düşer
            if (!client.Value.Writer.TryWrite(jpegBytes))
            {
                Pipeline.RecordDroppedFrame();
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsRunning && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (HttpListenerException) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"İstek kabul hatası: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        var clientIp = request.RemoteEndPoint?.Address.ToString() ?? "Bilinmeyen";

        try
        {
            // v0.6.0: Joker (*) CORS başlıkları kaldırıldı. İstemciler yerel WPF uygulamasıdır;
            // tarayıcı tabanlı çapraz kaynak erişimine ihtiyaç yoktur.

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";

            // --- 0. KABA KUVVET KİLİT KONTROLÜ ---
            if (path is "/api/v1/auth" or "/logs" or "/stream" or "/video" or "/mjpeg")
            {
                if (await TryRejectIfLockedOutAsync(response, clientIp, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }

            // --- 1. AÇIK ENDPOINT'LER ---
            if (path == "/status")
            {
                var providedPin = request.QueryString["pin"] ?? request.Headers["X-Session-Pin"];
                var isPinValid = !string.IsNullOrWhiteSpace(providedPin) &&
                                 string.Equals(providedPin.Trim(), SessionPin, StringComparison.Ordinal);
                await HandleStatusRequestAsync(response, isPinValid, clientIp).ConfigureAwait(false);
                return;
            }

            // --- 2. GÜVENLİ BELİRTEÇ ALMA (AUTH / TOKEN EXCHANGE) ---
            if (path == "/api/v1/auth" && request.HttpMethod == "POST")
            {
                await HandleAuthRequestAsync(request, response, clientIp, cancellationToken).ConfigureAwait(false);
                return;
            }

            // --- 3. YÜKSEK ÇÖZÜNÜRLÜKLÜ ÇEKİM (POST /api/v1/capture) ---
            if (path == "/api/v1/capture" && request.HttpMethod == "POST")
            {
                await HandleCaptureRequestAsync(request, response, clientIp, cancellationToken).ConfigureAwait(false);
                return;
            }

            // --- 4. FOTOĞRAF İNDİRME / LİSTELEME (/api/v1/photos) ---
            if (path == "/api/v1/photos" && request.HttpMethod == "GET")
            {
                await HandleListPhotosRequestAsync(request, response, clientIp, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/v1/photos/") && request.HttpMethod == "GET")
            {
                var photoId = path["/api/v1/photos/".Length..];
                await HandleDownloadPhotoRequestAsync(request, response, photoId, clientIp, cancellationToken).ConfigureAwait(false);
                return;
            }

            // --- 5. ESKİ PIN İLE KORUNAN ENDPOINT'LER (Geriye Uyumluluk: /logs, /stream) ---
            // v0.6.0: PIN artık yalnızca X-Session-Pin başlığıyla kabul edilir; sorgu dizesi
            // kaldırıldı (adres çubuğu/geçmiş/log sızıntısını önlemek için).
            // İSTİSNA: /logs — PC tanılama konsolu (v0.5.0.5) hâlâ PIN'i sorgu dizesiyle gönderiyor.
            // Bu istisna Paket 3'te PC istemcisi başlığa geçirildikten sonra kaldırılacak.
            var legacyPin = path == "/logs"
                ? (request.QueryString["pin"] ?? request.Headers["X-Session-Pin"])
                : request.Headers["X-Session-Pin"];
            var isLegacyPinValid = !string.IsNullOrWhiteSpace(legacyPin) &&
                                  string.Equals(legacyPin.Trim(), SessionPin, StringComparison.Ordinal);

            if (isLegacyPinValid)
            {
                ResetPinFailures(clientIp);
            }
            else if (!string.IsNullOrWhiteSpace(legacyPin))
            {
                RegisterPinFailure(clientIp);
            }

            if (!isLegacyPinValid)
            {
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Warn,
                    "CameraServer",
                    "AUTH_FAIL",
                    $"Yetkisiz bağlantı denemesi engellendi. İstemci IP: {clientIp}, Yol: {path}");
                response.StatusCode = 401;
                response.ContentType = "application/json; charset=utf-8";
                var errorBytes = Encoding.UTF8.GetBytes(
                    "{\"success\":false,\"error\":\"Yetkisiz erişim. Geçersiz veya eksik eşleştirme kodu ya da belirteci.\"}");
                response.ContentLength64 = errorBytes.Length;
                await response.OutputStream.WriteAsync(errorBytes, cancellationToken).ConfigureAwait(false);
                response.Close();
                return;
            }

            if (path == "/logs")
            {
                await HandleLogsRequestAsync(response, clientIp, cancellationToken).ConfigureAwait(false);
            }
            else if (path is "/stream" or "/video" or "/mjpeg")
            {
                await HandleStreamRequestAsync(response, clientIp, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response.StatusCode = 404;
                var notFoundBytes = Encoding.UTF8.GetBytes("{\"error\":\"Endpoint bulunamadı.\"}");
                response.ContentLength64 = notFoundBytes.Length;
                await response.OutputStream.WriteAsync(notFoundBytes, cancellationToken).ConfigureAwait(false);
                response.Close();
            }
        }
        catch (Exception ex)
        {
            Log($"İstek işleme hatası ({clientIp}): {ex.Message}");
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
            }
        }
    }

    private async Task HandleAuthRequestAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string clientIp,
        CancellationToken cancellationToken)
    {
        string? providedPin = request.Headers["X-Session-Pin"];

        if (string.IsNullOrWhiteSpace(providedPin) && request.HasEntityBody)
        {
            using var sr = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("pin", out var pinProp))
                    {
                        providedPin = pinProp.GetString();
                    }
                }
                catch { }
            }
        }

        var isValid = !string.IsNullOrWhiteSpace(providedPin) &&
                      string.Equals(providedPin.Trim(), SessionPin, StringComparison.Ordinal);

        if (!isValid)
        {
            RegisterPinFailure(clientIp);
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "CameraServer",
                "PIN_AUTH_FAIL",
                $"PIN doğrulama başarısız. İstemci: {clientIp}");
            response.StatusCode = 401;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Geçersiz eşleştirme kodu (PIN).\"}");
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        ResetPinFailures(clientIp);

        string token;
        lock (_gate)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _sessionAuthToken = token;
            _sessionTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        }

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "CameraServer",
            "TOKEN_ISSUED",
            $"İstemciye oturum belirteci verildi ({clientIp}).");

        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        var resJson = $"{{\"success\":true,\"token\":\"{token}\",\"expiresInSeconds\":3600}}";
        var resBytes = Encoding.UTF8.GetBytes(resJson);
        response.ContentLength64 = resBytes.Length;
        await response.OutputStream.WriteAsync(resBytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private async Task HandleCaptureRequestAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string clientIp,
        CancellationToken cancellationToken)
    {
        if (!ValidateBearerToken(request))
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "CameraServer",
                "CAPTURE_UNAUTHORIZED",
                $"Yetkisiz uzaktan çekim isteği reddedildi ({clientIp}).");
            response.StatusCode = 401;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Yetkisiz erişim. Geçerli bir oturum belirteci gereklidir.\"}");
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        if (!IsForeground || !IsRunning)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "CameraServer",
                "CAPTURE_REJECTED_BACKGROUND",
                $"Uygulama arka planda veya yayın durdurulmuş olduğu için uzaktan çekim engellendi ({clientIp}).");
            response.StatusCode = 403;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Telefon uygulaması arka planda veya kamera yayını kapalı. Çekim yapılamaz.\"}");
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        var captureCallback = OnStillCaptureRequested;
        if (captureCallback == null)
        {
            response.StatusCode = 503;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Kamera çekim servisi henüz hazır değil.\"}");
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        try
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraServer",
                "STILL_CAPTURE_START",
                $"Uzaktan yüksek çözünürlüklü fotoğraf çekimi başlatılıyor ({clientIp})...");

            using var photoStream = await captureCallback(cancellationToken).ConfigureAwait(false);
            if (photoStream == null || photoStream.Length == 0)
            {
                throw new InvalidOperationException("Kamera fotoğraf akışı boş döndü.");
            }

            // Orijinal veriyi telefona kaydet ve SHA-256 bütünlüğünü al
            var savedPhoto = await PhotoStorage.SavePhotoAsync(photoStream, null, cancellationToken).ConfigureAwait(false);

            response.StatusCode = 200;
            response.ContentType = "application/json; charset=utf-8";
            var resultJson = JsonSerializer.Serialize(new
            {
                success = true,
                photoId = savedPhoto.PhotoId,
                fileName = savedPhoto.FileName,
                fileSizeBytes = savedPhoto.FileSizeBytes,
                sha256 = savedPhoto.Sha256,
                width = savedPhoto.Width,
                height = savedPhoto.Height,
                createdAt = savedPhoto.CreatedAt,
                downloadUrl = $"/api/v1/photos/{savedPhoto.PhotoId}"
            });

            var resultBytes = Encoding.UTF8.GetBytes(resultJson);
            response.ContentLength64 = resultBytes.Length;
            await response.OutputStream.WriteAsync(resultBytes, cancellationToken).ConfigureAwait(false);
            response.Close();

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraServer",
                "STILL_CAPTURE_COMPLETE",
                $"Yüksek çözünürlüklü çekim başarıyla tamamlandı: {savedPhoto.FileName} ({savedPhoto.FileSizeBytes} bayt)");
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "CameraServer",
                "STILL_CAPTURE_FAIL",
                $"Fotoğraf çekim hatası: {ex.Message}",
                details: ex.ToString());

            response.StatusCode = 500;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Fotoğraf çekimi başarısız: {ex.Message}"
            }));
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
        }
    }

    private async Task HandleListPhotosRequestAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string clientIp,
        CancellationToken cancellationToken)
    {
        if (!ValidateBearerToken(request))
        {
            response.StatusCode = 401;
            response.Close();
            return;
        }

        var photos = PhotoStorage.GetPhotos();
        var usage = PhotoStorage.GetStorageUsage();

        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(new
        {
            success = true,
            totalCount = usage.TotalPhotosCount,
            totalBytes = usage.TotalBytesOccupied,
            formattedSize = usage.FormattedSize,
            photos
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private async Task HandleDownloadPhotoRequestAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string photoId,
        string clientIp,
        CancellationToken cancellationToken)
    {
        if (!ValidateBearerToken(request))
        {
            response.StatusCode = 401;
            response.Close();
            return;
        }

        var photo = PhotoStorage.GetPhoto(photoId);
        if (photo == null || !File.Exists(photo.FilePath))
        {
            response.StatusCode = 404;
            response.ContentType = "application/json; charset=utf-8";
            var errBytes = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Fotoğraf dosyası bulunamadı.\"}");
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        try
        {
            response.StatusCode = 200;
            response.ContentType = "image/jpeg";
            response.ContentLength64 = photo.FileSizeBytes;
            response.Headers.Add("X-Sha256", photo.Sha256);
            response.Headers.Add("X-Photo-Width", photo.Width.ToString());
            response.Headers.Add("X-Photo-Height", photo.Height.ToString());
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{photo.FileName}\"");

            using (var fs = PhotoStorage.OpenPhotoReadStream(photo.PhotoId))
            {
                if (fs == null)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                await fs.CopyToAsync(response.OutputStream, cancellationToken).ConfigureAwait(false);
            }

            PhotoStorage.MarkAsTransferred(photo.PhotoId);
            response.Close();

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraServer",
                "PHOTO_DOWNLOADED",
                $"Fotoğraf PC'ye başarıyla indirildi: {photo.FileName} ({photo.FileSizeBytes} bayt)");
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "CameraServer",
                "PHOTO_DOWNLOAD_ERR",
                $"Fotoğraf indirme hatası: {ex.Message}");
            try { response.Close(); } catch { }
        }
    }

    private bool TryGetPinLockoutSeconds(string clientIp, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (!_pinAttempts.TryGetValue(clientIp, out var state))
        {
            return false;
        }

        lock (state)
        {
            var remaining = state.LockedUntilUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            return true;
        }
    }

    private async Task<bool> TryRejectIfLockedOutAsync(
        HttpListenerResponse response,
        string clientIp,
        CancellationToken cancellationToken)
    {
        if (!TryGetPinLockoutSeconds(clientIp, out var retryAfterSeconds))
        {
            return false;
        }

        response.StatusCode = 429;
        response.ContentType = "application/json; charset=utf-8";
        response.Headers.Add("Retry-After", retryAfterSeconds.ToString());
        var json = $"{{\"success\":false,\"error\":\"Çok fazla hatalı eşleştirme kodu denemesi. {retryAfterSeconds} saniye sonra tekrar deneyin.\",\"retryAfterSeconds\":{retryAfterSeconds}}}";
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
        return true;
    }

    private void RegisterPinFailure(string clientIp)
    {
        var state = _pinAttempts.GetOrAdd(clientIp, _ => new PinAttemptState());
        lock (state)
        {
            state.FailedCount++;
            if (state.FailedCount < MaxFailedPinAttempts)
            {
                return;
            }

            state.FailedCount = 0;
            state.LockedUntilUtc = DateTimeOffset.UtcNow + PinLockoutDuration;
        }

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Warn,
            "CameraServer",
            "PIN_LOCKOUT",
            $"Çok sayıda hatalı PIN denemesi. İstemci {PinLockoutDuration.TotalSeconds:0} saniye kilitlendi ({clientIp}).");
    }

    private void ResetPinFailures(string clientIp)
    {
        _pinAttempts.TryRemove(clientIp, out _);
    }

    private bool ValidateBearerToken(HttpListenerRequest request)
    {
        var authHeader = request.Headers["Authorization"] ?? request.Headers["X-Auth-Token"];
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return false;
        }

        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();

        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(_sessionAuthToken) || DateTimeOffset.UtcNow > _sessionTokenExpiresAt)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(_sessionAuthToken));
        }
    }

    private static async Task HandleStatusRequestAsync(HttpListenerResponse response, bool isPinValid, string clientIp)
    {
        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        var json = isPinValid
            ? "{\"status\":\"ok\",\"service\":\"OlcuAtolyesiCameraService\",\"authenticated\":true}"
            : "{\"status\":\"ok\",\"service\":\"OlcuAtolyesiCameraService\",\"authenticated\":false,\"message\":\"PIN gerekli\"}";
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private async Task HandleStreamRequestAsync(HttpListenerResponse response, string clientIp, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        // Düşük gecikme için tek elemanlı ve DropOldest kanalı
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _clients[clientId] = channel;
        Interlocked.Increment(ref _activeClientCount);
        _lastClientIp = clientIp;
        ClientConnected?.Invoke(this, clientIp);
        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Event,
            "CameraServer",
            "CLIENT_CONNECTED",
            $"PC İstemcisi canlı kamera yayınına bağlandı ({clientIp}). Aktif istemci: {_activeClientCount}");
        Log($"PC İstemcisi bağlandı: {clientIp} (Aktif istemci: {_activeClientCount})");

        response.StatusCode = 200;
        response.ContentType = "multipart/x-mixed-replace; boundary=--frame";
        response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        response.Headers.Add("Pragma", "no-cache");
        response.Headers.Add("Expires", "0");

        var output = response.OutputStream;

        // Başlangıç boundary'sini hemen yaz ve flush et, böylece istemci HTTP 200 başlıklarını anında alır
        var initBytes = Encoding.ASCII.GetBytes("--frame\r\n");
        await output.WriteAsync(initBytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var reader = channel.Reader;
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                var frame = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (frame == null || frame.Length == 0)
                {
                    continue;
                }

                var header = $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Length}\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);
                var footerBytes = Encoding.ASCII.GetBytes("\r\n");

                await output.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
                await output.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                await output.WriteAsync(footerBytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException)
        {
            // İstemci pencereyi kapattı veya bağlantıyı kesti
        }
        catch (Exception ex)
        {
            Log($"Akış yazma hatası ({clientIp}): {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            Interlocked.Decrement(ref _activeClientCount);
            ClientDisconnected?.Invoke(this, clientIp);
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Event,
                "CameraServer",
                "CLIENT_DISCONNECTED",
                $"PC İstemcisi yayından ayrıldı ({clientIp}). Kalan istemci: {_activeClientCount}");
            Log($"İstemci ayrıldı: {clientIp} (Kalan istemci: {_activeClientCount})");
            try
            {
                response.Close();
            }
            catch
            {
            }
        }
    }

    private static async Task HandleLogsRequestAsync(
        HttpListenerResponse response,
        string clientIp,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = DiagnosticLogHub.Instance.GetRecentEntries(200);
            var json = JsonSerializer.Serialize(entries);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.StatusCode = 200;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            response.Close();

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraServer",
                "LOGS_FETCHED",
                $"Tanılama logları eşleştirilmiş PC istemcisine başarıyla aktarıldı ({clientIp}, {entries.Count} kayıt).");
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "CameraServer",
                "LOGS_FETCH_ERROR",
                $"Log aktarımı sırasında hata: {ex.Message}",
                details: ex.ToString());
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
            }
        }
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
        DiagnosticLogHub.Instance.Log(DiagnosticLevel.Debug, "CameraServer", "SERVER_LOG", message);
    }

    public void Dispose()
    {
        Stop();
    }
}
