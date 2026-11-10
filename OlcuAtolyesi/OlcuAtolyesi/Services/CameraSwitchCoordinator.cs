using OlcuAtolyesi.Diagnostics;

namespace OlcuAtolyesi.Services;

/// <summary>
/// Kamera değişim durum makinesi adımları.
/// </summary>
public enum CameraSwitchState
{
    Idle = 0,
    Streaming = 1,
    Switching = 2,
    Faulted = 3
}

/// <summary>
/// Android kamera sensörü değişimini (Ön/Arka) eşzamanlı çakışmalardan, yarış koşullarından (race condition)
/// ve donanım çakışmalarından koruyan iş parçacığı güvenli durum koordinatörü.
/// </summary>
public sealed class CameraSwitchCoordinator : IDisposable
{
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly object _stateLock = new();

    private CameraSwitchState _state = CameraSwitchState.Idle;
    private bool _isFrontCamera;
    private string? _currentCorrelationId;
    private bool _isDisposed;

    public CameraSwitchState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            lock (_stateLock)
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(this, value);
                }
            }
        }
    }

    public bool IsFrontCamera
    {
        get { lock (_stateLock) return _isFrontCamera; }
        set { lock (_stateLock) _isFrontCamera = value; }
    }

    public string? CurrentCorrelationId
    {
        get { lock (_stateLock) return _currentCorrelationId; }
    }

    public SemaphoreSlim CaptureGate => _captureGate;

    public event EventHandler<CameraSwitchState>? StateChanged;

    /// <summary>
    /// Kamera değişimini başlatmayı dener. Zaten bir değişim sürüyorsa false döner.
    /// </summary>
    public async Task<(bool CanSwitch, string CorrelationId)> TryBeginSwitchAsync()
    {
        if (_isDisposed)
        {
            return (false, string.Empty);
        }

        var cid = $"cam-switch-{Guid.NewGuid().ToString("N")[..8]}";

        if (!await _switchLock.WaitAsync(0))
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Warn,
                "CameraSwitch",
                "CAM_SWITCH_ALREADY_IN_PROGRESS",
                "Kamera değişimi zaten sürüyor; yeni istek güvenli biçimde reddedildi.",
                cid);
            return (false, string.Empty);
        }

        lock (_stateLock)
        {
            _currentCorrelationId = cid;
            State = CameraSwitchState.Switching;
        }

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "CameraSwitch",
            "CAM_SWITCH_STARTED",
            $"Kamera sensörü değişimi başlatıldı. Hedef: {(IsFrontCamera ? "Arka Kamera" : "Ön Kamera")}",
            cid);

        return (true, cid);
    }

    /// <summary>
    /// Devam eden kare yakalama işleminin bitmesini bekler ve yakalama kapısını kilitler.
    /// </summary>
    public async Task<bool> PauseCaptureAndAcquireGateAsync(int timeoutMs = 2500)
    {
        try
        {
            return await _captureGate.WaitAsync(timeoutMs);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Yakalama kapısını tekrar serbest bırakır.
    /// </summary>
    public void ReleaseCaptureGate()
    {
        try
        {
            if (_captureGate.CurrentCount == 0)
            {
                _captureGate.Release();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Android Camera2 HAL donanımının önceki oturumu kapatması ve tamponları boşaltması için
    /// güvenli bekleme sağlar.
    /// </summary>
    public async Task SettleHardwareDelayAsync(int ms = 300)
    {
        if (ms > 0)
        {
            await Task.Delay(ms);
        }
    }

    /// <summary>
    /// Kamera değişimini başarıyla tamamlar.
    /// </summary>
    public void CompleteSwitch(bool newIsFront, string correlationId)
    {
        lock (_stateLock)
        {
            _isFrontCamera = newIsFront;
            State = CameraSwitchState.Streaming;
            _currentCorrelationId = null;
        }

        ReleaseCaptureGate();
        _switchLock.Release();

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "CameraSwitch",
            "CAM_SWITCH_COMPLETED",
            $"Kamera değişimi başarıyla tamamlandı. Aktif sensör: {(newIsFront ? "Ön Kamera" : "Arka Kamera")}",
            correlationId);
    }

    /// <summary>
    /// Kamera değişimi başarısız olduğunda durumu günceller ve kilitleri serbest bırakır.
    /// </summary>
    public void FailSwitch(string correlationId, Exception? ex)
    {
        lock (_stateLock)
        {
            State = CameraSwitchState.Faulted;
            _currentCorrelationId = null;
        }

        ReleaseCaptureGate();
        _switchLock.Release();

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Error,
            "CameraSwitch",
            "CAM_SWITCH_FAILED",
            $"Kamera değişimi sırasında hata: {ex?.Message ?? "Bilinmeyen hata"}",
            correlationId,
            ex?.ToString());
    }

    /// <summary>
    /// Yayın başladığında durumu Streaming yapar.
    /// </summary>
    public void SetStreaming()
    {
        lock (_stateLock)
        {
            State = CameraSwitchState.Streaming;
        }
    }

    /// <summary>
    /// Yayın durduğunda durumu Idle yapar.
    /// </summary>
    public void ResetToIdle()
    {
        lock (_stateLock)
        {
            State = CameraSwitchState.Idle;
            _currentCorrelationId = null;
        }
        ReleaseCaptureGate();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _switchLock.Dispose();
        _captureGate.Dispose();
    }
}
