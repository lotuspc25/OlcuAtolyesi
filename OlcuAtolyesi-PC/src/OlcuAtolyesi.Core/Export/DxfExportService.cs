using System.Text;
using OlcuAtolyesi.Core.Models;

namespace OlcuAtolyesi.Core.Export;

/// <summary>Kalıcı ölçüm projesinden DXF dosyası üretir.</summary>
public sealed class DxfExportService
{
    /// <summary>Projeden DXF içeriği üretir; referans yoksa hata döner.</summary>
    public DxfExportResult BuildFromProject(DesktopProject project)
    {
        if (project is null)
        {
            return DxfExportResult.Fail("Proje boş olamaz.");
        }

        if (project.ReferenceLength is not { IsPositive: true } referenceLength)
        {
            return DxfExportResult.Fail("DXF oluşturmak için projede R1–R2 referans uzunluğu bulunmalıdır.");
        }

        var request = new DxfExportRequest(
            project.Name,
            project.PhotoSize,
            project.ReferenceSegment,
            referenceLength,
            project.MeasurementSegment,
            project.MeasuredLength);

        return DxfBuilder.Build(request);
    }

    /// <summary>DXF içeriğini UTF-8 (BOM'suz) olarak dosyaya yazar.</summary>
    public void SaveToFile(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Dosya yolu boş olamaz.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
