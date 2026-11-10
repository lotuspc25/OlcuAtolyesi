using System.Text.RegularExpressions;

namespace OlcuAtolyesi.Diagnostics;

/// <summary>
/// Tanılama günlüklerinde hassas verilerin (PIN, anahtar, parola) maskelenmesini sağlar.
/// </summary>
public static class DiagnosticDataMasker
{
    private static readonly Regex PinRegex = new(
        @"(pin|pın)([\s:=]+)(['""]?)(\d{6})(['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SecretRegex = new(
        @"(password|secret|token|auth|key)([\s:=]+)(['""]?)([^\s'"";,]{4,})(['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var masked = PinRegex.Replace(input, "${1}${2}${3}[MASKELENDİ]${5}");
        masked = SecretRegex.Replace(masked, "${1}${2}${3}[GİZLENDİ]${5}");

        return masked;
    }
}
