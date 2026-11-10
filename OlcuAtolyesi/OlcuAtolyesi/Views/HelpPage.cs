using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.Views;

public sealed class HelpPage : PrototypePage, IQueryAttributable
{
    private readonly VerticalStackLayout _content = ViewFactory.Stack(14);

    public HelpPage() : base(AppText.Help)
    {
        Content = PageBody(ViewFactory.Scroll(_content));
        Render("general");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var topic = query.TryGetValue("topic", out var raw) ? raw?.ToString() : "general";
        Render(Uri.UnescapeDataString(topic ?? "general"));
    }

    private void Render(string topic)
    {
        var content = topic switch
        {
            "ruler" => new HelpTopic(
                AppText.Ruler.Title,
                AppText.HelpContent.RulerWhat,
                AppText.HelpContent.RulerHow,
                AppText.HelpContent.RulerWorks,
                AppText.HelpContent.RulerConditions),
            "photo" => new HelpTopic(
                AppText.Photo.Title,
                AppText.HelpContent.PhotoWhat,
                AppText.HelpContent.PhotoHow,
                AppText.HelpContent.PhotoWorks,
                AppText.HelpContent.PhotoConditions),
            "camera" => new HelpTopic(
                AppText.Camera.Title,
                AppText.HelpContent.CameraWhat,
                AppText.HelpContent.CameraHow,
                AppText.HelpContent.CameraWorks,
                AppText.HelpContent.CameraConditions),
            _ => new HelpTopic(
                AppText.Help,
                AppText.HelpContent.GeneralWhat,
                AppText.HelpContent.GeneralHow,
                AppText.HelpContent.GeneralWorks,
                AppText.HelpContent.GeneralConditions)
        };

        Title = $"{AppText.Help} — {content.Title}";
        _content.Clear();
        _content.Add(ViewFactory.PageTitle(content.Title));
        _content.Add(ViewFactory.InfoCard(topic == "photo" ? PhotoText.Title : AppText.DemoWarning, AppText.PrototypeNote));
        _content.Add(Section(AppText.HelpContent.What, content.What));
        _content.Add(Section(AppText.HelpContent.How, content.How));
        _content.Add(Section(AppText.HelpContent.Works, content.Works));
        _content.Add(Section(AppText.HelpContent.Conditions, content.Conditions, "WarningCardStyle"));
    }

    private static Border Section(string title, string body, string style = "CardStyle")
    {
        var stack = ViewFactory.Stack(6);
        stack.Add(ViewFactory.SectionTitle(title));
        stack.Add(ViewFactory.Body(body));
        return ViewFactory.Card(stack, style);
    }

    private sealed record HelpTopic(string Title, string What, string How, string Works, string Conditions);
}

