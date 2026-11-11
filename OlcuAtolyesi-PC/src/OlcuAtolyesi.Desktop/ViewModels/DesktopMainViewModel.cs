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

public sealed class DesktopMainViewModel : ObservableObject
{
    private readonly DesktopSettingsService _settingsService = new();
    private readonly DesktopProjectService _projectService = new();
    private readonly DxfExportService _dxfExportService = new();

    private int _selectedTabIndex = 0;
    private string _statusMessage = "Hazır — Linux Debian x64 & Windows Uyumlu";

    // Ölçüm Alanı Özellikleri
    private double _r1X = 150, _r1Y = 350, _r2X = 650, _r2Y = 350;
    private double _m1X = 200, _m1Y = 200, _m2X = 600, _m2Y = 200;
    private double _knownReferenceLength = 85.60; // Kredi kartı genişliği (mm)
    private LengthUnit _selectedUnit = LengthUnit.Millimeter;
    private string _selectedReferenceObjectName = "Banka Kartı (85.60 mm)";
    private string _calculatedResultText = "Hesaplanıyor...";
    private double _scaleMmPerPixel;

    // Telefon Bağlantı Özellikleri
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
        // Komutlar
        SelectTabCommand = new RelayCommand<int>(i => SelectedTabIndex = i);
        SetDemoCardPointsCommand = new RelayCommand(SetDemoCardPoints);
        SetDemoA4PointsCommand = new RelayCommand(SetDemoA4Points);
        ExportDxfCommand = new RelayCommand(ExportCurrentMeasurementDxf);
        TestPhoneConnectionCommand = new RelayCommand(async () => await TestPhoneConnectionAsync());
        ProbeAdbCommand = new RelayCommand(async () => await ProbeAdbStatusAsync());
        CreateProjectCommand = new RelayCommand(CreateProject);
        RefreshProjectsCommand = new RelayCommand(LoadProjects);
        ClearLogsCommand = new RelayCommand(ClearLogs);

        // Başlangıç yüklemeleri
        UpdateCalculatedMeasurements();
        LoadProjects();
        _ = ProbeAdbStatusAsync();

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Info,
            "Desktop",
            "APP_START",
            $"Ölçü Atölyesi Çapraz Masaüstü başlatıldı. İşletim Sistemi: {OsDescription}",
            source: Core.Diagnostics.DiagnosticSource.PC);

        RefreshLogs();
    }

    #region Sistem ve Ortam Bilgileri
    public string AppTitle => $"Ölçü Atölyesi Çapraz Masaüstü {VersionInfo.DisplayVersion} (Linux & Windows)";
    public string VersionText => VersionInfo.DisplayVersion;
    public string OsDescription => RuntimeInformation.OSDescription;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;
    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsWindows => OperatingSystem.IsWindows();
    public string PlatformBadge => IsLinux ? "🐧 Linux (Debian Uyumlu)" : "🪟 Windows";
    public string MachineUser => $"{Environment.MachineName} / {Environment.UserName}";
    #endregion

    #region Navigasyon
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsDashboardTab));
                OnPropertyChanged(nameof(IsMeasureTab));
                OnPropertyChanged(nameof(IsPhoneTab));
                OnPropertyChanged(nameof(IsProjectsTab));
                OnPropertyChanged(nameof(IsDiagnosticsTab));
                OnPropertyChanged(nameof(IsSettingsTab));
            }
        }
    }

    public bool IsDashboardTab => SelectedTabIndex == 0;
    public bool IsMeasureTab => SelectedTabIndex == 1;
    public bool IsPhoneTab => SelectedTabIndex == 2;
    public bool IsProjectsTab => SelectedTabIndex == 3;
    public bool IsDiagnosticsTab => SelectedTabIndex == 4;
    public bool IsSettingsTab => SelectedTabIndex == 5;

    public RelayCommand<int> SelectTabCommand { get; }
    #endregion

    #region Durum Çubuğu
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    #endregion

    #region Ölçüm Modülü (R1-R2 & M1-M2)
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

    public RelayCommand SetDemoCardPointsCommand { get; }
    public RelayCommand SetDemoA4PointsCommand { get; }
    public RelayCommand ExportDxfCommand { get; }

    private void SetDemoCardPoints()
    {
        SelectedReferenceObjectName = "Banka Kartı (85.60 mm)";
        KnownReferenceLength = 85.60;
        R1X = 150; R1Y = 350; R2X = 550; R2Y = 350; // 400 piksel
        M1X = 150; M1Y = 180; M2X = 750; M2Y = 180; // 600 piksel -> 128.40 mm
        StatusMessage = "Demo banka kartı ve parça ölçüm koordinatları yüklendi.";
    }

    private void SetDemoA4Points()
    {
        SelectedReferenceObjectName = "A4 Kağıt (210.00 mm)";
        KnownReferenceLength = 210.0;
        R1X = 100; R1Y = 400; R2X = 800; R2Y = 400; // 700 piksel
        M1X = 200; M1Y = 220; M2X = 550; M2Y = 220; // 350 piksel -> 105.00 mm
        StatusMessage = "Demo A4 referans koordinatları yüklendi.";
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
                "Çapraz masaüstü otomatik DXF dışa aktarma",
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                false,
                null,
                new ImageSize(1000, 800),
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
                ? "ADB aracı kurulu değil. Debian terminalinde 'sudo apt install adb' kurabilirsiniz."
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

    #region Teşhis ve Sistem Günlükleri
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
