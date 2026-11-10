namespace OlcuAtolyesi.Core.Phone;

/// <summary>Android cihazdaki fiziksel veya mantıksal kamera yönü.</summary>
public enum RemoteCameraLensFacing
{
    Back = 0,
    Front = 1,
    External = 2
}

/// <summary>Uzaktan kamera odaklama modu sözleşmesi.</summary>
public enum RemoteFocusMode
{
    ContinuousPicture = 0,
    Auto = 1,
    Macro = 2,
    Manual = 3,
    Fixed = 4
}

/// <summary>Uzaktan kamera beyaz dengesi (AWB) modu sözleşmesi.</summary>
public enum RemoteWhiteBalanceMode
{
    Auto = 0,
    Daylight = 1,
    Cloudy = 2,
    Fluorescent = 3,
    Incandescent = 4,
    ManualKelvin = 5
}

/// <summary>Uzaktan kamera flaş / sürekli aydınlatma (torch) modu sözleşmesi.</summary>
public enum RemoteFlashMode
{
    Off = 0,
    Auto = 1,
    On = 2,
    Torch = 3
}

/// <summary>Cihazdan sorgulanacak desteklenen çözünürlük seçeneği.</summary>
public sealed record CameraResolutionOption(
    int Width,
    int Height,
    string AspectRatioLabel)
{
    public string DisplayText => $"{Width} × {Height} ({AspectRatioLabel})";
}

/// <summary>
/// Bağlı Android telefondan (Camera2 / CameraX API üzerinden) sorgulanacak kamera donanım yetenekleri.
/// <para>
/// MİMARİ KURAL: Hiçbir kamera yeteneği (zoom aralığı, manuel odak, pozlama, flaş/ışık, FPS veya çözünürlük)
/// varsayılmaz; ilerleyen sürümlerde cihazdan çalışma zamanında sorgulanarak doldurulur.
/// </para>
/// </summary>
public sealed record RemoteCameraCapabilities(
    string CameraId,
    RemoteCameraLensFacing LensFacing,
    IReadOnlyList<CameraResolutionOption> SupportedPhotoResolutions,
    IReadOnlyList<CameraResolutionOption> SupportedPreviewResolutions,
    double MinZoomRatio,
    double MaxZoomRatio,
    IReadOnlyList<int> SupportedPreviewFps,
    IReadOnlyList<RemoteFocusMode> SupportedFocusModes,
    bool SupportsManualFocusDistance,
    float MinimumFocusDistanceDiopters,
    bool SupportsExposureCompensation,
    int MinExposureCompensationIndex,
    int MaxExposureCompensationIndex,
    double ExposureCompensationStepEv,
    IReadOnlyList<RemoteWhiteBalanceMode> SupportedWhiteBalanceModes,
    bool HasFlashUnit,
    bool SupportsTorchMode);

/// <summary>Masaüstünden Android kameraya uygulanacak çekim ve önizleme parametreleri.</summary>
public sealed record RemoteCameraSettings(
    string CameraId,
    CameraResolutionOption PhotoResolution,
    int JpegQualityPercent,
    int TargetPreviewFps,
    double ZoomRatio,
    RemoteFocusMode FocusMode,
    float? ManualFocusDiopters,
    int ExposureCompensationIndex,
    RemoteWhiteBalanceMode WhiteBalanceMode,
    RemoteFlashMode FlashMode);

/// <summary>Arayüzde ve mimari belgelerde gösterilecek planlanan kamera kontrol kalemi.</summary>
public sealed record PlannedCameraControlDescriptor(
    string Title,
    string CapabilityQueryRule,
    string TransportNote,
    string StatusBadge = "Planlandı");

/// <summary>
/// Gelecekteki sürümlerde (v0.5.0+) USB veya Wi-Fi üzerinden Android telefon kamerasını
/// yönetmek için kullanılacak servis sözleşmesi.
/// <para>
/// v0.4.0 sürümünde canlı kamera aktarımı veya telefon kamerasını otomatik başlatma yapılmaz.
/// </para>
/// </summary>
public interface IRemotePhoneCameraService
{
    /// <summary>
    /// Bağlı ve yetkili Android cihazdaki kameraları ve her kameranın gerçek donanım yeteneklerini sorgular.
    /// Cihaz yetenekleri hiçbir zaman varsayılmaz.
    /// </summary>
    Task<IReadOnlyList<RemoteCameraCapabilities>> QueryCameraCapabilitiesAsync(
        string deviceSerial,
        CancellationToken cancellationToken = default);

    /// <summary>Seçili kameraya çözünürlük, odak, pozlama, beyaz dengesi ve flaş ayarlarını uygular.</summary>
    Task<bool> ApplyCameraSettingsAsync(
        string deviceSerial,
        RemoteCameraSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>Planlanan kamera kontrol sözleşmelerinin Türkçe mimari özet kataloğu.</summary>
public static class RemoteCameraContractCatalog
{
    public static IReadOnlyList<PlannedCameraControlDescriptor> GetPlannedControls() =>
    [
        new PlannedCameraControlDescriptor(
            "1. Kamera Seçimi (Arka / Ön / Geniş / Makro)",
            "Cihazdaki Camera2 CameraManager üzerinden mantıksal ve fiziksel kamera kimlikleri sorgulanır.",
            "USB / Wi-Fi kontrol kanalı"),
        new PlannedCameraControlDescriptor(
            "2. Canlı Görüntü (Önizleme Akışı)",
            "Parçanın cetvelli kalibrasyon zeminine hizalanması için düşük gecikmeli kare akışı sağlar.",
            "USB (ADB TCP tüneli) veya Wi-Fi (WebSocket) yüksek bant genişlikli kanal"),
        new PlannedCameraControlDescriptor(
            "3. Zoom (Yakınlaştırma Oranı)",
            "Cihazın bildirdiği MinZoomRatio – MaxZoomRatio sınırları içinde optik/dijital yakınlaştırma.",
            "Yetenek sorgusu zorunlu (varsayılmaz)"),
        new PlannedCameraControlDescriptor(
            "4. Çözünürlük ve Kalite (JPEG / Kayıpsız)",
            "Sensörün desteklediği en yüksek doğal fotoğraf çözünürlüğü ve sıkıştırma kalitesi seçimi.",
            "Yüksek çözünürlüklü kare aktarımı: USB / Wi-Fi"),
        new PlannedCameraControlDescriptor(
            "5. FPS (Kare Hızı Seçimi)",
            "Canlı önizleme için cihazın desteklediği AE_AVAILABLE_TARGET_FPS_RANGES değerleri sorgulanır.",
            "Akış kararlılığına göre 15 / 30 / 60 FPS"),
        new PlannedCameraControlDescriptor(
            "6. Otomatik / Manuel Odaklama (AF / MF)",
            "Ölçüm düzleminde netlik kilidi (AF Lock) ve cihaz destekliyorsa diyoptri bazlı manuel odak.",
            "LENS_INFO_MINIMUM_FOCUS_DISTANCE sorgulanır"),
        new PlannedCameraControlDescriptor(
            "7. Pozlama (AE Telafisi ve Kilit)",
            "Parlak metal veya koyu contalarda kenar kontrastını korumak için pozlama telafisi (EV) ve AE kilidi.",
            "CONTROL_AE_COMPENSATION_RANGE sorgulanır"),
        new PlannedCameraControlDescriptor(
            "8. Beyaz Dengesi (AWB Modları)",
            "Atölye aydınlatmasına göre Otomatik, Gün Işığı, Floresan, Akkor veya sabit renk sıcaklığı.",
            "CONTROL_AWB_AVAILABLE_MODES sorgulanır"),
        new PlannedCameraControlDescriptor(
            "9. Flaş / Sürekli Işık (Destekleniyorsa Torch)",
            "Yalnızca cihazda FLASH_INFO_AVAILABLE = true ise etkinleştirilir; gölgesiz kontur çekimi sağlar.",
            "Donanım desteği cihazdan doğrulanır")
    ];
}
