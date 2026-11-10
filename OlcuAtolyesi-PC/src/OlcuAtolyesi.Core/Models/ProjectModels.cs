using System.Globalization;
using OlcuAtolyesi.Core.Geometry;

namespace OlcuAtolyesi.Core.Models;

/// <summary>Mobil v0.4.0 MeasurementKind numaralandırmasıyla birebir uyumlu ölçüm türü.</summary>
public enum MeasurementKind
{
    Length = 0,
    Angle = 1,
    Diameter = 2,
    Radius = 3,
    Perimeter = 4,
    Area = 5,
    CameraDistance = 6,
    Height = 7
}

/// <summary>Mobil v0.4.0 DemoProjectType numaralandırmasıyla birebir uyumlu proje türü.</summary>
public enum ProjectType
{
    ConnectionPlate = 0,
    TableMeasurement = 1,
    GasketSketch = 2,
    PhotoMeasurement = 3,
    Other = 4
}

public static class ProjectTypeExtensions
{
    public static string DisplayName(this ProjectType type) => type switch
    {
        ProjectType.ConnectionPlate => "Bağlantı plakası",
        ProjectType.TableMeasurement => "Masa ölçüsü",
        ProjectType.GasketSketch => "Conta taslağı",
        ProjectType.PhotoMeasurement => "Fotoğraf ölçümü",
        ProjectType.Other => "Diğer",
        _ => "Fotoğraf ölçümü"
    };
}

/// <summary>Proje içindeki tekil ölçüm kaydı (mobil ProjectMeasurement ile uyumlu).</summary>
public sealed record ProjectMeasurementItem
{
    public ProjectMeasurementItem(
        Guid id,
        string name,
        MeasurementKind kind,
        LengthValue? length = null,
        double? angleDegrees = null,
        ImageSegment? imageSegment = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Ölçüm kimliği boş olamaz.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Ölçüm adı boş olamaz.", nameof(name));
        }

        if (angleDegrees is not null && (!double.IsFinite(angleDegrees.Value) || angleDegrees.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        Length = length;
        AngleDegrees = angleDegrees;
        ImageSegment = imageSegment;
    }

    public Guid Id { get; }

    public string Name { get; }

    public MeasurementKind Kind { get; }

    public LengthValue? Length { get; }

    public double? AngleDegrees { get; }

    public ImageSegment? ImageSegment { get; }
}

/// <summary>İçe aktarma sırasında aynı proje kimliğiyle karşılaşıldığında uygulanacak çakışma politikası.</summary>
public enum ProjectConflictPolicy
{
    /// <summary>Mevcut projeyi korur ve içe aktarılan projeye yeni kimlik vererek kopya olarak ekler.</summary>
    ImportAsCopy = 0,

    /// <summary>Mevcut projenin üzerine yazar.</summary>
    OverwriteExisting = 1,

    /// <summary>Mevcut projeyi korur ve içe aktarmayı atlar.</summary>
    SkipIfExists = 2
}

public static class ProjectConflictPolicyExtensions
{
    public static string DisplayName(this ProjectConflictPolicy policy) => policy switch
    {
        ProjectConflictPolicy.ImportAsCopy => "Kopya Olarak Ekle (Mevcut Korunur)",
        ProjectConflictPolicy.OverwriteExisting => "Mevcut Projenin Üzerine Yaz",
        ProjectConflictPolicy.SkipIfExists => "Mevcut Varsa Atla",
        _ => "Kopya Olarak Ekle"
    };
}

/// <summary>
/// Masaüstü ve mobil (v0.4.0) arasında uyumlu kalıcı ölçüm projesi kaydı.
/// Tüm noktalar fotoğrafın özgün piksel koordinatlarında (ImagePoint / ImageSegment) tutulur.
/// </summary>
public sealed record DesktopProject
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public DesktopProject(
        Guid id,
        string name,
        ProjectType type,
        string description,
        IReadOnlyList<ProjectMeasurementItem>? measurements,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isSample,
        string? photoRelativePath,
        ImageSize photoSize,
        ImageSegment referenceSegment,
        LengthValue? referenceLength,
        ImageSegment measurementSegment,
        LengthValue? measuredLength,
        LengthUnit displayUnit,
        string? originalPhotoFileName = null,
        string sourcePlatform = "PC")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Proje kimliği boş olamaz.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Proje adı boş olamaz.", nameof(name));
        }

        if (!photoSize.IsValid)
        {
            throw new ArgumentException("Fotoğraf boyutu geçerli ve sıfırdan büyük olmalıdır.", nameof(photoSize));
        }

        if (!referenceSegment.IsFinite || referenceSegment.ImageLength <= 0d)
        {
            throw new ArgumentException("Referans segmenti (R1–R2) geçerli ve sıfırdan büyük olmalıdır.", nameof(referenceSegment));
        }

        if (!measurementSegment.IsFinite)
        {
            throw new ArgumentException("Ölçüm segmenti (M1–M2) sonlu koordinatlar içermelidir.", nameof(measurementSegment));
        }

        if (referenceLength is { } refLen && !refLen.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceLength), "Referans uzunluğu sıfırdan büyük olmalıdır.");
        }

        Id = id;
        Name = name.Trim();
        Type = Enum.IsDefined(type) ? type : ProjectType.PhotoMeasurement;
        Description = description?.Trim() ?? string.Empty;
        Measurements = Array.AsReadOnly((measurements ?? []).ToArray());
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        IsSample = isSample;
        PhotoRelativePath = string.IsNullOrWhiteSpace(photoRelativePath) ? null : photoRelativePath.Trim();
        OriginalPhotoFileName = string.IsNullOrWhiteSpace(originalPhotoFileName) ? null : originalPhotoFileName.Trim();
        PhotoSize = photoSize;
        ReferenceSegment = referenceSegment;
        ReferenceLength = referenceLength;
        MeasurementSegment = measurementSegment;
        DisplayUnit = Enum.IsDefined(displayUnit) ? displayUnit : LengthUnit.Millimeter;
        SourcePlatform = string.IsNullOrWhiteSpace(sourcePlatform) ? "PC" : sourcePlatform.Trim();

        // Referans geçerliyse ölçüm sonucunu piksel koordinatlarından deterministik olarak doğrula/hesapla
        if (referenceLength is { IsPositive: true } validRef && referenceSegment.ImageLength > 0.0001d)
        {
            var scale = new ReferenceScale(referenceSegment, validRef);
            MeasuredLength = scale.Measure(measurementSegment, DisplayUnit);
        }
        else
        {
            MeasuredLength = measuredLength;
        }
    }

    public Guid Id { get; }

    public string Name { get; }

    public ProjectType Type { get; }

    public string Description { get; }

    public IReadOnlyList<ProjectMeasurementItem> Measurements { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public bool IsSample { get; }

    /// <summary>Proje çalışma alanına göre fotoğrafın göreli yolu (ör. projects/&lt;id&gt;/photo.jpg).</summary>
    public string? PhotoRelativePath { get; }

    /// <summary>Kullanıcının seçtiği özgün fotoğraf dosya adı.</summary>
    public string? OriginalPhotoFileName { get; }

    /// <summary>Fotoğrafın veya çalışma yüzeyinin özgün piksel boyutu.</summary>
    public ImageSize PhotoSize { get; }

    /// <summary>Fotoğraf piksel koordinatlarında R1–R2 referans doğrusu.</summary>
    public ImageSegment ReferenceSegment { get; }

    /// <summary>R1–R2 referansının bilinen gerçek uzunluğu ve birimi.</summary>
    public LengthValue? ReferenceLength { get; }

    /// <summary>Fotoğraf piksel koordinatlarında M1–M2 ölçüm doğrusu.</summary>
    public ImageSegment MeasurementSegment { get; }

    /// <summary>Hesaplanan M1–M2 gerçek uzunluğu.</summary>
    public LengthValue? MeasuredLength { get; }

    /// <summary>Seçili gösterim birimi.</summary>
    public LengthUnit DisplayUnit { get; }

    /// <summary>Kaydın oluşturulduğu veya içe aktarıldığı kaynak ("PC" veya "Mobil v0.4.0").</summary>
    public string SourcePlatform { get; }

    public bool HasPhotoPath => !string.IsNullOrWhiteSpace(PhotoRelativePath);

    public string TypeDisplayName => Type.DisplayName();

    public string FormattedResult => MeasuredLength?.Format(TurkishCulture) ?? "—";

    public string FormattedReference => ReferenceLength?.Format(TurkishCulture) ?? "—";

    public string PhotoDimensionsSummary => $"{PhotoSize.Width:0} × {PhotoSize.Height:0} px";
}

public sealed record ProjectImportResult(
    bool Success,
    DesktopProject? Project,
    string Message,
    bool WasConflictResolved = false,
    bool PhotoMissingWarning = false);

public sealed record ProjectExportResult(
    bool Success,
    string? OutputPath,
    string Message);
