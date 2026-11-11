using System.Windows.Input;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC.Resources.Strings;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed class DashboardModuleCardItem
{
    public DashboardModuleCardItem(ModuleDescriptor descriptor, Action<ModuleId> navigateAction)
    {
        Descriptor = descriptor;
        OpenCommand = new RelayCommand(() => navigateAction(descriptor.Id));
    }

    public ModuleDescriptor Descriptor { get; }

    public ModuleId Id => Descriptor.Id;

    public string Title => Descriptor.Title;

    public string IconGlyph => Descriptor.IconGlyph;

    public string ShortSummary => Descriptor.ShortSummary;

    public string StateBadge => Descriptor.State.BadgeText();

    public string TargetVersion => Descriptor.TargetVersion;

    public bool IsPlanned => Descriptor.State == ModuleImplementationState.Planned;

    public ICommand OpenCommand { get; }
}

public sealed class DashboardViewModel : PageViewModelBase
{
    private readonly DesktopSettingsService _settings;

    public DashboardViewModel(
        ModuleDescriptor descriptor,
        ModuleCatalogService catalog,
        DesktopSettingsService settings,
        Action<ModuleId> navigateAction)
        : base(descriptor)
    {
        _settings = settings;
        var allModules = catalog.GetAllModules();
        ModuleCards = allModules
            .Where(m => m.Id != ModuleId.Dashboard)
            .Select(m => new DashboardModuleCardItem(m, navigateAction))
            .ToList();

        ActiveModuleCount = allModules.Count(m => m.State == ModuleImplementationState.Active);
        FoundationModuleCount = allModules.Count(m => m.State == ModuleImplementationState.FoundationReady);
        PlannedModuleCount = allModules.Count(m => m.State == ModuleImplementationState.Planned);
        OpenSourceCount = catalog.GetOpenSourceCandidates().Count;

        _settings.PreferencesChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentThemeText));
            OnPropertyChanged(nameof(CurrentUnitText));
        };
    }

    public IReadOnlyList<DashboardModuleCardItem> ModuleCards { get; }

    public string WorkspaceHeading => $"Ölçü Atölyesi PC — Masaüstü Çalışma Alanı ({PcText.DisplayVersion})";

    public string DisplayVersion => PcText.DisplayVersion;

    public int ActiveModuleCount { get; }

    public int FoundationModuleCount { get; }

    public int PlannedModuleCount { get; }

    public int OpenSourceCount { get; }

    public string CurrentThemeText => _settings.Theme.DisplayName();

    public string CurrentUnitText => _settings.DefaultUnit.ToString() switch
    {
        nameof(Core.Geometry.LengthUnit.Millimeter) => "Milimetre (mm)",
        nameof(Core.Geometry.LengthUnit.Centimeter) => "Santimetre (cm)",
        nameof(Core.Geometry.LengthUnit.Meter) => "Metre (m)",
        nameof(Core.Geometry.LengthUnit.Inch) => "İnç (in)",
        _ => "Milimetre (mm)"
    };
}
