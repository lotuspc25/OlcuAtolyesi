using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.Views;

public sealed class ProjectsPage : PrototypePage
{
    private readonly DemoProjectService _projects;
    private readonly VerticalStackLayout _projectList = ViewFactory.Stack(10);
    private readonly Border _emptyState;

    public ProjectsPage() : base(AppText.Tabs.Projects, "general")
    {
        _projects = App.GetService<DemoProjectService>();

        _emptyState = ViewFactory.InfoCard(AppText.Projects.EmptyTitle, AppText.Projects.EmptyBody, "projects.png");

        var stack = ViewFactory.Stack(12);
        stack.Add(ViewFactory.InfoCard(AppText.Projects.StorageTitle, AppText.Projects.SessionNotice));
        var createButton = ViewFactory.PrimaryButton(AppText.Projects.NewProject, CreateProjectAsync);
        createButton.ImageSource = "add_on_primary.png";
        stack.Add(createButton);
        stack.Add(_emptyState);
        stack.Add(_projectList);

        Content = PageBody(ViewFactory.Scroll(stack));
        RefreshProjects();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _projects.ProjectsChanged -= OnProjectsChanged;
        _projects.ProjectsChanged += OnProjectsChanged;
        RefreshProjects();
    }

    protected override void OnDisappearing()
    {
        _projects.ProjectsChanged -= OnProjectsChanged;
        base.OnDisappearing();
    }

    private async void CreateProjectAsync(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(
            AppText.Projects.NewProject,
            AppText.Projects.ProjectNamePrompt,
            AppText.Confirm,
            AppText.Cancel,
            AppText.Projects.ProjectNamePlaceholder,
            60,
            Keyboard.Text);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _projects.Create(name, DemoProjectType.Other,
            "Kullanıcı tarafından oluşturulan proje kaydı.");
        SemanticScreenReader.Announce(AppText.Projects.CreatedForSession);
    }

    private void RefreshProjects()
    {
        var projects = _projects.GetAll();
        _projectList.Clear();
        _emptyState.IsVisible = projects.Count == 0;

        foreach (var project in projects)
        {
            _projectList.Add(CreateProjectCard(project));
        }
    }

    private Border CreateProjectCard(DemoProject project)
    {
        var content = ViewFactory.Stack(7);
        var heading = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        var badgeText = project.HasSavedMeasurementWorkspace ? AppText.SavedBadge : AppText.Demo;
        heading.Add(ViewFactory.StyledLabel(project.Name, "ToolTitleStyle", SemanticHeadingLevel.Level3), 0, 0);
        heading.Add(ViewFactory.StatusBadge(badgeText), 1, 0);
        content.Add(heading);
        content.Add(ViewFactory.Secondary(project.Type.DisplayName()));
        content.Add(ViewFactory.Body(project.Description));
        content.Add(ViewFactory.Caption($"{project.Measurements.Count} ölçü • {badgeText}"));

        var actions = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap };
        if (project.HasSavedMeasurementWorkspace)
        {
            AddAction(actions, AppText.Projects.OpenAndEdit, async (_, _) =>
                await Navigation.PushAsync(new PhotoMeasurePage(project, _projects.GetPhotoFilePath(project.Id))));
        }

        AddAction(actions, AppText.Projects.Details, async (_, _) =>
            await Shell.Current.GoToAsync($"{nameof(ProjectDetailPage)}?id={project.Id:D}"));
        AddAction(actions, AppText.Projects.Rename, async (_, _) => await RenameAsync(project));
        AddAction(actions, AppText.Projects.Delete, async (_, _) => await DeleteAsync(project), destructive: true);
        content.Add(actions);

        var card = ViewFactory.Card(content);
        SemanticProperties.SetDescription(card,
            $"{project.Name}. {project.Type.DisplayName()}. {badgeText}. {project.Measurements.Count} ölçü.");
        return card;
    }

    private static void AddAction(FlexLayout layout, string text, EventHandler handler, bool destructive = false)
    {
        var button = destructive
            ? ViewFactory.DestructiveButton(text, handler)
            : ViewFactory.SecondaryButton(text, handler);
        button.Margin = new Thickness(0, 0, 8, 8);
        layout.Add(button);
    }

    private async Task RenameAsync(DemoProject project)
    {
        var name = await DisplayPromptAsync(
            AppText.Projects.Rename,
            AppText.Projects.ProjectNamePrompt,
            AppText.Confirm,
            AppText.Cancel,
            AppText.Projects.ProjectNamePlaceholder,
            60,
            Keyboard.Text,
            project.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _projects.Rename(project.Id, name);
        }
    }

    private async Task DeleteAsync(DemoProject project)
    {
        var confirmed = await DisplayAlertAsync(
            AppText.Projects.DeleteTitle,
            $"{project.Name}\n\n{AppText.Projects.DeleteMessage}",
            AppText.Projects.Delete,
            AppText.Cancel);
        if (confirmed)
        {
            _projects.Delete(project.Id);
        }
    }

    private void OnProjectsChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RefreshProjects);
}
