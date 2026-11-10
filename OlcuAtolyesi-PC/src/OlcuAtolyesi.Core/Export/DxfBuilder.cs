using System.Globalization;
using System.Text;
using OlcuAtolyesi.Core.Geometry;

namespace OlcuAtolyesi.Core.Export;

/// <summary>
/// Ölçüm verisinden 2B DXF (R12 ASCII, AC1009) içeriği üretir.
/// Tüm koordinatlar milimetre cinsindendir ve $INSUNITS=4 olarak bildirilir.
/// Görüntü koordinatında Y aşağı, DXF'te Y yukarı olduğu için Y ekseni ters çevrilir.
/// </summary>
public static class DxfBuilder
{
    public const string ReferenceLayer = "REFERENCE";
    public const string MeasureLayer = "MEASURE";
    public const string LabelLayer = "LABELS";

    private const double MinimumReferencePixels = 0.0001d;

    public static DxfExportResult Build(DxfExportRequest request)
    {
        if (request is null)
        {
            return DxfExportResult.Fail("DXF isteği boş olamaz.");
        }

        if (request.ReferenceLength is not { IsPositive: true })
        {
            return DxfExportResult.Fail("DXF oluşturmak için önce R1–R2 referans uzunluğu belirlenmelidir.");
        }

        if (!request.ReferenceSegment.IsFinite || request.ReferenceSegment.ImageLength <= MinimumReferencePixels)
        {
            return DxfExportResult.Fail("Referans çizgisi (R1–R2) geçersiz; iki farklı nokta seçilmelidir.");
        }

        if (!request.MeasurementSegment.IsFinite)
        {
            return DxfExportResult.Fail("Ölçüm çizgisi (M1–M2) geçersiz koordinat içeriyor.");
        }

        if (!request.ImageSize.IsValid)
        {
            return DxfExportResult.Fail("Fotoğraf boyutu geçersiz.");
        }

        var millimetresPerPixel = request.ReferenceLength!.Value.Millimeters / request.ReferenceSegment.ImageLength;

        var sb = new StringBuilder(4096);
        WriteHeader(sb);
        WriteTables(sb);
        WriteEntities(sb, request, millimetresPerPixel);
        WriteFooter(sb);

        return DxfExportResult.Ok(sb.ToString());
    }

    /// <summary>Türkçe karakterleri DXF R12 uyumlu ASCII karşılıklarına çevirir.</summary>
    public static string ToAsciiText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                'ı' => 'i', 'İ' => 'I',
                'ş' => 's', 'Ş' => 'S',
                'ğ' => 'g', 'Ğ' => 'G',
                'ü' => 'u', 'Ü' => 'U',
                'ö' => 'o', 'Ö' => 'O',
                'ç' => 'c', 'Ç' => 'C',
                _ => ch
            });
        }

        var result = sb.ToString();
        var ascii = new StringBuilder(result.Length);
        foreach (var ch in result)
        {
            ascii.Append(ch <= 126 && ch >= 32 ? ch : '?');
        }

        return ascii.ToString().Trim();
    }

    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static (double X, double Y) ToDxf(ImagePoint point, ImageSize size, double mmPerPixel) =>
        (point.X * mmPerPixel, (size.Height - point.Y) * mmPerPixel);

    private static void WriteHeader(StringBuilder sb)
    {
        sb.Append("0\nSECTION\n2\nHEADER\n");
        sb.Append("9\n$ACADVER\n1\nAC1009\n");
        sb.Append("9\n$INSUNITS\n70\n4\n");
        sb.Append("9\n$MEASUREMENT\n70\n1\n");
        sb.Append("0\nENDSEC\n");
    }

    private static void WriteTables(StringBuilder sb)
    {
        sb.Append("0\nSECTION\n2\nTABLES\n");

        // LINE TYPE
        sb.Append("0\nTABLE\n2\nLTYPE\n70\n1\n");
        sb.Append("0\nLTYPE\n2\nCONTINUOUS\n70\n64\n3\nSolid line\n72\n65\n73\n0\n40\n0.0\n");
        sb.Append("0\nENDTAB\n");

        // LAYERS (renk: 1 kırmızı, 5 mavi, 7 beyaz)
        sb.Append("0\nTABLE\n2\nLAYER\n70\n3\n");
        sb.Append("0\nLAYER\n2\n").Append(ReferenceLayer).Append("\n70\n0\n62\n1\n6\nCONTINUOUS\n");
        sb.Append("0\nLAYER\n2\n").Append(MeasureLayer).Append("\n70\n0\n62\n5\n6\nCONTINUOUS\n");
        sb.Append("0\nLAYER\n2\n").Append(LabelLayer).Append("\n70\n0\n62\n7\n6\nCONTINUOUS\n");
        sb.Append("0\nENDTAB\n");

        sb.Append("0\nENDSEC\n");
    }

    private static void WriteEntities(StringBuilder sb, DxfExportRequest request, double mmPerPixel)
    {
        sb.Append("0\nSECTION\n2\nENTITIES\n");

        var r1 = ToDxf(request.ReferenceSegment.Start, request.ImageSize, mmPerPixel);
        var r2 = ToDxf(request.ReferenceSegment.End, request.ImageSize, mmPerPixel);
        var m1 = ToDxf(request.MeasurementSegment.Start, request.ImageSize, mmPerPixel);
        var m2 = ToDxf(request.MeasurementSegment.End, request.ImageSize, mmPerPixel);

        WriteLine(sb, ReferenceLayer, r1, r2);
        WriteLine(sb, MeasureLayer, m1, m2);

        WriteCircle(sb, ReferenceLayer, r1, 0.8d);
        WriteCircle(sb, ReferenceLayer, r2, 0.8d);
        WriteCircle(sb, MeasureLayer, m1, 0.8d);
        WriteCircle(sb, MeasureLayer, m2, 0.8d);

        var referenceLabel = $"R: {N(request.ReferenceLength!.Value.Millimeters)} mm";
        var measurementLabel = request.MeasuredLength is { } measured
            ? $"M: {N(measured.Millimeters)} mm"
            : "M: -";

        WriteText(sb, LabelLayer, Midpoint(r1, r2), referenceLabel);
        WriteText(sb, LabelLayer, Midpoint(m1, m2), measurementLabel);

        var projectLabel = ToAsciiText(request.ProjectName);
        if (projectLabel.Length > 0)
        {
            WriteText(sb, LabelLayer, (0d, 0d), projectLabel);
        }

        sb.Append("0\nENDSEC\n");
    }

    private static (double X, double Y) Midpoint((double X, double Y) a, (double X, double Y) b) =>
        ((a.X + b.X) / 2d, (a.Y + b.Y) / 2d);

    private static void WriteLine(StringBuilder sb, string layer, (double X, double Y) start, (double X, double Y) end)
    {
        sb.Append("0\nLINE\n8\n").Append(layer).Append('\n');
        sb.Append("10\n").Append(N(start.X)).Append("\n20\n").Append(N(start.Y)).Append("\n30\n0.0\n");
        sb.Append("11\n").Append(N(end.X)).Append("\n21\n").Append(N(end.Y)).Append("\n31\n0.0\n");
    }

    private static void WriteCircle(StringBuilder sb, string layer, (double X, double Y) center, double radius)
    {
        sb.Append("0\nCIRCLE\n8\n").Append(layer).Append('\n');
        sb.Append("10\n").Append(N(center.X)).Append("\n20\n").Append(N(center.Y)).Append("\n30\n0.0\n");
        sb.Append("40\n").Append(N(radius)).Append('\n');
    }

    private static void WriteText(StringBuilder sb, string layer, (double X, double Y) position, string value)
    {
        sb.Append("0\nTEXT\n8\n").Append(layer).Append('\n');
        sb.Append("10\n").Append(N(position.X)).Append("\n20\n").Append(N(position.Y)).Append("\n30\n0.0\n");
        sb.Append("40\n2.5\n");
        sb.Append("1\n").Append(value).Append('\n');
    }

    private static void WriteFooter(StringBuilder sb)
    {
        sb.Append("0\nEOF\n");
    }
}
