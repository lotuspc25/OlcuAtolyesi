using System.Globalization;

namespace OlcuAtolyesi.Models;

/// <summary>
/// Bir değeri, kullanıcının seçtiği birimle birlikte taşır. Hesaplamalar için
/// <see cref="Millimeters"/> kanonik değeri kullanılabilir.
/// </summary>
public readonly record struct LengthValue
{
    public LengthValue(double value, LengthUnit unit)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Uzunluk sonlu ve negatif olmayan bir sayı olmalıdır.");
        }

        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }

        Value = value;
        Unit = unit;
    }

    public double Value { get; }

    public LengthUnit Unit { get; }

    public double Millimeters => Value * Unit.MillimetersPerUnit();

    public bool IsPositive => Value > 0d;

    public LengthValue ConvertTo(LengthUnit targetUnit) =>
        new(Millimeters / targetUnit.MillimetersPerUnit(), targetUnit);

    public string Format(CultureInfo? culture = null, int? decimalPlaces = null)
    {
        culture ??= CultureInfo.GetCultureInfo("tr-TR");
        var places = decimalPlaces ?? Unit switch
        {
            LengthUnit.Millimeter => 1,
            LengthUnit.Centimeter => 2,
            LengthUnit.Meter => 3,
            LengthUnit.Inch => 2,
            _ => 2
        };

        return $"{Value.ToString($"F{places}", culture)} {Unit.Symbol()}";
    }
}
