using System.Diagnostics;
using OlcuAtolyesi.Diagnostics;

namespace OlcuAtolyesi.Services;

public sealed record PreviewMetrics(
    double Fps,
    int Width,
    int Height,
    double LatencyMs,
    long TotalFramesPublished,
    long DroppedFrames);

/// <summary>
/// Kamera donanımından elde edilen sürekli önizleme karelerini düşük gecikmeyle,
/// eski kareleri atarak (drop lagging frames) ve gerçek metrikleri (FPS, çözünürlük, gecikme)
/// hesaplayarak yayınlayan işlem hattı.
/// </summary>
public sealed class ContinuousPreviewPipeline
{
    private readonly object _gate = new();
    private readonly Queue<long> _frameTimestamps = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    private long _totalFramesPublished;
    private long _droppedFrames;
    private double _currentFps;
    private double _lastLatencyMs;
    private int _width = 1280;
    private int _height = 720;
    private long _lastFrameTicks;

    public PreviewMetrics GetMetrics()
    {
        lock (_gate)
        {
            CleanOldTimestamps();
            return new PreviewMetrics(
                _currentFps,
                _width,
                _height,
                _lastLatencyMs,
                _totalFramesPublished,
                _droppedFrames);
        }
    }

    /// <summary>
    /// Yeni kareyi işlem hattına kabul eder. Önceki kare henüz işleniyorsa veya
    /// minimum kare süresi dolmadıysa geciken kareyi güvenle atar.
    /// </summary>
    public bool TryProcessFrame(
        byte[] frameBytes,
        int width,
        int height,
        long captureStartTimestampMs,
        out byte[]? outputFrame)
    {
        outputFrame = null;
        if (frameBytes == null || frameBytes.Length == 0)
        {
            return false;
        }

        var nowTicks = _uptime.ElapsedTicks;
        var nowMs = _uptime.ElapsedMilliseconds;

        lock (_gate)
        {
            // Hedef: ~30 FPS (Minimum ~30 ms aralık)
            var elapsedSinceLastMs = (nowTicks - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency;
            if (_lastFrameTicks > 0 && elapsedSinceLastMs < 30.0)
            {
                _droppedFrames++;
                return false;
            }

            _lastFrameTicks = nowTicks;
            _width = width > 0 ? width : _width;
            _height = height > 0 ? height : _height;
            _lastLatencyMs = Math.Max(0, nowMs - captureStartTimestampMs);

            _frameTimestamps.Enqueue(nowMs);
            CleanOldTimestamps();
            _totalFramesPublished++;

            outputFrame = frameBytes;
            return true;
        }
    }

    private void CleanOldTimestamps()
    {
        var cutoff = _uptime.ElapsedMilliseconds - 1000;
        while (_frameTimestamps.Count > 0 && _frameTimestamps.Peek() < cutoff)
        {
            _frameTimestamps.Dequeue();
        }

        _currentFps = _frameTimestamps.Count;
    }

    public void RecordDroppedFrame()
    {
        lock (_gate)
        {
            _droppedFrames++;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _frameTimestamps.Clear();
            _totalFramesPublished = 0;
            _droppedFrames = 0;
            _currentFps = 0.0;
            _lastLatencyMs = 0.0;
            _lastFrameTicks = 0;
        }
    }
}
