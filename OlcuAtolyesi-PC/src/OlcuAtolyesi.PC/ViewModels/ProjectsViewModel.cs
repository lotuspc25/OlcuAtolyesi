using System.Globalization;
using System.Windows.Input;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed record ConflictPolicyChoice(ProjectConflictPolicy Policy, string DisplayName);

public sealed class ProjectListItemViewModel
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public ProjectListItemViewModel(
        DesktopProject project,
        bool photoFileExists,
        Action<Guid> openInMeasurementAction,
        Action<Guid> selectAction,
        Action<Guid> deleteAction)
    {
        Project = project;
        PhotoFileExists = photoFileExists;
        OpenInMeasurementCommand = new RelayCommand(() => openInMeasurementAction(project.Id));
        SelectCommand = new RelayCommand(() => selectAction(project.Id));
        DeleteCommand = new RelayCommand(() => deleteAction(project.Id));
    }

    public DesktopProject Project { get; }

    public Guid Id => Project.Id;

    public string Name => Project.Name;

    public string Description => string.IsNullOrWhiteSpace(Project.Description)
        ? "Açıklama girilmedi."
        : Project.Description;

    public string TypeBadge => Project.IsSample
        ? $"{Project.TypeDisplayName} • Örnek"
        : $"{Project.TypeDisplayName} • {Project.SourcePlatform}";

    public string ResultSummary => $"M1–M2: {Project.FormattedResult}";

    public string ReferenceSummary => $"Referans (R1–R2): {Project.FormattedReference}";

    public string CoordinatesSummary =>
        $"R1({Project.ReferenceSegment.Start.X:0.#};{Project.ReferenceSegment.Start.Y:0.#})–R2({Project.ReferenceSegment.End.X:0.#};{Project.ReferenceSegment.End.Y:0.#})  |  " +
        $"M1({Project.MeasurementSegment.Start.X:0.#};{Project.MeasurementSegment.Start.Y:0.#})–M2({Project.MeasurementSegment.End.X:0.#};{Project.MeasurementSegment.End.Y:0.#})";

    public string PhotoStatusSummary =>
        Project.HasPhotoPath
            ? (PhotoFileExists
                ? $"Fotoğraf: {Project.OriginalPhotoFileName ?? "photo.jpg"} ({Project.PhotoDimensionsSummary})"
                : $"Uyarı: Fotoğraf dosyası eksik ({Project.PhotoDimensionsSummary})")
            : $"Çalışma Yüzeyi: {Project.PhotoDimensionsSummary}";

    public bool PhotoFileExists { get; }

    public bool HasMissingPhotoWarning => Project.HasPhotoPath && !PhotoFileExists;

    public string UpdatedAtText =>
        $"Son kayıt: {Project.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", TurkishCulture)}";

    public ICommand OpenInMeasurementCommand { get; }

    public ICommand SelectCommand { get; }

    public ICommand DeleteCommand { get; }
}

public sealed class ProjectsViewModel : PageViewModelBase
{
    private readonly DesktopProjectService _projectService;
    private readonly DesktopSettingsService _settings;
    private readonly IPhotoFileDialogService _dialogService;
    private readonly Action<Guid>? _openProjectInWorkspace;
    private IReadOnlyList<ProjectListItemViewModel> _projects = [];
    private ProjectListItemViewModel? _selectedProject;
    private string _newProjectNameInput = "Yeni Ölçüm Projesi";
    private string _newProjectDescriptionInput = string.Empty;
    private string _renameInput = string.Empty;
    private string _editDescriptionInput = string.Empty;
    private ProjectConflictPolicy _selectedConflictPolicy = ProjectConflictPolicy.ImportAsCopy;
    private string _statusMessage = string.Empty;
    private bool _hasError;

    public ProjectsViewModel(
        ModuleDescriptor descriptor,
        DesktopProjectService projectService,
        DesktopSettingsService settings,
        IPhotoFileDialogService? dialogService = null,
        Action<Guid>? openProjectInWorkspace = null)
        : base(descriptor)
    {
        _projectService = projectService;
        _settings = settings;
        _dialogService = dialogService ?? new WpfPhotoFileDialogService();
        _openProjectInWorkspace = openProjectInWorkspace;

        ConflictChoices = Enum.GetValues<ProjectConflictPolicy>()
            .Select(p => new ConflictPolicyChoice(p, p.DisplayName()))
            .ToList();

        CreateProjectCommand = new RelayCommand(() => CreateNewProject());
        OpenSelectedCommand = new RelayCommand(
            () =>
            {
                if (_selectedProject is not null)
                {
                    OpenProjectInMeasurement(_selectedProject.Id);
                }
            });
        RenameSelectedCommand = new RelayCommand(() => RenameSelectedProject());
        DeleteSelectedCommand = new RelayCommand(
            () =>
            {
                if (_selectedProject is not null)
                {
                    DeleteProject(_selectedProject.Id);
                }
            });
        ImportPackageCommand = new RelayCommand(ImportPackageWithDialog);
        ExportSelectedPackageCommand = new RelayCommand(ExportSelectedWithDialog);
        RefreshCommand = new RelayCommand(RefreshList);

        _projectService.ProjectsChanged += (_, _) => RefreshList();
        RefreshList();

        if (_projectService.RecoveredFromBackupCount > 0)
        {
            StatusMessage = $"{_projectService.RecoveredFromBackupCount} proje yedeğinden (.bak) kurtarıldı.";
        }
        else if (_projectService.CorruptedEntryCount > 0)
        {
            HasError = true;
            StatusMessage = $"Uyarı: {_projectService.CorruptedEntryCount} bozuk proje kaydı atlandı; sağlam projeler yüklendi.";
        }
        else
        {
            StatusMessage = $"Toplam {_projects.Count} kayıtlı proje hazır.";
        }
    }

    public DesktopProjectService ProjectService => _projectService;

    public IReadOnlyList<ConflictPolicyChoice> ConflictChoices { get; }

    public IReadOnlyList<ProjectListItemViewModel> Projects
    {
        get => _projects;
        private set
        {
            if (SetProperty(ref _projects, value))
            {
                OnPropertyChanged(nameof(ProjectCount));
                OnPropertyChanged(nameof(HasProjects));
            }
        }
    }

    public int ProjectCount => _projects.Count;

    public bool HasProjects => _projects.Count > 0;

    public ProjectListItemViewModel? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                if (value is not null)
                {
                    RenameInput = value.Name;
                    EditDescriptionInput = value.Project.Description;
                }
                else
                {
                    RenameInput = string.Empty;
                    EditDescriptionInput = string.Empty;
                }

                OnPropertyChanged(nameof(HasSelectedProject));
            }
        }
    }

    public bool HasSelectedProject => _selectedProject is not null;

    public string NewProjectNameInput
    {
        get => _newProjectNameInput;
        set => SetProperty(ref _newProjectNameInput, value);
    }

    public string NewProjectDescriptionInput
    {
        get => _newProjectDescriptionInput;
        set => SetProperty(ref _newProjectDescriptionInput, value);
    }

    public string RenameInput
    {
        get => _renameInput;
        set => SetProperty(ref _renameInput, value);
    }

    public string EditDescriptionInput
    {
        get => _editDescriptionInput;
        set => SetProperty(ref _editDescriptionInput, value);
    }

    public ProjectConflictPolicy SelectedConflictPolicy
    {
        get => _selectedConflictPolicy;
        set => SetProperty(ref _selectedConflictPolicy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string StorageDirectoryText => _projectService.StorageRoot ?? _settings.WorkspaceDirectory;

    public ICommand CreateProjectCommand { get; }

    public ICommand OpenSelectedCommand { get; }

    public ICommand RenameSelectedCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand ImportPackageCommand { get; }

    public ICommand ExportSelectedPackageCommand { get; }

    public ICommand RefreshCommand { get; }

    public void RefreshList()
    {
        var previousSelectedId = _selectedProject?.Id;
        var all = _projectService.GetAll();
        var items = all
            .Select(p => new ProjectListItemViewModel(
                p,
                photoFileExists: _projectService.GetPhotoFilePath(p.Id) is not null,
                openInMeasurementAction: OpenProjectInMeasurement,
                selectAction: SelectProjectById,
                deleteAction: id => DeleteProject(id)))
            .ToList();

        Projects = items;
        SelectedProject = items.FirstOrDefault(i => i.Id == previousSelectedId) ?? items.FirstOrDefault();
    }

    public void SelectProjectById(Guid id)
    {
        var target = _projects.FirstOrDefault(p => p.Id == id);
        if (target is not null)
        {
            SelectedProject = target;
        }
    }

    public DesktopProject? CreateNewProject()
    {
        if (string.IsNullOrWhiteSpace(_newProjectNameInput))
        {
            HasError = true;
            StatusMessage = "Yeni proje adı boş olamaz.";
            return null;
        }

        try
        {
            var created = _projectService.Create(
                _newProjectNameInput.Trim(),
                ProjectType.PhotoMeasurement,
                _newProjectDescriptionInput,
                _settings.DefaultUnit);

            HasError = false;
            NewProjectDescriptionInput = string.Empty;
            SelectProjectById(created.Id);
            StatusMessage = $"\"{created.Name}\" projesi oluşturuldu.";
            return created;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Proje oluşturulamadı: {ex.Message}";
            return null;
        }
    }

    public bool RenameSelectedProject()
    {
        if (_selectedProject is null)
        {
            HasError = true;
            StatusMessage = "Yeniden adlandırmak için önce bir proje seçin.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_renameInput))
        {
            HasError = true;
            StatusMessage = "Proje adı boş olamaz.";
            return false;
        }

        try
        {
            var targetId = _selectedProject.Id;
            var updated = _projectService.Rename(targetId, _renameInput.Trim(), _editDescriptionInput);
            HasError = false;
            SelectProjectById(updated.Id);
            StatusMessage = $"Proje adı \"{updated.Name}\" olarak güncellendi.";
            return true;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Proje güncellenemedi: {ex.Message}";
            return false;
        }
    }

    public bool DeleteProject(Guid id)
    {
        var existing = _projectService.GetById(id);
        if (existing is null)
        {
            HasError = true;
            StatusMessage = "Silinecek proje bulunamadı.";
            return false;
        }

        var removed = _projectService.Delete(id);
        if (removed)
        {
            HasError = false;
            StatusMessage = $"\"{existing.Name}\" projesi silindi.";
        }
        else
        {
            HasError = true;
            StatusMessage = "Proje silinemedi.";
        }

        return removed;
    }

    public void OpenProjectInMeasurement(Guid id)
    {
        var project = _projectService.GetById(id);
        if (project is null)
        {
            HasError = true;
            StatusMessage = "Açılacak proje bulunamadı.";
            return;
        }

        SelectProjectById(id);
        HasError = false;
        StatusMessage = $"\"{project.Name}\" projesi Fotoğraftan Ölçüm çalışma alanında açıldı.";
        _openProjectInWorkspace?.Invoke(id);
    }

    public ProjectImportResult ImportPackageFromPath(string? path)
    {
        var result = _projectService.ImportProjectPackage(path, _selectedConflictPolicy);
        HasError = !result.Success || result.PhotoMissingWarning;
        StatusMessage = result.Message;
        if (result.Success && result.Project is not null)
        {
            SelectProjectById(result.Project.Id);
        }

        return result;
    }

    public ProjectExportResult ExportSelectedToZip(string? targetZipPath)
    {
        if (_selectedProject is null)
        {
            HasError = true;
            StatusMessage = "Dışa aktarmak için önce bir proje seçin.";
            return new ProjectExportResult(false, null, StatusMessage);
        }

        var result = _projectService.ExportProjectPackageZip(_selectedProject.Id, targetZipPath);
        HasError = !result.Success;
        StatusMessage = result.Message;
        return result;
    }

    private void ImportPackageWithDialog()
    {
        var path = _dialogService.PickProjectPackageToImport();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ImportPackageFromPath(path);
        }
    }

    private void ExportSelectedWithDialog()
    {
        if (_selectedProject is null)
        {
            HasError = true;
            StatusMessage = "Dışa aktarmak için önce bir proje seçin.";
            return;
        }

        var targetZip = _dialogService.PickProjectPackageToExport(_selectedProject.Name);
        if (!string.IsNullOrWhiteSpace(targetZip))
        {
            ExportSelectedToZip(targetZip);
        }
    }
}
