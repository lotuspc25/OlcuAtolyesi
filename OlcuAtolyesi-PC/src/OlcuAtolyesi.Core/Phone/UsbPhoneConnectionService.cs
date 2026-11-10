namespace OlcuAtolyesi.Core.Phone;

/// <summary><c>adb devices -l</c> çıktılarını güvenli ve hataya dayanıklı biçimde ayrıştırır.</summary>
public static class AdbOutputParser
{
    public sealed record ParseResult(
        IReadOnlyList<AndroidDeviceInfo> Devices,
        int MalformedLineCount);

    public static ParseResult ParseDevicesOutput(string? rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return new ParseResult([], 0);
        }

        var devices = new List<AndroidDeviceInfo>();
        var malformedCount = 0;

        var lines = rawOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('*'))
            {
                continue;
            }

            var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                malformedCount++;
                continue;
            }

            var serial = tokens[0];
            if (!AdbInputValidator.IsValidSerial(serial))
            {
                malformedCount++;
                continue;
            }

            var rawState = tokens[1];
            // "no permissions ..." gibi çok kelimeli durum satırlarını algıla
            if ( string.Equals(rawState, "no", StringComparison.OrdinalIgnoreCase) &&
                 tokens.Length >= 3 &&
                 string.Equals(tokens[2], "permissions", StringComparison.OrdinalIgnoreCase))
            {
                rawState = "unauthorized";
            }

            var state = MapState(rawState);

            // Geçerli bir durum belirteci değilse ve metadata da yoksa bozuk satır olarak işaretle
            if (state == AdbDeviceState.Unknown &&
                !IsKnownNonReadyToken(rawState) &&
                !tokens.Skip(2).Any(t => t.Contains(':')))
            {
                malformedCount++;
                continue;
            }

            var model = string.Empty;
            var product = string.Empty;
            var deviceCode = string.Empty;
            var transportId = string.Empty;

            for (var i = 2; i < tokens.Length; i++)
            {
                var token = tokens[i];
                var colonIdx = token.IndexOf(':');
                if (colonIdx <= 0 || colonIdx >= token.Length - 1)
                {
                    continue;
                }

                var key = token[..colonIdx];
                var val = token[(colonIdx + 1)..].Replace('_', ' ');
                switch (key.ToLowerInvariant())
                {
                    case "model":
                        model = val;
                        break;
                    case "product":
                        product = val;
                        break;
                    case "device":
                        deviceCode = val;
                        break;
                    case "transport_id":
                        transportId = val;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                model = !string.IsNullOrWhiteSpace(deviceCode) ? deviceCode : serial;
            }

            devices.Add(new AndroidDeviceInfo(
                SerialId: serial,
                State: state,
                Model: model,
                Product: product,
                DeviceCode: deviceCode,
                TransportId: transportId,
                AndroidVersion: null,
                Manufacturer: null,
                RawStateToken: rawState));
        }

        return new ParseResult(devices, malformedCount);
    }

    public static AdbDeviceState MapState(string? rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return AdbDeviceState.Unknown;
        }

        return rawState.Trim().ToLowerInvariant() switch
        {
            "device" => AdbDeviceState.Online,
            "unauthorized" => AdbDeviceState.Unauthorized,
            "authorizing" => AdbDeviceState.Unauthorized,
            "offline" => AdbDeviceState.Offline,
            _ => AdbDeviceState.Unknown
        };
    }

    private static bool IsKnownNonReadyToken(string rawState) =>
        rawState.ToLowerInvariant() is "recovery" or "sideload" or "bootloader" or "fastboot" or "rescue" or "connecting";
}

/// <summary>
/// USB üzerinden bağlı Android telefonları ADB aracılığıyla tarayan, durumlarını ayırt eden ve
/// seçili cihaz oturumunu güvenli biçimde yöneten servis.
/// </summary>
public sealed class UsbPhoneConnectionService
{
    public static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly IAdbCommandRunner _runner;
    private readonly TimeSpan _commandTimeout;
    private List<AndroidDeviceInfo> _devices = [];
    private AndroidDeviceInfo? _connectedDevice;

    public UsbPhoneConnectionService(
        IAdbCommandRunner? runner = null,
        TimeSpan? commandTimeout = null)
    {
        _runner = runner ?? new ProcessAdbCommandRunner();
        _commandTimeout = commandTimeout is { TotalMilliseconds: > 0 } validTimeout
            ? validTimeout
            : DefaultCommandTimeout;
    }

    public event EventHandler? StateChanged;

    public string? AdbExecutablePath => _runner.GetAdbExecutablePath();

    public bool IsAdbAvailable => !string.IsNullOrWhiteSpace(AdbExecutablePath);

    public IReadOnlyList<AndroidDeviceInfo> Devices
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_devices.ToArray());
            }
        }
    }

    public AndroidDeviceInfo? ConnectedDevice
    {
        get
        {
            lock (_gate)
            {
                return _connectedDevice;
            }
        }
    }

    public bool HasConnectedDevice
    {
        get
        {
            lock (_gate)
            {
                return _connectedDevice is not null;
            }
        }
    }

    /// <summary>USB (etkin), Wi-Fi (planlandı) ve Bluetooth (planlandı) iletişim mimarisi seçeneklerini döndürür.</summary>
    public static IReadOnlyList<TransportOptionDescriptor> GetTransportOptions() =>
    [
        new TransportOptionDescriptor(
            PhoneTransportKind.Usb,
            "USB Bağlantısı (ADB Köprüsü)",
            "Etkin • v0.4.0",
            IsActiveInCurrentVersion: true,
            "USB kablosu üzerinden düşük gecikmeli cihaz tanıma, seri numaralı güvenli komut yönetimi, yüksek çözünürlüklü fotoğraf ve proje paketi aktarımı."),
        new TransportOptionDescriptor(
            PhoneTransportKind.WiFi,
            "Wi-Fi Bağlantısı (Yerel Ağ REST / WebSocket)",
            "Planlandı",
            IsActiveInCurrentVersion: false,
            "Aynı yerel ağda QR kod ile eşleşme, kablosuz yüksek çözünürlüklü kamera önizleme akışı ve kayıpsız fotoğraf aktarımı."),
        new TransportOptionDescriptor(
            PhoneTransportKind.Bluetooth,
            "Bluetooth (BLE Keşif ve Düşük Bant Kontrol)",
            "Planlandı",
            IsActiveInCurrentVersion: false,
            "Yakındaki telefonları otomatik keşfetme, hızlı eşleştirme el sıkışması ve düşük bant genişlikli deklanşör/odak tetikleme iletişimi.")
    ];

    public AdbScanResult ScanDevices(CancellationToken cancellationToken = default) =>
        ScanDevicesAsync(cancellationToken).GetAwaiter().GetResult();

    public async Task<AdbScanResult> ScanDevicesAsync(CancellationToken cancellationToken = default)
    {
        var adbPath = _runner.GetAdbExecutablePath();
        if (string.IsNullOrWhiteSpace(adbPath))
        {
            lock (_gate)
            {
                _devices.Clear();
                _connectedDevice = null;
            }

            OnStateChanged();
            return new AdbScanResult(
                Success: false,
                AdbAvailable: false,
                AdbExecutablePath: null,
                Devices: [],
                Message: "Bilgisayarda ADB (Android Debug Bridge) aracı bulunamadı.",
                WarningOrErrorDetail: "Android SDK Platform-Tools paketini kurun veya adb.exe dizinini sistem PATH / ANDROID_HOME ortam değişkenine ekleyin.");
        }

        var listCmd = await _runner.RunAsync(["devices", "-l"], _commandTimeout, cancellationToken).ConfigureAwait(false);
        if (!listCmd.IsSuccess)
        {
            var reason = listCmd.IsTimedOut
                ? "ADB cihaz taraması zaman aşımına uğradı."
                : (listCmd.IsCancelled
                    ? "ADB cihaz taraması iptal edildi."
                    : (!string.IsNullOrWhiteSpace(listCmd.FailureReason)
                        ? listCmd.FailureReason
                        : "ADB cihaz listesi alınamadı."));

            var detail = !string.IsNullOrWhiteSpace(listCmd.StandardError)
                ? listCmd.StandardError.Trim()
                : listCmd.FailureReason;

            OnStateChanged();
            return new AdbScanResult(
                Success: false,
                AdbAvailable: true,
                AdbExecutablePath: adbPath,
                Devices: Devices,
                Message: reason,
                WarningOrErrorDetail: detail);
        }

        var parsed = AdbOutputParser.ParseDevicesOutput(listCmd.StandardOutput);
        var enriched = new List<AndroidDeviceInfo>(parsed.Devices.Count);

        foreach (var device in parsed.Devices)
        {
            // KURAL: Yalnızca 'device' (Online) durumundaki cihazlara ek sorgu gönderilir.
            // 'unauthorized' veya 'offline' cihazlara kesinlikle işlem gönderilmez.
            if (device.State == AdbDeviceState.Online)
            {
                var version = await QueryDevicePropertySafeAsync(device.SerialId, "ro.build.version.release", cancellationToken)
                    .ConfigureAwait(false);
                var manufacturer = await QueryDevicePropertySafeAsync(device.SerialId, "ro.product.manufacturer", cancellationToken)
                    .ConfigureAwait(false);
                var propModel = await QueryDevicePropertySafeAsync(device.SerialId, "ro.product.model", cancellationToken)
                    .ConfigureAwait(false);

                var bestModel = !string.IsNullOrWhiteSpace(propModel) ? propModel : device.Model;
                enriched.Add(device with
                {
                    Model = bestModel,
                    AndroidVersion = version,
                    Manufacturer = manufacturer
                });
            }
            else
            {
                enriched.Add(device);
            }
        }

        string? sessionNotice = null;
        lock (_gate)
        {
            _devices = enriched;
            if (_connectedDevice is not null)
            {
                var updatedMatch = _devices.FirstOrDefault(d =>
                    string.Equals(d.SerialId, _connectedDevice.SerialId, StringComparison.Ordinal));

                if (updatedMatch is null)
                {
                    sessionNotice = $"Önceden seçili '{_connectedDevice.SerialId}' cihazının bağlantısı kesildi.";
                    _connectedDevice = null;
                }
                else if (updatedMatch.State != AdbDeviceState.Online)
                {
                    sessionNotice = $"Seçili '{updatedMatch.SerialId}' cihazı artık '{updatedMatch.StateDisplayName}' durumunda olduğu için aktif oturum duraklatıldı.";
                    _connectedDevice = null;
                }
                else
                {
                    _connectedDevice = updatedMatch;
                }
            }
        }

        OnStateChanged();

        var warning = BuildScanWarning(enriched, parsed.MalformedLineCount, sessionNotice);
        var summary = enriched.Count == 0
            ? "Bağlı Android cihaz bulunamadı. Telefonu USB ile bağlayıp USB Hata Ayıklama modunu etkinleştirin."
            : $"Toplam {enriched.Count} Android cihaz algılandı ({enriched.Count(d => d.IsReady)} hazır).";

        return new AdbScanResult(
            Success: true,
            AdbAvailable: true,
            AdbExecutablePath: adbPath,
            Devices: enriched,
            Message: summary,
            WarningOrErrorDetail: warning,
            MalformedLineCount: parsed.MalformedLineCount);
    }

    /// <summary>
    /// Kullanıcının seçtiği cihazı doğrular ve yalnızca <see cref="AdbDeviceState.Online"/> durumundaysa aktif oturuma bağlar.
    /// </summary>
    public PhoneSessionOperationResult SelectDevice(string? serial)
    {
        if (!AdbInputValidator.IsValidSerial(serial))
        {
            return new PhoneSessionOperationResult(
                false,
                ConnectedDevice,
                "Geçersiz cihaz kimliği (seri numarası).",
                "Cihaz kimliği boş olamaz ve yalnızca güvenli karakterler içerebilir.");
        }

        lock (_gate)
        {
            var target = _devices.FirstOrDefault(d => string.Equals(d.SerialId, serial, StringComparison.Ordinal));
            if (target is null)
            {
                return new PhoneSessionOperationResult(
                    false,
                    _connectedDevice,
                    $"'{serial}' kimlikli cihaz güncel listede bulunamadı.",
                    "Lütfen önce 'Cihazları Tara' düğmesiyle cihaz listesini güncelleyin.");
            }

            if (target.State == AdbDeviceState.Unauthorized)
            {
                return new PhoneSessionOperationResult(
                    false,
                    _connectedDevice,
                    $"'{target.DisplayTitle}' ({target.SerialId}) yetkisiz durumda olduğu için seçilemez.",
                    target.GuidanceMessage);
            }

            if (target.State == AdbDeviceState.Offline)
            {
                return new PhoneSessionOperationResult(
                    false,
                    _connectedDevice,
                    $"'{target.DisplayTitle}' ({target.SerialId}) çevrimdışı olduğu için işlem gönderilemez.",
                    target.GuidanceMessage);
            }

            if (target.State != AdbDeviceState.Online)
            {
                return new PhoneSessionOperationResult(
                    false,
                    _connectedDevice,
                    $"'{target.DisplayTitle}' ({target.SerialId}) hazır durumda değil ({target.StateDisplayName}).",
                    target.GuidanceMessage);
            }

            _connectedDevice = target;
        }

        OnStateChanged();
        var active = ConnectedDevice!;
        return new PhoneSessionOperationResult(
            true,
            active,
            $"'{active.DisplayTitle}' ({active.SerialId}) masaüstü oturumuna bağlandı.");
    }

    public PhoneSessionOperationResult CheckConnection(CancellationToken cancellationToken = default) =>
        CheckConnectionAsync(cancellationToken).GetAwaiter().GetResult();

    /// <summary>Seçili cihazın bağlantısını <c>adb -s &lt;serial&gt; get-state</c> ile doğrular.</summary>
    public async Task<PhoneSessionOperationResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        var current = ConnectedDevice;
        if (current is null)
        {
            return new PhoneSessionOperationResult(
                false,
                null,
                "Kontrol edilecek seçili bir cihaz yok.",
                "Lütfen listeden bağlı ve yetkili bir cihaz seçin.");
        }

        if (!AdbInputValidator.IsValidSerial(current.SerialId))
        {
            DisconnectSelectedDevice();
            return new PhoneSessionOperationResult(false, null, "Seçili cihaz kimliği geçersiz.");
        }

        var stateCmd = await _runner.RunAsync(
            ["-s", current.SerialId, "get-state"],
            _commandTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!stateCmd.IsSuccess)
        {
            var stderr = stateCmd.StandardError.Trim();
            var mappedFromErr = stderr.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                ? AdbDeviceState.Unauthorized
                : (stderr.Contains("offline", StringComparison.OrdinalIgnoreCase)
                    ? AdbDeviceState.Offline
                    : AdbDeviceState.Unknown);

            UpdateDeviceStateInList(current.SerialId, mappedFromErr);
            lock (_gate)
            {
                _connectedDevice = null;
            }

            OnStateChanged();

            var msg = stateCmd.IsTimedOut
                ? $"'{current.DisplayTitle}' ({current.SerialId}) bağlantı kontrolü zaman aşımına uğradı."
                : $"'{current.DisplayTitle}' ({current.SerialId}) bağlantısı doğrulanamadı.";

            return new PhoneSessionOperationResult(
                false,
                null,
                msg,
                !string.IsNullOrWhiteSpace(stderr) ? stderr : stateCmd.FailureReason);
        }

        var reportedState = AdbOutputParser.MapState(stateCmd.StandardOutput.Trim());
        UpdateDeviceStateInList(current.SerialId, reportedState);

        if (reportedState != AdbDeviceState.Online)
        {
            lock (_gate)
            {
                _connectedDevice = null;
            }

            OnStateChanged();
            return new PhoneSessionOperationResult(
                false,
                null,
                $"'{current.DisplayTitle}' ({current.SerialId}) artık hazır değil ({reportedState.DisplayName()}).",
                "Oturum güvenlik amacıyla sonlandırıldı.");
        }

        return new PhoneSessionOperationResult(
            true,
            current,
            $"Bağlantı etkin ve sağlıklı: '{current.DisplayTitle}' ({current.SerialId} • {current.AndroidVersionDisplay}).");
    }

    /// <summary>
    /// Yalnızca uygulamanın seçili cihaz oturumunu sonlandırır.
    /// ADB sunucusunu (<c>kill-server</c>) kapatmaz ve diğer bağlı cihazları etkilemez.
    /// </summary>
    public PhoneSessionOperationResult DisconnectSelectedDevice()
    {
        AndroidDeviceInfo? previous;
        lock (_gate)
        {
            previous = _connectedDevice;
            _connectedDevice = null;
        }

        OnStateChanged();

        if (previous is null)
        {
            return new PhoneSessionOperationResult(
                true,
                null,
                "Aktif cihaz oturumu zaten kapalı.");
        }

        return new PhoneSessionOperationResult(
            true,
            null,
            $"'{previous.DisplayTitle}' ({previous.SerialId}) oturumu uygulamadan ayrıldı (ADB sunucusu ve diğer cihazlar korundu).");
    }

    /// <summary>
    /// Belirli bir cihaza seri numarası (<c>-s &lt;serial&gt;</c>) ile güvenli ADB komutu gönderir.
    /// Cihaz yetkisiz (<c>unauthorized</c>) veya çevrimdışı (<c>offline</c>) ise komutu çalıştırmadan reddeder.
    /// </summary>
    public async Task<AdbCommandResult> RunDeviceCommandAsync(
        string? serial,
        IReadOnlyList<string> deviceCommandArguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceCommandArguments);

        if (!AdbInputValidator.IsValidSerial(serial))
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: "Geçersiz cihaz seri numarası.");
        }

        AndroidDeviceInfo? known;
        lock (_gate)
        {
            known = _devices.FirstOrDefault(d => string.Equals(d.SerialId, serial, StringComparison.Ordinal));
        }

        if (known is null)
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: $"'{serial}' cihazı taranmış cihaz listesinde bulunmuyor.");
        }

        if (known.State == AdbDeviceState.Unauthorized)
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: "Yetkisiz (unauthorized) cihaza işlem gönderilemez.");
        }

        if (known.State == AdbDeviceState.Offline)
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: "Çevrimdışı (offline) cihaza işlem gönderilemez.");
        }

        if (known.State != AdbDeviceState.Online)
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: $"Cihaz hazır durumda değil ({known.StateDisplayName}).");
        }

        var fullArgs = new List<string>(deviceCommandArguments.Count + 2)
        {
            "-s",
            serial!
        };
        fullArgs.AddRange(deviceCommandArguments);

        return await _runner.RunAsync(fullArgs, _commandTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> QueryDevicePropertySafeAsync(
        string serial,
        string propertyName,
        CancellationToken cancellationToken)
    {
        if (!AdbInputValidator.IsValidSerial(serial))
        {
            return null;
        }

        var result = await _runner.RunAsync(
            ["-s", serial, "shell", "getprop", propertyName],
            _commandTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        var value = result.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void UpdateDeviceStateInList(string serial, AdbDeviceState newState)
    {
        lock (_gate)
        {
            var idx = _devices.FindIndex(d => string.Equals(d.SerialId, serial, StringComparison.Ordinal));
            if (idx >= 0)
            {
                _devices[idx] = _devices[idx] with { State = newState };
            }
        }
    }

    private static string? BuildScanWarning(
        IReadOnlyList<AndroidDeviceInfo> devices,
        int malformedLines,
        string? sessionNotice)
    {
        var messages = new List<string>();
        if (!string.IsNullOrWhiteSpace(sessionNotice))
        {
            messages.Add(sessionNotice);
        }

        var unauthorizedCount = devices.Count(d => d.State == AdbDeviceState.Unauthorized);
        if (unauthorizedCount > 0)
        {
            messages.Add($"{unauthorizedCount} cihazda USB hata ayıklama izni bekleniyor (telefon ekranından 'İzin Ver' onayı verin).");
        }

        var offlineCount = devices.Count(d => d.State == AdbDeviceState.Offline);
        if (offlineCount > 0)
        {
            messages.Add($"{offlineCount} cihaz çevrimdışı (offline) durumda.");
        }

        if (malformedLines > 0)
        {
            messages.Add($"ADB çıktısındaki {malformedLines} geçersiz/beklenmeyen satır atlandı.");
        }

        return messages.Count > 0 ? string.Join(" ", messages) : null;
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
