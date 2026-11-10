using System.Text.RegularExpressions;

namespace OlcuAtolyesi.Core.Diagnostics;

/// <summary>
/// Tanılama günlüklerinde hassas verilerin (PIN, anahtar, parola, özel veriler)
/// diske veya konsola yazılmadan önce güvenli biçimde maskelenmesini sağlayan yardımcı sınıf.
/// </summary>
public static class DiagnosticDataMasker
{
    // 6 haneli PIN eşleştirme kodlarını yakalar (ör. PIN: 482910, pin=482910, "pin": "482910")
    private static readonly Regex PinRegex = new(
        @"(pin|pın)([\s:=]+)(['""]?)(\d{6})(['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Parola ve gizli anahtar kalıplarını yakalar
    private static readonly Regex SecretRegex = new(
        @"(password|secret|token|auth|key)([\s:=]+)(['""]?)([^\s'"";,]{4,})(['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Metin içindeki PIN, token veya parola benzeri hassas verileri maskeler.
    /// </summary>
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
