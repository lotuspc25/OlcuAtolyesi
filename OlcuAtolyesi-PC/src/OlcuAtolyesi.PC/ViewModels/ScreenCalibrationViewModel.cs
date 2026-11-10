using System.Globalization;
using System.Windows.Input;
using OlcuAtolyesi.Core.Measurement;
using OlcuAtolyesi.Core.Mvvm;
using OlcuAtolyesi.Core.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed class ScreenCalibrationViewModel : ObservableObject
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly DesktopSettingsService _settings;
    private string _knownMillimetres = "85,60";
    private ReferenceObject _selectedPreset;
    private double _measuredDips = 400d;
    private string _resultText = string.Empty;

    public ScreenCalibrationViewModel(DesktopSettingsService settings)
    {
        _settings = settings;

        Presets = ReferenceObjectLibrary.All
            .Where(o => !string.Equals(o.Name, ReferenceObjectLibrary.ManualName, StringComparison.Ordinal))
            .ToList();

        _selectedPreset = Presets.FirstOrDefault(p => p.Name.Contains("Kredi kartı", StringComparison.Ordinal))
                          ?? Presets[0];
        _knownMillimetres = _selectedPreset.Millimetres.ToString("0.##", TurkishCulture);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);

        UpdateResult();
    }

    public IReadOnlyList<ReferenceObject> Presets { get; }

    public ReferenceObject SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value is not null)
            {
                KnownMillimetres = value.Millimetres.ToString("0.##", TurkishCulture);
            }
        }
    }

    public string KnownMillimetres
    {
        get => _knownMillimetres;
        set
        {
            if (SetProperty(ref _knownMillimetres, value))
            {
                UpdateResult();
            }
        }
    }

    public double MeasuredDips
    {
        get => _measuredDips;
        set
        {
            if (SetProperty(ref _measuredDips, value))
            {
                UpdateResult();
            }
        }
    }

    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    public ScreenCalibration? Result { get; private set; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public event EventHandler? RequestClose;

    private void UpdateResult()
    {
        if (double.TryParse(_knownMillimetres.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) &&
            mm > 0d && _measuredDips > 10d)
        {
            var calib = ScreenCalibration.FromMeasurement(mm, _measuredDips, _selectedPreset?.Name ?? "Özel");
            if (calib.IsValid)
            {
                var dipsPerMm = 1d / calib.MillimetresPerDip;
                ResultText = $"1 DIP = {calib.MillimetresPerDip.ToString("0.####", TurkishCulture)} mm  ·  1 mm = {dipsPerMm.ToString("0.##", TurkishCulture)} DIP";
                Result = calib;
                return;
            }
        }

        ResultText = "Geçersiz ölçüm (çizgiler arasındaki mesafeyi ve mm değerini kontrol edin).";
        Result = null;
    }

    private void Save()
    {
        if (Result is { IsValid: true } calib)
        {
            _settings.SetScreenCalibration(calib);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
