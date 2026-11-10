using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OlcuAtolyesi.Core.Phone;

public interface IScrcpyProcessManager : IDisposable
{
    string? ScrcpyExecutablePath { get; }

    bool IsScrcpyAvailable { get; }

    ScrcpySessionStatus CurrentStatus { get; }

    event EventHandler<ScrcpySessionStatus>? StatusChanged;

    Task<ScrcpyVersionInfo> GetVersionAsync(CancellationToken cancellationToken = default);

    Task<ScrcpyCameraQueryScanResult> QueryCamerasAsync(
        string serialId,
        CancellationToken cancellationToken = default);

    Task<ScrcpyCameraSizesQueryResult> QueryCameraSizesAsync(
        string serialId,
        string cameraId,
        CancellationToken cancellationToken = default);

    Task<ScrcpyLaunchResult> StartCameraSessionAsync(
        ScrcpyCameraSessionConfig config,
        CancellationToken cancellationToken = default);

    Task<bool> StopCameraSessionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// scrcpy yürütmesini ve canlı kamera oturumu yaşam döngüsünü kabuksuz, güvenli argümanlarla yönetir.
/// Uygulamanın başlatmadığı scrcpy süreçlerine kesinlikle müdahale etmez.
/// </summary>
public sealed class ScrcpyProcessManager : IScrcpyProcessManager, IDisposable
{
    private static readonly Regex SafeCameraIdRegex = new(@"^[a-zA-Z0-9_\.\-]+$", RegexOptions.Compiled);
    private static readonly Regex SafeSizeRegex = new(@"^\d{3,5}x\d{3,5}$", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly IScrcpyLocator _locator;
    private readonly TimeSpan _defaultTimeout;
    private Process? _managedProcess;
    private int? _managedProcessId;
    private ScrcpySessionStatus _currentStatus = ScrcpySessionStatus.Stopped();
    private bool _disposed;

    public ScrcpyProcessManager(
        IScrcpyLocator? locator = null,
        TimeSpan? defaultTimeout = null)
    {
        _locator = locator ?? new ScrcpyLocator();
        _defaultTimeout = defaultTimeout is { TotalMilliseconds: > 0 } validTimeout
            ? validTimeout
            : TimeSpan.FromSeconds(8);
    }

    public event EventHandler<ScrcpySessionStatus>? StatusChanged;

    public string? ScrcpyExecutablePath => _locator.FindScrcpyExecutablePath();

    public bool IsScrcpyAvailable => !string.IsNullOrWhiteSpace(ScrcpyExecutablePath);

    public ScrcpySessionStatus CurrentStatus
    {
        get
        {
            lock (_gate)
            {
                return _currentStatus;
            }
        }
    }

    public async Task<ScrcpyVersionInfo> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var scrcpyPath = ScrcpyExecutablePath;
        if (string.IsNullOrWhiteSpace(scrcpyPath))
        {
            return ScrcpyVersionInfo.NotFound;
        }

        var run = await ExecuteScrcpyCommandAsync(["--version"], _defaultTimeout, cancellationToken).ConfigureAwait(false);
        var combined = $"{run.StandardOutput}\n{run.StandardError}".Trim();
        return ScrcpyOutputParser.ParseVersion(combined);
    }

    public async Task<ScrcpyCameraQueryScanResult> QueryCamerasAsync(
        string serialId,
        CancellationToken cancellationToken = default)
    {
        if (!AdbInputValidator.IsValidSerial(serialId))
        {
            return new ScrcpyCameraQueryScanResult(
                false,
                [],
                "Geçersiz cihaz seri numarası.",
                "Cihaz seri numarası boş olamaz ve yalnızca güvenli karakterler içerebilir.");
        }

        var scrcpyPath = ScrcpyExecutablePath;
        if (string.IsNullOrWhiteSpace(scrcpyPath))
        {
            return new ScrcpyCameraQueryScanResult(
                false,
                [],
                "Bilgisayarda scrcpy aracı bulunamadı.",
                "scrcpy 5.0+ aracını kurun veya scrcpy.exe dizinini PATH'e ekleyin.");
        }

        var args = new[]
        {
            "-s", serialId,
            "--video-source=camera",
            "--list-cameras"
        };

        var result = await ExecuteScrcpyCommandAsync(args, _defaultTimeout, cancellationToken).ConfigureAwait(false);
        var combined = $"{result.StandardOutput}\n{result.StandardError}";

        if (combined.Contains("Device disconnected", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("device offline", StringComparison.OrdinalIgnoreCase))
        {
            return new ScrcpyCameraQueryScanResult(
                false,
                [],
                "Cihaz bağlantısı kesildi veya cihaz çevrimdışı.",
                combined.Trim());
        }

        if (combined.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new ScrcpyCameraQueryScanResult(
                false,
                [],
                "Cihaz yetkisiz (unauthorized) durumda; telefonda izin verin.",
                combined.Trim());
        }

        var cameras = ScrcpyOutputParser.ParseCameraList(combined);
        if (cameras.Count == 0)
        {
            var detailMsg = !string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardError.Trim()
                : (combined.Contains("Device disconnected", StringComparison.OrdinalIgnoreCase)
                    ? "Cihaz bağlantısı koptu."
                    : "Bağlı cihazda kamera sensörü algılanamadı veya kamera servisi meşgul.");
            return new ScrcpyCameraQueryScanResult(
                false,
                [],
                "Kamera sensörü bulunamadı veya listelenemedi.",
                detailMsg);
        }

        return new ScrcpyCameraQueryScanResult(
            true,
            cameras,
            $"{cameras.Count} adet kamera sensörü başarıyla algılandı.");
    }

    public async Task<ScrcpyCameraSizesQueryResult> QueryCameraSizesAsync(
        string serialId,
        string cameraId,
        CancellationToken cancellationToken = default)
    {
        if (!AdbInputValidator.IsValidSerial(serialId))
        {
            return new ScrcpyCameraSizesQueryResult(false, cameraId, [], "Geçersiz cihaz seri numarası.");
        }

        if (string.IsNullOrWhiteSpace(cameraId) || !SafeCameraIdRegex.IsMatch(cameraId))
        {
            return new ScrcpyCameraSizesQueryResult(false, cameraId, [], "Geçersiz kamera kimliği.");
        }

        var scrcpyPath = ScrcpyExecutablePath;
        if (string.IsNullOrWhiteSpace(scrcpyPath))
        {
            return new ScrcpyCameraSizesQueryResult(false, cameraId, [], "scrcpy aracı bulunamadı.");
        }

        var args = new[]
        {
            "-s", serialId,
            "--video-source=camera",
            $"--camera-id={cameraId}",
            "--list-camera-sizes"
        };

        var result = await ExecuteScrcpyCommandAsync(args, _defaultTimeout, cancellationToken).ConfigureAwait(false);
        var combined = $"{result.StandardOutput}\n{result.StandardError}";
        var sizes = ScrcpyOutputParser.ParseCameraSizes(combined);

        // scrcpy --list-camera-sizes boş dönerse veya hata verirse standart boyutları sun
        if (sizes.Count == 0)
        {
            sizes = GetFallbackStandardSizes();
        }

        return new ScrcpyCameraSizesQueryResult(
            true,
            cameraId,
            sizes,
            $"{sizes.Count} çözünürlük seçeneği listelendi.");
    }

    public async Task<ScrcpyLaunchResult> StartCameraSessionAsync(
        ScrcpyCameraSessionConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!AdbInputValidator.IsValidSerial(config.SerialId))
        {
            return new ScrcpyLaunchResult(false, null, "Geçersiz cihaz seri numarası.");
        }

        if (string.IsNullOrWhiteSpace(config.CameraId) || !SafeCameraIdRegex.IsMatch(config.CameraId))
        {
            return new ScrcpyLaunchResult(false, null, "Geçersiz kamera kimliği seçimi.");
        }

        var size = string.IsNullOrWhiteSpace(config.CameraSize) ? "1920x1080" : config.CameraSize.Trim();
        if (!SafeSizeRegex.IsMatch(size))
        {
            return new ScrcpyLaunchResult(false, null, "Geçersiz kamera çözünürlük biçimi (ör. 1920x1080 olmalıdır).");
        }

        var bitrate = string.IsNullOrWhiteSpace(config.VideoBitRate) ? "20M" : config.VideoBitRate.Trim();

        var scrcpyPath = ScrcpyExecutablePath;
        if (string.IsNullOrWhiteSpace(scrcpyPath) || !File.Exists(scrcpyPath))
        {
            UpdateStatus(new ScrcpySessionStatus(
                ScrcpySessionState.Error,
                null,
                config.SerialId,
                config.CameraId,
                size,
                "scrcpy aracı bulunamadığı için kamera başlatılamadı.",
                "scrcpy.exe dosyasının bilgisayarda kurulu olduğundan emin olun."));
            return new ScrcpyLaunchResult(false, null, "scrcpy.exe bulunamadı.");
        }

        // Önceki kendi başlattığımız oturum varsa temizle
        await StopCameraSessionAsync(cancellationToken).ConfigureAwait(false);

        UpdateStatus(new ScrcpySessionStatus(
            ScrcpySessionState.Starting,
            null,
            config.SerialId,
            config.CameraId,
            size,
            $"Kamera oturumu başlatılıyor (Kamera ID: {config.CameraId}, {size})..."));

        var windowTitle = string.IsNullOrWhiteSpace(config.WindowTitle)
            ? $"Ölçü Atölyesi — Canlı Kamera (ID: {config.CameraId} • {config.SerialId})"
            : config.WindowTitle.Trim();

        var startInfo = new ProcessStartInfo
        {
            FileName = scrcpyPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // GÜVENLİK: Kabuk olmadan doğrudan ArgumentList kullanımı
        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(config.SerialId);
        startInfo.ArgumentList.Add("--video-source=camera");
        startInfo.ArgumentList.Add($"--camera-id={config.CameraId}");
        startInfo.ArgumentList.Add($"--camera-size={size}");
        startInfo.ArgumentList.Add($"--video-bit-rate={bitrate}");

        if (config.NoAudio)
        {
            startInfo.ArgumentList.Add("--no-audio");
        }

        if (config.CameraFps is { } fps && fps > 0)
        {
            startInfo.ArgumentList.Add($"--camera-fps={fps}");
        }

        startInfo.ArgumentList.Add($"--window-title={windowTitle}");

        if (config.WindowWidth is { } ww && ww > 0)
        {
            startInfo.ArgumentList.Add($"--window-width={ww}");
        }

        if (config.WindowHeight is { } wh && wh > 0)
        {
            startInfo.ArgumentList.Add($"--window-height={wh}");
        }

        if (config.WindowX is { } wx)
        {
            startInfo.ArgumentList.Add($"--window-x={wx}");
        }

        if (config.WindowY is { } wy)
        {
            startInfo.ArgumentList.Add($"--window-y={wy}");
        }

        Process process;
        try
        {
            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
        }
        catch (Exception ex)
        {
            UpdateStatus(new ScrcpySessionStatus(
                ScrcpySessionState.Error,
                null,
                config.SerialId,
                config.CameraId,
                size,
                "Kamera süreci oluşturulamadı.",
                ex.Message));
            return new ScrcpyLaunchResult(false, null, $"Süreç oluşturulamadı: {ex.Message}");
        }

        var stderrSb = new StringBuilder();
        var stdoutSb = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdoutSb.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrSb.AppendLine(e.Data);
            }
        };

        process.Exited += (_, _) =>
        {
            HandleManagedProcessExited(process, stderrSb.ToString());
        };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                UpdateStatus(new ScrcpySessionStatus(
                    ScrcpySessionState.Error,
                    null,
                    config.SerialId,
                    config.CameraId,
                    size,
                    "scrcpy süreci başlatılamadı."));
                return new ScrcpyLaunchResult(false, null, "scrcpy süreci başlatılamadı.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_gate)
            {
                _managedProcess = process;
                _managedProcessId = process.Id;
            }
        }
        catch (Exception ex)
        {
            process.Dispose();
            UpdateStatus(new ScrcpySessionStatus(
                ScrcpySessionState.Error,
                null,
                config.SerialId,
                config.CameraId,
                size,
                "scrcpy başlatılamadı.",
                ex.Message));
            return new ScrcpyLaunchResult(false, null, $"scrcpy başlatılamadı: {ex.Message}");
        }

        // Başlangıçta anlık çökme/hata kontrolü (ör. kamera meşgul, Android < 12, vb.)
        try
        {
            await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await StopCameraSessionAsync(CancellationToken.None).ConfigureAwait(false);
            return new ScrcpyLaunchResult(false, null, "Kamera başlatma iptal edildi.");
        }

        if (process.HasExited)
        {
            var err = stderrSb.ToString().Trim();
            var friendlyMsg = BuildFriendlyErrorMessage(err);
            UpdateStatus(new ScrcpySessionStatus(
                ScrcpySessionState.Error,
                null,
                config.SerialId,
                config.CameraId,
                size,
                friendlyMsg,
                err));
            return new ScrcpyLaunchResult(false, null, friendlyMsg, err);
        }

        UpdateStatus(new ScrcpySessionStatus(
            ScrcpySessionState.Running,
            process.Id,
            config.SerialId,
            config.CameraId,
            size,
            $"Canlı kamera açık (PID: {process.Id} • {size}).",
            StartedAt: DateTimeOffset.UtcNow));

        return new ScrcpyLaunchResult(
            true,
            process.Id,
            $"Kamera oturumu başarıyla başlatıldı (PID: {process.Id}).");
    }

    public async Task<bool> StopCameraSessionAsync(CancellationToken cancellationToken = default)
    {
        Process? target;
        int? targetPid;
        lock (_gate)
        {
            target = _managedProcess;
            targetPid = _managedProcessId;
            _managedProcess = null;
            _managedProcessId = null;
        }

        if (target is null)
        {
            UpdateStatus(ScrcpySessionStatus.Stopped("Kamera oturumu zaten kapalı."));
            return true;
        }

        try
        {
            if (!target.HasExited)
            {
                try
                {
                    target.CloseMainWindow();
                }
                catch
                {
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                try
                {
                    await target.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (!target.HasExited)
                    {
                        target.Kill(entireProcessTree: true);
                    }
                }
            }

            target.Dispose();
        }
        catch
        {
        }

        UpdateStatus(ScrcpySessionStatus.Stopped(
            targetPid.HasValue
                ? $"Canlı kamera oturumu (PID: {targetPid.Value}) kapatıldı."
                : "Canlı kamera kapatıldı."));
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopCameraSessionAsync().GetAwaiter().GetResult();
    }

    private void HandleManagedProcessExited(Process process, string standardError)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_managedProcess, process))
            {
                return;
            }

            _managedProcess = null;
            _managedProcessId = null;
        }

        try
        {
            var exitCode = process.ExitCode;
            process.Dispose();

            if (exitCode != 0)
            {
                var friendly = BuildFriendlyErrorMessage(standardError);
                UpdateStatus(new ScrcpySessionStatus(
                    ScrcpySessionState.Error,
                    null,
                    _currentStatus.SerialId,
                    _currentStatus.CameraId,
                    _currentStatus.Resolution,
                    friendly,
                    standardError.Trim()));
            }
            else
            {
                UpdateStatus(ScrcpySessionStatus.Stopped("Kamera oturumu kullanıcı tarafından kapatıldı."));
            }
        }
        catch
        {
            UpdateStatus(ScrcpySessionStatus.Stopped("Kamera oturumu sonlandı."));
        }
    }

    private static string BuildFriendlyErrorMessage(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "scrcpy kamera süreci beklenmedik biçimde sonlandı.";
        }

        if (stderr.Contains("Camera is already in use", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("in use", StringComparison.OrdinalIgnoreCase))
        {
            return "Hata: Telefon kamerası meşgul (başka bir uygulama tarafından kullanılıyor olabilir).";
        }

        if (stderr.Contains("requires Android 12", StringComparison.OrdinalIgnoreCase))
        {
            return "Hata: scrcpy kamera yansıtma özelliği Android 12 ve üzeri gerektirir.";
        }

        if (stderr.Contains("Device disconnected", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("device not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Hata: Telefonun USB bağlantısı kesildi.";
        }

        if (stderr.Contains("could not find camera", StringComparison.OrdinalIgnoreCase))
        {
            return "Hata: Seçilen kamera kimliği cihazda bulunamadı.";
        }

        return $"scrcpy hatası: {stderr.Trim()}";
    }

    private async Task<AdbCommandResult> ExecuteScrcpyCommandAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var scrcpyPath = ScrcpyExecutablePath;
        if (string.IsNullOrWhiteSpace(scrcpyPath))
        {
            return new AdbCommandResult(-1, string.Empty, string.Empty, FailureReason: "scrcpy bulunamadı.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = scrcpyPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new AdbCommandResult(-1, string.Empty, string.Empty, FailureReason: "scrcpy başlatılamadı.");
            }
        }
        catch (Exception ex)
        {
            return new AdbCommandResult(-1, string.Empty, ex.Message, FailureReason: ex.Message);
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new AdbCommandResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new AdbCommandResult(-1, string.Empty, string.Empty, IsCancelled: true, FailureReason: "İptal edildi.");
            }

            return new AdbCommandResult(-1, string.Empty, string.Empty, IsTimedOut: true, FailureReason: "Zaman aşımı.");
        }
    }

    public static IReadOnlyList<ScrcpyCameraSizeOption> GetFallbackStandardSizes() =>
    [
        new(3840, 2160, "16:9", [30]),
        new(1920, 1080, "16:9", [15, 20, 24, 30]),
        new(1280, 720, "16:9", [15, 20, 24, 30]),
        new(4080, 3060, "4:3", [15, 20, 24, 30]),
        new(4000, 3000, "4:3", [15, 20, 24, 30]),
        new(640, 480, "4:3", [30])
    ];

    private void UpdateStatus(ScrcpySessionStatus status)
    {
        lock (_gate)
        {
            _currentStatus = status;
        }

        StatusChanged?.Invoke(this, status);
    }
}
