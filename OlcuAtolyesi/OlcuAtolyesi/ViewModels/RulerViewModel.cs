using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.ViewModels;

public sealed class RulerViewModel : ObservableObject
{
    public const double DemoSpanMillimeters = 200d;
    public const double DefaultStart = 0.24d;
    public const double DefaultEnd = 0.68d;

    private double _startPosition = DefaultStart;
    private double _endPosition = DefaultEnd;
    private LengthUnit _unit;
    private bool _isLocked;

    public RulerViewModel(LengthUnit unit)
    {
        _unit = unit;
    }

    public double StartPosition
    {
        get => _startPosition;
        private set
        {
            if (SetProperty(ref _startPosition, Math.Clamp(value, 0d, 1d)))
            {
                NotifyMeasurementChanged();
            }
        }
    }

    public double EndPosition
    {
        get => _endPosition;
        private set
        {
            if (SetProperty(ref _endPosition, Math.Clamp(value, 0d, 1d)))
            {
                NotifyMeasurementChanged();
            }
        }
    }

    public LengthUnit Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                NotifyMeasurementChanged();
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                OnPropertyChanged(nameof(LockButtonText));
                OnPropertyChanged(nameof(LockStatusText));
            }
        }
    }

    public double MeasurementMillimeters => Math.Abs(EndPosition - StartPosition) * DemoSpanMillimeters;

    public string MeasurementText =>
        new LengthValue(MeasurementMillimeters, LengthUnit.Millimeter).ConvertTo(Unit).Format();

    public string LockButtonText => IsLocked ? AppText.Ruler.Unlock : AppText.Ruler.Lock;

    public string LockStatusText => IsLocked ? AppText.Ruler.Locked : AppText.Ruler.Unlocked;

    public bool MoveGuide(int guideIndex, double normalizedPosition)
    {
        if (IsLocked)
        {
            return false;
        }

        if (guideIndex == 0)
        {
            StartPosition = normalizedPosition;
        }
        else if (guideIndex == 1)
        {
            EndPosition = normalizedPosition;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(guideIndex));
        }

        return true;
    }

    public void Reset()
    {
        StartPosition = DefaultStart;
        EndPosition = DefaultEnd;
        IsLocked = false;
    }

    private void NotifyMeasurementChanged()
    {
        OnPropertyChanged(nameof(MeasurementMillimeters));
        OnPropertyChanged(nameof(MeasurementText));
    }
}
