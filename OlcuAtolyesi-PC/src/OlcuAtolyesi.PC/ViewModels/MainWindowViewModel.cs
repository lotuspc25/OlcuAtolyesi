using System.IO;
using System.Windows.Input;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Phone;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC.Resources.Strings;
using OlcuAtolyesi.PC.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed class NavigationMenuItem : ObservableObject
{
    private bool _isSelected;

    public NavigationMenuItem(ModuleDescriptor descriptor, Action<ModuleId> selectAction)
    {
        Descriptor = descriptor;
        SelectCommand = new RelayCommand(() => selectAction(descriptor.Id));
    }

    public ModuleDescriptor Descriptor { get; }

    public ModuleId Id => Descriptor.Id;

    public string Title => Descriptor.Title;

    public string IconGlyph => Descriptor.IconGlyph;

    public string StateBadge => Descriptor.State == ModuleImplementationState.Planned ? "Planlandı" : PcText.DisplayVersion;

    public bool IsPlanned => Descriptor.State == ModuleImplementationState.Planned;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand SelectCommand { get; }
}

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DesktopSettingsService _settings;
    private readonly DesktopProjectService _projectService;
    private readonly UsbPhoneConnectionService _phoneConnectionService;
    private readonly IScrcpyProcessManager _scrcpyManager;
    private readonly Dictionary<ModuleId, PageViewModelBase> _pages;
    private PageViewModelBase _currentPage;
    private Views.DiagnosticConsoleWindow? _diagnosticWindow;
    private Views.MeasurementWindow? _measurementWindow;
    private Views.ScreenCalibrationWindow? _calibrationWindow;

    public MainWindowViewModel(
        DesktopSettingsService? settings = null,
        ModuleCatalogService? catalog = null,
        DesktopProjectService? projectService = null,
        IPhotoFileDialogService? dialogService = null,
        UsbPhoneConnectionService? phoneConnectionService = null,
        IScrcpyProcessManager? scrcpyManager = null)
    {
        _settings = settings ?? new DesktopSettingsService(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OlcuAtolyesiPC"));
        _projectService = projectService ?? new DesktopProjectService(_settings.WorkspaceDirectory, seedSamples: true);
        _phoneConnectionService = phoneConnectionService ?? new UsbPhoneConnectionService();
        _scrcpyManager = scrcpyManager ?? new ScrcpyProcessManager();
        var catalogService = catalog ?? new ModuleCatalogService();
        var dialogs = dialogService ?? new WpfPhotoFileDialogService();

        _pages = CreatePages(catalogService, _settings, _projectService, dialogs, _phoneConnectionService, _scrcpyManager);
        NavigationItems = catalogService
            .GetAllModules()
            .Select(m => new NavigationMenuItem(m, NavigateTo))
            .ToList();

        _currentPage = _pages[ModuleId.Dashboard];
        UpdateSelectionState(ModuleId.Dashboard);

        ToggleThemeCommand = new RelayCommand(() =>
        {
            var next = _settings.Theme == AppThemeMode.Dark ? AppThemeMode.Light : AppThemeMode.Dark;
            _settings.SetTheme(next);
        });

        _settings.PreferencesChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentThemeLabel));
            OnPropertyChanged(nameof(DefaultUnitLabel));
        };

        OpenDiagnosticConsoleCommand = new RelayCommand(OpenDiagnosticConsole);
        OpenMeasurementWindowCommand = new RelayCommand(OpenMeasurementWindow);
        OpenScreenCalibrationCommand = new RelayCommand(OpenScreenCalibration);
    }

    public string AppTitle => PcText.AppTitle;

    public string WindowTitle => PcText.WindowTitle;

    public string DisplayVersion => PcText.DisplayVersion;

    public string PlatformVersionText => $"{PcText.DisplayVersion} • Windows Masaüstü";

    public string VersionText => PcText.Version;

    public string Subtitle => PcText.Subtitle;

    public DesktopSettingsService Settings => _settings;

    public DesktopProjectService ProjectService => _projectService;

    public UsbPhoneConnectionService PhoneConnectionService => _phoneConnectionService;

    public IReadOnlyList<NavigationMenuItem> NavigationItems { get; }

    public PageViewModelBase CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CurrentModuleTitle));
                OnPropertyChanged(nameof(CurrentModuleStateBadge));
            }
        }
    }

    public string CurrentModuleTitle => CurrentPage.Title;

    public string CurrentModuleStateBadge => CurrentPage.StateBadge;

    public string CurrentThemeLabel => _settings.Theme.DisplayName();

    public string DefaultUnitLabel => _settings.DefaultUnit.ToString();

    public ICommand ToggleThemeCommand { get; }

    public ICommand OpenDiagnosticConsoleCommand { get; }

    public ICommand OpenMeasurementWindowCommand { get; }

    public ICommand OpenScreenCalibrationCommand { get; }

    public PhotoMeasurementViewModel PhotoMeasurementPage => (PhotoMeasurementViewModel)_pages[ModuleId.PhotoMeasurement];

    public void OpenScreenCalibration()
    {
        if (_calibrationWindow is { IsLoaded: true })
        {
            _calibrationWindow.Activate();
            return;
        }

        _calibrationWindow = new Views.ScreenCalibrationWindow(_settings);
        _calibrationWindow.Closed += (_, _) =>
        {
            _calibrationWindow = null;
            PhotoMeasurementPage.RefreshScreenCalibration();
        };
        _calibrationWindow.Show();
    }

    /// <summary>
    /// Ölçüm çalışma alanını bağımsız, tam ekran bir pencerede açar.
    /// Aynı ViewModel örneği paylaşıldığı için noktalar ve sonuçlar anında senkron kalır.
    /// </summary>
    public void OpenMeasurementWindow()
    {
        if (_measurementWindow == null || !_measurementWindow.IsLoaded)
        {
            _measurementWindow = new Views.MeasurementWindow(PhotoMeasurementPage);
            _measurementWindow.Closed += (_, _) => _measurementWindow = null;
            _measurementWindow.Show();
            return;
        }

        if (_measurementWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            _measurementWindow.WindowState = System.Windows.WindowState.Normal;
        }

        _measurementWindow.Activate();
    }

    public ICommand ToggleMagnifierCommand => PhotoMeasurementPage.ToggleMagnifierCommand;

    public ICommand MagnifierZoom2Command => PhotoMeasurementPage.MagnifierZoom2Command;

    public ICommand MagnifierZoom4Command => PhotoMeasurementPage.MagnifierZoom4Command;

    public ICommand MagnifierZoom8Command => PhotoMeasurementPage.MagnifierZoom8Command;

    public ICommand MagnifierZoomInCommand => PhotoMeasurementPage.MagnifierZoomInCommand;

    public ICommand MagnifierZoomOutCommand => PhotoMeasurementPage.MagnifierZoomOutCommand;

    public void OpenDiagnosticConsole()
    {
        if (_diagnosticWindow == null || !_diagnosticWindow.IsLoaded)
        {
            _diagnosticWindow = new Views.DiagnosticConsoleWindow();
            _diagnosticWindow.Closed += (_, _) => _diagnosticWindow = null;
            _diagnosticWindow.Show();
        }
        else
        {
            if (_diagnosticWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _diagnosticWindow.WindowState = System.Windows.WindowState.Normal;
            }
            _diagnosticWindow.Activate();
        }
    }

    public void NavigateTo(ModuleId moduleId)
    {
        if (!_pages.TryGetValue(moduleId, out var target))
        {
            return;
        }

        CurrentPage = target;
        UpdateSelectionState(moduleId);
    }

    public void OpenProjectInPhotoMeasurement(Guid projectId)
    {
        var photoVm = (PhotoMeasurementViewModel)_pages[ModuleId.PhotoMeasurement];
        if (photoVm.LoadProject(projectId))
        {
            NavigateTo(ModuleId.PhotoMeasurement);
        }
    }

    public bool OpenCapturedPhotoInMeasurement(string photoPath)
    {
        if (_pages.TryGetValue(ModuleId.PhotoMeasurement, out var page) && page is PhotoMeasurementViewModel photoVm)
        {
            if (photoVm.LoadCapturedPhoto(photoPath))
            {
                NavigateTo(ModuleId.PhotoMeasurement);
                return true;
            }
        }

        return false;
    }

    public PageViewModelBase GetPage(ModuleId moduleId) => _pages[moduleId];

    private void UpdateSelectionState(ModuleId selectedId)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Id == selectedId;
        }
    }

    private Dictionary<ModuleId, PageViewModelBase> CreatePages(
        ModuleCatalogService catalog,
        DesktopSettingsService settings,
        DesktopProjectService projectService,
        IPhotoFileDialogService dialogService,
        UsbPhoneConnectionService phoneConnectionService,
        IScrcpyProcessManager scrcpyManager)
    {
        var dict = new Dictionary<ModuleId, PageViewModelBase>
        {
            [ModuleId.Dashboard] = new DashboardViewModel(
                catalog.GetModule(ModuleId.Dashboard),
                catalog,
                settings,
                NavigateTo),

            [ModuleId.Projects] = new ProjectsViewModel(
                catalog.GetModule(ModuleId.Projects),
                projectService,
                settings,
                dialogService,
                OpenProjectInPhotoMeasurement),

            [ModuleId.PhoneConnection] = new PhoneConnectionViewModel(
                catalog.GetModule(ModuleId.PhoneConnection),
                phoneConnectionService),

            [ModuleId.CameraCalibration] = new CameraCalibrationViewModel(
                catalog.GetModule(ModuleId.CameraCalibration),
                phoneConnectionService,
                scrcpyManager,
                NavigateTo,
                openCapturedPhotoAction: OpenCapturedPhotoInMeasurement),

            [ModuleId.PhotoMeasurement] = new PhotoMeasurementViewModel(
                catalog.GetModule(ModuleId.PhotoMeasurement),
                settings,
                dialogService,
                projectService),

            [ModuleId.CadExport] = new ModulePageViewModel(
                catalog.GetModule(ModuleId.CadExport),
                "2B DXF için ezdxf (MIT) şema referanslı yerel C# yazıcı; 3B STEP ve STL katı modelleme için Open CASCADE / OCCT (LGPL-2.1) dinamik kütüphanesi ve FreeCAD başsız (freecadcmd) köprüsü.",
                [
                    new PlannedActionItem("2B DXF Dosyası Oluştur • Tamamlandı (v0.6.0)", "Ölçülen referans ve mesafe geometrisini standart AutoCAD R12 ASCII DXF ($INSUNITS=4 mm) formatında dışa aktarır (v0.6.0)."),
                    new PlannedActionItem("3B STL (Ekstrüzyon) Oluştur • Planlandı", "2B profili girilen et kalınlığıyla 3B baskıya uygun su geçirmez STL ağa dönüştürür (v0.6.0)."),
                    new PlannedActionItem("Analitik STEP (AP214) Oluştur • Planlandı", "Open CASCADE çekirdeği ile CAD yazılımları için gerçek B-Rep katı model üretir (v0.8.0).")
                ]),

            [ModuleId.Print3D] = new ModulePageViewModel(
                catalog.GetModule(ModuleId.Print3D),
                "AGPL-3.0 lisans sınırını korumak için OrcaSlicer ve PrusaSlicer yalnızca ayrı süreç (out-of-process CLI) olarak çağrılır; yazıcılarla Moonraker (Klipper) ve OctoPrint REST API üzerinden haberleşilir.",
                [
                    new PlannedActionItem("OrcaSlicer / PrusaSlicer ile Dilimle • Planlandı", "Üretilen STL modelini komut satırından dilimleyip süre ve filament tahmini alır (v0.9.0)."),
                    new PlannedActionItem("3B Yazıcıya Gönder (Moonraker / OctoPrint) • Planlandı", "Yerel ağdaki desteklenen 3B yazıcıya G-code yükler ve durumu izler (v1.0.0).")
                ]),

            [ModuleId.Settings] = new SettingsViewModel(
                catalog.GetModule(ModuleId.Settings),
                settings),

            [ModuleId.Help] = new HelpViewModel(
                catalog.GetModule(ModuleId.Help),
                catalog)
        };

        return dict;
    }

    public void Dispose()
    {
        _diagnosticWindow?.Close();
        _measurementWindow?.Close();
        _calibrationWindow?.Close();
        _scrcpyManager.Dispose();
    }
}
