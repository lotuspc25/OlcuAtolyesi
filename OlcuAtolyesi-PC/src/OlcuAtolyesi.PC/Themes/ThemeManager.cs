using System.IO.Packaging;
using System.Windows;
using Microsoft.Win32;
using OlcuAtolyesi.Core.Services;

namespace OlcuAtolyesi.PC.Themes;

public static class ThemeManager
{
    private static readonly Uri LightThemeUri;
    private static readonly Uri DarkThemeUri;

    static ThemeManager()
    {
        // pack:// URI şemasının bağımsız test süreçlerinde de kayıtlı olmasını garanti eder
        _ = PackUriHelper.UriSchemePack;
        _ = Application.Current;
        LightThemeUri = new Uri("/OlcuAtolyesi.PC;component/Themes/LightTheme.xaml", UriKind.Relative);
        DarkThemeUri = new Uri("/OlcuAtolyesi.PC;component/Themes/DarkTheme.xaml", UriKind.Relative);
    }

    public static bool ResolveIsDark(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Dark => true,
        AppThemeMode.Light => false,
        AppThemeMode.System => IsWindowsInDarkMode(),
        _ => true
    };

    public static void ApplyTheme(ResourceDictionary rootResources, AppThemeMode mode)
    {
        ArgumentNullException.ThrowIfNull(rootResources);
        var isDark = ResolveIsDark(mode);
        var targetUri = isDark ? DarkThemeUri : LightThemeUri;

        var existingTheme = rootResources.MergedDictionaries.FirstOrDefault(d =>
            d.Source is not null &&
            (d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
             d.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)));

        var newDict = new ResourceDictionary { Source = targetUri };
        if (existingTheme is not null)
        {
            var index = rootResources.MergedDictionaries.IndexOf(existingTheme);
            rootResources.MergedDictionaries[index] = newDict;
        }
        else
        {
            rootResources.MergedDictionaries.Insert(0, newDict);
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0;
            }
        }
        catch
        {
        }

        return true;
    }
}
