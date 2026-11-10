using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

public sealed class SettingsChangedEventArgs(AppPreferences preferences) : EventArgs
{
    public AppPreferences Preferences { get; } = preferences;
}

public sealed class SettingsService
{
    public const string ThemePreferenceKey = "settings.theme";
    public const string DefaultLengthUnitKey = "settings.defaultLengthUnit";

    private readonly IPreferenceStore _store;

    public SettingsService(IPreferenceStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Current = Load();
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppPreferences Current { get; private set; }

    public ThemePreference Theme => Current.Theme;

    public LengthUnit DefaultLengthUnit => Current.DefaultLengthUnit;

    public void SetTheme(ThemePreference theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        if (theme == Current.Theme)
        {
            return;
        }

        _store.Set(ThemePreferenceKey, theme.ToString());
        Current = Current with { Theme = theme };
        OnChanged();
    }

    public void SetDefaultLengthUnit(LengthUnit unit)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }

        if (unit == Current.DefaultLengthUnit)
        {
            return;
        }

        _store.Set(DefaultLengthUnitKey, unit.ToString());
        Current = Current with { DefaultLengthUnit = unit };
        OnChanged();
    }

    private AppPreferences Load()
    {
        var theme = ParseDefinedEnum(_store.Get(ThemePreferenceKey), AppPreferences.Default.Theme);
        var unit = ParseDefinedEnum(_store.Get(DefaultLengthUnitKey), AppPreferences.Default.DefaultLengthUnit);
        return new AppPreferences(theme, unit);
    }

    private static TEnum ParseDefinedEnum<TEnum>(string? storedValue, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(storedValue, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : fallback;

    private void OnChanged() => Changed?.Invoke(this, new SettingsChangedEventArgs(Current));
}
