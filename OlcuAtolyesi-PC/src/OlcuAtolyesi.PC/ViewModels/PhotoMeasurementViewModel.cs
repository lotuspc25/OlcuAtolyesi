using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OlcuAtolyesi.Core.Export;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Measurement;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC.Controls;
using OlcuAtolyesi.PC.Media;
using OlcuAtolyesi.PC.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed record UnitChoice(LengthUnit Unit, string DisplayName);

public sealed class PhotoMeasurementViewModel : PageViewModelBase
{
    private const string DefaultReferenceText = "120";
    private const string DefaultProjectName = "Yeni Ölçüm Projesi";
    private const string DemoInfoText = "Fotoğraf açılmadı — demo çalışma alanı gösteriliyor. Gerçek ölçüm için Fotoğraf Aç'ı kullanın.";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IPhotoFileDialogService _dialog;
    private readonly DesktopProjectService _projectService;
    private readonly DesktopSettingsService _settings;
    private readonly DxfExportService _dxfExportService = new();
    private string _dxfExportFeedback = string.Empty;
    private MeasurementSession _session;
    private string _referenceInput = DefaultReferenceText;
    private string? _referenceError;
    private LengthUnit _selectedUnit;
    private string _statusMessage = string.Empty;
    private bool _hasValidReference;
    private BitmapSource? _photoImage;
    private string _photoInfoText = DemoInfoText;
    private string? _photoError;
    private string? _currentPhotoPath;
    private string? _currentPhotoFileName;
    private Guid? _activeProjectId;
    private string _projectNameInput = DefaultProjectName;
    private string _projectDescriptionInput = string.Empty;
    private string? _projectSaveFeedback;
    private int _fitRequest;
    private bool _isMagnifierEnabled = true;
    private double _magnifierZoom = 4d;
    private BitmapSource? _originalPhotoBitmap;
    private readonly ImageAdjustmentViewModel _imageAdjustment;

    public ImageAdjustmentViewModel ImageAdjustment => _imageAdjustment;

    public PhotoMeasurementViewModel(
        ModuleDescriptor descriptor,
        DesktopSettingsService settings,
        IPhotoFileDialogService? dialogService = null,
        DesktopProjectService? projectService = null)
        : base(descriptor)
    {
        _dialog = dialogService ?? new WpfPhotoFileDialogService();
        _projectService = projectService ?? new DesktopProjectService(settings.WorkspaceDirectory, seedSamples: false);
        _settings = settings;
        _selectedUnit = settings.DefaultUnit;
        UnitChoices = Enum.GetValues<LengthUnit>()
            .Select(u => new UnitChoice(u, u.DisplayName()))
            .ToList();

        _session = new MeasurementSession(MeasurementSession.DemoImageSize, _selectedUnit, useDemoDefaults: true);
        _session.Changed += OnSessionChanged;

        OpenPhotoCommand = new RelayCommand(OpenPhoto);
        ApplyReferenceCommand = new RelayCommand(ApplyReferenceText);
        ReferenceToolCommand = new RelayCommand(() => Session.ArmReference());
        MeasureToolCommand = new RelayCommand(() => Session.ArmMeasure());
        CancelToolCommand = new RelayCommand(() => Session.CancelArm());
        UndoCommand = new RelayCommand(() => Session.Undo());
        ResetCommand = new RelayCommand(() => Session.Reset());
        FitViewCommand = new RelayCommand(() => FitRequest++);
        RecalculateCommand = new RelayCommand(ApplyReferenceText);
        ResetDemoCoordinatesCommand = new RelayCommand(ResetToDefaults);
        SaveProjectCommand = new RelayCommand(() => SaveCurrentProject());
        SaveAsNewProjectCommand = new RelayCommand(() => SaveAsNewProject());
        ExportDxfCommand = new RelayCommand(ExportDxf);
        NewWorkspaceCommand = new RelayCommand(StartNewWorkspace);
        ToggleMagnifierCommand = new RelayCommand(() => IsMagnifierEnabled = !IsMagnifierEnabled);
        CycleMagnifierZoomCommand = new RelayCommand(() =>
        {
            _magnifierZoom = MeasurementCanvas.NextMagnifierZoom(_magnifierZoom);
            OnPropertyChanged(nameof(MagnifierZoom));
            OnPropertyChanged(nameof(MagnifierZoomText));
        });
        MagnifierZoom2Command = new RelayCommand(() => SetMagnifierZoom(2d));
        MagnifierZoom4Command = new RelayCommand(() => SetMagnifierZoom(4d));
        MagnifierZoom8Command = new RelayCommand(() => SetMagnifierZoom(8d));
        MagnifierZoomInCommand = new RelayCommand(() => SetMagnifierZoom(NextZoomUp(_magnifierZoom)));
        MagnifierZoomOutCommand = new RelayCommand(() => SetMagnifierZoom(NextZoomDown(_magnifierZoom)));

        _imageAdjustment = new ImageAdjustmentViewModel();
        _imageAdjustment.Changed += (_, _) => RefreshDisplayImage();

        settings.PreferencesChanged += (_, prefs) => SelectedUnit = prefs.DefaultUnit;

        ApplyReferenceText();
    }

    public DesktopProjectService ProjectService => _projectService;

    public ICommand ApplyReferenceCommand { get; }

    public IReadOnlyList<UnitChoice> UnitChoices { get; }

    public IReadOnlyList<UnitChoice> Units => UnitChoices;

    public IReadOnlyList<ReferenceObject> ReferenceObjects => ReferenceObjectLibrary.All;

    private ReferenceObject _selectedReferenceObject = ReferenceObjectLibrary.All[0];
    public ReferenceObject SelectedReferenceObject
    {
        get => _selectedReferenceObject;
        set
        {
            if (SetProperty(ref _selectedReferenceObject, value) && value is not null &&
                !string.Equals(value.Name, ReferenceObjectLibrary.ManualName, StringComparison.Ordinal))
            {
                ReferenceInput = value.Millimetres.ToString("0.##", TurkishCulture);
            }
        }
    }

    private bool _showRuler = true;
    public bool ShowRuler
    {
        get => _showRuler;
        set => SetProperty(ref _showRuler, value);
    }

    public double ScreenMillimetresPerDip => _settings.GetScreenCalibration().MillimetresPerDip;

    public bool HasReferenceWarning =>
        Session.KnownLength is not { IsPositive: true };

    public string ReferenceWarningText =>
        "Referans uzunluğu girilmedi. Cetvel şu an PİKSEL gösteriyor. Gerçek ölçü (mm) için " +
        "referans nesnesini fotoğrafta işaretleyip uzunluğunu girin.";

    public string BottomInfoText
    {
        get
        {
            var parts = new List<string>();

            if (Session.KnownLength is { IsPositive: true } known &&
                Session.ReferenceSegment.ImageLength > 0.0001d)
            {
                var mmPerPixel = known.Millimeters / Session.ReferenceSegment.ImageLength;
                parts.Add($"1 piksel = {mmPerPixel.ToString("0.####", TurkishCulture)} mm");
                parts.Add($"Referans: {known.Format(TurkishCulture)} ({_selectedReferenceObject.Name})");
            }
            else
            {
                parts.Add("1 piksel = ? mm (referans bekleniyor)");
            }

            parts.Add($"Sonuç: {(Session.Result is { } result ? result.Format(TurkishCulture) : "—")}");
            parts.Add($"{Session.ImageSize.Width:0} × {Session.ImageSize.Height:0} px");

            return string.Join("  ·  ", parts);
        }
    }

    public void RefreshScreenCalibration() => OnPropertyChanged(nameof(ScreenMillimetresPerDip));

    public string ReferenceLengthInput
    {
        get => ReferenceInput;
        set => ReferenceInput = value;
    }

    public ICommand PickPhotoCommand => OpenPhotoCommand;

    public ICommand ResetZoomCommand => FitViewCommand;

    public string ResolutionSummary => PhotoInfoText;

    public string LoupeStatusText =>
        IsMagnifierEnabled ? $"Büyüteç: Açık ({MagnifierZoom:0}×)" : "Büyüteç: Kapalı";

    public IReadOnlyList<string> CurrentCapabilities => Descriptor.CurrentCapabilities;

    public IReadOnlyList<string> PlannedCapabilities => Descriptor.PlannedCapabilities;

    /// <summary>Aktif düzenlenmekte olan projenin kimliği; yeni/kaydedilmemiş oturumda null olur.</summary>
    public Guid? ActiveProjectId
    {
        get => _activeProjectId;
        private set
        {
            if (SetProperty(ref _activeProjectId, value))
            {
                OnPropertyChanged(nameof(HasActiveProject));
                OnPropertyChanged(nameof(ActiveProjectBadgeText));
            }
        }
    }

    public bool HasActiveProject => _activeProjectId is not null;

    public string ActiveProjectBadgeText =>
        _activeProjectId is not null
            ? $"Aktif Proje: {ProjectNameInput}"
            : "Kaydedilmemiş Çalışma";

    public string ProjectNameInput
    {
        get => _projectNameInput;
        set
        {
            if (SetProperty(ref _projectNameInput, value))
            {
                OnPropertyChanged(nameof(ActiveProjectBadgeText));
            }
        }
    }

    public string ProjectDescriptionInput
    {
        get => _projectDescriptionInput;
        set => SetProperty(ref _projectDescriptionInput, value);
    }

    public string? ProjectSaveFeedback
    {
        get => _projectSaveFeedback;
        private set
        {
            if (SetProperty(ref _projectSaveFeedback, value))
            {
                OnPropertyChanged(nameof(HasProjectSaveFeedback));
            }
        }
    }

    public bool HasProjectSaveFeedback => !string.IsNullOrWhiteSpace(_projectSaveFeedback);

    public string DxfExportFeedback
    {
        get => _dxfExportFeedback;
        private set
        {
            if (SetProperty(ref _dxfExportFeedback, value))
            {
                OnPropertyChanged(nameof(HasDxfExportFeedback));
            }
        }
    }

    public bool HasDxfExportFeedback => !string.IsNullOrWhiteSpace(_dxfExportFeedback);

    /// <summary>Fotoğraf piksel koordinatlarında R1/R2/M1/M2 durumunu tutan oturum.</summary>
    public MeasurementSession Session
    {
        get => _session;
        private set
        {
            if (ReferenceEquals(_session, value))
            {
                return;
            }

            _session.Changed -= OnSessionChanged;
            _session = value;
            _session.Changed += OnSessionChanged;
            OnPropertyChanged();
        }
    }

    public BitmapSource? PhotoImage
    {
        get => _photoImage;
        private set
        {
            if (SetProperty(ref _photoImage, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
            }
        }
    }

    public bool HasPhoto => _photoImage is not null;

    public string PhotoInfoText
    {
        get => _photoInfoText;
        private set
        {
            if (SetProperty(ref _photoInfoText, value))
            {
                OnPropertyChanged(nameof(PhotoDimensionsSummary));
            }
        }
    }

    public string PhotoDimensionsSummary => PhotoInfoText;

    public string? PhotoError
    {
        get => _photoError;
        private set
        {
            if (SetProperty(ref _photoError, value))
            {
                OnPropertyChanged(nameof(HasPhotoError));
            }
        }
    }

    public bool HasPhotoError => !string.IsNullOrEmpty(_photoError);

    /// <summary>Her artışta çalışma alanı görünümü fotoğrafa sığdırılır.</summary>
    public int FitRequest
    {
        get => _fitRequest;
        private set => SetProperty(ref _fitRequest, value);
    }

    public string ReferenceInput
    {
        get => _referenceInput;
        set
        {
            if (SetProperty(ref _referenceInput, value))
            {
                ApplyReferenceText();
            }
        }
    }

    public LengthUnit SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            var previous = _selectedUnit;
            if (!SetProperty(ref _selectedUnit, value))
            {
                return;
            }

            var previousParse = ReferenceLengthParser.ParsePositive(_referenceInput, previous);
            if (previousParse.IsValid && previousParse.Length is { } previousLength)
            {
                _referenceInput = previousLength.ConvertTo(value).Value.ToString("0.######", TurkishCulture);
                OnPropertyChanged(nameof(ReferenceInput));
            }

            _session.Unit = value;
            ApplyReferenceText();
        }
    }

    public double R1X
    {
        get => _session.R1.X;
        set => _session.MovePoint(MeasurePoint.R1, new ImagePoint(value, _session.R1.Y));
    }

    public double R1Y
    {
        get => _session.R1.Y;
        set => _session.MovePoint(MeasurePoint.R1, new ImagePoint(_session.R1.X, value));
    }

    public double R2X
    {
        get => _session.R2.X;
        set => _session.MovePoint(MeasurePoint.R2, new ImagePoint(value, _session.R2.Y));
    }

    public double R2Y
    {
        get => _session.R2.Y;
        set => _session.MovePoint(MeasurePoint.R2, new ImagePoint(_session.R2.X, value));
    }

    public double M1X
    {
        get => _session.M1.X;
        set => _session.MovePoint(MeasurePoint.M1, new ImagePoint(value, _session.M1.Y));
    }

    public double M1Y
    {
        get => _session.M1.Y;
        set => _session.MovePoint(MeasurePoint.M1, new ImagePoint(_session.M1.X, value));
    }

    public double M2X
    {
        get => _session.M2.X;
        set => _session.MovePoint(MeasurePoint.M2, new ImagePoint(value, _session.M2.Y));
    }

    public double M2Y
    {
        get => _session.M2.Y;
        set => _session.MovePoint(MeasurePoint.M2, new ImagePoint(_session.M2.X, value));
    }

    public bool HasValidReference
    {
        get => _hasValidReference;
        private set => SetProperty(ref _hasValidReference, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public LengthValue? CalculatedResult => _session.Result;

    public string CalculatedResultText => CalculatedResult?.Format(TurkishCulture) ?? "—";

    public bool CanUndo => _session.CanUndo;

    public string ToolHint => ToolStateAdvisor.GetHint(
        Session.ArmedPoint,
        hasKnownLength: Session.KnownLength is { IsPositive: true },
        hasReferenceLength: Session.ReferenceScale is not null);

    public bool IsReferenceToolActive => Session.ArmedPoint is MeasurePoint.R1 or MeasurePoint.R2;

    public bool IsMeasureToolActive => Session.ArmedPoint is MeasurePoint.M1 or MeasurePoint.M2;

    public string ReferencePixelSummary =>
        $"R1({_session.R1.X:0.#}; {_session.R1.Y:0.#}) – R2({_session.R2.X:0.#}; {_session.R2.Y:0.#}) = {_session.ReferenceSegment.ImageLength.ToString("0.0", TurkishCulture)} piksel";

    public string MeasurementPixelSummary =>
        $"M1({_session.M1.X:0.#}; {_session.M1.Y:0.#}) – M2({_session.M2.X:0.#}; {_session.M2.Y:0.#}) = {_session.Measurement.ImageLength.ToString("0.0", TurkishCulture)} piksel";

    public ICommand OpenPhotoCommand { get; }

    public ICommand ReferenceToolCommand { get; }

    public ICommand MeasureToolCommand { get; }

    public ICommand CancelToolCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand FitViewCommand { get; }

    public ICommand RecalculateCommand { get; }

    public ICommand ResetDemoCoordinatesCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand SaveAsNewProjectCommand { get; }

    public ICommand ExportDxfCommand { get; }

    public ICommand NewWorkspaceCommand { get; }

    public ICommand ToggleMagnifierCommand { get; }

    public ICommand CycleMagnifierZoomCommand { get; }

    public ICommand MagnifierZoom2Command { get; }

    public ICommand MagnifierZoom4Command { get; }

    public ICommand MagnifierZoom8Command { get; }

    public ICommand MagnifierZoomInCommand { get; }

    public ICommand MagnifierZoomOutCommand { get; }

    private void SetMagnifierZoom(double zoom)
    {
        _magnifierZoom = Math.Clamp(zoom, 2d, 8d);
        OnPropertyChanged(nameof(MagnifierZoom));
        OnPropertyChanged(nameof(MagnifierZoomText));
    }

    private static double NextZoomUp(double current) => current < 3d ? 4d : current < 6d ? 8d : 8d;

    private static double NextZoomDown(double current) => current > 6d ? 4d : current > 3d ? 2d : 2d;

    public bool IsMagnifierEnabled
    {
        get => _isMagnifierEnabled;
        set
        {
            if (SetProperty(ref _isMagnifierEnabled, value))
            {
                OnPropertyChanged(nameof(MagnifierButtonText));
            }
        }
    }

    public double MagnifierZoom
    {
        get => _magnifierZoom;
        set
        {
            if (SetProperty(ref _magnifierZoom, value))
            {
                OnPropertyChanged(nameof(MagnifierZoomText));
            }
        }
    }

    public string MagnifierButtonText => IsMagnifierEnabled ? "Büyüteç: Açık" : "Büyüteç: Kapalı";

    public string MagnifierZoomText => $"{MagnifierZoom:0}×";

    private void RefreshDisplayImage()
    {
        if (_originalPhotoBitmap is null)
        {
            PhotoImage = null;
            return;
        }

        try
        {
            var transformed = _imageAdjustment.ApplyGeometry(_originalPhotoBitmap);
            var filtered = PhotoFilterPipeline.Apply(transformed, _imageAdjustment.FilterSettings);
            PhotoImage = filtered;
        }
        catch
        {
            // Filtre hatası fotoğrafı kaybettirmesin
            PhotoImage = _originalPhotoBitmap;
        }
    }

    /// <summary>
    /// Bir fotoğrafı yükler. Başarısız olursa mevcut fotoğraf ve ölçümler korunur, hata <see cref="PhotoError"/> içinde bildirilir.
    /// </summary>
    public bool LoadPhotoFromPath(string? path)
    {
        var result = PhotoLoader.Load(path);
        if (!result.Success || result.Image is null)
        {
            PhotoError = result.ErrorMessage ?? "Fotoğraf yüklenemedi.";
            return false;
        }

        PhotoError = null;
        _originalPhotoBitmap = result.Image;
        RefreshDisplayImage();
        _currentPhotoPath = path;
        _currentPhotoFileName = result.FileName;
        ActiveProjectId = null;
        ProjectDescriptionInput = string.Empty;
        Session = new MeasurementSession(result.OriginalSize, _selectedUnit);
        PhotoInfoText = $"{result.FileName} — {result.OriginalSize.Width:0} × {result.OriginalSize.Height:0} piksel";

        if (!string.IsNullOrWhiteSpace(result.FileName))
        {
            ProjectNameInput = Path.GetFileNameWithoutExtension(result.FileName);
        }

        ApplyReferenceText();
        FitRequest++;
        return true;
    }

    /// <summary>
    /// Canlı kameradan yakalanan bir fotoğrafı yükler ve yeni bir ölçüm oturumu başlatır.
    /// Başarısız olursa mevcut fotoğraf, ölçümler ve aktif proje korunur; hata <see cref="PhotoError"/> içinde bildirilir.
    /// </summary>
    public bool LoadCapturedPhoto(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            PhotoError = "Kamera karesi dosya yolu boş.";
            return false;
        }

        var result = PhotoLoader.Load(path);
        if (!result.Success || result.Image is null)
        {
            PhotoError = result.ErrorMessage ?? "Kamera karesi yüklenemedi.";
            return false;
        }

        PhotoError = null;
        _originalPhotoBitmap = result.Image;
        RefreshDisplayImage();
        _currentPhotoPath = path;
        _currentPhotoFileName = result.FileName;

        // Önceki projeyi yeni kamera karesine bağlama; yeni bağımsız oturum başlat
        ActiveProjectId = null;
        ProjectDescriptionInput = string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(result.FileName);
        ProjectNameInput = string.IsNullOrWhiteSpace(baseName) ? DefaultProjectName : baseName;

        // Yeni ölçüm oturumu oluştur: R1-R2 ve M1-M2 eski fotoğraftan taşınmaz
        Session = new MeasurementSession(result.OriginalSize, _selectedUnit);
        var isHighRes = result.OriginalSize.Width > 1920 || result.OriginalSize.Height > 1080;
        PhotoInfoText = isHighRes
            ? $"{result.FileName} — {result.OriginalSize.Width:0} × {result.OriginalSize.Height:0} piksel (Yüksek Çözünürlüklü Fotoğraf)"
            : $"{result.FileName} — {result.OriginalSize.Width:0} × {result.OriginalSize.Height:0} piksel (Kamera Karesi)";

        // Kullanıcıya yeni fotoğraf için referansı belirlemesi gerektiğini açıkça bildir
        _referenceInput = string.Empty;
        OnPropertyChanged(nameof(ReferenceInput));
        ApplyReferenceText();
        HasValidReference = false;
        StatusMessage = isHighRes
            ? "Yüksek çözünürlüklü fotoğraf aktarıldı. Ölçüm için lütfen referans noktalarını (R1 ve R2) belirleyin ve kalibrasyon yapın."
            : "Yeni kamera karesi aktarıldı. Lütfen referans noktalarını (R1 ve R2) referans nesnesi üzerine yerleştirin.";
        ProjectSaveFeedback = isHighRes
            ? "Yüksek çözünürlüklü fotoğraf aktarıldı. Yeni kalibrasyon gereklidir."
            : "Kamera karesi başarıyla aktarıldı. Yeni ölçüm oturumu başlatıldı.";

        FitRequest++;
        return true;
    }

    /// <summary>
    /// Telefondan kayıpsız aktarılan yüksek çözünürlüklü ölçüm fotoğrafını yükler,
    /// eski referansı geçersiz kılar ve yeni kalibrasyon oturumu başlatır.
    /// </summary>
    public bool LoadHighResPhoto(string? path)
    {
        return LoadCapturedPhoto(path);
    }

    /// <summary>
    /// Aktif bir proje varsa onun üzerine kaydeder; yoksa yeni proje olarak kaydeder.
    /// </summary>
    public DesktopProject? SaveCurrentProject()
    {
        var nameToUse = string.IsNullOrWhiteSpace(_projectNameInput)
            ? DefaultProjectName
            : _projectNameInput.Trim();

        try
        {
            DesktopProject saved;
            if (_activeProjectId is { } existingId && _projectService.GetById(existingId) is not null)
            {
                saved = _projectService.UpdatePhotoMeasurementProject(
                    existingId,
                    nameToUse,
                    _projectDescriptionInput,
                    _currentPhotoPath,
                    _session.ImageSize,
                    _session.ReferenceSegment,
                    _session.KnownLength,
                    _session.Measurement,
                    _session.Result,
                    _selectedUnit,
                    _currentPhotoFileName);
                ProjectSaveFeedback = $"\"{saved.Name}\" projesi güncellendi ({saved.FormattedResult}).";
            }
            else
            {
                saved = _projectService.SavePhotoMeasurementProject(
                    nameToUse,
                    _projectDescriptionInput,
                    _currentPhotoPath,
                    _session.ImageSize,
                    _session.ReferenceSegment,
                    _session.KnownLength,
                    _session.Measurement,
                    _session.Result,
                    _selectedUnit,
                    _currentPhotoFileName);
                ProjectSaveFeedback = $"\"{saved.Name}\" yeni proje olarak kaydedildi ({saved.FormattedResult}).";
            }

            ActiveProjectId = saved.Id;
            ProjectNameInput = saved.Name;
            _currentPhotoPath = _projectService.GetPhotoFilePath(saved.Id) ?? _currentPhotoPath;
            return saved;
        }
        catch (Exception ex)
        {
            ProjectSaveFeedback = $"Proje kaydedilemedi: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Mevcut fotoğraf ve ölçüm oturumunu her zaman yeni bir proje kimliğiyle kaydeder ("Farklı Kaydet").
    /// </summary>
    public DesktopProject? SaveAsNewProject(string? customName = null)
    {
        var baseName = !string.IsNullOrWhiteSpace(customName)
            ? customName.Trim()
            : (!string.IsNullOrWhiteSpace(_projectNameInput) ? _projectNameInput.Trim() : DefaultProjectName);

        if (_activeProjectId is not null &&
            string.IsNullOrWhiteSpace(customName) &&
            _projectService.GetById(_activeProjectId.Value) is { } current &&
            string.Equals(current.Name, baseName, StringComparison.CurrentCultureIgnoreCase))
        {
            baseName = $"{baseName} (Kopya)";
        }

        try
        {
            var saved = _projectService.SavePhotoMeasurementProject(
                baseName,
                _projectDescriptionInput,
                _currentPhotoPath,
                _session.ImageSize,
                _session.ReferenceSegment,
                _session.KnownLength,
                _session.Measurement,
                _session.Result,
                _selectedUnit,
                _currentPhotoFileName);

            ActiveProjectId = saved.Id;
            ProjectNameInput = saved.Name;
            _currentPhotoPath = _projectService.GetPhotoFilePath(saved.Id) ?? _currentPhotoPath;
            ProjectSaveFeedback = $"\"{saved.Name}\" farklı proje olarak kaydedildi ({saved.FormattedResult}).";
            return saved;
        }
        catch (Exception ex)
        {
            ProjectSaveFeedback = $"Farklı kaydetme başarısız: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Kaydedilmiş bir projeyi (fotoğrafı, R1/R2 referansı, bilinen gerçek uzunluğu, birimi ve M1/M2 ölçüm noktalarıyla)
    /// çalışma alanına kayıpsız olarak geri yükler.
    /// </summary>
    public bool LoadProject(Guid projectId)
    {
        var project = _projectService.GetById(projectId);
        if (project is null)
        {
            PhotoError = "Açılmak istenen proje kaydı bulunamadı.";
            return false;
        }

        ActiveProjectId = project.Id;
        ProjectNameInput = project.Name;
        ProjectDescriptionInput = project.Description;

        _selectedUnit = project.DisplayUnit;
        OnPropertyChanged(nameof(SelectedUnit));

        var photoPath = _projectService.GetPhotoFilePath(project.Id);
        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            var loadResult = PhotoLoader.Load(photoPath);
            if (loadResult.Success && loadResult.Image is not null)
            {
                PhotoError = null;
                _originalPhotoBitmap = loadResult.Image;
                RefreshDisplayImage();
                _currentPhotoPath = photoPath;
                _currentPhotoFileName = project.OriginalPhotoFileName ?? loadResult.FileName;
                PhotoInfoText = $"{_currentPhotoFileName} — {project.PhotoSize.Width:0} × {project.PhotoSize.Height:0} piksel";
            }
            else
            {
                _originalPhotoBitmap = null;
                PhotoImage = null;
                _currentPhotoPath = null;
                _currentPhotoFileName = project.OriginalPhotoFileName;
                PhotoError = "Uyarı: Projeye ait fotoğraf dosyası bozuk veya okunamadı; ölçüm koordinatları yüklendi.";
                PhotoInfoText = $"Fotoğraf okunamadı — {project.PhotoSize.Width:0} × {project.PhotoSize.Height:0} piksel (Kayıtlı koordinatlar)";
            }
        }
        else if (project.HasPhotoPath)
        {
            _originalPhotoBitmap = null;
            PhotoImage = null;
            _currentPhotoPath = null;
            _currentPhotoFileName = project.OriginalPhotoFileName;
            PhotoError = "Uyarı: Projeye ait fotoğraf dosyası diskte bulunamadı; ölçüm koordinatları yüklendi.";
            PhotoInfoText = $"Eksik fotoğraf — {project.PhotoSize.Width:0} × {project.PhotoSize.Height:0} piksel (Kayıtlı koordinatlar)";
        }
        else
        {
            PhotoError = null;
            _originalPhotoBitmap = null;
            PhotoImage = null;
            _currentPhotoPath = null;
            _currentPhotoFileName = null;
            PhotoInfoText = $"{project.Name} — {project.PhotoSize.Width:0} × {project.PhotoSize.Height:0} piksel";
        }

        var restoredSession = new MeasurementSession(project.PhotoSize, project.DisplayUnit);
        restoredSession.RestoreSegments(project.ReferenceSegment, project.MeasurementSegment, clearHistory: true);

        if (project.ReferenceLength is { IsPositive: true } refLen)
        {
            var convertedRef = refLen.ConvertTo(project.DisplayUnit);
            _referenceInput = convertedRef.Value.ToString("0.######", TurkishCulture);
            OnPropertyChanged(nameof(ReferenceInput));
            restoredSession.SetKnownLength(convertedRef);
            _referenceError = null;
        }
        else
        {
            _referenceInput = string.Empty;
            OnPropertyChanged(nameof(ReferenceInput));
            restoredSession.SetKnownLength(null);
            _referenceError = "Geçerli bir referans uzunluğu girin.";
        }

        Session = restoredSession;
        RefreshOutputs();
        FitRequest++;
        ProjectSaveFeedback = $"\"{project.Name}\" projesi yüklendi.";
        return true;
    }

    public void StartNewWorkspace()
    {
        ActiveProjectId = null;
        ProjectNameInput = DefaultProjectName;
        ProjectDescriptionInput = string.Empty;
        _originalPhotoBitmap = null;
        PhotoImage = null;
        PhotoError = null;
        _currentPhotoPath = null;
        _currentPhotoFileName = null;
        _imageAdjustment.ResetAll();
        PhotoInfoText = DemoInfoText;
        Session = new MeasurementSession(MeasurementSession.DemoImageSize, _selectedUnit, useDemoDefaults: true);
        _referenceInput = DefaultReferenceText;
        OnPropertyChanged(nameof(ReferenceInput));
        ApplyReferenceText();
        FitRequest++;
        ProjectSaveFeedback = "Yeni ölçüm çalışma alanı başlatıldı.";
    }

    public void ResetToDefaults()
    {
        _session.Reset();
        _referenceInput = DefaultReferenceText;
        OnPropertyChanged(nameof(ReferenceInput));
        ApplyReferenceText();
    }

    private void ExportDxf()
    {
        try
        {
            // 1. Önce kayıtlı proje varsa ondan, yoksa güncel oturumdan veri topla
            var project = _activeProjectId is { } existingId ? _projectService.GetById(existingId) : null;
            DxfExportResult result;

            if (project is not null)
            {
                result = _dxfExportService.BuildFromProject(project);
            }
            else
            {
                if (Session.KnownLength is not { IsPositive: true } knownLength)
                {
                    DxfExportFeedback = "DXF için önce R1–R2 referans uzunluğunu girin.";
                    return;
                }

                var request = new DxfExportRequest(
                    string.IsNullOrWhiteSpace(ProjectNameInput) ? "Olcu Atolyesi Olcumu" : ProjectNameInput,
                    Session.ImageSize,
                    Session.ReferenceSegment,
                    knownLength,
                    Session.Measurement,
                    Session.Result);

                result = DxfBuilder.Build(request);
            }

            if (!result.Success || result.Content is null)
            {
                DxfExportFeedback = result.Message;
                return;
            }

            var suggestedName = (!string.IsNullOrWhiteSpace(ProjectNameInput) ? ProjectNameInput : "Olcu_Atolyesi") + ".dxf";
            var targetPath = _dialog.PickDxfExportPath(suggestedName);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                DxfExportFeedback = "DXF dışa aktarma iptal edildi.";
                return;
            }

            _dxfExportService.SaveToFile(targetPath, result.Content);
            DxfExportFeedback = $"DXF kaydedildi: {targetPath}";
        }
        catch (Exception ex)
        {
            DxfExportFeedback = $"DXF dışa aktarma hatası: {ex.Message}";
        }
    }

    private void OpenPhoto()
    {
        var path = _dialog.PickPhotoFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            LoadPhotoFromPath(path);
        }
    }

    private void ApplyReferenceText()
    {
        var parse = ReferenceLengthParser.ParsePositive(_referenceInput, _selectedUnit);
        _referenceError = parse.IsValid ? null : parse.ErrorMessage ?? "Geçersiz referans uzunluğu.";
        _session.SetKnownLength(parse.IsValid ? parse.Length : null);
        RefreshOutputs();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => RefreshOutputs();

    private void RefreshOutputs()
    {
        var scale = _session.ReferenceScale;
        HasValidReference = scale is not null;

        if (_referenceError is not null)
        {
            StatusMessage = _referenceError;
        }
        else if (scale is null)
        {
            StatusMessage = "Referans noktaları R1 ve R2 birbirinden farklı olmalıdır.";
        }
        else
        {
            StatusMessage = $"Ölçek: 1 piksel = {scale.MillimetersPerPixel.ToString("0.0000", TurkishCulture)} mm";
        }

        OnPropertyChanged(nameof(R1X));
        OnPropertyChanged(nameof(R1Y));
        OnPropertyChanged(nameof(R2X));
        OnPropertyChanged(nameof(R2Y));
        OnPropertyChanged(nameof(M1X));
        OnPropertyChanged(nameof(M1Y));
        OnPropertyChanged(nameof(M2X));
        OnPropertyChanged(nameof(M2Y));
        OnPropertyChanged(nameof(ReferencePixelSummary));
        OnPropertyChanged(nameof(MeasurementPixelSummary));
        OnPropertyChanged(nameof(CalculatedResult));
        OnPropertyChanged(nameof(CalculatedResultText));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(ToolHint));
        OnPropertyChanged(nameof(IsReferenceToolActive));
        OnPropertyChanged(nameof(IsMeasureToolActive));
        OnPropertyChanged(nameof(HasReferenceWarning));
        OnPropertyChanged(nameof(BottomInfoText));
    }
}
