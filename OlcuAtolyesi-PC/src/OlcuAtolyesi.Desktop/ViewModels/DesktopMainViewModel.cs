using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using OlcuAtolyesi.Core;
using OlcuAtolyesi.Core.Diagnostics;
using OlcuAtolyesi.Core.Export;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Measurement;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Phone;
using OlcuAtolyesi.Core.Services;

namespace OlcuAtolyesi.Desktop.ViewModels;

public sealed class DesktopNavMenuItem : ObservableObject
{
    private bool _isSelected;
    public ModuleDescriptor Descriptor { get; }
    public ModuleId Id => Descriptor.Id;
    public string Title => Descriptor.Title;
    public string IconGlyph => Descriptor.IconGlyph;
    public string StateBadge => Descriptor.State == ModuleImplementationState.Planned ? "Planlandı" : $"{VersionInfo.DisplayVersion} (Aktif)";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }

    public DesktopNavMenuItem(ModuleDescriptor descriptor, Action<ModuleId> selectAction)
    {
        Descriptor = descriptor;
        SelectCommand = new RelayCommand(() => selectAction(descriptor.Id));
    }
}

public sealed class DashboardModuleCardItem
{
    public ModuleId Id { get; }
    public string Title { get; }
    public string IconGlyph { get; }
    public string StateBadge { get; }
    public string ShortSummary { get; }
    public string TargetVersion { get; }
    public RelayCommand OpenCommand { get; }

    public DashboardModuleCardItem(ModuleDescriptor descriptor, Action<ModuleId> openAction)
    {
        Id = descriptor.Id;
        Title = descriptor.Title;
        IconGlyph = descriptor.IconGlyph;
        StateBadge = descriptor.State == ModuleImplementationState.Planned ? "Planlandı" : $"{VersionInfo.DisplayVersion} (Aktif)";
        ShortSummary = descriptor.ShortSummary;
        TargetVersion = descriptor.TargetVersion;
        OpenCommand = new RelayCommand(() => openAction(descriptor.Id));
    }
}

public sealed class DesktopMainViewModel : ObservableObject
{
    private readonly DesktopSettingsService _settingsService = new();
    private readonly DesktopProjectService _projectService = new();
    private readonly DxfExportService _dxfExportService = new();
    private readonly ModuleCatalogService _catalogService = new();

    private ModuleId _selectedModuleId = ModuleId.Dashboard;
    private bool _isDiagnosticConsoleOpen = false;
    private string _statusMessage = "Hazır — Linux Debian x64 & Windows Uyumlu";

    // Ölçüm Modülü (R1-R2 & M1-M2)
    private double _r1X = 150, _r1Y = 320, _r2X = 550, _r2Y = 320;
    private double _m1X = 150, _m1Y = 160, _m2X = 750, _m2Y = 160;
    private double _knownReferenceLength = 85.60;
    private LengthUnit _selectedUnit = LengthUnit.Millimeter;
    private string _selectedReferenceObjectName = "Banka Kartı (85.60 mm)";
    private string _calculatedResultText = "128.40 mm (Ölçek: 0.2140 mm/px)";
    private double _scaleMmPerPixel = 0.2140;

    // Büyüteç ve Görüntü Filtreleri
    private bool _isMagnifierEnabled = false;
    private double _magnifierZoom = 2.0;
    private int _filterBrightness = 0;
    private int _filterContrast = 0;
    private bool _filterGrayscale = false;
    private bool _filterNegative = false;
    private bool _filterEdgeDetection = false;

    // Telefon Bağlantısı
    private string _phoneIp = "192.168.1.100";
    private int _phonePort = 8080;
    private string _phonePin = "123456";
    private string _phoneStatusText = "Telefon bekleniyor (Wi-Fi MJPEG veya USB ADB)";
    private bool _isPhoneConnecting;

    // Projeler
    private string _newProjectName = "Debian-Ölçüm-Projesi-1";
    private string _projectStatusMessage = "";

    public DesktopMainViewModel()
    {
        // 9 Modül Menüsü
        NavigationItems = new ObservableCollection<DesktopNavMenuItem>(
            _catalogService.GetAllModules().Select(m => new DesktopNavMenuItem(m, NavigateTo)));

        // Dashboard Kartları
        DashboardCards = new ObservableCollection<DashboardModuleCardItem>(
            _catalogService.GetAllModules()
                .Where(m => m.Id != ModuleId.Dashboard)
                .Select(m => new DashboardModuleCardItem(m, NavigateTo)));

        // Komutlar
        NavigateCommand = new RelayCommand<ModuleId>(NavigateTo);
        ToggleDiagnosticConsoleCommand = new RelayCommand(() => IsDiagnosticConsoleOpen = !IsDiagnosticConsoleOpen);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        SetDemoCardPointsCommand = new RelayCommand(SetDemoCardPoints);
        SetDemoA4PointsCommand = new RelayCommand(SetDemoA4Points);
        ExportDxfCommand = new RelayCommand(ExportCurrentMeasurementDxf);
        TestPhoneConnectionCommand = new RelayCommand(async () => await TestPhoneConnectionAsync());
        ProbeAdbCommand = new RelayCommand(async () => await ProbeAdbStatusAsync());
        CreateProjectCommand = new RelayCommand(CreateProject);
        RefreshProjectsCommand = new RelayCommand(LoadProjects);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        ToggleMagnifierCommand = new RelayCommand(() => IsMagnifierEnabled = !IsMagnifierEnabled);
        SetZoom2Command = new RelayCommand(() => MagnifierZoom = 2.0);
        SetZoom4Command = new RelayCommand(() => MagnifierZoom = 4.0);
        SetZoom8Command = new RelayCommand(() => MagnifierZoom = 8.0);
        ResetFiltersCommand = new RelayCommand(ResetFilters);

        // Başlangıç verileri
        UpdateCalculatedMeasurements();
        LoadProjects();
        _ = ProbeAdbStatusAsync();
        UpdateSelectionState();

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "Desktop",
            "APP_START",
            $"Ölçü Atölyesi Çapraz Masaüstü {VersionInfo.DisplayVersion} başlatıldı. OS: {OsDescription}",
            source: Core.Diagnostics.DiagnosticSource.PC);

        RefreshLogs();
    }

    #region Sistem ve Ortam Bilgisi
    public string AppTitle => "Ölçü Atölyesi PC";
    public string WindowTitle => $"Ölçü Atölyesi Çapraz Masaüstü {VersionInfo.DisplayVersion} (Linux & Windows)";
    public string VersionText => $"{VersionInfo.DisplayVersion} • Çapraz Masaüstü";
    public string PlatformVersionText => IsLinux ? "Linux (Debian) • .NET 10" : "Windows • .NET 10";
    public string OsDescription => RuntimeInformation.OSDescription;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;
    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsWindows => OperatingSystem.IsWindows();
    public string PlatformBadge => IsLinux ? "🐧 Linux (Debian Uyumlu)" : "🪟 Windows Masaüstü";
    public string MachineUser => $"{Environment.MachineName} / {Environment.UserName}";
    public string CurrentThemeLabel => "Koyu Tema";
    #endregion

    #region Navigasyon ve Modül Yönetimi
    public ObservableCollection<DesktopNavMenuItem> NavigationItems { get; }
    public ObservableCollection<DashboardModuleCardItem> DashboardCards { get; }

    public ModuleId SelectedModuleId
    {
        get => _selectedModuleId;
        set
        {
            if (SetProperty(ref _selectedModuleId, value))
            {
                UpdateSelectionState();
                OnPropertyChanged(nameof(CurrentModuleTitle));
                OnPropertyChanged(nameof(CurrentModuleSubtitle));
                OnPropertyChanged(nameof(CurrentModuleStateBadge));
                OnPropertyChanged(nameof(IsDashboard));
                OnPropertyChanged(nameof(IsProjects));
                OnPropertyChanged(nameof(IsPhoneConnection));
                OnPropertyChanged(nameof(IsCameraCalibration));
                OnPropertyChanged(nameof(IsPhotoMeasurement));
                OnPropertyChanged(nameof(IsScreenCalibration));
                OnPropertyChanged(nameof(IsCadExport));
                OnPropertyChanged(nameof(IsPrint3D));
                OnPropertyChanged(nameof(IsSettings));
                OnPropertyChanged(nameof(IsHelp));
            }
        }
    }

    public bool IsDiagnosticConsoleOpen
    {
        get => _isDiagnosticConsoleOpen;
        set => SetProperty(ref _isDiagnosticConsoleOpen, value);
    }

    public bool IsDashboard => SelectedModuleId == ModuleId.Dashboard;
    public bool IsProjects => SelectedModuleId == ModuleId.Projects;
    public bool IsPhoneConnection => SelectedModuleId == ModuleId.PhoneConnection;
    public bool IsCameraCalibration => SelectedModuleId == ModuleId.CameraCalibration;
    public bool IsPhotoMeasurement => SelectedModuleId == ModuleId.PhotoMeasurement;
    public bool IsCadExport => SelectedModuleId == ModuleId.CadExport;
    public bool IsPrint3D => SelectedModuleId == ModuleId.Print3D;
    public bool IsScreenCalibration => SelectedModuleId == (ModuleId)99; // Özel ID
    public bool IsSettings => SelectedModuleId == ModuleId.Settings;
    public bool IsHelp => SelectedModuleId == ModuleId.Help;

    public string CurrentModuleTitle => SelectedModuleId switch
    {
        ModuleId.Dashboard => "1. Ana Panel (Genel Bakış)",
        ModuleId.Projects => "2. Projeler (Kayıtlı Proje Yönetimi)",
        ModuleId.PhoneConnection => "3. Telefon Bağlantısı (USB & Wi-Fi)",
        ModuleId.CameraCalibration => "4. Kamera ve Kalibrasyon (Canlı Akış)",
        ModuleId.PhotoMeasurement => "5. Fotoğraftan Ölçüm (Teknik Stüdyo)",
        ModuleId.CadExport => "6. CAD İhracı (DXF / STEP / STL)",
        ModuleId.Print3D => "7. 3B Baskı Hazırlığı",
        ModuleId.Settings => "8. Ayarlar & Yapılandırma",
        ModuleId.Help => "9. Yardım & Kullanım Kılavuzu",
        _ => "Ölçü Atölyesi Çapraz Masaüstü"
    };

    public string CurrentModuleSubtitle => SelectedModuleId switch
    {
        ModuleId.Dashboard => "Masaüstü çalışma alanının genel durumu, modül hazırlık seviyeleri ve açık kaynak entegrasyonu.",
        ModuleId.Projects => "Kalıcı proje arşivi, JSON/fotoğraf atomik depolama ve mobil paket uyumluluğu.",
        ModuleId.PhoneConnection => "USB ADB köprüsü ve yerel ağ üzerinden Android telefon algılama ve denetleme.",
        ModuleId.CameraCalibration => "Wi-Fi MJPEG kamera yayını, PIN eşleme, anlık yüksek çözünürlüklü çekim.",
        ModuleId.PhotoMeasurement => "A ve B cetvelleri, R1-R2 referans, M1-M2 ölçüm, büyüteç, filtreler ve DXF ihracı.",
        ModuleId.CadExport => "2B DXF R12 ASCII, katmanlar, milimetrik CAD dönüşümü ve vektörel dışa aktarma.",
        ModuleId.Print3D => "Ölçülen modellerin 3B baskıya hazırlanması ve dilimleyici CLI entegrasyonu.",
        ModuleId.Settings => "Ölçü birimleri, tema seçimi ve yerel çalışma dizini yapılandırması.",
        ModuleId.Help => "Sık sorulan sorular, klavye kısayolları ve adım adım ölçüm rehberi.",
        _ => ""
    };

    public string CurrentModuleStateBadge => SelectedModuleId switch
    {
        ModuleId.Dashboard => $"{VersionInfo.DisplayVersion} (Aktif)",
        ModuleId.Projects => $"{VersionInfo.DisplayVersion} (Aktif)",
        ModuleId.PhoneConnection => "USB Aktif / Wi-Fi Hazır",
        ModuleId.CameraCalibration => "Wi-Fi Aktif / scrcpy Hazır",
        ModuleId.PhotoMeasurement => $"{VersionInfo.DisplayVersion} (Aktif)",
        ModuleId.CadExport => $"{VersionInfo.DisplayVersion} (Aktif)",
        ModuleId.Print3D => "Temeli Hazır",
        ModuleId.Settings => $"{VersionInfo.DisplayVersion} (Aktif)",
        ModuleId.Help => $"{VersionInfo.DisplayVersion} (Aktif)",
        _ => "v0.6.9"
    };

    public RelayCommand<ModuleId> NavigateCommand { get; }
    public RelayCommand ToggleDiagnosticConsoleCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public void NavigateTo(ModuleId id)
    {
        SelectedModuleId = id;
    }

    private void UpdateSelectionState()
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Id == SelectedModuleId;
        }
    }

    private void ToggleTheme()
    {
        StatusMessage = "Koyu tema varsayılan olarak devrededir.";
    }
    #endregion

    #region Durum Çubuğu
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    #endregion

    #region Fotoğraftan Ölçüm Modülü (R1-R2 / M1-M2 / Büyüteç / Filtreler)
    public double R1X { get => _r1X; set { if (SetProperty(ref _r1X, value)) UpdateCalculatedMeasurements(); } }
    public double R1Y { get => _r1Y; set { if (SetProperty(ref _r1Y, value)) UpdateCalculatedMeasurements(); } }
    public double R2X { get => _r2X; set { if (SetProperty(ref _r2X, value)) UpdateCalculatedMeasurements(); } }
    public double R2Y { get => _r2Y; set { if (SetProperty(ref _r2Y, value)) UpdateCalculatedMeasurements(); } }

    public double M1X { get => _m1X; set { if (SetProperty(ref _m1X, value)) UpdateCalculatedMeasurements(); } }
    public double M1Y { get => _m1Y; set { if (SetProperty(ref _m1Y, value)) UpdateCalculatedMeasurements(); } }
    public double M2X { get => _m2X; set { if (SetProperty(ref _m2X, value)) UpdateCalculatedMeasurements(); } }
    public double M2Y { get => _m2Y; set { if (SetProperty(ref _m2Y, value)) UpdateCalculatedMeasurements(); } }

    public double KnownReferenceLength
    {
        get => _knownReferenceLength;
        set { if (SetProperty(ref _knownReferenceLength, value)) UpdateCalculatedMeasurements(); }
    }

    public LengthUnit SelectedUnit
    {
        get => _selectedUnit;
        set { if (SetProperty(ref _selectedUnit, value)) UpdateCalculatedMeasurements(); }
    }

    public string SelectedReferenceObjectName
    {
        get => _selectedReferenceObjectName;
        set
        {
            if (SetProperty(ref _selectedReferenceObjectName, value))
            {
                if (value.Contains("Banka Kartı")) KnownReferenceLength = 85.60;
                else if (value.Contains("A4")) KnownReferenceLength = 210.0;
                else if (value.Contains("1 TL")) KnownReferenceLength = 26.15;
                else if (value.Contains("50 Kuruş")) KnownReferenceLength = 23.85;
            }
        }
    }

    public string CalculatedResultText
    {
        get => _calculatedResultText;
        private set => SetProperty(ref _calculatedResultText, value);
    }

    public double ScaleMmPerPixel
    {
        get => _scaleMmPerPixel;
        private set => SetProperty(ref _scaleMmPerPixel, value);
    }

    public bool IsMagnifierEnabled
    {
        get => _isMagnifierEnabled;
        set => SetProperty(ref _isMagnifierEnabled, value);
    }

    public double MagnifierZoom
    {
        get => _magnifierZoom;
        set => SetProperty(ref _magnifierZoom, value);
    }

    public int FilterBrightness
    {
        get => _filterBrightness;
        set => SetProperty(ref _filterBrightness, value);
    }

    public int FilterContrast
    {
        get => _filterContrast;
        set => SetProperty(ref _filterContrast, value);
    }

    public bool FilterGrayscale
    {
        get => _filterGrayscale;
        set => SetProperty(ref _filterGrayscale, value);
    }

    public bool FilterNegative
    {
        get => _filterNegative;
        set => SetProperty(ref _filterNegative, value);
    }

    public bool FilterEdgeDetection
    {
        get => _filterEdgeDetection;
        set => SetProperty(ref _filterEdgeDetection, value);
    }

    public RelayCommand SetDemoCardPointsCommand { get; }
    public RelayCommand SetDemoA4PointsCommand { get; }
    public RelayCommand ExportDxfCommand { get; }
    public RelayCommand ToggleMagnifierCommand { get; }
    public RelayCommand SetZoom2Command { get; }
    public RelayCommand SetZoom4Command { get; }
    public RelayCommand SetZoom8Command { get; }
    public RelayCommand ResetFiltersCommand { get; }

    private void SetDemoCardPoints()
    {
        SelectedReferenceObjectName = "Banka Kartı (85.60 mm)";
        KnownReferenceLength = 85.60;
        R1X = 150; R1Y = 320; R2X = 550; R2Y = 320;
        M1X = 150; M1Y = 160; M2X = 750; M2Y = 160;
        StatusMessage = "Demo banka kartı (85.6 mm) koordinatları yüklendi.";
    }

    private void SetDemoA4Points()
    {
        SelectedReferenceObjectName = "A4 Kağıt (210.00 mm)";
        KnownReferenceLength = 210.0;
        R1X = 100; R1Y = 360; R2X = 800; R2Y = 360;
        M1X = 200; M1Y = 180; M2X = 550; M2Y = 180;
        StatusMessage = "Demo A4 kağıt (210 mm) koordinatları yüklendi.";
    }

    private void ResetFilters()
    {
        FilterBrightness = 0;
        FilterContrast = 0;
        FilterGrayscale = false;
        FilterNegative = false;
        FilterEdgeDetection = false;
        StatusMessage = "Görüntü filtreleri sıfırlandı.";
    }

    private void UpdateCalculatedMeasurements()
    {
        var refSegment = new ImageSegment(new ImagePoint(R1X, R1Y), new ImagePoint(R2X, R2Y));
        var measSegment = new ImageSegment(new ImagePoint(M1X, M1Y), new ImagePoint(M2X, M2Y));

        if (refSegment.ImageLength <= 0.0001 || KnownReferenceLength <= 0)
        {
            CalculatedResultText = "Geçersiz referans (R1 ve R2 çakışık olamaz).";
            ScaleMmPerPixel = 0;
            return;
        }

        try
        {
            var refScale = new ReferenceScale(refSegment, new LengthValue(KnownReferenceLength, LengthUnit.Millimeter));
            ScaleMmPerPixel = refScale.MillimetersPerPixel;
            var result = refScale.Measure(measSegment, SelectedUnit);

            CalculatedResultText = $"{result.Value:F2} {result.Unit.Symbol()} (Ölçek: {ScaleMmPerPixel:F4} mm/px)";
        }
        catch (Exception ex)
        {
            CalculatedResultText = $"Hesaplama hatası: {ex.Message}";
        }
    }

    private void ExportCurrentMeasurementDxf()
    {
        try
        {
            var refSegment = new ImageSegment(new ImagePoint(R1X, R1Y), new ImagePoint(R2X, R2Y));
            var measSegment = new ImageSegment(new ImagePoint(M1X, M1Y), new ImagePoint(M2X, M2Y));
            var refLength = new LengthValue(KnownReferenceLength, LengthUnit.Millimeter);
            var measuredLength = new LengthValue(KnownReferenceLength * (measSegment.ImageLength / refSegment.ImageLength), LengthUnit.Millimeter);

            var project = new DesktopProject(
                Guid.NewGuid(),
                "Oto-Olcu-Dxf",
                ProjectType.PhotoMeasurement,
                "Çapraz masaüstü otomatik DXF ihracı",
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                false,
                null,
                new ImageSize(1200, 800),
                refSegment,
                refLength,
                measSegment,
                measuredLength,
                SelectedUnit);

            var result = _dxfExportService.BuildFromProject(project);
            if (result.Success && result.Content is not null)
            {
                var exportDir = Path.Combine(_settingsService.WorkspaceDirectory, "DxfExport");
                Directory.CreateDirectory(exportDir);
                var filePath = Path.Combine(exportDir, $"OlcuAtolyesi_{DateTime.Now:yyyyMMdd_HHmmss}.dxf");
                _dxfExportService.SaveToFile(filePath, result.Content);
                StatusMessage = $"DXF başarıyla kaydedildi: {filePath}";
            }
            else
            {
                StatusMessage = $"DXF hatası: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"DXF dışa aktarma hatası: {ex.Message}";
        }
    }
    #endregion

    #region Telefon Kamera Bağlantısı
    public string PhoneIp { get => _phoneIp; set => SetProperty(ref _phoneIp, value); }
    public int PhonePort { get => _phonePort; set => SetProperty(ref _phonePort, value); }
    public string PhonePin { get => _phonePin; set => SetProperty(ref _phonePin, value); }
    public string PhoneStatusText { get => _phoneStatusText; set => SetProperty(ref _phoneStatusText, value); }
    public bool IsPhoneConnecting { get => _isPhoneConnecting; set => SetProperty(ref _isPhoneConnecting, value); }

    public RelayCommand TestPhoneConnectionCommand { get; }
    public RelayCommand ProbeAdbCommand { get; }

    private async Task TestPhoneConnectionAsync()
    {
        IsPhoneConnecting = true;
        PhoneStatusText = $"Telefon kamerasına bağlanılıyor: http://{PhoneIp}:{PhonePort} ...";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"http://{PhoneIp}:{PhonePort}/status");
            if (response.IsSuccessStatusCode)
            {
                PhoneStatusText = $"Bağlantı Başarılı! Telefon kamerası aktif (HTTP {response.StatusCode})";
                StatusMessage = "Telefondan canlı yayın hazır.";
            }
            else
            {
                PhoneStatusText = $"Telefondan yanıt alındı fakat durum kodu: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            PhoneStatusText = $"Bağlantı kurulamadı: {ex.Message} (Telefon ve bu cihaz aynı Wi-Fi ağında olmalıdır)";
            StatusMessage = "Telefon bağlantı testi başarısız.";
        }
        finally
        {
            IsPhoneConnecting = false;
        }
    }

    private async Task ProbeAdbStatusAsync()
    {
        try
        {
            var locator = new AdbLocator();
            var adbPath = locator.FindAdbExecutablePath();

            if (!string.IsNullOrWhiteSpace(adbPath))
            {
                var runner = new ProcessAdbCommandRunner(locator);
                var versionResult = await runner.RunAsync(new[] { "version" }, TimeSpan.FromSeconds(3));

                if (versionResult.IsSuccess)
                {
                    var devicesResult = await runner.RunAsync(new[] { "devices" }, TimeSpan.FromSeconds(3));
                    PhoneStatusText = $"ADB Tespit Edildi ({adbPath}): {versionResult.StandardOutput.Split('\n')[0].Trim()}\nCihazlar:\n{devicesResult.StandardOutput.Trim()}";
                    StatusMessage = "ADB sürücüsü hazır.";
                    return;
                }
            }

            PhoneStatusText = IsLinux
                ? "ADB aracı kurulu değil. İsteğe bağlı olarak 'sudo apt install adb' kurabilirsiniz."
                : "ADB bulunamadı. Android SDK platform-tools yolunu kontrol ediniz.";
        }
        catch (Exception ex)
        {
            PhoneStatusText = $"ADB kontrolü: {ex.Message}";
        }
    }
    #endregion

    #region Projeler
    public ObservableCollection<DesktopProject> Projects { get; } = new();
    public string NewProjectName { get => _newProjectName; set => SetProperty(ref _newProjectName, value); }
    public string ProjectStatusMessage { get => _projectStatusMessage; set => SetProperty(ref _projectStatusMessage, value); }
    public string WorkspacePath => _settingsService.WorkspaceDirectory;

    public RelayCommand CreateProjectCommand { get; }
    public RelayCommand RefreshProjectsCommand { get; }

    private void LoadProjects()
    {
        try
        {
            Projects.Clear();
            var list = _projectService.GetAll();
            foreach (var p in list)
            {
                Projects.Add(p);
            }
            ProjectStatusMessage = $"{Projects.Count} kayıtlı proje listelendi. (Dizin: {WorkspacePath})";
        }
        catch (Exception ex)
        {
            ProjectStatusMessage = $"Proje yükleme hatası: {ex.Message}";
        }
    }

    private void CreateProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        try
        {
            var proj = _projectService.Create(
                NewProjectName.Trim(),
                ProjectType.PhotoMeasurement,
                "Çapraz masaüstü projesi",
                SelectedUnit);

            LoadProjects();
            StatusMessage = $"'{proj.Name}' projesi başarıyla oluşturuldu.";
        }
        catch (Exception ex)
        {
            ProjectStatusMessage = $"Kayıt hatası: {ex.Message}";
        }
    }
    #endregion

    #region Teşhis Konsolu
    public ObservableCollection<DiagnosticLogEntry> Logs { get; } = new();
    public RelayCommand ClearLogsCommand { get; }

    public void RefreshLogs()
    {
        Logs.Clear();
        var fileLogger = DiagnosticLogHub.Instance.FileLogger;
        foreach (var entry in fileLogger.ReadRecentEntries(50))
        {
            Logs.Add(entry);
        }
    }

    private void ClearLogs()
    {
        Logs.Clear();
        try
        {
            var currentLog = DiagnosticLogHub.Instance.FileLogger.CurrentLogPath;
            if (File.Exists(currentLog))
            {
                File.WriteAllText(currentLog, string.Empty);
            }
        }
        catch
        {
        }
        StatusMessage = "Günlükler temizlendi.";
    }
    #endregion
}
