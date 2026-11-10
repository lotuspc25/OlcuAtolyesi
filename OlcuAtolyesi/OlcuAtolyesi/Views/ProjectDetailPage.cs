using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;

namespace OlcuAtolyesi.Views;

public sealed class ProjectDetailPage : PrototypePage, IQueryAttributable
{
    private readonly DemoProjectService _projects;
    private readonly SettingsService _settings;
    private readonly VerticalStackLayout _content = ViewFactory.Stack(12);
    private Guid _projectId;

    public ProjectDetailPage() : base("Proje ayrıntısı", "general")
    {
        _projects = App.GetService<DemoProjectService>();
        _settings = App.GetService<SettingsService>();
        Content = PageBody(ViewFactory.Scroll(_content));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
        {
            _projectId = id;
            Render();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        _content.Clear();
        var project = _projects.GetById(_projectId);
        if (project is null)
        {
            _content.Add(ViewFactory.InfoCard(AppText.Projects.EmptyTitle, AppText.Projects.EmptyBody));
            return;
        }

        Title = project.Name;
        var badgeText = project.HasSavedMeasurementWorkspace ? AppText.SavedBadge : AppText.Demo;
        _content.Add(ViewFactory.PageTitle(project.Name));
        _content.Add(ViewFactory.StatusBadge(badgeText));
        _content.Add(ViewFactory.Secondary(project.Type.DisplayName()));
        _content.Add(ViewFactory.Body(project.Description));
        _content.Add(ViewFactory.Caption(AppText.Projects.SessionNotice));

        if (project.HasSavedMeasurementWorkspace)
        {
            _content.Add(ViewFactory.PrimaryButton(
                AppText.Projects.OpenAndEdit,
                async (_, _) => await Navigation.PushAsync(
                    new PhotoMeasurePage(project, _projects.GetPhotoFilePath(project.Id)))));
        }

        var projectActions = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap };
        AddAction(projectActions, AppText.Projects.Rename, async (_, _) => await RenameAsync(project));
        AddAction(projectActions, AppText.Projects.Delete, async (_, _) => await DeleteAsync(project), true);
        _content.Add(projectActions);

        if (project.ReferenceSegment is { } refSegment && project.ReferenceLength is { } refLength)
        {
            _content.Add(ViewFactory.SectionTitle(AppText.Projects.ReferenceInfo));
            var refStack = ViewFactory.Stack(4);
            refStack.Add(ViewFactory.StyledLabel(refLength.Format(), "ToolTitleStyle"));
            refStack.Add(ViewFactory.Caption(
                $"R1 ({refSegment.Start.X:0.#}; {refSegment.Start.Y:0.#}) — R2 ({refSegment.End.X:0.#}; {refSegment.End.Y:0.#})"));
            _content.Add(ViewFactory.Card(refStack));
        }

        _content.Add(ViewFactory.SectionTitle(AppText.Projects.Measurements));
        if (project.Measurements.Count == 0)
        {
            _content.Add(ViewFactory.Secondary("Bu projede henüz kayıtlı ölçü yok."));
        }
        else
        {
            var displayUnit = project.DisplayUnit ?? _settings.DefaultLengthUnit;
            foreach (var measurement in project.Measurements)
            {
                var value = measurement.Length is { } length
                    ? length.ConvertTo(displayUnit).Format()
                    : measurement.AngleDegrees is { } angle
                        ? $"{angle:0.0}°".Replace('.', ',')
                        : "—";
                var row = new Grid
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
                    ColumnSpacing = 12,
                    RowSpacing = 4
                };
                row.Add(ViewFactory.Body(measurement.Name), 0, 0);
                row.Add(ViewFactory.StyledLabel(value, "ToolTitleStyle"), 1, 0);
                if (measurement.ImageSegment is { } seg)
                {
                    var coordLabel = ViewFactory.Caption(
                        $"M1 ({seg.Start.X:0.#}; {seg.Start.Y:0.#}) — M2 ({seg.End.X:0.#}; {seg.End.Y:0.#})");
                    row.Add(coordLabel, 0, 1);
                    Grid.SetColumnSpan(coordLabel, 2);
                }

                _content.Add(ViewFactory.Card(row));
            }
        }

        _content.Add(ViewFactory.SectionTitle(AppText.Projects.ExportTitle));
        _content.Add(ViewFactory.Caption(AppText.Projects.ExportPlanned));
        var exports = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap };
        AddDisabled(exports, AppText.Projects.Dxf);
        AddDisabled(exports, AppText.Projects.Stl);
        AddDisabled(exports, AppText.Projects.OpenOnComputer);
        _content.Add(exports);
    }

    private static void AddAction(FlexLayout layout, string text, EventHandler handler, bool destructive = false)
    {
        var button = destructive
            ? ViewFactory.DestructiveButton(text, handler)
            : ViewFactory.SecondaryButton(text, handler);
        button.Margin = new Thickness(0, 0, 8, 8);
        layout.Add(button);
    }

    private static void AddDisabled(FlexLayout layout, string text)
    {
        var button = ViewFactory.SecondaryButton($"{text} • {AppText.Planned}", (_, _) => { });
        button.IsEnabled = false;
        button.Margin = new Thickness(0, 0, 8, 8);
        SemanticProperties.SetDescription(button, $"{text}. {AppText.Planned}. Bu sürümde kullanılamaz.");
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
            Render();
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
            await Shell.Current.GoToAsync("..");
        }
    }
}
