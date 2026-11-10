using System.Globalization;

namespace OlcuAtolyesi.Core.Phone;

public enum ScrcpyCameraFacing
{
    Back = 0,
    Front = 1,
    External = 2,
    Unknown = 3
}

public static class ScrcpyCameraFacingExtensions
{
    public static string DisplayName(this ScrcpyCameraFacing facing) => facing switch
    {
        ScrcpyCameraFacing.Back => "Arka Kamera",
        ScrcpyCameraFacing.Front => "Ön Kamera",
        ScrcpyCameraFacing.External => "Harici Kamera",
        _ => "Kamera Sensörü"
    };
}

/// <summary>scrcpy sürüm bilgisi ve destek durumu.</summary>
public sealed record ScrcpyVersionInfo(
    string RawOutput,
    string VersionText,
    bool IsFound,
    bool IsSupported)
{
    public static ScrcpyVersionInfo NotFound =>
        new(string.Empty, "Bulunamadı", IsFound: false, IsSupported: false);
}

/// <summary>scrcpy --video-source=camera --list-cameras ile sorgulanan cihaz kamerası bilgisi.</summary>
public sealed record ScrcpyCameraInfo(
    string Id,
    ScrcpyCameraFacing Facing,
    int NativeWidth,
    int NativeHeight,
    IReadOnlyList<int> SupportedFps,
    double MinZoom,
    double MaxZoom,
    string RawDetails = "")
{
    public string DisplayTitle
    {
        get
        {
            var facingText = Facing.DisplayName();
            var resText = NativeWidth > 0 && NativeHeight > 0 ? $"{NativeWidth}×{NativeHeight}" : "Çözünürlük algılanamadı";
            var fpsText = SupportedFps.Count > 0 ? $" • {string.Join("/", SupportedFps)} FPS" : string.Empty;
            var zoomText = MinZoom > 0 && MaxZoom >= MinZoom && (MinZoom != 1.0 || MaxZoom != 1.0)
                ? $" • Zoom: {MinZoom.ToString("0.##", CultureInfo.InvariantCulture)}–{MaxZoom.ToString("0.##", CultureInfo.InvariantCulture)}×"
                : string.Empty;
            return $"{facingText} (ID: {Id} • {resText}{fpsText}{zoomText})";
        }
    }

    public string DimensionsSummary =>
        NativeWidth > 0 && NativeHeight > 0 ? $"{NativeWidth}×{NativeHeight}" : "—";

    public string FpsSummary =>
        SupportedFps.Count > 0 ? string.Join("/", SupportedFps) + " FPS" : "15/20/24/30 FPS";
}

/// <summary>scrcpy --list-camera-sizes ile sorgulanan veya kameranın doğal çözünürlük seçeneği.</summary>
public sealed record ScrcpyCameraSizeOption(
    int Width,
    int Height,
    string AspectRatioLabel = "",
    IReadOnlyList<int>? SupportedFps = null,
    bool IsHighSpeed = false)
{
    public string SizeArgument => $"{Width}x{Height}";

    public string DisplayText
    {
        get
        {
            var ar = !string.IsNullOrWhiteSpace(AspectRatioLabel) ? $" ({AspectRatioLabel})" : string.Empty;
            var hs = IsHighSpeed ? " [Yüksek Hız / Slow-Motion]" : string.Empty;
            var fps = SupportedFps is { Count: > 0 } ? $" • {string.Join("/", SupportedFps)} FPS" : string.Empty;
            return $"{Width}×{Height}{ar}{fps}{hs}";
        }
    }

    public bool HasFps(int fps) => SupportedFps is { Count: > 0 } && SupportedFps.Contains(fps);

    public int DefaultOrBestFps =>
        SupportedFps is { Count: > 0 }
            ? (IsHighSpeed
                ? SupportedFps.Max()
                : (SupportedFps.Contains(30) ? 30 : (SupportedFps.Contains(60) ? 60 : SupportedFps[0])))
            : 30;
}

/// <summary>Canlı kamera oturumu başlatma parametreleri.</summary>
public sealed record ScrcpyCameraSessionConfig(
    string SerialId,
    string CameraId = "0",
    string CameraSize = "1920x1080",
    string VideoBitRate = "20M",
    bool NoAudio = true,
    int? CameraFps = null,
    string? WindowTitle = null,
    int? WindowWidth = null,
    int? WindowHeight = null,
    int? WindowX = null,
    int? WindowY = null)
{
    public static readonly IReadOnlyList<string> StandardBitrates = ["8M", "12M", "16M", "20M", "30M"];
    public static readonly IReadOnlyList<int> CommonFpsOptions = [15, 20, 24, 30, 60];

    public IReadOnlyList<string> BuildCommandLineArguments()
    {
        var list = new List<string>
        {
            "-s",
            SerialId,
            "--video-source=camera",
            $"--camera-id={CameraId}",
            $"--camera-size={CameraSize}",
            $"--video-bit-rate={VideoBitRate}"
        };

        if (NoAudio) list.Add("--no-audio");
        if (CameraFps is > 0) list.Add($"--camera-fps={CameraFps.Value}");
        if (!string.IsNullOrWhiteSpace(WindowTitle)) list.Add($"--window-title={WindowTitle}");
        if (WindowWidth is > 0) list.Add($"--window-width={WindowWidth.Value}");
        if (WindowHeight is > 0) list.Add($"--window-height={WindowHeight.Value}");
        if (WindowX is not null) list.Add($"--window-x={WindowX.Value}");
        if (WindowY is not null) list.Add($"--window-y={WindowY.Value}");

        return list;
    }
}

public enum ScrcpySessionState
{
    Stopped = 0,
    Starting = 1,
    Running = 2,
    Error = 3
}

public static class ScrcpySessionStateExtensions
{
    public static string DisplayName(this ScrcpySessionState state) => state switch
    {
        ScrcpySessionState.Stopped => "Kamera Kapalı",
        ScrcpySessionState.Starting => "Kamera Başlatılıyor...",
        ScrcpySessionState.Running => "Canlı Kamera Açık (Yönetilen Pencere)",
        ScrcpySessionState.Error => "Kamera Oturumu Hatası",
        _ => "Bilinmeyen Durum"
    };
}

public sealed record ScrcpySessionStatus(
    ScrcpySessionState State,
    int? ProcessId,
    string? SerialId,
    string? CameraId,
    string? Resolution,
    string Message,
    string? ErrorMessage = null,
    DateTimeOffset? StartedAt = null)
{
    public bool IsActive => State is ScrcpySessionState.Starting or ScrcpySessionState.Running;

    public string StateBadge => State.DisplayName();

    public static ScrcpySessionStatus Stopped(string message = "Kamera kapalı.") =>
        new(ScrcpySessionState.Stopped, null, null, null, null, message);
}

public sealed record ScrcpyLaunchResult(
    bool Success,
    int? ProcessId,
    string Message,
    string? ErrorDetail = null);

public sealed record ScrcpyCameraQueryScanResult(
    bool Success,
    IReadOnlyList<ScrcpyCameraInfo> Cameras,
    string Message,
    string? ErrorDetail = null);

public sealed record ScrcpyCameraSizesQueryResult(
    bool Success,
    string CameraId,
    IReadOnlyList<ScrcpyCameraSizeOption> Sizes,
    string Message,
    string? ErrorDetail = null);
