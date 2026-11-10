namespace OlcuAtolyesi.Models;

public enum DemoProjectType
{
    ConnectionPlate,
    TableMeasurement,
    GasketSketch,
    PhotoMeasurement,
    Other
}

public static class DemoProjectTypeExtensions
{
    public static string DisplayName(this DemoProjectType type) => type switch
    {
        DemoProjectType.ConnectionPlate => "Bağlantı plakası",
        DemoProjectType.TableMeasurement => "Masa ölçüsü",
        DemoProjectType.GasketSketch => "Conta taslağı",
        DemoProjectType.PhotoMeasurement => "Fotoğraf ölçümü",
        DemoProjectType.Other => "Diğer",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Desteklenmeyen proje türü.")
    };
}

/// <summary>Kalıcı veya oturumluk olarak saklanabilen proje kaydı.</summary>
public sealed record DemoProject
{
    public DemoProject(
        Guid id,
        string name,
        DemoProjectType type,
        string description,
        IReadOnlyList<ProjectMeasurement>? measurements,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isSample,
        string? photoRelativePath = null,
        ImageSize? photoSize = null,
        ImageSegment? referenceSegment = null,
        LengthValue? referenceLength = null,
        LengthUnit? displayUnit = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Proje kimliği boş olamaz.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Proje adı boş olamaz.", nameof(name));
        }

        if (referenceSegment is { } refSeg && (!refSeg.IsFinite || refSeg.ImageLength <= 0d))
        {
            throw new ArgumentException("Referans segmenti geçerli ve sıfırdan büyük olmalıdır.", nameof(referenceSegment));
        }

        if (referenceLength is { } refLen && !refLen.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceLength), "Referans uzunluğu sıfırdan büyük olmalıdır.");
        }

        Id = id;
        Name = name.Trim();
        Type = type;
        Description = description?.Trim() ?? string.Empty;
        Measurements = Array.AsReadOnly((measurements ?? []).ToArray());
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        IsSample = isSample;
        PhotoRelativePath = string.IsNullOrWhiteSpace(photoRelativePath) ? null : photoRelativePath.Trim();
        PhotoSize = photoSize;
        ReferenceSegment = referenceSegment;
        ReferenceLength = referenceLength;
        DisplayUnit = displayUnit;
    }

    public Guid Id { get; }

    public string Name { get; }

    public DemoProjectType Type { get; }

    public string Description { get; }

    public IReadOnlyList<ProjectMeasurement> Measurements { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    /// <summary>Uygulamayla gelen örnek kayıt olduğunu belirtir; gerçek ölçüm iddiası değildir.</summary>
    public bool IsSample { get; }

    /// <summary>Uygulama veri klasörüne göre kalıcı fotoğraf dosyasının göreli yolu.</summary>
    public string? PhotoRelativePath { get; }

    /// <summary>Fotoğrafın veya ölçüm tuvalinin piksel/birim boyutu.</summary>
    public ImageSize? PhotoSize { get; }

    /// <summary>Fotoğraf üzerindeki R1–R2 referans noktaları.</summary>
    public ImageSegment? ReferenceSegment { get; }

    /// <summary>R1–R2 referansının bilinen gerçek uzunluğu ve birimi.</summary>
    public LengthValue? ReferenceLength { get; }

    /// <summary>Ölçüm ekranında seçili gösterim birimi.</summary>
    public LengthUnit? DisplayUnit { get; }

    public bool HasSavedMeasurementWorkspace =>
        ReferenceSegment is not null || Type == DemoProjectType.PhotoMeasurement;
}
