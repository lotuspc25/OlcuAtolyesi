using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

public static class UnitConverter
{
    public static double Convert(double value, LengthUnit from, LengthUnit to)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value * from.MillimetersPerUnit() / to.MillimetersPerUnit();
    }

    public static LengthValue Convert(LengthValue value, LengthUnit to) => value.ConvertTo(to);
}
