using System.Windows.Input;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed record ThemeOptionItem(AppThemeMode Mode, string DisplayName);

public sealed class SettingsViewModel : PageViewModelBase
{
    private readonly DesktopSettingsService _settings;
    private AppThemeMode _selectedTheme;
    private LengthUnit _selectedDefaultUnit;
    private string _saveFeedback = "Tercihler yerel çalışma alanında otomatik olarak saklanır.";

    public SettingsViewModel(ModuleDescriptor descriptor, DesktopSettingsService settings)
        : base(descriptor)
    {
        _settings = settings;
        _selectedTheme = settings.Theme;
        _selectedDefaultUnit = settings.DefaultUnit;

        ThemeOptions =
        [
            new ThemeOptionItem(AppThemeMode.Dark, AppThemeMode.Dark.DisplayName()),
            new ThemeOptionItem(AppThemeMode.Light, AppThemeMode.Light.DisplayName()),
            new ThemeOptionItem(AppThemeMode.System, AppThemeMode.System.DisplayName())
        ];

        UnitOptions = Enum.GetValues<LengthUnit>()
            .Select(u => new UnitChoice(u, u.DisplayName()))
            .ToList();

        SetThemeCommand = new RelayCommand<AppThemeMode>(mode => SelectedTheme = mode);
    }

    public IReadOnlyList<ThemeOptionItem> ThemeOptions { get; }

    public IReadOnlyList<UnitChoice> UnitOptions { get; }

    public AppThemeMode SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _settings.SetTheme(value);
                SaveFeedback = $"Tema '{value.DisplayName()}' olarak uygulandı ve kaydedildi.";
            }
        }
    }

    public LengthUnit SelectedDefaultUnit
    {
        get => _selectedDefaultUnit;
        set
        {
            if (SetProperty(ref _selectedDefaultUnit, value))
            {
                _settings.SetDefaultUnit(value);
                SaveFeedback = $"Varsayılan ölçü birimi '{value.DisplayName()}' olarak kaydedildi.";
            }
        }
    }

    public string WorkspaceDirectory => _settings.WorkspaceDirectory;

    public string SaveFeedback
    {
        get => _saveFeedback;
        private set => SetProperty(ref _saveFeedback, value);
    }

    public ICommand SetThemeCommand { get; }
}
