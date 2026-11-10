namespace OlcuAtolyesi.Services;

/// <summary>
/// Canlı önizleme hattında hedef kare hızını aşmayı engelleyen basit zamanlayıcı.
/// Cihazdan bağımsız saf mantıktır; otomatik testlerle doğrulanabilir.
/// </summary>
public sealed class PreviewFrameRateLimiter
{
    private readonly TimeSpan _minimumInterval;
    private long? _lastTimestampMs;

    public PreviewFrameRateLimiter(double targetFps)
    {
        if (targetFps <= 0 || double.IsNaN(targetFps) || double.IsInfinity(targetFps))
        {
            throw new ArgumentOutOfRangeException(nameof(targetFps), "Hedef FPS pozitif olmalıdır.");
        }

        TargetFps = targetFps;
        _minimumInterval = TimeSpan.FromMilliseconds(1000d / targetFps);
    }

    public double TargetFps { get; }

    /// <summary>Belirtilen zaman damgasında yeni kare alınmasına izin verilip verilmediğini döndürür.</summary>
    public bool ShouldCapture(long nowTimestampMs)
    {
        if (_lastTimestampMs.HasValue && nowTimestampMs - _lastTimestampMs.Value < _minimumInterval.TotalMilliseconds)
        {
            return false;
        }

        _lastTimestampMs = nowTimestampMs;
        return true;
    }

    public void Reset() => _lastTimestampMs = null;
}
