using OlcuAtolyesi.Views;

namespace OlcuAtolyesi;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(RulerPage), typeof(RulerPage));
        Routing.RegisterRoute(nameof(PhotoSourcePage), typeof(PhotoSourcePage));
        Routing.RegisterRoute(nameof(PhotoMeasurePage), typeof(PhotoMeasurePage));
        Routing.RegisterRoute(nameof(CameraMeasurePage), typeof(CameraMeasurePage));
        Routing.RegisterRoute(nameof(PcCameraPage), typeof(PcCameraPage));
        Routing.RegisterRoute(nameof(ProjectDetailPage), typeof(ProjectDetailPage));
        Routing.RegisterRoute(nameof(HelpPage), typeof(HelpPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
    }
}

