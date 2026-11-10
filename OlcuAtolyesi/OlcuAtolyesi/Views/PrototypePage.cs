using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.Views;

public abstract class PrototypePage : ContentPage
{
    private readonly string? _helpTopic;

    protected PrototypePage(string title, string? helpTopic = null)
    {
        Title = title;
        _helpTopic = helpTopic;
        Shell.SetNavBarIsVisible(this, true);

        if (!string.IsNullOrWhiteSpace(helpTopic))
        {
            ToolbarItems.Add(ViewFactory.HelpToolbar(OpenHelpAsync));
        }
    }

    protected View PageBody(View view, Thickness? padding = null)
    {
        var grid = new Grid
        {
            Padding = padding ?? new Thickness(16, 12, 16, 24),
            Children = { view }
        };
        return grid;
    }

    protected Task OpenHelpAsync()
    {
        var topic = Uri.EscapeDataString(_helpTopic ?? "general");
        return Shell.Current.GoToAsync($"{nameof(HelpPage)}?topic={topic}");
    }
}
