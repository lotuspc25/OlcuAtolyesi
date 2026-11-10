using Microsoft.Maui.Storage;
using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.Views;

public sealed class HomePage : PrototypePage
{
    private const string OnboardingDismissedKey = "home.onboardingDismissed";
    private readonly DemoProjectService _projects;
    private readonly VerticalStackLayout _recentProjects = ViewFactory.Stack(8);
    private readonly Label _emptyRecent = ViewFactory.Secondary(AppText.Home.NoRecentProjects);
    private readonly Border _onboarding;

    public HomePage() : base(AppText.Tabs.Home, "general")
    {
        _projects = App.GetService<DemoProjectService>();

        var onboardingContent = ViewFactory.Stack(7);
        onboardingContent.Add(ViewFactory.SectionTitle(AppText.Home.OnboardingTitle));
        onboardingContent.Add(ViewFactory.Body(AppText.Home.OnboardingBody));
        onboardingContent.Add(ViewFactory.SecondaryButton(AppText.Home.DismissOnboarding, DismissOnboarding));
        _onboarding = ViewFactory.Card(onboardingContent, "InfoCardStyle");
        _onboarding.IsVisible = !Preferences.Default.Get(OnboardingDismissedKey, false);

        var photoButton = ViewFactory.PrimaryButton(
            AppText.Home.PhotoAction,
            async (_, _) => await Shell.Current.GoToAsync(nameof(PhotoSourcePage)),
            "Kamera, galeri veya demo seçimini açar.");
        photoButton.ImageSource = "photo_on_primary.png";

        var quickAccess = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        quickAccess.Add(QuickCard("ruler.png", AppText.Home.RulerAction,
            async (_, _) => await Shell.Current.GoToAsync(nameof(RulerPage))), 0, 0);
        quickAccess.Add(QuickCard("camera.png", AppText.Home.CameraAction,
            async (_, _) => await Shell.Current.GoToAsync(nameof(CameraMeasurePage))), 1, 0);

        var pcCameraButton = ViewFactory.SecondaryButton(
            AppText.PcCamera.Action,
            async (_, _) => await Shell.Current.GoToAsync(nameof(PcCameraPage)),
            AppText.PcCamera.Description);

        var stack = ViewFactory.Stack(13);
        stack.Add(ViewFactory.PageTitle(AppText.AppName));
        stack.Add(ViewFactory.Secondary(AppText.Subtitle));
        stack.Add(ViewFactory.Body(AppText.Home.Intro));
        stack.Add(_onboarding);
        stack.Add(photoButton);
        stack.Add(ViewFactory.SectionTitle(AppText.Home.QuickAccess));
        stack.Add(quickAccess);
        stack.Add(pcCameraButton);
        stack.Add(ViewFactory.InfoCard(AppText.Home.PrototypeLabel, AppText.PrototypeNote, "info.png"));
        stack.Add(ViewFactory.SectionTitle(AppText.Home.RecentProjects));
        stack.Add(_emptyRecent);
        stack.Add(_recentProjects);

        Content = PageBody(ViewFactory.Scroll(stack));
        RefreshRecentProjects();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _projects.ProjectsChanged -= OnProjectsChanged;
        _projects.ProjectsChanged += OnProjectsChanged;
        RefreshRecentProjects();
    }

    protected override void OnDisappearing()
    {
        _projects.ProjectsChanged -= OnProjectsChanged;
        base.OnDisappearing();
    }

    private static Border QuickCard(string icon, string title, EventHandler tapped)
    {
        var content = ViewFactory.Stack(7);
        content.Add(ViewFactory.Icon(icon, 32));
        var titleLabel = ViewFactory.StyledLabel(title, "ToolTitleStyle", SemanticHeadingLevel.Level3);
        titleLabel.HorizontalTextAlignment = TextAlignment.Center;
        content.Add(titleLabel);
        content.Add(ViewFactory.StatusBadge(AppText.Demo));
        var card = ViewFactory.Card(content);
        card.MinimumHeightRequest = 128;
        var gesture = new TapGestureRecognizer();
        gesture.Tapped += (sender, args) => tapped(sender, args);
        card.GestureRecognizers.Add(gesture);
        SemanticProperties.SetDescription(card, $"{title}. Demo aracını aç.");
        return card;
    }

    private void RefreshRecentProjects()
    {
        var projects = _projects.GetRecent(3);
        _recentProjects.Clear();
        _emptyRecent.IsVisible = projects.Count == 0;
        foreach (var project in projects)
        {
            _recentProjects.Add(RecentProjectCard(project));
        }
    }

    private static Border RecentProjectCard(DemoProject project)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            RowSpacing = 3
        };
        grid.Add(ViewFactory.StyledLabel(project.Name, "ToolTitleStyle", SemanticHeadingLevel.Level3), 0, 0);
        grid.Add(ViewFactory.Secondary(project.Type.DisplayName()), 0, 1);
        var badgeText = project.HasSavedMeasurementWorkspace ? AppText.SavedBadge : AppText.Demo;
        var badge = ViewFactory.StatusBadge(badgeText);
        grid.Add(badge, 1, 0);
        Grid.SetRowSpan(badge, 2);

        var card = ViewFactory.Card(grid);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
            await Shell.Current.GoToAsync($"{nameof(ProjectDetailPage)}?id={project.Id:D}");
        card.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(card, $"{project.Name}. {badgeText} proje ayrıntısını aç.");
        return card;
    }

    private void OnProjectsChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RefreshRecentProjects);

    private void DismissOnboarding(object? sender, EventArgs e)
    {
        Preferences.Default.Set(OnboardingDismissedKey, true);
        _onboarding.IsVisible = false;
        SemanticScreenReader.Announce("İlk kullanım yönlendirmesi kapatıldı.");
    }
}

