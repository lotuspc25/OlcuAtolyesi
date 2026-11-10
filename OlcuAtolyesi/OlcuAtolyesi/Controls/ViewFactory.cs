using OlcuAtolyesi.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace OlcuAtolyesi.Controls;

public static class ViewFactory
{
    public static Label PageTitle(string text) => StyledLabel(text, "PageTitleStyle", SemanticHeadingLevel.Level1);

    public static Label SectionTitle(string text) => StyledLabel(text, "SectionTitleStyle", SemanticHeadingLevel.Level2);

    public static Label Body(string text) => StyledLabel(text, "BodyStyle");

    public static Label Secondary(string text) => StyledLabel(text, "SecondaryTextStyle");

    public static Label Caption(string text) => StyledLabel(text, "CaptionStyle");

    public static Label StyledLabel(
        string text,
        string styleKey,
        SemanticHeadingLevel heading = SemanticHeadingLevel.None)
    {
        var label = new Label
        {
            Text = text,
            LineBreakMode = LineBreakMode.WordWrap
        };
        label.SetDynamicResource(VisualElement.StyleProperty, styleKey);
        if (heading != SemanticHeadingLevel.None)
        {
            SemanticProperties.SetHeadingLevel(label, heading);
        }

        return label;
    }

    public static Button PrimaryButton(string text, EventHandler clicked, string? semanticHint = null) =>
        Button(text, clicked, "PrimaryButtonStyle", semanticHint);

    public static Button SecondaryButton(string text, EventHandler clicked, string? semanticHint = null) =>
        Button(text, clicked, "SecondaryButtonStyle", semanticHint);

    public static Button DestructiveButton(string text, EventHandler clicked, string? semanticHint = null) =>
        Button(text, clicked, "DestructiveButtonStyle", semanticHint);

    public static Button Button(string text, EventHandler clicked, string styleKey, string? semanticHint = null)
    {
        var button = new Button
        {
            Text = text,
            LineBreakMode = LineBreakMode.WordWrap
        };
        button.SetDynamicResource(VisualElement.StyleProperty, styleKey);
        button.Clicked += clicked;
        SemanticProperties.SetDescription(button, text);
        if (!string.IsNullOrWhiteSpace(semanticHint))
        {
            SemanticProperties.SetHint(button, semanticHint);
        }

        return button;
    }

    public static Border Card(View content, string styleKey = "CardStyle")
    {
        var border = new Border { Content = content };
        border.SetDynamicResource(VisualElement.StyleProperty, styleKey);
        return border;
    }

    public static Border InfoCard(string title, string body, string icon = "info.png", string styleKey = "InfoCardStyle")
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            RowSpacing = 3
        };
        var image = Icon(icon, 28);
        image.VerticalOptions = LayoutOptions.Start;
        grid.Add(image, 0, 0);
        Grid.SetRowSpan(image, 2);
        grid.Add(StyledLabel(title, "SectionTitleStyle", SemanticHeadingLevel.Level2), 1, 0);
        grid.Add(Secondary(body), 1, 1);
        return Card(grid, styleKey);
    }

    public static Image Icon(string source, double size = 32)
    {
        var image = new Image
        {
            Source = source,
            WidthRequest = size,
            HeightRequest = size,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetDescription(image, string.Empty);
        return image;
    }

    public static Border StatusBadge(string text, bool planned = false)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            FontFamily = "OpenSansSemibold",
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        label.SetDynamicResource(Label.TextColorProperty, planned ? "TextSecondary" : "PrimaryDark");

        var border = new Border
        {
            Content = label,
            Padding = new Thickness(9, 4),
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
        border.SetDynamicResource(Border.StrokeProperty, planned ? "BorderLight" : "Primary");
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, planned ? "Surface" : "AccentSoft");
        SemanticProperties.SetDescription(border, $"Durum: {text}");
        return border;
    }

    public static Border ToolCard(
        string icon,
        string title,
        string description,
        string status,
        EventHandler? tapped = null,
        bool planned = false)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(48)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            RowSpacing = 3
        };

        var iconView = Icon(icon, 32);
        grid.Add(iconView, 0, 0);
        Grid.SetRowSpan(iconView, 2);
        grid.Add(StyledLabel(title, "ToolTitleStyle", SemanticHeadingLevel.Level3), 1, 0);
        grid.Add(Secondary(description), 1, 1);
        var badge = StatusBadge(status, planned);
        badge.VerticalOptions = LayoutOptions.Start;
        grid.Add(badge, 2, 0);
        Grid.SetRowSpan(badge, 2);

        var card = Card(grid);
        card.MinimumHeightRequest = 80;
        SemanticProperties.SetDescription(card, $"{title}. {description}. Durum: {status}.");
        if (planned)
        {
            SemanticProperties.SetHint(card, AppText.Tools.PlannedHint);
            card.Opacity = 0.82;
        }
        else if (tapped is not null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (sender, args) => tapped(sender, args);
            card.GestureRecognizers.Add(tap);
            SemanticProperties.SetHint(card, "Açmak için dokun.");
        }

        return card;
    }

    public static VerticalStackLayout Stack(double spacing = 12) => new()
    {
        Spacing = spacing,
        HorizontalOptions = LayoutOptions.Fill
    };

    public static ScrollView Scroll(View content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Default,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never
    };

    public static ToolbarItem HelpToolbar(Func<Task> openHelp)
    {
        var item = new ToolbarItem
        {
            Text = AppText.HelpButton,
            Order = ToolbarItemOrder.Primary,
            Priority = 0
        };
        SemanticProperties.SetDescription(item, AppText.Help);
        item.Clicked += async (_, _) => await openHelp();
        return item;
    }
}
