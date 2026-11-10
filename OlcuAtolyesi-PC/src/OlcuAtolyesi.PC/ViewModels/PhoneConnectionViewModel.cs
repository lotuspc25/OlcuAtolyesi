using System.Windows.Input;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Phone;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed class PhoneDeviceListItemViewModel
{
    public PhoneDeviceListItemViewModel(
        AndroidDeviceInfo device,
        bool isConnectedInSession,
        Action<string> selectDeviceAction)
    {
        Device = device;
        IsConnectedInSession = isConnectedInSession;
        SelectCommand = new RelayCommand(() => selectDeviceAction(device.SerialId));
    }

    public AndroidDeviceInfo Device { get; }

    public string SerialId => Device.SerialId;

    public string DisplayTitle => Device.DisplayTitle;

    public string StateBadge => Device.StateDisplayName;

    public string AndroidVersionText => Device.AndroidVersionDisplay;

    public string GuidanceText => Device.GuidanceMessage;

    public string HardwareDetailsText =>
        string.Join("  •  ", new[]
        {
            $"Seri No: {Device.SerialId}",
            !string.IsNullOrWhiteSpace(Device.Product) ? $"Ürün: {Device.Product}" : null,
            !string.IsNullOrWhiteSpace(Device.DeviceCode) ? $"Kod: {Device.DeviceCode}" : null,
            !string.IsNullOrWhiteSpace(Device.TransportId) ? $"Taşıma ID: {Device.TransportId}" : null
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool IsReady => Device.IsReady;

    public bool IsConnectedInSession { get; }

    public string SelectButtonText =>
        IsConnectedInSession
            ? "Seçili Cihaz ✓"
            : (Device.IsReady ? "Cihazı Seç" : "Seçilemez (Hazır Değil)");

    public ICommand SelectCommand { get; }
}

public sealed class PhoneConnectionViewModel : PageViewModelBase
{
    private readonly UsbPhoneConnectionService _connectionService;
    private IReadOnlyList<PhoneDeviceListItemViewModel> _devices = [];
    private PhoneDeviceListItemViewModel? _selectedListDevice;
    private string _statusMessage;
    private string? _warningOrErrorDetail;
    private string _adbStatusText = string.Empty;
    private bool _isAdbAvailable;

    public PhoneConnectionViewModel(
        ModuleDescriptor descriptor,
        UsbPhoneConnectionService? connectionService = null)
        : base(descriptor)
    {
        _connectionService = connectionService ?? new UsbPhoneConnectionService();

        TransportOptions = UsbPhoneConnectionService.GetTransportOptions();
        PlannedCameraControls = RemoteCameraContractCatalog.GetPlannedControls();

        ScanDevicesCommand = new RelayCommand(() => ScanDevices());
        SelectDeviceCommand = new RelayCommand(() => SelectHighlightedDevice());
        CheckConnectionCommand = new RelayCommand(() => CheckConnection());
        RefreshConnectionCommand = new RelayCommand(() => RefreshConnection());
        DisconnectCommand = new RelayCommand(() => Disconnect());

        _connectionService.StateChanged += (_, _) => SyncFromService();

        UpdateAdbAvailabilityBanner();
        _statusMessage = _isAdbAvailable
            ? "USB üzerinden bağlı Android telefonları listelemek için 'Cihazları Tara' düğmesine tıklayın."
            : "Bilgisayarda ADB aracı bulunamadı. Android SDK Platform-Tools kurulumunu kontrol edin.";
    }

    public UsbPhoneConnectionService ConnectionService => _connectionService;

    public IReadOnlyList<TransportOptionDescriptor> TransportOptions { get; }

    public IReadOnlyList<PlannedCameraControlDescriptor> PlannedCameraControls { get; }

    public IReadOnlyList<PhoneDeviceListItemViewModel> Devices
    {
        get => _devices;
        private set
        {
            if (SetProperty(ref _devices, value))
            {
                OnPropertyChanged(nameof(DeviceCount));
                OnPropertyChanged(nameof(HasDevices));
                OnPropertyChanged(nameof(ReadyDeviceCount));
            }
        }
    }

    public int DeviceCount => _devices.Count;

    public int ReadyDeviceCount => _devices.Count(d => d.IsReady);

    public bool HasDevices => _devices.Count > 0;

    public PhoneDeviceListItemViewModel? SelectedListDevice
    {
        get => _selectedListDevice;
        set
        {
            if (SetProperty(ref _selectedListDevice, value))
            {
                OnPropertyChanged(nameof(HasSelectedListDevice));
            }
        }
    }

    public bool HasSelectedListDevice => _selectedListDevice is not null;

    public bool IsAdbAvailable
    {
        get => _isAdbAvailable;
        private set => SetProperty(ref _isAdbAvailable, value);
    }

    public string AdbStatusText
    {
        get => _adbStatusText;
        private set => SetProperty(ref _adbStatusText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? WarningOrErrorDetail
    {
        get => _warningOrErrorDetail;
        private set
        {
            if (SetProperty(ref _warningOrErrorDetail, value))
            {
                OnPropertyChanged(nameof(HasWarningOrError));
            }
        }
    }

    public bool HasWarningOrError => !string.IsNullOrWhiteSpace(_warningOrErrorDetail);

    public bool HasConnectedDevice => _connectionService.ConnectedDevice is not null;

    public string ConnectionSessionBadge =>
        _connectionService.ConnectedDevice is { } active
            ? $"Aktif Oturum: {active.DisplayTitle} ({active.SerialId})"
            : "Aktif Cihaz Seçilmedi";

    public string ConnectedDeviceTitle =>
        _connectionService.ConnectedDevice?.DisplayTitle ?? "Seçili cihaz yok";

    public string ConnectedDeviceSerial =>
        _connectionService.ConnectedDevice?.SerialId ?? "—";

    public string ConnectedDeviceAndroidVersion =>
        _connectionService.ConnectedDevice?.AndroidVersionDisplay ?? "—";

    public string ConnectedDeviceStateText =>
        _connectionService.ConnectedDevice?.StateDisplayName ?? "Bağlı değil";

    public ICommand ScanDevicesCommand { get; }

    public ICommand SelectDeviceCommand { get; }

    public ICommand CheckConnectionCommand { get; }

    public ICommand RefreshConnectionCommand { get; }

    public ICommand DisconnectCommand { get; }

    /// <summary><c>adb devices -l</c> ile bağlı cihazları tarar ve listeyi günceller.</summary>
    public AdbScanResult ScanDevices(CancellationToken cancellationToken = default)
    {
        var result = _connectionService.ScanDevices(cancellationToken);
        UpdateAdbAvailabilityBanner();
        StatusMessage = result.Message;
        WarningOrErrorDetail = result.WarningOrErrorDetail;
        SyncFromService();
        return result;
    }

    /// <summary>Verilen seri numaralı cihazı seçer (yalnızca Online ise oturum açar).</summary>
    public PhoneSessionOperationResult SelectDeviceBySerial(string? serial)
    {
        var result = _connectionService.SelectDevice(serial);
        StatusMessage = result.Message;
        WarningOrErrorDetail = result.ErrorDetail;
        SyncFromService();
        return result;
    }

    /// <summary>Listede vurgulanmış olan cihazı seçer.</summary>
    public PhoneSessionOperationResult SelectHighlightedDevice()
    {
        if (_selectedListDevice is null)
        {
            StatusMessage = "Lütfen önce cihaz listesinden bir telefon seçin.";
            WarningOrErrorDetail = null;
            return new PhoneSessionOperationResult(false, _connectionService.ConnectedDevice, StatusMessage);
        }

        return SelectDeviceBySerial(_selectedListDevice.SerialId);
    }

    /// <summary>Aktif seçili cihazın bağlantısını kontrol eder.</summary>
    public PhoneSessionOperationResult CheckConnection(CancellationToken cancellationToken = default)
    {
        var result = _connectionService.CheckConnection(cancellationToken);
        StatusMessage = result.Message;
        WarningOrErrorDetail = result.ErrorDetail;
        SyncFromService();
        return result;
    }

    /// <summary>Cihaz listesini yeniden tarar ve aktif cihaz varsa durumunu doğrular.</summary>
    public AdbScanResult RefreshConnection(CancellationToken cancellationToken = default)
    {
        var previousSerial = _connectionService.ConnectedDevice?.SerialId;
        var scan = ScanDevices(cancellationToken);
        if (scan.Success && !string.IsNullOrWhiteSpace(previousSerial) && _connectionService.ConnectedDevice is not null)
        {
            var check = _connectionService.CheckConnection(cancellationToken);
            StatusMessage = check.Message;
            WarningOrErrorDetail = check.ErrorDetail ?? scan.WarningOrErrorDetail;
            SyncFromService();
        }

        return scan;
    }

    /// <summary>Yalnızca masaüstü uygulamasındaki seçili cihaz oturumunu sonlandırır (ADB sunucusunu kapatmaz).</summary>
    public PhoneSessionOperationResult Disconnect()
    {
        var result = _connectionService.DisconnectSelectedDevice();
        StatusMessage = result.Message;
        WarningOrErrorDetail = result.ErrorDetail;
        SyncFromService();
        return result;
    }

    private void UpdateAdbAvailabilityBanner()
    {
        var path = _connectionService.AdbExecutablePath;
        IsAdbAvailable = !string.IsNullOrWhiteSpace(path);
        AdbStatusText = IsAdbAvailable
            ? $"ADB Hazır: {path}"
            : "Uyarı: Bilgisayarda ADB (adb.exe) bulunamadı. PATH veya Android SDK Platform-Tools dizinini kontrol edin.";
    }

    private void SyncFromService()
    {
        var previousSelectedSerial = _selectedListDevice?.SerialId;
        var activeSerial = _connectionService.ConnectedDevice?.SerialId;

        var items = _connectionService.Devices
            .Select(d => new PhoneDeviceListItemViewModel(
                d,
                isConnectedInSession: string.Equals(d.SerialId, activeSerial, StringComparison.Ordinal),
                selectDeviceAction: serial =>
                {
                    SelectDeviceBySerial(serial);
                }))
            .ToList();

        Devices = items;
        SelectedListDevice = items.FirstOrDefault(i => string.Equals(i.SerialId, activeSerial, StringComparison.Ordinal))
                             ?? items.FirstOrDefault(i => string.Equals(i.SerialId, previousSelectedSerial, StringComparison.Ordinal))
                             ?? items.FirstOrDefault();

        OnPropertyChanged(nameof(HasConnectedDevice));
        OnPropertyChanged(nameof(ConnectionSessionBadge));
        OnPropertyChanged(nameof(ConnectedDeviceTitle));
        OnPropertyChanged(nameof(ConnectedDeviceSerial));
        OnPropertyChanged(nameof(ConnectedDeviceAndroidVersion));
        OnPropertyChanged(nameof(ConnectedDeviceStateText));
    }
}
