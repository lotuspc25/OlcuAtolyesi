using System.Diagnostics;
using System.Net;
using System.Text;

namespace OlcuAtolyesi.Core.Phone;

public sealed record CameraStreamConnectResult(
    bool Success,
    string Message,
    string? ErrorDetail = null,
    bool IsUnauthorized = false);

public interface ICameraStreamClient : IDisposable
{
    bool IsConnected { get; }
    double Fps { get; }
    long ReceivedFrameCount { get; }
    string? CurrentServerUrl { get; }

    event EventHandler<byte[]>? FrameReceived;
    event EventHandler<string>? StatusChanged;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<bool>? ConnectionStateChanged;

    Task<CameraStreamConnectResult> ConnectAsync(
        string serverUrl,
        string pin,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

/// <summary>
/// Ölçü Atölyesi Android Kamera Servisi'nden yerel ağ (Wi-Fi) üzerinden
/// multipart/x-mixed-replace MJPEG akışını alan istemci.
/// </summary>
public sealed class CameraStreamClient : ICameraStreamClient
{
    private static readonly byte[] HeaderEndCrlf = [0x0D, 0x0A, 0x0D, 0x0A]; // \r\n\r\n
    private static readonly byte[] HeaderEndLf = [0x0A, 0x0A]; // \n\n
    private static readonly byte[] DefaultBoundary = "--frame"u8.ToArray();
    private static readonly byte[] JpegSoi = [0xFF, 0xD8];

    private readonly object _gate = new();
    private HttpClient? _httpClient;
    private CancellationTokenSource? _streamCts;
    private Task? _readTask;

    private volatile bool _isConnected;
    private double _fps;
    private long _receivedFrameCount;
    private string? _currentServerUrl;

    private int _fpsCounter;
    private readonly Stopwatch _fpsStopwatch = new();

    public bool IsConnected => _isConnected;
    public double Fps => _fps;
    public long ReceivedFrameCount => _receivedFrameCount;
    public string? CurrentServerUrl => _currentServerUrl;

    public event EventHandler<byte[]>? FrameReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<bool>? ConnectionStateChanged;

    public async Task<CameraStreamConnectResult> ConnectAsync(
        string serverUrl,
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return new CameraStreamConnectResult(false, "Sunucu adresi boş olamaz.");
        }

        var normalizedUrl = serverUrl.Trim();
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

        lock (_gate)
        {
            if (_isConnected)
            {
                _ = DisconnectAsync();
            }

            _streamCts?.Dispose();
            _streamCts = new CancellationTokenSource();
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        var streamUrl = $"{normalizedUrl}stream?pin={Uri.EscapeDataString(cleanedPin)}";
        _currentServerUrl = normalizedUrl;

        try
        {
            NotifyStatus("Android kamera servisine bağlanılıyor...");

            var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
            request.Headers.Add("X-Session-Pin", cleanedPin);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(8));

            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var errDetail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                NotifyError("Eşleştirme kodu (PIN) hatalı.");
                await CleanupStateAsync().ConfigureAwait(false);
                return new CameraStreamConnectResult(
                    false,
                    "Eşleştirme kodu (PIN) hatalı veya eksik.",
                    !string.IsNullOrWhiteSpace(errDetail) ? errDetail : "Telefondaki 6 haneli PIN kodunu kontrol edin.",
                    IsUnauthorized: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"Bağlantı hatası (HTTP {(int)response.StatusCode} {response.StatusCode})";
                NotifyError(msg);
                await CleanupStateAsync().ConfigureAwait(false);
                return new CameraStreamConnectResult(false, msg);
            }

            // Multipart boundary bilgisini Content-Type başlığından çıkart
            var boundaryBytes = DefaultBoundary;
            if (response.Content.Headers.ContentType != null)
            {
                var boundaryParam = response.Content.Headers.ContentType.Parameters
                    .FirstOrDefault(p => string.Equals(p.Name, "boundary", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(boundaryParam?.Value))
                {
                    var bVal = boundaryParam.Value.Trim('"', '\'');
                    if (!bVal.StartsWith("--"))
                    {
                        bVal = "--" + bVal;
                    }
                    boundaryBytes = Encoding.ASCII.GetBytes(bVal);
                }
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _isConnected = true;
                _fpsCounter = 0;
                _fps = 0;
                _fpsStopwatch.Restart();
            }

            NotifyStatus($"Kamera servisine bağlandı ({normalizedUrl}). Canlı görüntü alınıyor.");
            NotifyConnectionState(true);

            var streamToken = _streamCts.Token;
            _readTask = Task.Run(() => ReadMjpegStreamAsync(stream, boundaryBytes, streamToken), streamToken);

            return new CameraStreamConnectResult(true, "Canlı kamera yayınına başarıyla bağlanıldı.");
        }
        catch (OperationCanceledException)
        {
            await CleanupStateAsync().ConfigureAwait(false);
            NotifyStatus("Bağlantı iptal edildi.");
            return new CameraStreamConnectResult(false, "Bağlantı isteği iptal edildi veya zaman aşımına uğradı.");
        }
        catch (Exception ex)
        {
            await CleanupStateAsync().ConfigureAwait(false);
            var msg = "Telefona bağlanılamadı. Wi-Fi bağlantınızı ve IP/Port adresini kontrol edin.";
            NotifyError($"{msg} Detay: {ex.Message}");
            return new CameraStreamConnectResult(false, msg, ex.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        lock (_gate)
        {
            _streamCts?.Cancel();
        }

        if (_readTask != null)
        {
            try
            {
                await _readTask.ConfigureAwait(false);
            }
            catch
            {
            }
            _readTask = null;
        }

        await CleanupStateAsync().ConfigureAwait(false);

        lock (_gate)
        {
            _streamCts?.Dispose();
            _streamCts = null;
        }

        NotifyStatus("Kamera bağlantısı kesildi.");
    }

    private async Task CleanupStateAsync()
    {
        bool wasConnected;
        lock (_gate)
        {
            wasConnected = _isConnected;
            _isConnected = false;

            try { _streamCts?.Cancel(); } catch { }
            try { _httpClient?.Dispose(); } catch { }
            _httpClient = null;

            _fps = 0;
            _fpsCounter = 0;
            _fpsStopwatch.Reset();
        }

        if (wasConnected)
        {
            NotifyConnectionState(false);
        }
    }

    private async Task ReadMjpegStreamAsync(Stream stream, byte[] boundaryBytes, CancellationToken token)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[16384];

        try
        {
            while (!token.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (bytesRead <= 0)
                {
                    break;
                }

                ms.Write(buffer, 0, bytesRead);

                // Bellekteki tam kareleri sırayla ayrıştır
                while (true)
                {
                    var data = ms.GetBuffer();
                    var dataLength = (int)ms.Length;
                    if (dataLength < 4)
                    {
                        break;
                    }

                    // 1. Başlık sonunu ara (\r\n\r\n veya \n\n)
                    var headerEnd = FindSequence(data, dataLength, HeaderEndCrlf, 0);
                    var headerEndLen = 4;
                    if (headerEnd < 0)
                    {
                        headerEnd = FindSequence(data, dataLength, HeaderEndLf, 0);
                        headerEndLen = 2;
                    }

                    if (headerEnd < 0)
                    {
                        // Başlık henüz tamamlanmadı, ağdan daha fazla veri bekleniyor
                        break;
                    }

                    var headerText = Encoding.ASCII.GetString(data, 0, headerEnd);
                    var bodyStartIndex = headerEnd + headerEndLen;
                    var contentLength = TryExtractContentLength(headerText);

                    if (contentLength.HasValue && contentLength.Value > 0)
                    {
                        var neededTotal = bodyStartIndex + contentLength.Value;
                        if (dataLength < neededTotal)
                        {
                            // Gövdenin tamamı henüz gelmedi, bekle
                            break;
                        }

                        // Karenin tamamı mevcut
                        var frameBytes = new byte[contentLength.Value];
                        Buffer.BlockCopy(data, bodyStartIndex, frameBytes, 0, contentLength.Value);

                        var nextPos = neededTotal;
                        while (nextPos < dataLength && (data[nextPos] == '\r' || data[nextPos] == '\n'))
                        {
                            nextPos++;
                        }

                        TrimBuffer(ms, nextPos);
                        DispatchFrame(frameBytes);
                        continue;
                    }
                    else
                    {
                        // Content-Length yoksa sonraki boundary'yi ara
                        var nextBoundary = FindSequence(data, dataLength, boundaryBytes, bodyStartIndex);
                        if (nextBoundary < 0)
                        {
                            // Sonraki boundary henüz gelmedi, bekle
                            break;
                        }

                        var bodyLength = nextBoundary - bodyStartIndex;
                        while (bodyLength > 0 && (data[bodyStartIndex + bodyLength - 1] == '\r' || data[bodyStartIndex + bodyLength - 1] == '\n'))
                        {
                            bodyLength--;
                        }

                        if (bodyLength > 0)
                        {
                            var frameBytes = new byte[bodyLength];
                            Buffer.BlockCopy(data, bodyStartIndex, frameBytes, 0, bodyLength);
                            DispatchFrame(frameBytes);
                        }

                        TrimBuffer(ms, nextBoundary);
                        continue;
                    }
                }

                // Bozuk veri / bellek şişmesi koruması
                if (ms.Length > 10 * 1024 * 1024)
                {
                    var data = ms.GetBuffer();
                    var dataLength = (int)ms.Length;
                    var resyncIndex = FindSequence(data, dataLength, boundaryBytes, 1);
                    if (resyncIndex > 0)
                    {
                        TrimBuffer(ms, resyncIndex);
                    }
                    else
                    {
                        ms.SetLength(0);
                        ms.Position = 0;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_isConnected)
            {
                NotifyError($"Kamera akışı kesildi: {ex.Message}");
            }
        }
        finally
        {
            await CleanupStateAsync().ConfigureAwait(false);
            NotifyStatus("Canlı kamera akışı sonlandı.");
        }
    }

    private void DispatchFrame(byte[] frameBytes)
    {
        if (frameBytes == null || frameBytes.Length < 4)
        {
            return;
        }

        // JPEG başlangıç işaretçisini (SOI: 0xFF, 0xD8) doğrula
        var startIndex = 0;
        if (frameBytes[0] != 0xFF || frameBytes[1] != 0xD8)
        {
            startIndex = FindSequence(frameBytes, frameBytes.Length, JpegSoi, 0);
            if (startIndex < 0)
            {
                return;
            }
        }

        byte[] finalBytes;
        if (startIndex == 0)
        {
            finalBytes = frameBytes;
        }
        else
        {
            finalBytes = new byte[frameBytes.Length - startIndex];
            Buffer.BlockCopy(frameBytes, startIndex, finalBytes, 0, finalBytes.Length);
        }

        _receivedFrameCount++;
        _fpsCounter++;
        if (!_fpsStopwatch.IsRunning)
        {
            _fpsStopwatch.Start();
        }
        else if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _fps = Math.Round(_fpsCounter * 1000.0 / _fpsStopwatch.ElapsedMilliseconds, 1);
            _fpsCounter = 0;
            _fpsStopwatch.Restart();
        }

        FrameReceived?.Invoke(this, finalBytes);
    }

    private static int? TryExtractContentLength(string headerText)
    {
        var lines = headerText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var valPart = line.Substring("Content-Length:".Length).Trim();
                if (int.TryParse(valPart, out var len) && len > 0 && len < 30_000_000)
                {
                    return len;
                }
            }
        }

        return null;
    }

    private static void TrimBuffer(MemoryStream ms, int consumedBytes)
    {
        var remaining = (int)ms.Length - consumedBytes;
        if (remaining > 0)
        {
            var buf = ms.GetBuffer();
            Buffer.BlockCopy(buf, consumedBytes, buf, 0, remaining);
            ms.SetLength(remaining);
            ms.Position = remaining;
        }
        else
        {
            ms.SetLength(0);
            ms.Position = 0;
        }
    }

    private static int FindSequence(byte[] buffer, int length, byte[] pattern, int startIndex)
    {
        if (pattern == null || pattern.Length == 0 || length < pattern.Length || startIndex < 0)
        {
            return -1;
        }

        var maxStart = length - pattern.Length;
        for (var i = startIndex; i <= maxStart; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private void NotifyStatus(string message) => StatusChanged?.Invoke(this, message);
    private void NotifyError(string message) => ErrorOccurred?.Invoke(this, message);
    private void NotifyConnectionState(bool isConnected) => ConnectionStateChanged?.Invoke(this, isConnected);

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
