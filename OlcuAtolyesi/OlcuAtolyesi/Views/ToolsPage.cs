using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.Views;

public sealed class ToolsPage : PrototypePage
{
    public ToolsPage() : base(AppText.Tabs.Tools, "general")
    {
        var stack = ViewFactory.Stack(14);
        stack.Add(ViewFactory.InfoCard("Araçların durumu", AppText.PrototypeNote));

        AddGroup(stack, AppText.Tools.ScreenGroup,
            ViewFactory.ToolCard("ruler.png", AppText.Tools.Ruler, AppText.Tools.RulerDescription, AppText.Demo,
                async (_, _) => await Shell.Current.GoToAsync(nameof(RulerPage))),
            ViewFactory.ToolCard("angle.png", AppText.Tools.Protractor, AppText.Tools.ProtractorDescription, AppText.Planned,
                planned: true));

        AddGroup(stack, AppText.Tools.PhotoGroup,
            ViewFactory.ToolCard("photo.png", AppText.Tools.PhotoLength, AppText.Tools.PhotoLengthDescription, "Fotoğraf / Demo",
                async (_, _) => await Shell.Current.GoToAsync(nameof(PhotoSourcePage))),
            ViewFactory.ToolCard("angle.png", AppText.Tools.PhotoAngle, AppText.Tools.PhotoAngleDescription, AppText.Planned,
                planned: true),
            ViewFactory.ToolCard("diameter.png", AppText.Tools.Diameter, AppText.Tools.DiameterDescription, AppText.Planned,
                planned: true),
            ViewFactory.ToolCard("area.png", AppText.Tools.PerimeterArea, AppText.Tools.PerimeterAreaDescription, AppText.Planned,
                planned: true));

        AddGroup(stack, AppText.Tools.LiveGroup,
            ViewFactory.ToolCard("camera.png", AppText.PcCamera.Title, AppText.PcCamera.Description, "Aktif",
                async (_, _) => await Shell.Current.GoToAsync(nameof(PcCameraPage))),
            ViewFactory.ToolCard("camera.png", AppText.Tools.CameraDistance, AppText.Tools.CameraDistanceDescription, AppText.Demo,
                async (_, _) => await Shell.Current.GoToAsync(nameof(CameraMeasurePage))),
            ViewFactory.ToolCard("height.png", AppText.Tools.Height, AppText.Tools.HeightDescription, AppText.Planned,
                planned: true),
            ViewFactory.ToolCard("level.png", AppText.Tools.Level, AppText.Tools.LevelDescription, AppText.Planned,
                planned: true));

        Content = PageBody(ViewFactory.Scroll(stack));
    }

    private static void AddGroup(VerticalStackLayout parent, string title, params View[] cards)
    {
        parent.Add(ViewFactory.SectionTitle(title));
        foreach (var card in cards)
        {
            parent.Add(card);
        }
    }
}


