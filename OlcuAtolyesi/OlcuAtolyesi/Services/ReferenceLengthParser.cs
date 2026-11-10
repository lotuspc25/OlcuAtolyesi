using System.Globalization;
using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

public readonly record struct ReferenceLengthParseResult(
    bool IsValid,
    LengthValue? Length,
    string? ErrorMessage)
{
    public static ReferenceLengthParseResult Success(LengthValue length) => new(true, length, null);

    public static ReferenceLengthParseResult Failure(string errorMessage) => new(false, null, errorMessage);
}

public static class ReferenceLengthParser
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private const NumberStyles AcceptedStyles = NumberStyles.Float;

    /// <summary>
    /// Türkçe ondalık virgülü öncelikli olarak okur. Virgül kullanılmamışsa
    /// cihazların sayısal klavyelerinde görülebilen noktalı yazımı da kabul eder.
    /// </summary>
    public static ReferenceLengthParseResult ParsePositive(string? text, LengthUnit unit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ReferenceLengthParseResult.Failure("Referans değeri boş bırakılamaz.");
        }

        var valueText = text.Trim();
        var parsed = double.TryParse(valueText, AcceptedStyles, TurkishCulture, out var value);

        if (!parsed && !valueText.Contains(','))
        {
            parsed = double.TryParse(valueText, AcceptedStyles, CultureInfo.InvariantCulture, out value);
        }

        if (!parsed || !double.IsFinite(value))
        {
            return ReferenceLengthParseResult.Failure("Geçerli bir sayı girin (ör. 12,5).");
        }

        if (value <= 0d)
        {
            return ReferenceLengthParseResult.Failure("Referans uzunluğu sıfırdan büyük olmalıdır.");
        }

        return ReferenceLengthParseResult.Success(new LengthValue(value, unit));
    }
}
