using Microsoft.Extensions.DependencyInjection;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi;

public partial class App : Application
{
    private readonly SettingsService _settings;

    public App(IServiceProvider serviceProvider, SettingsService settings)
    {
        ServiceProvider = serviceProvider;
        _settings = settings;
        InitializeComponent();
        try { Media.TemporaryPhoto.RemoveStaleCopies(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        ApplyTheme(_settings.Theme);
        _settings.Changed += OnSettingsChanged;
    }

    public IServiceProvider ServiceProvider { get; }

    public static T GetService<T>() where T : notnull
    {
        var app = Current as App
            ?? throw new InvalidOperationException("Uygulama hizmetleri henüz hazır değil.");
        return app.ServiceProvider.GetRequiredService<T>();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) =>
        ApplyTheme(e.Preferences.Theme);

    private void ApplyTheme(ThemePreference theme)
    {
        UserAppTheme = theme switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}

