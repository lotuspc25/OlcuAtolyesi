using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OlcuAtolyesi.Core.Diagnostics;

namespace OlcuAtolyesi.Core.Phone;

public sealed record RemotePhotoItem(
    string PhotoId,
    string FileName,
    long FileSizeBytes,
    string Sha256,
    int Width,
    int Height,
    DateTimeOffset CreatedAt,
    bool IsTransferredToPc);

public sealed record RemoteCaptureResult(
    bool Success,
    string? PhotoId,
    string? FileName,
    long FileSizeBytes,
    string? Sha256,
    int Width,
    int Height,
    string? ErrorMessage);

public interface IRemotePhotoClient : IDisposable
{
    bool IsAuthenticated { get; }
    string? SessionToken { get; }
    string? CurrentBaseUrl { get; }

    Task<bool> AuthenticateAsync(string baseUrl, string pin, CancellationToken cancellationToken = default);
    Task<RemoteCaptureResult> TriggerCaptureAsync(CancellationToken cancellationToken = default);
    Task<string> DownloadPhotoAsync(
        string photoId,
        string expectedSha256,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemotePhotoItem>> ListPhotosAsync(CancellationToken cancellationToken = default);
    void ResetAuth();
}

/// <summary>
/// Android telefondaki Ölçü Atölyesi Kamera Servisi ile güvenli yetkilendirme,
/// yüksek çözünürlüklü uzaktan fotoğraf çekim komutu ve SHA-256 bütünlük doğrulamalı
/// dosya indirme işlemlerini yöneten istemci.
/// </summary>
public sealed class RemotePhotoClient : IRemotePhotoClient
{
    private readonly object _gate = new();
    private readonly HttpClient _httpClient;
    private string? _sessionToken;
    private string? _currentPin;
    private string? _currentBaseUrl;

    public RemotePhotoClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_sessionToken);
    public string? SessionToken => _sessionToken;
    public string? CurrentBaseUrl => _currentBaseUrl;

    public void ResetAuth()
    {
        lock (_gate)
        {
            _sessionToken = null;
            _currentPin = null;
            _currentBaseUrl = null;
        }
    }

    public async Task<bool> AuthenticateAsync(
        string baseUrl,
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return false;

        var normalizedUrl = baseUrl.Trim();
        if (!normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = "http://" + normalizedUrl;
        }
        if (!normalizedUrl.EndsWith("/"))
        {
            normalizedUrl += "/";
        }

        var cleanedPin = (pin ?? string.Empty).Trim();

        try
        {
            var authUrl = $"{normalizedUrl}api/v1/auth";
            var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
            request.Headers.Add("X-Session-Pin", cleanedPin);

            var jsonBody = JsonSerializer.Serialize(new { pin = cleanedPin });
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Warn,
                    "RemotePhotoClient",
                    "AUTH_REJECTED",
                    $"Kimlik doğrulama reddedildi (HTTP {(int)response.StatusCode}).");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("success", out var succ) && succ.GetBoolean() &&
                doc.RootElement.TryGetProperty("token", out var tokenProp))
            {
                lock (_gate)
                {
                    _sessionToken = tokenProp.GetString();
                    _currentBaseUrl = normalizedUrl;
                    _currentPin = cleanedPin;
                }

                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Info,
                    "RemotePhotoClient",
                    "AUTH_SUCCESS",
                    "Telefona oturum belirteci ile başarıyla kimlik doğrulandı.");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "RemotePhotoClient",
                "AUTH_ERROR",
                $"Kimlik doğrulama hatası: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Önbellekteki PIN ile oturumu yeniden açar. Telefon sunucusu yeniden başlatıldığında
    /// veya PIN yenilendiğinde eski belirteç geçersizleşir; bu metot yeni belirteç alır.
    /// </summary>
    private async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        string? pin;
        string? baseUrl;

        lock (_gate)
        {
            pin = _currentPin;
            baseUrl = _currentBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Warn,
            "RemotePhotoClient",
            "TOKEN_REFRESH_START",
            "Oturum belirteci geçersiz; PIN ile yeniden kimlik doğrulama deneniyor.");

        return await AuthenticateAsync(baseUrl, pin, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCaptureRequestAsync(
        string baseUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}api/v1/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(25));

        return await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string baseUrl,
        string relativeUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var url = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : $"{baseUrl}{relativeUrl.TrimStart('/')}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemoteCaptureResult> TriggerCaptureAsync(CancellationToken cancellationToken = default)
    {
        string? token;
        string? baseUrl;

        lock (_gate)
        {
            token = _sessionToken;
            baseUrl = _currentBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return new RemoteCaptureResult(false, null, null, 0, null, 0, 0, "Oturum açık değil. Lütfen önce bağlanın.");
        }

        try
        {
            var response = await SendCaptureRequestAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);

            // 401: belirteç süresi geçmiş veya telefon sunucusu yeniden başlatılmış olabilir.
            // Bir kez yeniden kimlik doğrula ve isteği tekrarla (sonsuz döngü yok).
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                response.Dispose();

                if (await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
                {
                    lock (_gate)
                    {
                        token = _sessionToken;
                        baseUrl = _currentBaseUrl;
                    }

                    if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return new RemoteCaptureResult(false, null, null, 0, null, 0, 0,
                            "Oturum yenilenemedi. Lütfen telefon bağlantısını kontrol edin.");
                    }

                    response = await SendCaptureRequestAsync(baseUrl, token, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return new RemoteCaptureResult(false, null, null, 0, null, 0, 0,
                        "Yetkisiz erişim. Telefondaki PIN kodunu kontrol edip yeniden bağlanın.");
                }
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errMsg = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Yetkisiz erişim. Telefondaki PIN kodunu kontrol edip yeniden bağlanın.",
                    System.Net.HttpStatusCode.Forbidden => "Telefon uygulaması arka planda veya kamera yayını kapalı. Telefonda uygulamayı ön plana alıp yayını başlatın.",
                    System.Net.HttpStatusCode.NotFound => "Telefonda çekim servisi bulunamadı. Uygulama sürümünü kontrol edin.",
                    (System.Net.HttpStatusCode)429 => "Telefonda çok fazla hatalı PIN denemesi yapıldı. Yaklaşık 1 dakika sonra tekrar deneyin.",
                    _ => $"Fotoğraf çekim isteği reddedildi (HTTP {(int)response.StatusCode})."
                };
                try
                {
                    using var errDoc = JsonDocument.Parse(content);
                    if (errDoc.RootElement.TryGetProperty("error", out var eProp))
                    {
                        errMsg = eProp.GetString() ?? errMsg;
                    }
                }
                catch { }

                return new RemoteCaptureResult(false, null, null, 0, null, 0, 0, errMsg);
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            if (!success)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "Bilinmeyen hata.";
                return new RemoteCaptureResult(false, null, null, 0, null, 0, 0, err);
            }

            var photoId = root.TryGetProperty("photoId", out var pid) ? pid.GetString() : null;
            var fileName = root.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;
            var size = root.TryGetProperty("fileSizeBytes", out var sz) ? sz.GetInt64() : 0;
            var sha256 = root.TryGetProperty("sha256", out var sh) ? sh.GetString() : null;
            var width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
            var height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 0;

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "RemotePhotoClient",
                "CAPTURE_SUCCESS",
                $"Telefonda çekim tamamlandı: {fileName} ({size} bayt, {width}x{height})");

            return new RemoteCaptureResult(true, photoId, fileName, size, sha256, width, height, null);
        }
        catch (OperationCanceledException)
        {
            return new RemoteCaptureResult(false, null, null, 0, null, 0, 0, "Fotoğraf çekim isteği zaman aşımına uğradı.");
        }
        catch (Exception ex)
        {
            return new RemoteCaptureResult(false, null, null, 0, null, 0, 0, $"Çekim sırasında hata: {ex.Message}");
        }
    }

    public async Task<string> DownloadPhotoAsync(
        string photoId,
        string expectedSha256,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(photoId))
        {
            throw new ArgumentException("Fotoğraf kimliği boş olamaz.", nameof(photoId));
        }

        Directory.CreateDirectory(targetDirectory);
        var finalPath = Path.Combine(targetDirectory, $"{photoId}.jpg");

        // 1. Önbellek kontrolü (Aynı dosya varsa tekrar indirme ve gereksiz kopya oluşturma)
        if (File.Exists(finalPath))
        {
            var existingSha = ComputeFileSha256(finalPath);
            if (string.Equals(existingSha, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Info,
                    "RemotePhotoClient",
                    "CACHE_HIT",
                    $"Fotoğraf önbellekte mevcut, tekrar indirilmedi: {Path.GetFileName(finalPath)}");
                progress?.Report(1.0);
                return finalPath;
            }
        }

        string? token;
        string? baseUrl;
        lock (_gate)
        {
            token = _sessionToken;
            baseUrl = _currentBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Oturum açık değil. Dosya indirilemez.");
        }

        var relativeUrl = $"api/v1/photos/{Uri.EscapeDataString(photoId)}";
        var tempPath = Path.Combine(targetDirectory, $"{photoId}_{Guid.NewGuid().ToString("N")[..6]}.tmp");

        try
        {
            var response = await SendAuthorizedGetAsync(baseUrl, relativeUrl, token, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                response.Dispose();

                if (await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
                {
                    lock (_gate)
                    {
                        token = _sessionToken;
                        baseUrl = _currentBaseUrl;
                    }

                    if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
                    {
                        throw new InvalidOperationException("Oturum yenilenemedi. Dosya indirilemez.");
                    }

                    response = await SendAuthorizedGetAsync(baseUrl, relativeUrl, token, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new HttpRequestException("Yetkisiz erişim. Oturum belirteci yenilenemedi.", null, System.Net.HttpStatusCode.Unauthorized);
                }
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Fotoğraf indirilemedi: HTTP {(int)response.StatusCode} {response.StatusCode}");
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var headerSha = response.Headers.TryGetValues("X-Sha256", out var vals) ? vals.FirstOrDefault() : null;

                using (var netStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buffer = new byte[65536];
                    long downloaded = 0;
                    int read;

                    while ((read = await netStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        sha256.AppendData(buffer, 0, read);
                        downloaded += read;

                        if (totalBytes > 0)
                        {
                            progress?.Report((double)downloaded / totalBytes);
                        }
                    }

                    await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    var computedSha = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();

                    // 2. SHA-256 Bütünlük Doğrulaması
                    var targetSha = !string.IsNullOrWhiteSpace(expectedSha256) ? expectedSha256.ToLowerInvariant() : headerSha?.ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(targetSha) && !string.Equals(computedSha, targetSha, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"SHA-256 bütünlük doğrulaması başarısız! Beklenen: {targetSha}, Hesaplanan: {computedSha}");
                    }
                }

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tempPath, finalPath);

                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Info,
                    "RemotePhotoClient",
                    "DOWNLOAD_SUCCESS",
                    $"Fotoğraf indirildi ve bütünlüğü doğrulandı: {Path.GetFileName(finalPath)} (SHA-256: {expectedSha256})");

                progress?.Report(1.0);
                return finalPath;
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "RemotePhotoClient",
                "DOWNLOAD_FAILED",
                $"Fotoğraf indirme veya bütünlük hatası: {ex.Message}");
            throw;
        }
    }

    public async Task<IReadOnlyList<RemotePhotoItem>> ListPhotosAsync(CancellationToken cancellationToken = default)
    {
        string? token;
        string? baseUrl;
        lock (_gate)
        {
            token = _sessionToken;
            baseUrl = _currentBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return [];
        }

        try
        {
            var response = await SendAuthorizedGetAsync(baseUrl, "api/v1/photos", token, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                response.Dispose();

                if (await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
                {
                    lock (_gate)
                    {
                        token = _sessionToken;
                        baseUrl = _currentBaseUrl;
                    }

                    if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return [];
                    }

                    response = await SendAuthorizedGetAsync(baseUrl, "api/v1/photos", token, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return [];
                }
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode) return [];

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(content);
                JsonElement arrayEl = default;
                var hasArray = false;

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    arrayEl = doc.RootElement;
                    hasArray = true;
                }
                else if (doc.RootElement.TryGetProperty("photos", out var photosProp) && photosProp.ValueKind == JsonValueKind.Array)
                {
                    arrayEl = photosProp;
                    hasArray = true;
                }

                if (hasArray)
                {
                    var list = new List<RemotePhotoItem>();
                    foreach (var el in arrayEl.EnumerateArray())
                    {
                        var id = el.TryGetProperty("photoId", out var i) ? i.GetString() ?? "" : "";
                        var fn = el.TryGetProperty("fileName", out var f) ? f.GetString() ?? "" : "";
                        var sz = el.TryGetProperty("fileSizeBytes", out var s) ? s.GetInt64() : 0;
                        var sh = el.TryGetProperty("sha256", out var h) ? h.GetString() ?? "" : "";
                        var w = el.TryGetProperty("width", out var wid) ? wid.GetInt32() : 0;
                        var ht = el.TryGetProperty("height", out var hei) ? hei.GetInt32() : 0;
                        var dt = el.TryGetProperty("createdAt", out var d) ? d.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                        var tr = el.TryGetProperty("isTransferredToPc", out var t) && t.GetBoolean();
                        list.Add(new RemotePhotoItem(id, fn, sz, sh, w, ht, dt, tr));
                    }
                    return list;
                }

                return [];
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "RemotePhotoClient",
                "LIST_PHOTOS_ERR",
                $"Uzaktan fotoğraf listeleme hatası: {ex.Message}");
            return [];
        }
    }

    public static string ComputeFileSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
