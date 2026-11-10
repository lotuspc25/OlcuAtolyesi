using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Phone;

namespace OlcuAtolyesi.PC.ViewModels;

public enum CameraConnectionMode
{
    ScrcpyUsb = 0,
    WifiDirect = 1
}

public sealed class CameraCalibrationViewModel : PageViewModelBase
{
    private readonly UsbPhoneConnectionService _phoneService;
    private readonly IScrcpyProcessManager _scrcpyManager;
    private readonly ICameraStreamClient _streamClient;
    private readonly IRemotePhotoClient _remotePhotoClient;
    private readonly Action<ModuleId>? _navigateAction;
    private readonly Func<string, bool>? _openCapturedPhotoAction;

    private CameraConnectionMode _activeMode = CameraConnectionMode.ScrcpyUsb;

    private bool _isBusy;
    private bool _isScrcpyInstalled;
    private string _scrcpyVersionText = "Kontrol ediliyor...";
    private string _scrcpyExecutablePath = string.Empty;

    private IReadOnlyList<ScrcpyCameraInfo> _availableCameras = [];
    private ScrcpyCameraInfo? _selectedCamera;

    private IReadOnlyList<ScrcpyCameraSizeOption> _availableCameraSizes = [];
    private ScrcpyCameraSizeOption? _selectedCameraSize;

    private IReadOnlyList<string> _availableResolutions = ["1920x1080", "1280x720", "3840x2160", "2560x1440"];
    private string _selectedResolution = "1920x1080";

    private IReadOnlyList<int> _availableFpsOptions = [30, 60, 24, 15];
    private int _selectedFps = 30;

    private IReadOnlyList<string> _availableBitrates = ["20M", "16M", "12M", "8M", "4M"];
    private string _selectedBitrate = "20M";

    // Wi-Fi Bağlantı Alanları
    private string _wifiServerAddress = "http://192.168.1.100:8088";
    private string _wifiPairingPin = string.Empty;
    private bool _isWifiConnecting;
    private ImageSource? _liveFrameSource;
    private string _wifiResolutionText = "-";
    private byte[]? _lastReceivedJpeg;
    private string? _capturedPhotoPath;
    private bool _isCapturingHighRes;
    private string _highResCaptureProgressText = string.Empty;

    private double _decodedFps;
    private int _decodedFpsCounter;
    private readonly System.Diagnostics.Stopwatch _decodedFpsStopwatch = new();
    private long _successfulDecodes;
    private long _failedDecodes;

    private string _statusMessage = "Hazır";
    private string? _errorDetails;

    public CameraCalibrationViewModel(
        ModuleDescriptor descriptor,
        UsbPhoneConnectionService? phoneService = null,
        IScrcpyProcessManager? scrcpyManager = null,
        Action<ModuleId>? navigateAction = null,
        ICameraStreamClient? streamClient = null,
        Func<string, bool>? openCapturedPhotoAction = null,
        IRemotePhotoClient? remotePhotoClient = null)
        : base(descriptor)
    {
        _phoneService = phoneService ?? new UsbPhoneConnectionService();
        _scrcpyManager = scrcpyManager ?? new ScrcpyProcessManager();
        _streamClient = streamClient ?? new CameraStreamClient();
        _navigateAction = navigateAction;
        _openCapturedPhotoAction = openCapturedPhotoAction;
        _remotePhotoClient = remotePhotoClient ?? new RemotePhotoClient();

        _streamClient.FrameReceived += OnLiveFrameReceived;
        _streamClient.StatusChanged += (_, msg) => StatusMessage = msg;
        _streamClient.ErrorOccurred += (_, err) => ErrorDetails = err;
        _streamClient.ConnectionStateChanged += OnStreamConnectionStateChanged;

        PlannedCalibrationActions =
        [
            new PlannedActionItem(
                "Kalibrasyon Sayfası (ArUco / Cetvel) Yazdır • Planlandı",
                "A4 boyutunda milimetrik kalibrasyon ve köşe işaretçi şablonu üretir (v0.5.0)."),
            new PlannedActionItem(
                "Perspektif ve Lens Kalibrasyonu Başlat • Planlandı",
                "Eğik çekilen kamera görüntüsünü ortoparalel (kuşbakışı) gerçek ölçekli düzleme dönüştürür (v0.5.0)."),
            new PlannedActionItem(
                "Canlı Görüntüden Anlık Fotoğraf Yakala • Planlandı",
                "Kameradan dondurulan yüksek çözünürlüklü kareyi doğrudan Fotoğraftan Ölçüm modülüne aktarır (v0.5.0).")
        ];

        RefreshScrcpyCommand = new RelayCommand(async () => await RefreshScrcpyStatusAsync());
        ScanCamerasCommand = new RelayCommand(async () => await ScanCamerasAsync());
        ScanResolutionsCommand = new RelayCommand(async () => await ScanResolutionsAsync());
        StartCameraCommand = new RelayCommand(async () => await StartCameraAsync());
        StopCameraCommand = new RelayCommand(async () => await StopCameraAsync());
        NavigateToPhoneConnectionCommand = new RelayCommand(() => _navigateAction?.Invoke(ModuleId.PhoneConnection));

        ConnectWifiCommand = new RelayCommand(async () => await ConnectWifiAsync(), () => CanConnectWifi);
        DisconnectWifiCommand = new RelayCommand(async () => await DisconnectWifiAsync(), () => CanDisconnectWifi);
        CaptureCurrentFrameCommand = new RelayCommand(CaptureCurrentFrame, () => HasLiveFrame);
        CaptureHighResPhotoCommand = new RelayCommand(async () => await CaptureHighResPhotoAsync(), () => CanCaptureHighResPhoto);
        LoadPendingPhotosCommand = new RelayCommand(async () => await LoadPendingPhotosAsync(), () => CanLoadPendingPhotos);
        DownloadPendingPhotoCommand = new RelayCommand<RemotePhotoItem>(async item => await DownloadPendingPhotoAsync(item), item => item != null && !IsBusy);

        _phoneService.StateChanged += (_, _) => OnPhoneStateChanged();
        _scrcpyManager.StatusChanged += (_, _) => OnScrcpySessionStateChanged();

        // Senkron başlangıç kontrolü
        CheckInitialScrcpyStatus();

        if (HasConnectedPhone && IsScrcpyInstalled)
        {
            _ = ScanCamerasAsync();
        }
    }

    public IReadOnlyList<PlannedActionItem> PlannedCalibrationActions { get; }

    public CameraConnectionMode ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                OnPropertyChanged(nameof(IsUsbMode));
                OnPropertyChanged(nameof(IsWifiMode));
            }
        }
    }

    public bool IsUsbMode
    {
        get => _activeMode == CameraConnectionMode.ScrcpyUsb;
        set { if (value) ActiveMode = CameraConnectionMode.ScrcpyUsb; }
    }

    public bool IsWifiMode
    {
        get => _activeMode == CameraConnectionMode.WifiDirect;
        set { if (value) ActiveMode = CameraConnectionMode.WifiDirect; }
    }

    public string WifiServerAddress
    {
        get => _wifiServerAddress;
        set => SetProperty(ref _wifiServerAddress, value);
    }

    public string WifiPairingPin
    {
        get => _wifiPairingPin;
        set => SetProperty(ref _wifiPairingPin, value);
    }

    public bool IsWifiConnecting
    {
        get => _isWifiConnecting;
        private set
        {
            if (SetProperty(ref _isWifiConnecting, value))
            {
                OnPropertyChanged(nameof(CanConnectWifi));
                OnPropertyChanged(nameof(CanDisconnectWifi));
            }
        }
    }

    public bool IsWifiConnected => _streamClient.IsConnected;

    public ImageSource? LiveFrameSource
    {
        get => _liveFrameSource;
        private set
        {
            if (SetProperty(ref _liveFrameSource, value))
            {
                OnPropertyChanged(nameof(HasLiveFrame));
            }
        }
    }

    public bool HasLiveFrame => _liveFrameSource != null;

    /// <summary>
    /// Yalnızca başarıyla çözümlenen ve arayüze aktarılan kareler üzerinden hesaplanan anlık FPS.
    /// Bozuk veya çözümlenemeyen kareler FPS sayacına dahil edilmez.
    /// </summary>
    public double WifiFps => _decodedFps;

    public long ReceivedFrameCount => _streamClient.ReceivedFrameCount;

    public string WifiResolutionText
    {
        get => _wifiResolutionText;
        private set => SetProperty(ref _wifiResolutionText, value);
    }

    public string? CapturedPhotoPath => _capturedPhotoPath;

    public bool CanConnectWifi => !IsBusy && !IsWifiConnecting && !IsWifiConnected;
    public bool CanDisconnectWifi => !IsBusy && (IsWifiConnected || IsWifiConnecting);
    public bool CanCaptureHighResPhoto => IsWifiConnected && !IsBusy && !_isCapturingHighRes;
    public bool CanLoadPendingPhotos => IsWifiConnected && !IsBusy;

    public bool IsCapturingHighRes
    {
        get => _isCapturingHighRes;
        private set
        {
            if (SetProperty(ref _isCapturingHighRes, value))
            {
                OnPropertyChanged(nameof(CanCaptureHighResPhoto));
                (CaptureHighResPhotoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string HighResCaptureProgressText
    {
        get => _highResCaptureProgressText;
        private set => SetProperty(ref _highResCaptureProgressText, value);
    }

    public ObservableCollection<RemotePhotoItem> PendingRemotePhotos { get; } = new();
    public bool HasPendingRemotePhotos => PendingRemotePhotos.Count > 0;

    public ICommand ConnectWifiCommand { get; }
    public ICommand DisconnectWifiCommand { get; }
    public ICommand CaptureCurrentFrameCommand { get; }
    public ICommand CaptureHighResPhotoCommand { get; }
    public ICommand LoadPendingPhotosCommand { get; }
    public ICommand DownloadPendingPhotoCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanStartCamera));
                OnPropertyChanged(nameof(CanStopCamera));
                OnPropertyChanged(nameof(CanScanCameras));
            }
        }
    }

    public bool IsScrcpyInstalled
    {
        get => _isScrcpyInstalled;
        private set
        {
            if (SetProperty(ref _isScrcpyInstalled, value))
            {
                OnPropertyChanged(nameof(CanStartCamera));
                OnPropertyChanged(nameof(CanScanCameras));
            }
        }
    }

    public string ScrcpyVersionText
    {
        get => _scrcpyVersionText;
        private set => SetProperty(ref _scrcpyVersionText, value);
    }

    public string ScrcpyExecutablePath
    {
        get => _scrcpyExecutablePath;
        private set => SetProperty(ref _scrcpyExecutablePath, value);
    }

    public bool HasConnectedPhone => _phoneService.ConnectedDevice is not null && _phoneService.ConnectedDevice.IsReady;

    public bool HasNoConnectedPhone => !HasConnectedPhone;

    public string ConnectedPhoneTitle => _phoneService.ConnectedDevice?.DisplayTitle ?? "Telefon Bağlı Değil";

    public string ConnectedPhoneSerial => _phoneService.ConnectedDevice?.SerialId ?? string.Empty;

    public IReadOnlyList<ScrcpyCameraInfo> AvailableCameras
    {
        get => _availableCameras;
        private set
        {
            if (SetProperty(ref _availableCameras, value))
            {
                OnPropertyChanged(nameof(HasDetectedCameras));
            }
        }
    }

    public bool HasDetectedCameras => _availableCameras.Count > 0;

    public ScrcpyCameraInfo? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                OnPropertyChanged(nameof(SelectedCameraId));
                OnPropertyChanged(nameof(CanStartCamera));
                _ = OnCameraSelectionChangedAsync(value);
            }
        }
    }

    public string SelectedCameraId => _selectedCamera?.Id ?? "0";

    public IReadOnlyList<ScrcpyCameraSizeOption> AvailableCameraSizes
    {
        get => _availableCameraSizes;
        private set
        {
            if (SetProperty(ref _availableCameraSizes, value))
            {
                OnPropertyChanged(nameof(HasDetectedCameraSizes));
            }
        }
    }

    public bool HasDetectedCameraSizes => _availableCameraSizes.Count > 0;

    public ScrcpyCameraSizeOption? SelectedCameraSize
    {
        get => _selectedCameraSize;
        set
        {
            if (SetProperty(ref _selectedCameraSize, value))
            {
                if (value is not null)
                {
                    SelectedResolution = value.SizeArgument;
                    if (value.SupportedFps is { Count: > 0 } fpsList)
                    {
                        AvailableFpsOptions = fpsList;
                        SelectedFps = value.DefaultOrBestFps;
                    }
                    else if (_selectedCamera?.SupportedFps is { Count: > 0 } camFps)
                    {
                        AvailableFpsOptions = camFps;
                        SelectedFps = camFps.Contains(30) ? 30 : camFps[0];
                    }
                }
            }
        }
    }

    public IReadOnlyList<string> AvailableResolutions
    {
        get => _availableResolutions;
        private set => SetProperty(ref _availableResolutions, value);
    }

    public string SelectedResolution
    {
        get => _selectedResolution;
        set => SetProperty(ref _selectedResolution, value);
    }

    public IReadOnlyList<int> AvailableFpsOptions
    {
        get => _availableFpsOptions;
        private set => SetProperty(ref _availableFpsOptions, value);
    }

    public int SelectedFps
    {
        get => _selectedFps;
        set => SetProperty(ref _selectedFps, value);
    }

    public IReadOnlyList<string> AvailableBitrates => _availableBitrates;

    public string SelectedBitrate
    {
        get => _selectedBitrate;
        set => SetProperty(ref _selectedBitrate, value);
    }

    public ScrcpySessionStatus SessionStatus => _scrcpyManager.CurrentStatus;

    public bool IsSessionRunning => _scrcpyManager.CurrentStatus.IsActive;

    public string SessionStatusBadge => _scrcpyManager.CurrentStatus.StateBadge;

    public int? SessionProcessId => _scrcpyManager.CurrentStatus.ProcessId;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorDetails
    {
        get => _errorDetails;
        private set
        {
            if (SetProperty(ref _errorDetails, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_errorDetails);

    public bool CanStartCamera =>
        !IsBusy &&
        IsScrcpyInstalled &&
        HasConnectedPhone &&
        !IsSessionRunning;

    public bool CanStopCamera =>
        !IsBusy &&
        IsSessionRunning;

    public bool CanScanCameras =>
        !IsBusy &&
        IsScrcpyInstalled &&
        HasConnectedPhone &&
        !IsSessionRunning;

    public ICommand RefreshScrcpyCommand { get; }
    public ICommand ScanCamerasCommand { get; }
    public ICommand ScanResolutionsCommand { get; }
    public ICommand StartCameraCommand { get; }
    public ICommand StopCameraCommand { get; }
    public ICommand NavigateToPhoneConnectionCommand { get; }

    private void CheckInitialScrcpyStatus()
    {
        var scrcpyPath = _scrcpyManager.ScrcpyExecutablePath;
        if (!string.IsNullOrWhiteSpace(scrcpyPath))
        {
            IsScrcpyInstalled = true;
            ScrcpyExecutablePath = scrcpyPath;
            ScrcpyVersionText = "scrcpy kurulu ve hazır";
            StatusMessage = "scrcpy motoru hazır. Telefon kamerasını başlatabilirsiniz.";
            ErrorDetails = null;
        }
        else
        {
            IsScrcpyInstalled = false;
            ScrcpyExecutablePath = string.Empty;
            ScrcpyVersionText = "scrcpy bulunamadı";
            ErrorDetails = "Bilgisayarda scrcpy 5.0+ bulunamadı. WinGet ('winget install Genymobile.scrcpy') ile kurulum yapabilirsiniz.";
            StatusMessage = "scrcpy motoru eksik. Canlı kamera önizlemesi için scrcpy kurulumu gereklidir.";
        }
    }

    public async Task RefreshScrcpyStatusAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "scrcpy motoru kontrol ediliyor...";
            var ver = await _scrcpyManager.GetVersionAsync(CancellationToken.None);
            if (ver.IsFound)
            {
                IsScrcpyInstalled = true;
                ScrcpyExecutablePath = _scrcpyManager.ScrcpyExecutablePath ?? string.Empty;
                ScrcpyVersionText = $"scrcpy {ver.VersionText} (Kurulu ve Hazır)";
                ErrorDetails = null;
                StatusMessage = "scrcpy motoru hazır. Telefon kamerasını başlatabilirsiniz.";
            }
            else
            {
                IsScrcpyInstalled = false;
                ScrcpyExecutablePath = string.Empty;
                ScrcpyVersionText = "scrcpy bulunamadı";
                ErrorDetails = "Bilgisayarda scrcpy 5.0+ bulunamadı. Lütfen WinGet ('winget install Genymobile.scrcpy') ile kurulum yapın.";
                StatusMessage = "scrcpy motoru eksik. Canlı kamera önizlemesi için scrcpy kurulumu gereklidir.";
            }
        }
        catch (Exception ex)
        {
            ErrorDetails = ex.Message;
            StatusMessage = "scrcpy kontrolü sırasında hata oluştu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ScanCamerasAsync()
    {
        if (!CanScanCameras)
        {
            return;
        }

        var serial = _phoneService.ConnectedDevice?.SerialId;
        if (string.IsNullOrWhiteSpace(serial))
        {
            StatusMessage = "Bağlı bir Android telefon seçili değil.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"{ConnectedPhoneTitle} üzerindeki kameralar taranıyor...";
            ErrorDetails = null;

            var result = await _scrcpyManager.QueryCamerasAsync(serial, CancellationToken.None);
            if (result.Success && result.Cameras.Count > 0)
            {
                AvailableCameras = result.Cameras;
                var initialCam = AvailableCameras.FirstOrDefault(c => c.Facing == ScrcpyCameraFacing.Back)
                                 ?? AvailableCameras[0];
                SelectedCamera = initialCam;
                StatusMessage = $"{AvailableCameras.Count} adet kamera algılandı. Aktif kamera: {SelectedCamera.DisplayTitle}";
            }
            else
            {
                AvailableCameras = [];
                SelectedCamera = null;
                AvailableCameraSizes = [];
                SelectedCameraSize = null;
                StatusMessage = result.Message;
                ErrorDetails = !string.IsNullOrWhiteSpace(result.ErrorDetail)
                    ? result.ErrorDetail
                    : "Kamera listesi sorgulanamadı veya cihazda sensör bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            AvailableCameras = [];
            SelectedCamera = null;
            AvailableCameraSizes = [];
            SelectedCameraSize = null;
            ErrorDetails = ex.Message;
            StatusMessage = "Kamera taraması sırasında hata oluştu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OnCameraSelectionChangedAsync(ScrcpyCameraInfo? camera)
    {
        if (camera is null)
        {
            AvailableCameraSizes = [];
            SelectedCameraSize = null;
            return;
        }

        var serial = _phoneService.ConnectedDevice?.SerialId;
        if (string.IsNullOrWhiteSpace(serial) || !IsScrcpyInstalled)
        {
            return;
        }

        try
        {
            var result = await _scrcpyManager.QueryCameraSizesAsync(serial, camera.Id, CancellationToken.None);
            if (result.Success && result.Sizes.Count > 0)
            {
                AvailableCameraSizes = result.Sizes;
                AvailableResolutions = result.Sizes.Select(s => s.SizeArgument).Distinct().ToList();

                // Standart (non-high-speed) 1080p tercih et; yoksa ilk standart boyutu; yoksa ilkini seç
                var preferred = result.Sizes.FirstOrDefault(s => !s.IsHighSpeed && s.Width == 1920 && s.Height == 1080)
                                ?? result.Sizes.FirstOrDefault(s => !s.IsHighSpeed)
                                ?? result.Sizes[0];
                SelectedCameraSize = preferred;
            }
            else
            {
                // Fallback standart çözünürlükler
                var fallbackSizes = ScrcpyProcessManager.GetFallbackStandardSizes();
                AvailableCameraSizes = fallbackSizes;
                AvailableResolutions = fallbackSizes.Select(s => s.SizeArgument).Distinct().ToList();
                SelectedCameraSize = fallbackSizes.FirstOrDefault(s => s.Width == 1920 && s.Height == 1080)
                                     ?? fallbackSizes[0];
            }
        }
        catch (Exception ex)
        {
            ErrorDetails = ex.Message;
        }
    }

    public async Task ScanResolutionsAsync()
    {
        var serial = _phoneService.ConnectedDevice?.SerialId;
        if (string.IsNullOrWhiteSpace(serial) || !IsScrcpyInstalled || SelectedCamera is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"{SelectedCamera.DisplayTitle} için desteklenen modlar sorgulanıyor...";
            var result = await _scrcpyManager.QueryCameraSizesAsync(serial, SelectedCameraId, CancellationToken.None);
            if (result.Success && result.Sizes.Count > 0)
            {
                AvailableCameraSizes = result.Sizes;
                AvailableResolutions = result.Sizes.Select(s => s.SizeArgument).Distinct().ToList();

                var currentArg = SelectedCameraSize?.SizeArgument ?? SelectedResolution;
                var currentIsHs = SelectedCameraSize?.IsHighSpeed ?? false;

                var match = result.Sizes.FirstOrDefault(s => s.SizeArgument == currentArg && s.IsHighSpeed == currentIsHs)
                            ?? result.Sizes.FirstOrDefault(s => !s.IsHighSpeed && s.Width == 1920 && s.Height == 1080)
                            ?? result.Sizes[0];

                SelectedCameraSize = match;
                StatusMessage = $"{AvailableCameraSizes.Count} adet kamera çözünürlük modu listelendi.";
            }
            else
            {
                StatusMessage = result.Message;
                if (!string.IsNullOrWhiteSpace(result.ErrorDetail))
                {
                    ErrorDetails = result.ErrorDetail;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StartCameraAsync()
    {
        if (!CanStartCamera)
        {
            return;
        }

        var serial = _phoneService.ConnectedDevice?.SerialId;
        if (string.IsNullOrWhiteSpace(serial))
        {
            StatusMessage = "Lütfen önce 'Telefon Bağlantısı' modülünden hazır bir Android telefon seçin.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Kamera oturumu başlatılıyor...";
            ErrorDetails = null;

            var config = new ScrcpyCameraSessionConfig(
                SerialId: serial,
                CameraId: SelectedCameraId,
                CameraSize: SelectedResolution,
                VideoBitRate: SelectedBitrate,
                NoAudio: true,
                CameraFps: SelectedFps,
                WindowTitle: $"Ölçü Atölyesi PC — Canlı Kamera [{ConnectedPhoneTitle}]",
                WindowWidth: 960,
                WindowHeight: 540,
                WindowX: 120,
                WindowY: 120);

            var launchResult = await _scrcpyManager.StartCameraSessionAsync(config, CancellationToken.None);
            if (launchResult.Success)
            {
                StatusMessage = $"Canlı kamera oturumu başlatıldı (PID: {launchResult.ProcessId}). Yönetilen harici pencerede akış sağlanıyor.";
                ErrorDetails = null;
            }
            else
            {
                StatusMessage = launchResult.Message;
                ErrorDetails = launchResult.ErrorDetail;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Kamera başlatılırken beklenmedik bir hata oluştu.";
            ErrorDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnScrcpySessionStateChanged();
        }
    }

    public async Task StopCameraAsync()
    {
        if (!CanStopCamera)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Kamera oturumu sonlandırılıyor...";
            await _scrcpyManager.StopCameraSessionAsync(CancellationToken.None);
            StatusMessage = "Canlı kamera oturumu kullanıcı tarafından durduruldu.";
            ErrorDetails = null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Kamera sonlandırılırken hata oluştu.";
            ErrorDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnScrcpySessionStateChanged();
        }
    }

    private void OnPhoneStateChanged()
    {
        OnPropertyChanged(nameof(HasConnectedPhone));
        OnPropertyChanged(nameof(HasNoConnectedPhone));
        OnPropertyChanged(nameof(ConnectedPhoneTitle));
        OnPropertyChanged(nameof(ConnectedPhoneSerial));
        OnPropertyChanged(nameof(CanStartCamera));
        OnPropertyChanged(nameof(CanScanCameras));

        if (HasConnectedPhone && _availableCameras.Count == 0 && !IsBusy && IsScrcpyInstalled)
        {
            _ = ScanCamerasAsync();
        }
        else if (!HasConnectedPhone && _availableCameras.Count > 0)
        {
            AvailableCameras = [];
            SelectedCamera = null;
            AvailableCameraSizes = [];
            SelectedCameraSize = null;
        }
    }

    private void OnScrcpySessionStateChanged()
    {
        OnPropertyChanged(nameof(SessionStatus));
        OnPropertyChanged(nameof(IsSessionRunning));
        OnPropertyChanged(nameof(SessionStatusBadge));
        OnPropertyChanged(nameof(SessionProcessId));
        OnPropertyChanged(nameof(CanStartCamera));
        OnPropertyChanged(nameof(CanStopCamera));
        OnPropertyChanged(nameof(CanScanCameras));
    }

    public async Task ConnectWifiAsync()
    {
        if (string.IsNullOrWhiteSpace(WifiServerAddress))
        {
            StatusMessage = "Lütfen telefonun Wi-Fi IP ve port adresini girin.";
            return;
        }

        try
        {
            IsWifiConnecting = true;
            StatusMessage = $"{WifiServerAddress} adresine bağlanılıyor...";
            ErrorDetails = null;

            var result = await _streamClient.ConnectAsync(WifiServerAddress, WifiPairingPin);
            if (result.Success)
            {
                StatusMessage = "Telefondan canlı görüntü başarıyla alınıyor.";
                ErrorDetails = null;
            }
            else
            {
                StatusMessage = result.Message;
                ErrorDetails = !string.IsNullOrWhiteSpace(result.ErrorDetail) ? result.ErrorDetail : result.Message;
                ResetWifiStreamingState();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Wi-Fi bağlantısı sırasında beklenmedik hata.";
            ErrorDetails = ex.Message;
            ResetWifiStreamingState();
        }
        finally
        {
            IsWifiConnecting = false;
            UpdateWifiCommandStates();
        }
    }

    public async Task DisconnectWifiAsync()
    {
        try
        {
            StatusMessage = "Wi-Fi kamera akışı kapatılıyor...";
            await _streamClient.DisconnectAsync();
            StatusMessage = "Wi-Fi kamera bağlantısı kesildi.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Bağlantı kesilirken hata oluştu.";
            ErrorDetails = ex.Message;
        }
        finally
        {
            ResetWifiStreamingState();
        }
    }

    private void OnStreamConnectionStateChanged(object? sender, bool isConnected)
    {
        if (!isConnected)
        {
            ResetWifiStreamingState();
        }
        else
        {
            ExecuteOnUi(UpdateWifiCommandStates);
        }
    }

    private void ResetWifiStreamingState()
    {
        ExecuteOnUi(() =>
        {
            _decodedFps = 0.0;
            _decodedFpsCounter = 0;
            _decodedFpsStopwatch.Reset();
            LiveFrameSource = null;
            WifiResolutionText = "-";
            _lastReceivedJpeg = null;
            _isWifiConnecting = false;

            UpdateWifiCommandStates();
        });
    }

    private void UpdateWifiCommandStates()
    {
        OnPropertyChanged(nameof(IsWifiConnected));
        OnPropertyChanged(nameof(CanConnectWifi));
        OnPropertyChanged(nameof(CanDisconnectWifi));
        OnPropertyChanged(nameof(CanCaptureHighResPhoto));
        OnPropertyChanged(nameof(CanLoadPendingPhotos));
        OnPropertyChanged(nameof(HasPendingRemotePhotos));
        OnPropertyChanged(nameof(HasLiveFrame));
        OnPropertyChanged(nameof(WifiFps));
        OnPropertyChanged(nameof(WifiResolutionText));
        OnPropertyChanged(nameof(ReceivedFrameCount));

        (ConnectWifiCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DisconnectWifiCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureCurrentFrameCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureHighResPhotoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadPendingPhotosCommand as RelayCommand)?.RaiseCanExecuteChanged();

        CommandManager.InvalidateRequerySuggested();
    }

    public async Task CaptureHighResPhotoAsync()
    {
        if (!CanCaptureHighResPhoto) return;

        try
        {
            IsBusy = true;
            IsCapturingHighRes = true;
            StatusMessage = "Telefona yüksek çözünürlüklü çekim komutu iletiliyor...";
            HighResCaptureProgressText = "1/4: Telefona çekim komutu gönderiliyor...";
            ErrorDetails = null;

            // 1. Yetkilendirme kontrolü
            if (!_remotePhotoClient.IsAuthenticated)
            {
                var authOk = await _remotePhotoClient.AuthenticateAsync(WifiServerAddress, WifiPairingPin, CancellationToken.None);
                if (!authOk)
                {
                    StatusMessage = "Telefonla kimlik doğrulama başarısız. PIN kodunun telefonda görünen güncel kod olduğundan emin olun.";
                    ErrorDetails = "Telefona geçerli bir oturum belirteci ile bağlanılamadı.";
                    return;
                }
            }

            // 2. Çekim komutu gönder (Telefon donanımından gerçek still-capture)
            HighResCaptureProgressText = "2/4: Telefon fotoğrafı çekiyor ve dahili depolamaya kaydediyor...";
            var captureResult = await _remotePhotoClient.TriggerCaptureAsync(CancellationToken.None);
            if (!captureResult.Success || string.IsNullOrWhiteSpace(captureResult.PhotoId))
            {
                StatusMessage = captureResult.ErrorMessage ?? "Telefonda yüksek çözünürlüklü fotoğraf çekilemedi.";
                ErrorDetails = captureResult.ErrorMessage;
                return;
            }

            // 3. Dosyayı PC'ye kayıpsız aktar (ve SHA-256 bütünlüğünü doğrula)
            HighResCaptureProgressText = $"3/4: PC'ye kayıpsız aktarılıyor ({captureResult.FileSizeBytes / 1024} KB)...";
            var localFolder = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi", "Captures");
            Directory.CreateDirectory(localFolder);

            var downloadedPath = await _remotePhotoClient.DownloadPhotoAsync(
                captureResult.PhotoId,
                captureResult.Sha256 ?? string.Empty,
                localFolder,
                null,
                CancellationToken.None);

            // 4. SHA-256 doğrulandı; Fotoğraftan Ölçüm modülünde aç ve kalibrasyonu sıfırla
            HighResCaptureProgressText = "4/4: Bütünlük doğrulandı. Ölçüm modülünde açılıyor...";
            if (_openCapturedPhotoAction != null)
            {
                var opened = _openCapturedPhotoAction(downloadedPath);
                if (opened)
                {
                    _capturedPhotoPath = downloadedPath;
                    StatusMessage = $"Yüksek çözünürlüklü fotoğraf başarıyla aktarıldı: {Path.GetFileName(downloadedPath)} ({captureResult.Width}x{captureResult.Height})";
                    ErrorDetails = null;
                }
                else
                {
                    StatusMessage = "Fotoğraf indirildi ancak ölçüm modülünde açılamadı.";
                    ErrorDetails = "Görüntü dosyası okunamadı veya formatı geçersiz.";
                }
            }
            else
            {
                _capturedPhotoPath = downloadedPath;
                StatusMessage = $"Fotoğraf indirildi ve kaydedildi: {Path.GetFileName(downloadedPath)}";
                _navigateAction?.Invoke(ModuleId.PhotoMeasurement);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Yüksek çözünürlüklü fotoğraf aktarımında hata oluştu.";
            ErrorDetails = $"{ex.Message} (Fotoğraf telefonda korunuyor; bekleyenler listesinden tekrar indirebilirsiniz).";
        }
        finally
        {
            IsBusy = false;
            IsCapturingHighRes = false;
            HighResCaptureProgressText = string.Empty;
            UpdateWifiCommandStates();
        }
    }

    public async Task LoadPendingPhotosAsync()
    {
        if (!CanLoadPendingPhotos) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Telefondaki kayıtlı ölçüm fotoğrafları sorgulanıyor...";

            if (!_remotePhotoClient.IsAuthenticated)
            {
                var authOk = await _remotePhotoClient.AuthenticateAsync(WifiServerAddress, WifiPairingPin, CancellationToken.None);
                if (!authOk)
                {
                    StatusMessage = "Telefonla kimlik doğrulama başarısız. PIN kodunun telefonda görünen güncel kod olduğundan emin olun.";
                    return;
                }
            }

            var photos = await _remotePhotoClient.ListPhotosAsync(CancellationToken.None);
            ExecuteOnUi(() =>
            {
                PendingRemotePhotos.Clear();
                foreach (var p in photos)
                {
                    PendingRemotePhotos.Add(p);
                }
                OnPropertyChanged(nameof(HasPendingRemotePhotos));
                StatusMessage = $"{photos.Count} adet kayıtlı fotoğraf listelendi.";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "Telefondaki fotoğraflar listelenirken hata oluştu.";
            ErrorDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
            UpdateWifiCommandStates();
        }
    }

    public async Task DownloadPendingPhotoAsync(RemotePhotoItem? item)
    {
        if (item == null || IsBusy) return;

        try
        {
            IsBusy = true;
            StatusMessage = $"{item.FileName} indiriliyor...";

            var localFolder = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi", "Captures");
            Directory.CreateDirectory(localFolder);

            var downloadedPath = await _remotePhotoClient.DownloadPhotoAsync(
                item.PhotoId,
                item.Sha256,
                localFolder,
                null,
                CancellationToken.None);

            if (_openCapturedPhotoAction != null)
            {
                var opened = _openCapturedPhotoAction(downloadedPath);
                if (opened)
                {
                    _capturedPhotoPath = downloadedPath;
                    StatusMessage = $"Fotoğraf aktarıldı: {item.FileName}";
                    ErrorDetails = null;
                }
                else
                {
                    StatusMessage = "Fotoğraf indirildi fakat ölçüm modülünde açılamadı.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Fotoğraf indirilirken hata oluştu.";
            ErrorDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
            UpdateWifiCommandStates();
        }
    }

    public void CaptureCurrentFrame()
    {
        var jpeg = _lastReceivedJpeg;
        if (jpeg == null || jpeg.Length == 0)
        {
            StatusMessage = "Yakalanacak aktif bir kamera karesi yok.";
            return;
        }

        // JPEG bütünlük kontrolü: Minimum 4 bayt, SOI (0xFF, 0xD8) ve EOI (0xFF, 0xD9)
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[^2] != 0xFF || jpeg[^1] != 0xD9)
        {
            StatusMessage = "Yakalanan kamera karesi geçerli veya eksiksiz bir JPEG formatında değil.";
            ErrorDetails = "JPEG başlangıç (SOI 0xFFD8) veya bitiş (EOI 0xFFD9) belirteci eksik ya da bozuk.";
            return;
        }

        try
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi", "Captures");
            Directory.CreateDirectory(tempFolder);
            CleanupOldCaptures(tempFolder);

            var filePath = Path.Combine(tempFolder, $"camera_capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            File.WriteAllBytes(filePath, jpeg);

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                StatusMessage = "Kare diske yazılamadı veya oluşturulan dosya boş.";
                return;
            }

            // Ölçüm modülüne aktarımı gerçekleştir
            if (_openCapturedPhotoAction != null)
            {
                var opened = _openCapturedPhotoAction(filePath);
                if (opened)
                {
                    _capturedPhotoPath = filePath;
                    StatusMessage = $"Kare başarıyla donduruldu ve aktarıldı: {Path.GetFileName(filePath)}";
                    ErrorDetails = null;
                }
                else
                {
                    StatusMessage = "Kare yakalandı fakat ölçüm modülünde açılamadı.";
                    ErrorDetails = "Görüntü dosyası okunamadı veya formatı geçersiz.";
                    // Hatalı aktarımda navigasyon yapılmaz; mevcut ekran ve çalışma korunur
                }
            }
            else
            {
                // Doğrudan aktarım eylemi verilmediyse geri uyumluluk: dosya yolunu ata ve sayfaya git
                _capturedPhotoPath = filePath;
                StatusMessage = $"Kare başarıyla donduruldu ve kaydedildi: {Path.GetFileName(filePath)}";
                _navigateAction?.Invoke(ModuleId.PhotoMeasurement);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Kare yakalanırken hata oluştu.";
            ErrorDetails = ex.Message;
        }
    }

    private static void CleanupOldCaptures(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            var directory = new DirectoryInfo(folder);
            var cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (var file in directory.GetFiles("camera_capture_*.jpg"))
            {
                if (file.CreationTimeUtc < cutoff)
                {
                    try { file.Delete(); } catch { }
                }
            }
        }
        catch { }
    }

    private void OnLiveFrameReceived(object? sender, byte[] jpegBytes)
    {
        if (jpegBytes == null || jpegBytes.Length == 0)
        {
            return;
        }

        try
        {
            using var ms = new MemoryStream(jpegBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _lastReceivedJpeg = jpegBytes;
            _successfulDecodes++;

            _decodedFpsCounter++;
            if (!_decodedFpsStopwatch.IsRunning)
            {
                _decodedFpsStopwatch.Start();
            }
            else if (_decodedFpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _decodedFps = Math.Round(_decodedFpsCounter * 1000.0 / _decodedFpsStopwatch.ElapsedMilliseconds, 1);
                _decodedFpsCounter = 0;
                _decodedFpsStopwatch.Restart();
            }

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;

            ExecuteOnUi(() =>
            {
                LiveFrameSource = bitmap;
                WifiResolutionText = $"{width} × {height}";
                ErrorDetails = null;
                OnPropertyChanged(nameof(WifiFps));
                OnPropertyChanged(nameof(ReceivedFrameCount));
                OnPropertyChanged(nameof(HasLiveFrame));
                (CaptureCurrentFrameCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _failedDecodes++;
            ExecuteOnUi(() =>
            {
                if (_successfulDecodes == 0)
                {
                    ErrorDetails = $"Kamera karesi çözümlenemedi ({ex.GetType().Name}): {ex.Message}";
                }
            });
        }
    }

    private void ExecuteOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && dispatcher.Thread.IsAlive && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.BeginInvoke(action);
                return;
            }
            catch { }
        }
        action();
    }

    public void Dispose()
    {
        _streamClient.FrameReceived -= OnLiveFrameReceived;
        _streamClient.ConnectionStateChanged -= OnStreamConnectionStateChanged;
        _streamClient.Dispose();
        _scrcpyManager.Dispose();
    }
}
