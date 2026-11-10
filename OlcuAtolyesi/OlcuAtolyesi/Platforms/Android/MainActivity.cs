using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using OlcuAtolyesi.Diagnostics;

namespace OlcuAtolyesi
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            SetupExceptionHandling();
            CleanStaleFastDeploymentFiles();
            base.OnCreate(savedInstanceState);
        }

        private static void SetupExceptionHandling()
        {
            try
            {
                AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
                {
                    DiagnosticLogHub.Instance.Log(
                        DiagnosticLevel.Critical,
                        "AndroidRuntime",
                        "UNHANDLED_NATIVE_EX",
                        $"İşlenmemiş Android çalışma zamanı hatası: {args.Exception.Message}",
                        details: args.Exception.ToString());
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    DiagnosticLogHub.Instance.Log(
                        DiagnosticLevel.Critical,
                        "AppDomain",
                        "UNHANDLED_DOMAIN_EX",
                        $"İşlenmemiş AppDomain istisnası: {args.ExceptionObject}",
                        details: args.ExceptionObject?.ToString());
                };
            }
            catch
            {
            }
        }

        private void CleanStaleFastDeploymentFiles()
        {
            try
            {
                var filesDir = FilesDir?.AbsolutePath;
                if (!string.IsNullOrEmpty(filesDir))
                {
                    var overrideDir = System.IO.Path.Combine(filesDir, ".__override__");
                    if (System.IO.Directory.Exists(overrideDir))
                    {
                        System.IO.Directory.Delete(overrideDir, true);
                    }
                }
            }
            catch
            {
                // Sessizce yutulur; kritik değildir.
            }
        }
    }
}
