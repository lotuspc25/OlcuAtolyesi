using System.Windows;
using System.Windows.Threading;
using OlcuAtolyesi.Core.Diagnostics;
using OlcuAtolyesi.PC.Themes;
using OlcuAtolyesi.PC.ViewModels;

namespace OlcuAtolyesi.PC;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global hata yakalama ve tanılama günlüğüne kaydetme
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Critical,
                "AppCrash",
                "UNHANDLED_DOMAIN_EXCEPTION",
                $"Uygulama genelinde yakalanmamış kritik istisna: {ex?.Message}",
                details: ex?.ToString());
        };

        DispatcherUnhandledException += (s, args) =>
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Critical,
                "AppCrash",
                "UNHANDLED_DISPATCHER_EXCEPTION",
                $"WPF UI iş parçacığında yakalanmamış istisna: {args.Exception.Message}",
                details: args.Exception.ToString());
            // Kullanıcıya çökmeden devam etme şansı tanımak veya güvenli loglamak için:
            // İşaretlenmezse varsayılan WPF çöküş davranışı çalışır, ancak log kaydedilmiştir.
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "AppCrash",
                "UNOBSERVED_TASK_EXCEPTION",
                $"Gözlemlenmeyen arka plan görev istisnası: {args.Exception.Message}",
                details: args.Exception.ToString());
            args.SetObserved();
        };

        DiagnosticLogHub.Instance.Log(
            DiagnosticLevel.Event,
            "AppLifecycle",
            "APP_STARTUP",
            "Ölçü Atölyesi PC v0.5.0.5 başarıyla başlatıldı.");

        var viewModel = new MainWindowViewModel();
        ThemeManager.ApplyTheme(Resources, viewModel.Settings.Theme);
        viewModel.Settings.PreferencesChanged += (_, prefs) =>
            ThemeManager.ApplyTheme(Resources, prefs.Theme);

        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DiagnosticLogHub.Instance.CleanShutdown();
        }
        catch
        {
        }

        base.OnExit(e);
    }
}
