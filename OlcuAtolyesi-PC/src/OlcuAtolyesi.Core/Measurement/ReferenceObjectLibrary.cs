namespace OlcuAtolyesi.Core.Measurement;

/// <summary>Kullanıcının referans olarak kullanabileceği, ölçüsü bilinen nesne.</summary>
public sealed record ReferenceObject(string Name, double Millimetres, string Note);

/// <summary>
/// Ölçüsü standart olan nesneler. Kullanıcı bunlardan birini seçerek referans uzunluğunu
/// tek adımda girer; ölçüyü elle yazmaz.
/// </summary>
public static class ReferenceObjectLibrary
{
    public const string ManualName = "Özel (Manuel Giriş)";

    public static IReadOnlyList<ReferenceObject> All { get; } =
    [
        new("Kredi Kartı / Kimlik (ISO 7810 ID-1)", 85.60, "ISO/IEC 7810 ID-1 uzun kenar"),
        new("Kredi Kartı (Kısa Kenar)", 53.98, "ISO/IEC 7810 ID-1 kısa kenar"),
        new("1 TL Madeni Para", 26.15, "TCMB standart madeni para çapı"),
        new("50 Kuruş Madeni Para", 23.85, "TCMB standart madeni para çapı"),
        new("A4 Kağıt (Uzun Kenar)", 297.00, "210 × 297 mm"),
        new("A4 Kağıt (Kısa Kenar)", 210.00, "210 × 297 mm"),
        new("Cetvel 100 mm", 100.00, "Standart metrik cetvel"),
        new(ManualName, 0d, "Kullanıcı ölçüyü kendisi girer")
    ];

    public static ReferenceObject? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        return All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ??
               All.FirstOrDefault(r => r.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains(r.Name, StringComparison.OrdinalIgnoreCase));
    }
}
