using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC.ViewModels;

namespace OlcuAtolyesi.PC.Views;

public partial class ScreenCalibrationWindow : Window
{
    private enum DragTarget { None, Left, Right }

    private readonly ScreenCalibrationViewModel _vm;
    private DragTarget _activeDrag = DragTarget.None;
    private double _leftX = 80d;
    private double _rightX = 480d;

    public ScreenCalibrationWindow(DesktopSettingsService settings)
    {
        InitializeComponent();
        _vm = new ScreenCalibrationViewModel(settings);
        DataContext = _vm;

        _vm.RequestClose += (_, _) => Close();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var width = DragCanvas.ActualWidth > 100d ? DragCanvas.ActualWidth : 580d;
        _leftX = Math.Max(40d, (width / 2d) - 200d);
        _rightX = Math.Min(width - 40d, (width / 2d) + 200d);
        UpdateVisuals();
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DragCanvas);
        var distLeft = Math.Abs(pos.X - _leftX);
        var distRight = Math.Abs(pos.X - _rightX);

        if (distLeft < 25d && distLeft <= distRight)
        {
            _activeDrag = DragTarget.Left;
            DragCanvas.CaptureMouse();
        }
        else if (distRight < 25d)
        {
            _activeDrag = DragTarget.Right;
            DragCanvas.CaptureMouse();
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_activeDrag == DragTarget.None)
        {
            return;
        }

        var pos = e.GetPosition(DragCanvas);
        var width = DragCanvas.ActualWidth > 50d ? DragCanvas.ActualWidth : 600d;

        if (_activeDrag == DragTarget.Left)
        {
            _leftX = Math.Clamp(pos.X, 10d, _rightX - 20d);
        }
        else if (_activeDrag == DragTarget.Right)
        {
            _rightX = Math.Clamp(pos.X, _leftX + 20d, width - 10d);
        }

        UpdateVisuals();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeDrag != DragTarget.None)
        {
            _activeDrag = DragTarget.None;
            DragCanvas.ReleaseMouseCapture();
        }
    }

    private void UpdateVisuals()
    {
        Canvas.SetLeft(LeftLine, _leftX);
        LeftLine.X1 = 0; LeftLine.X2 = 0;
        Canvas.SetLeft(LeftHandle, _leftX - 7d);

        Canvas.SetLeft(RightLine, _rightX);
        RightLine.X1 = 0; RightLine.X2 = 0;
        Canvas.SetLeft(RightHandle, _rightX - 7d);

        Canvas.SetLeft(SpanRect, _leftX);
        SpanRect.Width = Math.Max(0d, _rightX - _leftX);

        var dips = Math.Round(_rightX - _leftX, 1);
        DistanceLabel.Text = $"{dips:0.#} DIP";
        Canvas.SetLeft(DistanceLabel, ((_leftX + _rightX) / 2d) - 30d);

        _vm.MeasuredDips = dips;
    }
}
