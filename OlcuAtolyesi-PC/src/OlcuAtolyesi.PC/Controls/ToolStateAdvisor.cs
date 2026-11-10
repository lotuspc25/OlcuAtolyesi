using OlcuAtolyesi.Core.Geometry;

namespace OlcuAtolyesi.PC.Controls;

/// <summary>
/// Kullanıcıya "şimdi ne yapmalıyım" diye yol gösteren kısa Türkçe metinleri üretir.
/// Görünümden bağımsız saf mantıktır; otomatik testlerle doğrulanabilir.
/// </summary>
public static class ToolStateAdvisor
{
    public const string Idle =
        "Başlamak için 'R — Referans Belirle' veya 'M — Mesafe Ölç' düğmesine basın.";

    public const string ReferenceStart =
        "1/2 — Fotoğrafta referansın BAŞLANGIÇ noktasına tıklayın (R1).";

    public const string ReferenceEnd =
        "2/2 — Referansın BİTİŞ noktasına tıklayın (R2).";

    public const string MeasureStart =
        "1/2 — Ölçmek istediğiniz mesafenin BAŞLANGIÇ noktasına tıklayın (M1).";

    public const string MeasureEnd =
        "2/2 — Mesafenin BİTİŞ noktasına tıklayın (M2).";

    public const string NeedKnownLength =
        "Referans noktaları belirlendi — şimdi gerçek uzunluğu girin (ör. 100 mm) ve 'Uygula'ya basın.";

    /// <summary>Seçili araca ve duruma göre yol gösterici metni döndürür.</summary>
    public static string GetHint(MeasurePoint? armedPoint, bool hasKnownLength, bool hasReferenceLength)
    {
        if (armedPoint is { } armed)
        {
            return armed switch
            {
                MeasurePoint.R1 => ReferenceStart,
                MeasurePoint.R2 => ReferenceEnd,
                MeasurePoint.M1 => MeasureStart,
                MeasurePoint.M2 => MeasureEnd,
                _ => Idle
            };
        }

        if (hasReferenceLength && hasKnownLength)
        {
            return "Referans hazır. 'M — Mesafe Ölç' ile ölçüme başlayabilirsiniz.";
        }

        return Idle;
    }
}
