using System.ComponentModel;
using System.Globalization;
using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Drawing;
using OlcuAtolyesi.Media;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;
using OlcuAtolyesi.ViewModels;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace OlcuAtolyesi.Views;

public sealed class PhotoMeasurePage : PrototypePage
{
    private const double HandleHitRadius = 32d;
    private const double ViewportPadding = 16d;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly SettingsService _settings;
    private readonly DemoProjectService _projects;
    private readonly PhotoMeasureViewModel _viewModel;
    private readonly PhotoPlateDrawable _drawable;
    private readonly GraphicsView _graphics;
    private readonly Entry _referenceEntry;
    private readonly Label _referenceMessage;
    private readonly Label _resultLabel;
    private readonly Label _zoomLabel;
    private readonly Label _selectionMessage;
    private readonly Button _saveButton;
    private TemporaryPhoto? _photo;
    private IImage? _loadedImage;
    private Guid? _editingProjectId;
    private ImageViewportTransform _transform = new(1, 0, 0);
    private ImageViewportTransform _panStartTransform = new(1, 0, 0);
    private int _activePoint = -1;
    private int _armedPoint = -1;
    private int _chainNextPoint = -1;
    private int _armedPointBeforeTouch = -1;
    private int _chainNextBeforeTouch = -1;
    private bool _hasUndoForActivePoint;
    private bool _hasFit;
    private bool _isPinching;
    private bool _isPanning;
    private bool _isReferenceTextDirty;
    private bool _isUpdatingReferenceText;
    private bool _wasAttached;
    private bool _released;

    public PhotoMeasurePage() : this(null, null, null)
    {
    }

    public PhotoMeasurePage(TemporaryPhoto? photo) : this(photo, null, null)
    {
    }

    public PhotoMeasurePage(DemoProject existingProject, string? persistentPhotoPath)
        : this(null, existingProject, persistentPhotoPath)
    {
    }

    private PhotoMeasurePage(TemporaryPhoto? photo, DemoProject? existingProject, string? persistentPhotoPath)
        : base(existingProject?.Name ?? AppText.Photo.Title, "photo")
    {
        _settings = App.GetService<SettingsService>();
        _projects = App.GetService<DemoProjectService>();
        _photo = photo;
        var savedPhotoMissing = false;

        if (existingProject is not null)
        {
            _editingProjectId = existingProject.Id;
            var initialUnit = existingProject.DisplayUnit ?? _settings.Current.DefaultLengthUnit;
            var fallbackSize = existingProject.PhotoSize is { Width: > 0, Height: > 0 } size
                ? size
                : PhotoMeasureViewModel.DemoImageSize;

            if (!string.IsNullOrWhiteSpace(persistentPhotoPath) && File.Exists(persistentPhotoPath))
            {
#if ANDROID
                try
                {
                    using var input = File.OpenRead(persistentPhotoPath);
                    _loadedImage = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(input);
                    if (_loadedImage is not null && _loadedImage.Width > 0 && _loadedImage.Height > 0)
                    {
                        _viewModel = new PhotoMeasureViewModel(
                            initialUnit,
                            new ImageSize(_loadedImage.Width, _loadedImage.Height),
                            isRealPhoto: true);
                    }
                    else
                    {
                        _loadedImage?.Dispose();
                        _loadedImage = null;
                        savedPhotoMissing = true;
                        _viewModel = new PhotoMeasureViewModel(initialUnit, fallbackSize, isRealPhoto: false);
                    }
                }
                catch
                {
                    _loadedImage?.Dispose();
                    _loadedImage = null;
                    savedPhotoMissing = true;
                    _viewModel = new PhotoMeasureViewModel(initialUnit, fallbackSize, isRealPhoto: false);
                }
#else
                _viewModel = new PhotoMeasureViewModel(initialUnit, fallbackSize, isRealPhoto: false);
#endif
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(existingProject.PhotoRelativePath))
                {
                    savedPhotoMissing = true;
                }

                _viewModel = new PhotoMeasureViewModel(initialUnit, fallbackSize, isRealPhoto: false);
            }

            var savedMeasurement = existingProject.Measurements
                .FirstOrDefault(m => m.ImageSegment is not null)?.ImageSegment;
            _viewModel.LoadSavedState(
                existingProject.ReferenceSegment,
                existingProject.ReferenceLength,
                savedMeasurement,
                initialUnit);
        }
        else if (photo is not null)
        {
#if ANDROID
            try
            {
                using var input = File.OpenRead(photo.Path);
                _loadedImage = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(input);
                if (_loadedImage is null || _loadedImage.Width <= 0 || _loadedImage.Height <= 0)
                {
                    throw new InvalidOperationException(PhotoText.Failed);
                }

                _viewModel = new PhotoMeasureViewModel(
                    _settings.Current.DefaultLengthUnit,
                    new ImageSize(_loadedImage.Width, _loadedImage.Height),
                    isRealPhoto: true);
            }
            catch
            {
                _loadedImage?.Dispose();
                _loadedImage = null;
                photo.Dispose();
                _photo = null;
                throw;
            }
#else
            photo.Dispose();
            _photo = null;
            throw new NotSupportedException(PhotoText.Unsupported);
#endif
        }
        else
        {
            _viewModel = new PhotoMeasureViewModel(_settings.Current.DefaultLengthUnit);
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _drawable = new PhotoPlateDrawable
        {
            ViewModel = _viewModel,
            Image = _loadedImage,
            Transform = _transform
        };
        _graphics = new GraphicsView
        {
            Drawable = _drawable,
            HeightRequest = 340,
            MinimumHeightRequest = 260
        };
        SemanticProperties.SetDescription(_graphics,
            _viewModel.IsRealPhoto
                ? "Seçilen gerçek fotoğraf, R1–R2 referans çizgisi ve M1–M2 ölçüm çizgisi."
                : "Çizilmiş bağlantı plakası, R1–R2 referans çizgisi ve M1–M2 ölçüm çizgisi.");
        _graphics.SizeChanged += (_, _) => FitViewIfNeeded();
        _graphics.StartInteraction += OnStartInteraction;
        _graphics.DragInteraction += OnDragInteraction;
        _graphics.EndInteraction += OnEndInteraction;

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _graphics.GestureRecognizers.Add(pinch);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnGraphicsTapped;
        _graphics.GestureRecognizers.Add(tap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _graphics.GestureRecognizers.Add(pan);

        var initialReferenceText = _viewModel.KnownLength is { } savedKnown
            ? savedKnown.Value.ToString("0.###", TurkishCulture)
            : "120";
        _referenceEntry = new Entry
        {
            Text = initialReferenceText,
            Keyboard = Keyboard.Numeric,
            ReturnType = ReturnType.Done,
            MaxLength = 12,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        SemanticProperties.SetDescription(_referenceEntry, AppText.Photo.ReferenceLength);
        _referenceEntry.TextChanged += (_, _) =>
        {
            if (!_isUpdatingReferenceText)
            {
                _isReferenceTextDirty = true;
            }
        };

        var unitPicker = new Picker
        {
            Title = AppText.Ruler.Unit,
            ItemsSource = Enum.GetValues<LengthUnit>().Select(unit => unit.DisplayName()).ToList(),
            SelectedIndex = (int)_viewModel.Unit
        };
        unitPicker.SelectedIndexChanged += (_, _) =>
        {
            if (unitPicker.SelectedIndex >= 0)
            {
                _viewModel.Unit = (LengthUnit)unitPicker.SelectedIndex;
                if (_viewModel.KnownLength is { } known && !_isReferenceTextDirty)
                {
                    _isUpdatingReferenceText = true;
                    _referenceEntry.Text = known.Value.ToString("0.###", TurkishCulture);
                    _isUpdatingReferenceText = false;
                }
            }
        };

        var initialRefMessage = savedPhotoMissing
            ? AppText.Photo.SavedPhotoMissing
            : _viewModel.HasReference
                ? AppText.Photo.ReferenceApplied
                : AppText.Photo.NeedReference;
        _referenceMessage = ViewFactory.Secondary(initialRefMessage);
        if (_viewModel.HasReference && !savedPhotoMissing)
        {
            _referenceMessage.SetDynamicResource(Label.TextColorProperty, "Success");
        }
        else if (savedPhotoMissing)
        {
            _referenceMessage.SetDynamicResource(Label.TextColorProperty, "Danger");
        }

        _resultLabel = ViewFactory.StyledLabel(_viewModel.ResultText, "ResultStyle", SemanticHeadingLevel.Level2);
        _zoomLabel = ViewFactory.Caption($"{AppText.Photo.Zoom}: 1,00×");
        _selectionMessage = ViewFactory.Secondary(string.Empty);
        _selectionMessage.IsVisible = false;

        var referenceGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(132))
            },
            ColumnSpacing = 8
        };
        referenceGrid.Add(_referenceEntry, 0, 0);
        referenceGrid.Add(unitPicker, 1, 0);

        var pointActions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap
        };
        AddAction(pointActions, AppText.Photo.SelectReference,
            (_, _) => ArmSegment(PhotoMeasureViewModel.PointR1, PhotoMeasureViewModel.PointR2));
        AddAction(pointActions, AppText.Photo.SelectMeasure,
            (_, _) => ArmSegment(PhotoMeasureViewModel.PointM1, PhotoMeasureViewModel.PointM2));
        AddAction(pointActions, AppText.Photo.SelectR1, (_, _) => ArmPoint(PhotoMeasureViewModel.PointR1));
        AddAction(pointActions, AppText.Photo.SelectR2, (_, _) => ArmPoint(PhotoMeasureViewModel.PointR2));
        AddAction(pointActions, AppText.Photo.SelectM1, (_, _) => ArmPoint(PhotoMeasureViewModel.PointM1));
        AddAction(pointActions, AppText.Photo.SelectM2, (_, _) => ArmPoint(PhotoMeasureViewModel.PointM2));
        AddAction(pointActions, AppText.Undo, (_, _) =>
        {
            if (_viewModel.Undo())
            {
                _graphics.Invalidate();
                SemanticScreenReader.Announce(AppText.Undo);
            }
        });
        AddAction(pointActions, AppText.Reset, (_, _) =>
        {
            _viewModel.Reset();
            _armedPoint = -1;
            _chainNextPoint = -1;
            _selectionMessage.IsVisible = false;
            _graphics.Invalidate();
        });
        AddAction(pointActions, AppText.Photo.ResetView, (_, _) => FitView(force: true));

        _saveButton = ViewFactory.PrimaryButton(
            _editingProjectId.HasValue ? AppText.Photo.SaveChanges : AppText.Photo.AddToProject,
            AddToProject);

        var stack = ViewFactory.Stack(12);
        stack.Add(_viewModel.IsRealPhoto
            ? ViewFactory.InfoCard(AppText.Photo.RealPhotoHeader, AppText.Photo.RealPhotoIntro)
            : ViewFactory.InfoCard(AppText.DemoWarning, $"{AppText.Photo.Intro} {AppText.Photo.NoCamera}"));
        stack.Add(StepRow());
        stack.Add(ViewFactory.SectionTitle(AppText.Photo.StepReference));
        stack.Add(ViewFactory.Caption(AppText.Photo.ReferenceHint));
        stack.Add(referenceGrid);
        stack.Add(ViewFactory.PrimaryButton(AppText.Photo.ApplyReference, ApplyReference));
        stack.Add(_referenceMessage);
        stack.Add(ViewFactory.SectionTitle(AppText.Photo.StepMeasure));
        stack.Add(_graphics);
        stack.Add(_zoomLabel);
        stack.Add(ViewFactory.Caption(AppText.Photo.InteractionHint));
        stack.Add(pointActions);
        stack.Add(_selectionMessage);
        stack.Add(ViewFactory.SectionTitle(AppText.Photo.StepReview));
        stack.Add(ViewFactory.Secondary(_viewModel.IsRealPhoto
            ? AppText.Photo.RealMeasurementResult
            : AppText.Photo.MeasurementResult));
        stack.Add(_resultLabel);
        stack.Add(_saveButton);

        Content = PageBody(ViewFactory.Scroll(stack));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _graphics.Invalidate();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is not null)
        {
            _wasAttached = true;
        }
        else if (_wasAttached && !_released)
        {
            _released = true;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _graphics.Drawable = null;
            _drawable.Image = null;
            _loadedImage?.Dispose();
            _loadedImage = null;
            _photo?.Dispose();
            _photo = null;
        }
    }

    private static Grid StepRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 6
        };
        grid.Add(ViewFactory.StatusBadge("1  Referans"), 0, 0);
        grid.Add(ViewFactory.StatusBadge("2  Ölç"), 1, 0);
        grid.Add(ViewFactory.StatusBadge("3  İncele"), 2, 0);
        return grid;
    }

    private static void AddAction(FlexLayout layout, string text, EventHandler handler)
    {
        var button = ViewFactory.SecondaryButton(text, handler);
        button.Margin = new Thickness(0, 0, 8, 8);
        layout.Add(button);
    }

    private void ApplyReference(object? sender, EventArgs e)
    {
        var result = _viewModel.ApplyReference(_referenceEntry.Text);
        if (result.IsValid)
        {
            _isReferenceTextDirty = false;
            _referenceMessage.Text = AppText.Photo.ReferenceApplied;
        }
        else
        {
            _referenceMessage.Text = result.ErrorMessage ?? AppText.Photo.NeedReference;
        }

        _referenceMessage.SetDynamicResource(Label.TextColorProperty, result.IsValid ? "Success" : "Danger");
        _graphics.Invalidate();
        SemanticScreenReader.Announce(_referenceMessage.Text);
    }

    private async void AddToProject(object? sender, EventArgs e)
    {
        if (_viewModel.Result is not { } result || _viewModel.ReferenceScale is not { } referenceScale)
        {
            _referenceMessage.Text = AppText.Photo.NeedReference;
            _referenceMessage.SetDynamicResource(Label.TextColorProperty, "Danger");
            SemanticScreenReader.Announce(_referenceMessage.Text);
            return;
        }

        var measurementName = _viewModel.IsRealPhoto ? "M1–M2 mesafe ölçümü" : "Fotoğraf uzunluk ölçümü";

        try
        {
            if (_editingProjectId is { } existingId)
            {
                _projects.UpdatePhotoMeasurementProject(
                    existingId,
                    name: null,
                    sourcePhotoPath: _photo?.Path,
                    imageSize: _viewModel.ImageSize,
                    referenceSegment: _viewModel.ReferenceSegment,
                    referenceLength: referenceScale.KnownLength,
                    measurementSegment: _viewModel.Measurement,
                    measuredLength: result,
                    displayUnit: _viewModel.Unit,
                    measurementName: measurementName);

                await DisplayAlertAsync(AppText.AppName, AppText.Photo.UpdatedProject, AppText.Confirm);
                return;
            }

            var defaultName = _viewModel.IsRealPhoto ? "Fotoğraftan ölçüm" : "Bağlantı plakası ölçümü";
            var enteredName = await DisplayPromptAsync(
                AppText.Photo.AddToProject,
                AppText.Projects.ProjectNamePrompt,
                AppText.Confirm,
                AppText.Cancel,
                AppText.Projects.ProjectNamePlaceholder,
                60,
                Keyboard.Text,
                defaultName);

            if (enteredName is null)
            {
                return;
            }

            var projectName = string.IsNullOrWhiteSpace(enteredName) ? defaultName : enteredName.Trim();
            var projectDescription = _viewModel.IsRealPhoto
                ? "Gerçek fotoğraf üzerinde R1–R2 referansı ile ölçüldü ve cihazda kalıcı olarak saklandı."
                : "Çizilmiş demo plaka üzerinde R1–R2 referansı ile oluşturuldu.";

            var created = _projects.SavePhotoMeasurementProject(
                projectName,
                projectDescription,
                _photo?.Path,
                _viewModel.ImageSize,
                _viewModel.ReferenceSegment,
                referenceScale.KnownLength,
                _viewModel.Measurement,
                result,
                _viewModel.Unit,
                measurementName);

            _editingProjectId = created.Id;
            Title = created.Name;
            _saveButton.Text = AppText.Photo.SaveChanges;
            await DisplayAlertAsync(AppText.AppName, AppText.Photo.AddedToSession, AppText.Confirm);
        }
        catch
        {
            await DisplayAlertAsync(AppText.AppName, AppText.Photo.SaveFailed, AppText.Confirm);
        }
    }

    private void ArmSegment(int startPoint, int endPoint)
    {
        _armedPoint = startPoint;
        _chainNextPoint = endPoint;
        _selectionMessage.Text = GetArmedMessage(startPoint);
        _selectionMessage.IsVisible = true;
        SemanticScreenReader.Announce(_selectionMessage.Text);
    }

    private void ArmPoint(int point)
    {
        _armedPoint = point;
        _chainNextPoint = -1;
        _selectionMessage.Text = GetArmedMessage(point);
        _selectionMessage.IsVisible = true;
        SemanticScreenReader.Announce(_selectionMessage.Text);
    }

    private static string GetArmedMessage(int point) => point switch
    {
        PhotoMeasureViewModel.PointR1 => "Referans başlangıcı (R1) seçimi açık. Görsele dokun.",
        PhotoMeasureViewModel.PointR2 => "Referans bitişi (R2) seçimi açık. Görsele dokun.",
        PhotoMeasureViewModel.PointM1 => "Ölçüm başlangıcı (M1) seçimi açık. Görsele dokun.",
        PhotoMeasureViewModel.PointM2 => "Ölçüm bitişi (M2) seçimi açık. Görsele dokun.",
        _ => string.Empty
    };

    private void FitViewIfNeeded()
    {
        if (!_hasFit && _graphics.Width > 0 && _graphics.Height > 0)
        {
            FitView(force: true);
        }
    }

    private void FitView(bool force)
    {
        if (!force && _hasFit)
        {
            return;
        }

        if (_graphics.Width <= 0 || _graphics.Height <= 0)
        {
            return;
        }

        _transform = ImageViewportTransform.Fit(
            _viewModel.ImageSize,
            new ViewSize(_graphics.Width, _graphics.Height),
            ViewportPadding);
        _drawable.Transform = _transform;
        _hasFit = true;
        UpdateZoomLabel();
        _graphics.Invalidate();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPinching || e.Touches.Length != 1)
        {
            if (_activePoint >= 0)
            {
                CancelActivePointInteraction();
            }

            return;
        }

        _armedPointBeforeTouch = _armedPoint;
        _chainNextBeforeTouch = _chainNextPoint;

        var touch = new ViewPoint(e.Touches[0].X, e.Touches[0].Y);
        if (_armedPoint >= 0)
        {
            _viewModel.CaptureUndoState();
            _hasUndoForActivePoint = true;
            _activePoint = _armedPoint;
            _viewModel.MovePoint(_activePoint, _transform.ViewToImage(touch));
            ShowActivePoint();
            return;
        }

        var candidates = new (int PointIndex, double Distance)[]
        {
            (PhotoMeasureViewModel.PointM1, _transform.ImageToView(_viewModel.M1).DistanceTo(touch)),
            (PhotoMeasureViewModel.PointM2, _transform.ImageToView(_viewModel.M2).DistanceTo(touch)),
            (PhotoMeasureViewModel.PointR1, _transform.ImageToView(_viewModel.R1).DistanceTo(touch)),
            (PhotoMeasureViewModel.PointR2, _transform.ImageToView(_viewModel.R2).DistanceTo(touch))
        };

        var closest = candidates.OrderBy(c => c.Distance).First();
        if (closest.Distance <= HandleHitRadius)
        {
            _activePoint = closest.PointIndex;
            _viewModel.CaptureUndoState();
            _hasUndoForActivePoint = true;
            _isPanning = false;
            ShowActivePoint();
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPinching || e.Touches.Length != 1)
        {
            if (_activePoint >= 0)
            {
                CancelActivePointInteraction();
            }

            return;
        }

        if (_activePoint < 0)
        {
            return;
        }

        var touch = new ViewPoint(e.Touches[0].X, e.Touches[0].Y);
        _viewModel.MovePoint(_activePoint, _transform.ViewToImage(touch));
        _graphics.Invalidate();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPinching)
        {
            return;
        }

        if (_activePoint >= 0)
        {
            _hasUndoForActivePoint = false;
            if (_armedPoint >= 0)
            {
                if (_chainNextPoint >= 0)
                {
                    _armedPoint = _chainNextPoint;
                    _chainNextPoint = -1;
                    _selectionMessage.Text = GetArmedMessage(_armedPoint);
                    _selectionMessage.IsVisible = true;
                    SemanticScreenReader.Announce(_selectionMessage.Text);
                }
                else
                {
                    _armedPoint = -1;
                    _selectionMessage.IsVisible = false;
                    SemanticScreenReader.Announce(_viewModel.ResultText);
                }
            }
            else
            {
                SemanticScreenReader.Announce(_viewModel.ResultText);
            }
        }

        _activePoint = -1;
        ShowActivePoint();
    }

    private void OnGraphicsTapped(object? sender, TappedEventArgs e)
    {
        if (_isPinching || _activePoint >= 0 || _armedPoint < 0 || e.GetPosition(_graphics) is not { } position)
        {
            return;
        }

        _viewModel.CaptureUndoState();
        _viewModel.MovePoint(_armedPoint, _transform.ViewToImage(new ViewPoint(position.X, position.Y)));
        if (_chainNextPoint >= 0)
        {
            _armedPoint = _chainNextPoint;
            _chainNextPoint = -1;
            _selectionMessage.Text = GetArmedMessage(_armedPoint);
            _selectionMessage.IsVisible = true;
            SemanticScreenReader.Announce(_selectionMessage.Text);
        }
        else
        {
            _armedPoint = -1;
            _selectionMessage.IsVisible = false;
            SemanticScreenReader.Announce(_viewModel.ResultText);
        }

        _graphics.Invalidate();
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isPinching || _activePoint >= 0 || _armedPoint >= 0)
        {
            _isPanning = false;
            return;
        }

        if (e.StatusType == GestureStatus.Started)
        {
            _panStartTransform = _transform;
            _isPanning = true;
        }
        else if (e.StatusType == GestureStatus.Running && _isPanning && _graphics.Width > 0 && _graphics.Height > 0)
        {
            var viewport = new ViewSize(_graphics.Width, _graphics.Height);
            _transform = _panStartTransform
                .PanBy(e.TotalX, e.TotalY)
                .ClampToViewport(_viewModel.ImageSize, viewport, ViewportPadding);
            _drawable.Transform = _transform;
            _graphics.Invalidate();
        }
        else if (e.StatusType is GestureStatus.Completed or GestureStatus.Canceled)
        {
            _isPanning = false;
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _isPinching = true;
            _isPanning = false;
            if (_activePoint >= 0)
            {
                CancelActivePointInteraction();
            }

            return;
        }

        if (e.Status is GestureStatus.Completed or GestureStatus.Canceled)
        {
            _isPinching = false;
            return;
        }

        if (e.Status != GestureStatus.Running || _graphics.Width <= 0 || _graphics.Height <= 0 || !double.IsFinite(e.Scale) || e.Scale <= 0d)
        {
            return;
        }

        var viewport = new ViewSize(_graphics.Width, _graphics.Height);
        var anchor = new ViewPoint(e.ScaleOrigin.X * _graphics.Width, e.ScaleOrigin.Y * _graphics.Height);
        var minimumScale = ImageViewportTransform.Fit(_viewModel.ImageSize, viewport, ViewportPadding).Scale;
        var maximumScale = minimumScale * 5d;
        var targetScale = Math.Clamp(_transform.Scale * e.Scale, minimumScale, maximumScale);

        _transform = _transform
            .ZoomAt(anchor, targetScale / _transform.Scale, minimumScale, maximumScale)
            .ClampToViewport(_viewModel.ImageSize, viewport, ViewportPadding);
        _drawable.Transform = _transform;
        UpdateZoomLabel(minimumScale);
        _graphics.Invalidate();
    }

    private void CancelActivePointInteraction()
    {
        if (_hasUndoForActivePoint)
        {
            _viewModel.CancelLastUndoCapture();
            _hasUndoForActivePoint = false;
        }

        _armedPoint = _armedPointBeforeTouch;
        _chainNextPoint = _chainNextBeforeTouch;
        _selectionMessage.IsVisible = _armedPoint >= 0;
        _activePoint = -1;
        ShowActivePoint();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _resultLabel.Text = _viewModel.ResultText;
        var labelTitle = _viewModel.IsRealPhoto
            ? AppText.Photo.RealMeasurementResult
            : AppText.Photo.MeasurementResult;
        SemanticProperties.SetDescription(_resultLabel, $"{labelTitle}: {_viewModel.ResultText}");
        _graphics.Invalidate();
    }

    private void ShowActivePoint()
    {
        _drawable.ActivePoint = _activePoint;
        _graphics.Invalidate();
    }

    private void UpdateZoomLabel(double? fitScale = null)
    {
        fitScale ??= _graphics.Width > 0 && _graphics.Height > 0
            ? ImageViewportTransform.Fit(
                _viewModel.ImageSize,
                new ViewSize(_graphics.Width, _graphics.Height),
                ViewportPadding).Scale
            : _transform.Scale;
        var zoom = _transform.Scale / Math.Max(0.0001, fitScale.Value);
        _zoomLabel.Text = $"{AppText.Photo.Zoom}: {zoom:0.00}×".Replace('.', ',');
    }
}
