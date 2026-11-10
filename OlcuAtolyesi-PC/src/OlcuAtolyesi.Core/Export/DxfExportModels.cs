using OlcuAtolyesi.Core.Geometry;

namespace OlcuAtolyesi.Core.Export;

/// <summary>DXF üretimi için gerekli ölçüm verisi. Görünümden bağımsızdır.</summary>
public sealed record DxfExportRequest(
    string ProjectName,
    ImageSize ImageSize,
    ImageSegment ReferenceSegment,
    LengthValue? ReferenceLength,
    ImageSegment MeasurementSegment,
    LengthValue? MeasuredLength);

/// <summary>DXF üretim sonucu.</summary>
public sealed record DxfExportResult(bool Success, string? Content, string Message)
{
    public static DxfExportResult Ok(string content) => new(true, content, "DXF içeriği üretildi.");

    public static DxfExportResult Fail(string message) => new(false, null, message);
}
