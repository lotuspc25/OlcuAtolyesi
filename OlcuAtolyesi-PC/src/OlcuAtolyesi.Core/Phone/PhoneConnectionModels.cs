namespace OlcuAtolyesi.Core.Phone;

/// <summary>PC ile Android telefon arasındaki iletişim kanalı türü.</summary>
public enum PhoneTransportKind
{
    Usb = 0,
    WiFi = 1,
    Bluetooth = 2
}

public static class PhoneTransportKindExtensions
{
    public static string DisplayName(this PhoneTransportKind kind) => kind switch
    {
        PhoneTransportKind.Usb => "USB (ADB Köprüsü)",
        PhoneTransportKind.WiFi => "Wi-Fi (Yerel Ağ REST / WebSocket)",
        PhoneTransportKind.Bluetooth => "Bluetooth (BLE Keşif ve Kontrol)",
        _ => "USB"
    };
}

/// <summary>Desteklenen ve gelecekte planlanan bağlantı seçeneklerinin mimari tanımı.</summary>
public sealed record TransportOptionDescriptor(
    PhoneTransportKind Kind,
    string Title,
    string StatusBadge,
    bool IsActiveInCurrentVersion,
    string BandwidthAndRoleSummary);

/// <summary>ADB tarafından bildirilen Android cihaz bağlantı durumu.</summary>
public enum AdbDeviceState
{
    /// <summary>Cihaz bağlı ve USB hata ayıklama yetkisi onaylanmış (adb: "device").</summary>
    Online = 0,

    /// <summary>Cihaz bağlı ancak telefonda USB hata ayıklama izni henüz onaylanmamış (adb: "unauthorized").</summary>
    Unauthorized = 1,

    /// <summary>Cihaz bağlı ancak ADB yanıt vermiyor (adb: "offline").</summary>
    Offline = 2,

    /// <summary>Diğer/bilinmeyen durumlar (recovery, sideload, authorizing vb.).</summary>
    Unknown = 3
}

public static class AdbDeviceStateExtensions
{
    public static string DisplayName(this AdbDeviceState state) => state switch
    {
        AdbDeviceState.Online => "Bağlı ve Yetkili (Hazır)",
        AdbDeviceState.Unauthorized => "Yetkisiz (Telefonda Onay Bekleniyor)",
        AdbDeviceState.Offline => "Çevrimdışı (Yanıt Vermiyor)",
        _ => "Hazır Değil / Bilinmeyen Durum"
    };
}

/// <summary>USB üzerinden algılanan bir Android telefonun kimlik, model, sürüm ve durum bilgileri.</summary>
public sealed record AndroidDeviceInfo(
    string SerialId,
    AdbDeviceState State,
    string Model,
    string Product = "",
    string DeviceCode = "",
    string TransportId = "",
    string? AndroidVersion = null,
    string? Manufacturer = null,
    string RawStateToken = "device")
{
    public bool IsReady => State == AdbDeviceState.Online;

    public string StateDisplayName => State.DisplayName();

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(Manufacturer) &&
        !Model.StartsWith(Manufacturer, StringComparison.OrdinalIgnoreCase)
            ? $"{Manufacturer} {Model}"
            : (string.IsNullOrWhiteSpace(Model) ? SerialId : Model);

    public string AndroidVersionDisplay =>
        !string.IsNullOrWhiteSpace(AndroidVersion)
            ? $"Android {AndroidVersion}"
            : (State == AdbDeviceState.Online ? "Android (Sürüm okunamadı)" : "Sorgulanmadı (Cihaz yetkili/çevrimiçi değil)");

    public string GuidanceMessage => State switch
    {
        AdbDeviceState.Online =>
            "Cihaz iletişime hazır. Bu cihazı seçerek masaüstü oturumuna bağlayabilirsiniz.",
        AdbDeviceState.Unauthorized =>
            "Telefon ekranında açılan 'USB hata ayıklamaya izin verilsin mi?' penceresinde 'İzin Ver' seçeneğini onaylayın ve ardından 'Bağlantıyı Yenile' düğmesine tıklayın. Yetkisiz cihaza hiçbir komut gönderilmez.",
        AdbDeviceState.Offline =>
            "Cihaz çevrimdışı görünüyor. USB kablosunu çıkarıp yeniden takın, telefonda USB modunu 'Dosya Aktarımı' olarak kontrol edin ve 'Bağlantıyı Yenile' düğmesine basın.",
        _ =>
            $"Cihaz şu anda '{RawStateToken}' durumunda olduğu için işlem gönderilemez."
    };
}

/// <summary>ADB komut çalıştırma sonucu.</summary>
public sealed record AdbCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool IsTimedOut = false,
    bool IsCancelled = false,
    string? FailureReason = null)
{
    public bool IsSuccess => !IsTimedOut && !IsCancelled && ExitCode == 0 && string.IsNullOrEmpty(FailureReason);
}

/// <summary>ADB cihaz tarama işlemi sonucu.</summary>
public sealed record AdbScanResult(
    bool Success,
    bool AdbAvailable,
    string? AdbExecutablePath,
    IReadOnlyList<AndroidDeviceInfo> Devices,
    string Message,
    string? WarningOrErrorDetail = null,
    int MalformedLineCount = 0);

/// <summary>Cihaz seçme veya bağlantı kontrolü işlemi sonucu.</summary>
public sealed record PhoneSessionOperationResult(
    bool Success,
    AndroidDeviceInfo? ActiveDevice,
    string Message,
    string? ErrorDetail = null);
