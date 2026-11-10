#if ANDROID
using CommunityToolkit.Maui;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        builder.UseMauiCommunityToolkitCamera();
#endif
        builder.Services.AddSingleton<IPreferenceStore, MauiPreferenceStore>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton(_ => new DemoProjectService(
            seedSamples: true,
            storageDirectory: FileSystem.AppDataDirectory));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

