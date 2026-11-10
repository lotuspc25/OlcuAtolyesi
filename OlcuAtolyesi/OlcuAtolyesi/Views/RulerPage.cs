using System.ComponentModel;
using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Drawing;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;
using OlcuAtolyesi.ViewModels;

namespace OlcuAtolyesi.Views;

public sealed class RulerPage : PrototypePage
{
    private readonly RulerViewModel _viewModel;
    private readonly RulerDrawable _drawable = new();
    private readonly GraphicsView _graphics;
    private readonly Label _resultLabel;
    private readonly Label _lockStatusLabel;
    private readonly Button _lockButton;
    private readonly Border _calibrationPanel;
    private readonly Entry _referenceEntry;
    private readonly Label _knownLengthLabel;
    private readonly Label _calibrationMessage;
    private int _activeGuide = -1;

    public RulerPage() : base(AppText.Ruler.Title, "ruler")
    {
        var settings = App.GetService<SettingsService>();
        _viewModel = new RulerViewModel(settings.Current.DefaultLengthUnit);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _drawable.StartPosition = _viewModel.StartPosition;
        _drawable.EndPosition = _viewModel.EndPosition;
        _drawable.DistanceText = _viewModel.MeasurementText;

        _graphics = new GraphicsView
        {
            Drawable = _drawable,
            HeightRequest = 260,
            MinimumHeightRequest = 220
        };
        SemanticProperties.SetDescription(_graphics,
            "İki sürüklenebilir kılavuzlu demo cetvel alanı. Sonuç aşağıdaki metinde de gösterilir.");
        _graphics.StartInteraction += OnStartInteraction;
        _graphics.DragInteraction += OnDragInteraction;
        _graphics.EndInteraction += OnEndInteraction;

        _resultLabel = ViewFactory.StyledLabel(_viewModel.MeasurementText, "ResultStyle", SemanticHeadingLevel.Level2);
        SemanticProperties.SetDescription(_resultLabel, $"{AppText.Ruler.Measurement}: {_viewModel.MeasurementText}");

        var unitPicker = new Picker
        {
            Title = AppText.Ruler.Unit,
            ItemsSource = Enum.GetValues<LengthUnit>().Select(unit => unit.DisplayName()).ToList(),
            SelectedIndex = (int)_viewModel.Unit,
            HorizontalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetDescription(unitPicker, AppText.Ruler.Unit);
        unitPicker.SelectedIndexChanged += (_, _) =>
        {
            if (unitPicker.SelectedIndex >= 0)
            {
                _viewModel.Unit = (LengthUnit)unitPicker.SelectedIndex;
            }
        };

        _lockButton = ViewFactory.SecondaryButton(_viewModel.LockButtonText, (_, _) =>
        {
            _viewModel.IsLocked = !_viewModel.IsLocked;
            SemanticScreenReader.Announce(_viewModel.LockStatusText);
        });
        var resetButton = ViewFactory.SecondaryButton(AppText.Reset, (_, _) =>
        {
            _viewModel.Reset();
            SemanticScreenReader.Announce($"{AppText.Reset}. {_viewModel.MeasurementText}");
        });

        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start
        };
        actions.Add(_lockButton);
        actions.Add(resetButton);
        actions.Add(ViewFactory.SecondaryButton(AppText.Ruler.Calibration, ToggleCalibration));
        foreach (var child in actions.Children)
        {
            FlexLayout.SetBasis((BindableObject)child, FlexBasis.Auto);
            FlexLayout.SetGrow((BindableObject)child, 0);
            ((View)child).Margin = new Thickness(0, 0, 8, 8);
        }

        _lockStatusLabel = ViewFactory.Secondary(_viewModel.LockStatusText);

        _referenceEntry = new Entry
        {
            Placeholder = "Örn. 85,5",
            Keyboard = Keyboard.Numeric,
            ReturnType = ReturnType.Done,
            MaxLength = 12,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        SemanticProperties.SetDescription(_referenceEntry, AppText.Ruler.KnownLength);

        var matchLabel = ViewFactory.Secondary("100");
        var matchProgress = new ProgressBar { Progress = 0.5, HeightRequest = 8 };
        var matchSlider = new Slider { Minimum = 20, Maximum = 180, Value = 100 };
        SemanticProperties.SetDescription(matchSlider, AppText.Ruler.VisualMatch);
        matchSlider.ValueChanged += (_, args) =>
        {
            matchLabel.Text = $"{args.NewValue:0} / 180";
            matchProgress.Progress = args.NewValue / 180d;
        };

        _calibrationMessage = ViewFactory.Secondary(string.Empty);
        _calibrationMessage.IsVisible = false;

        _knownLengthLabel = ViewFactory.Secondary($"{AppText.Ruler.KnownLength} ({_viewModel.Unit.Symbol()})");

        var calibrationStack = ViewFactory.Stack(8);
        calibrationStack.Add(ViewFactory.SectionTitle(AppText.Ruler.CalibrationTitle));
        calibrationStack.Add(ViewFactory.Body(AppText.Ruler.CalibrationIntro));
        calibrationStack.Add(_knownLengthLabel);
        calibrationStack.Add(_referenceEntry);
        calibrationStack.Add(ViewFactory.Secondary(AppText.Ruler.VisualMatch));
        calibrationStack.Add(matchSlider);
        calibrationStack.Add(matchProgress);
        calibrationStack.Add(matchLabel);
        calibrationStack.Add(ViewFactory.PrimaryButton(AppText.Ruler.Validate, ValidateCalibration));
        calibrationStack.Add(_calibrationMessage);
        calibrationStack.Add(ViewFactory.Caption(AppText.Ruler.CalibrationCaution));
        _calibrationPanel = ViewFactory.Card(calibrationStack, "WarningCardStyle");
        _calibrationPanel.IsVisible = false;

        var stack = ViewFactory.Stack(12);
        stack.Add(ViewFactory.InfoCard(AppText.DemoWarning, AppText.Ruler.Intro));
        stack.Add(_graphics);
        stack.Add(ViewFactory.Caption(AppText.Ruler.TickNote));
        stack.Add(ViewFactory.SectionTitle(AppText.Ruler.Measurement));
        stack.Add(_resultLabel);
        stack.Add(unitPicker);
        stack.Add(actions);
        stack.Add(_lockStatusLabel);
        stack.Add(_calibrationPanel);

        Content = PageBody(ViewFactory.Scroll(stack));
    }

    protected override bool OnBackButtonPressed()
    {
        if (_calibrationPanel.IsVisible)
        {
            _calibrationPanel.IsVisible = false;
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _graphics.Invalidate();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (_viewModel.IsLocked || e.Touches.Length == 0)
        {
            if (_viewModel.IsLocked)
            {
                SemanticScreenReader.Announce(AppText.Ruler.Locked);
            }

            return;
        }

        var x = e.Touches[0].X;
        var startX = RulerDrawable.PositionToX(_graphics.Width, _viewModel.StartPosition);
        var endX = RulerDrawable.PositionToX(_graphics.Width, _viewModel.EndPosition);
        var startDistance = Math.Abs(x - startX);
        var endDistance = Math.Abs(x - endX);
        _activeGuide = startDistance <= endDistance ? 0 : 1;

        if (Math.Min(startDistance, endDistance) > 36)
        {
            _activeGuide = -1;
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (_activeGuide < 0 || e.Touches.Length == 0)
        {
            return;
        }

        _viewModel.MoveGuide(_activeGuide, RulerDrawable.XToPosition(_graphics.Width, e.Touches[0].X));
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        if (_activeGuide >= 0)
        {
            SemanticScreenReader.Announce(_viewModel.MeasurementText);
        }

        _activeGuide = -1;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _drawable.StartPosition = _viewModel.StartPosition;
        _drawable.EndPosition = _viewModel.EndPosition;
        _drawable.DistanceText = _viewModel.MeasurementText;
        _drawable.IsLocked = _viewModel.IsLocked;
        _resultLabel.Text = _viewModel.MeasurementText;
        _knownLengthLabel.Text = $"{AppText.Ruler.KnownLength} ({_viewModel.Unit.Symbol()})";
        _lockButton.Text = _viewModel.LockButtonText;
        _lockStatusLabel.Text = _viewModel.LockStatusText;
        SemanticProperties.SetDescription(_resultLabel, $"{AppText.Ruler.Measurement}: {_viewModel.MeasurementText}");
        _graphics.Invalidate();
    }

    private void ValidateCalibration(object? sender, EventArgs e)
    {
        var result = ReferenceLengthParser.ParsePositive(_referenceEntry.Text, _viewModel.Unit);
        _calibrationMessage.IsVisible = true;
        _calibrationMessage.Text = result.IsValid
            ? AppText.Ruler.CalibrationAccepted
            : AppText.Ruler.InvalidReference;
        _calibrationMessage.SetDynamicResource(Label.TextColorProperty, result.IsValid ? "Success" : "Danger");
        SemanticScreenReader.Announce(_calibrationMessage.Text);
    }

    private void ToggleCalibration(object? sender, EventArgs e)
    {
        _calibrationPanel.IsVisible = !_calibrationPanel.IsVisible;
        if (_calibrationPanel.IsVisible)
        {
            _referenceEntry.Focus();
        }
    }
}
