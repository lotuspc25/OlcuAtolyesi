namespace OlcuAtolyesi.Models;

public enum LengthUnit
{
    Millimeter,
    Centimeter,
    Meter,
    Inch
}

public static class LengthUnitExtensions
{
    public static double MillimetersPerUnit(this LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => 1d,
        LengthUnit.Centimeter => 10d,
        LengthUnit.Meter => 1000d,
        LengthUnit.Inch => 25.4d,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Desteklenmeyen uzunluk birimi.")
    };

    public static string Symbol(this LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => "mm",
        LengthUnit.Centimeter => "cm",
        LengthUnit.Meter => "m",
        LengthUnit.Inch => "inç",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Desteklenmeyen uzunluk birimi.")
    };

    public static string DisplayName(this LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => "Milimetre",
        LengthUnit.Centimeter => "Santimetre",
        LengthUnit.Meter => "Metre",
        LengthUnit.Inch => "İnç",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Desteklenmeyen uzunluk birimi.")
    };
}
