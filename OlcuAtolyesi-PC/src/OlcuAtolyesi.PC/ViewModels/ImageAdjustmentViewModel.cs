using System.Windows.Input;
using System.Windows.Media.Imaging;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.PC.Media;

namespace OlcuAtolyesi.PC.ViewModels;

/// <summary>Görüntü sekmesindeki ayarları tutar; değiştiğinde görüntünün yenilenmesini bildirir.</summary>
public sealed class ImageAdjustmentViewModel : ObservableObject
{
    private double _brightness;
    private double _contrast;
    private double _sharpen;
    private bool _grayscale;
    private bool _negative;
    private bool _edgeHighlight;
    private bool _showGrid;
    private int _quarterTurns;
    private bool _flipHorizontal;
    private bool _flipVertical;
    private ImageRect? _cropRect;

    public ImageAdjustmentViewModel()
    {
        RotateLeftCommand = new RelayCommand(RotateLeft);
        RotateRightCommand = new RelayCommand(RotateRight);
        Rotate180Command = new RelayCommand(Rotate180);
        FlipHorizontalCommand = new RelayCommand(ToggleFlipHorizontal);
        FlipVerticalCommand = new RelayCommand(ToggleFlipVertical);
        ResetAllCommand = new RelayCommand(ResetAll);
    }

    public event EventHandler? Changed;

    public ICommand RotateLeftCommand { get; }
    public ICommand RotateRightCommand { get; }
    public ICommand Rotate180Command { get; }
    public ICommand FlipHorizontalCommand { get; }
    public ICommand FlipVerticalCommand { get; }
    public ICommand ResetAllCommand { get; }

    public PhotoFilterSettings FilterSettings =>
        new(_brightness, _contrast, _sharpen, _grayscale, _negative, _edgeHighlight);

    public ImageDisplaySettings DisplaySettings =>
        new(_quarterTurns, _flipHorizontal, _flipVertical, _cropRect);

    public bool ShowGrid
    {
        get => _showGrid;
        set => SetAndNotify(ref _showGrid, value, nameof(ShowGrid));
    }

    public double Brightness { get => _brightness; set => SetAndNotify(ref _brightness, value, nameof(Brightness)); }
    public double Contrast { get => _contrast; set => SetAndNotify(ref _contrast, value, nameof(Contrast)); }
    public double Sharpen { get => _sharpen; set => SetAndNotify(ref _sharpen, value, nameof(Sharpen)); }
    public bool Grayscale { get => _grayscale; set => SetAndNotify(ref _grayscale, value, nameof(Grayscale)); }
    public bool Negative { get => _negative; set => SetAndNotify(ref _negative, value, nameof(Negative)); }
    public bool EdgeHighlight { get => _edgeHighlight; set => SetAndNotify(ref _edgeHighlight, value, nameof(EdgeHighlight)); }

    public void RotateRight() { _quarterTurns = (_quarterTurns + 1) % 4; NotifyGeometry(); }
    public void RotateLeft() { _quarterTurns = (_quarterTurns + 3) % 4; NotifyGeometry(); }
    public void Rotate180() { _quarterTurns = (_quarterTurns + 2) % 4; NotifyGeometry(); }
    public void ToggleFlipHorizontal() { _flipHorizontal = !_flipHorizontal; NotifyGeometry(); }
    public void ToggleFlipVertical() { _flipVertical = !_flipVertical; NotifyGeometry(); }

    public void ResetAll()
    {
        _brightness = 0; _contrast = 0; _sharpen = 0;
        _grayscale = false; _negative = false; _edgeHighlight = false;
        _showGrid = false;
        _quarterTurns = 0; _flipHorizontal = false; _flipVertical = false; _cropRect = null;

        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Contrast));
        OnPropertyChanged(nameof(Sharpen));
        OnPropertyChanged(nameof(Grayscale));
        OnPropertyChanged(nameof(Negative));
        OnPropertyChanged(nameof(EdgeHighlight));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(DisplaySettings));
        OnPropertyChanged(nameof(FilterSettings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Geometrik dönüşümü bitmap'e uygular (kırp -> aynala -> döndür).</summary>
    public BitmapSource ApplyGeometry(BitmapSource source)
    {
        BitmapSource result = source;

        if (_cropRect is { } crop && crop.IsValid)
        {
            var x = (int)Math.Round(Math.Clamp(crop.X, 0d, source.PixelWidth - 2d));
            var y = (int)Math.Round(Math.Clamp(crop.Y, 0d, source.PixelHeight - 2d));
            var w = (int)Math.Round(Math.Clamp(crop.Width, 2d, source.PixelWidth - x));
            var h = (int)Math.Round(Math.Clamp(crop.Height, 2d, source.PixelHeight - y));
            result = new CroppedBitmap(result, new System.Windows.Int32Rect(x, y, w, h));
        }

        if (_flipHorizontal)
        {
            result = new TransformedBitmap(result, new System.Windows.Media.ScaleTransform(-1d, 1d));
        }

        if (_flipVertical)
        {
            result = new TransformedBitmap(result, new System.Windows.Media.ScaleTransform(1d, -1d));
        }

        var turns = ((_quarterTurns % 4) + 4) % 4;
        if (turns > 0)
        {
            result = new TransformedBitmap(
                result,
                new System.Windows.Media.RotateTransform(90d * turns));
        }

        if (result.CanFreeze)
        {
            result.Freeze();
        }

        return result;
    }

    private void NotifyGeometry()
    {
        OnPropertyChanged(nameof(DisplaySettings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetAndNotify<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName is nameof(Brightness) or nameof(Contrast) or nameof(Sharpen) or nameof(Grayscale) or nameof(Negative) or nameof(EdgeHighlight))
        {
            OnPropertyChanged(nameof(FilterSettings));
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
