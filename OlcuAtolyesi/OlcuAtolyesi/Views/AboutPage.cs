using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.Views;

public sealed class AboutPage : PrototypePage
{
    public AboutPage() : base(AppText.About.Title, "general")
    {
        var stack = ViewFactory.Stack(14);
        stack.Add(ViewFactory.PageTitle(AppText.AppName));
        stack.Add(ViewFactory.Secondary(AppText.Version));
        stack.Add(ViewFactory.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                ViewFactory.Body(AppText.About.Body),
                ViewFactory.Body(AppText.About.Scope),
                ViewFactory.Body(AppText.About.Privacy)
            }
        }));
        stack.Add(ViewFactory.InfoCard(AppText.DemoWarning, AppText.PrototypeNote));
        Content = PageBody(ViewFactory.Scroll(stack));
    }
}
