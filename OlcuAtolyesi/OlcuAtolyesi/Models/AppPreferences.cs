namespace OlcuAtolyesi.Models;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed record AppPreferences(ThemePreference Theme, LengthUnit DefaultLengthUnit)
{
    public static AppPreferences Default { get; } = new(ThemePreference.System, LengthUnit.Millimeter);
}
