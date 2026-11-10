using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OlcuAtolyesi.Core.Diagnostics;

/// <summary>
/// Ölçü Atölyesi merkezi tanılama konsolu ve günlük kayıtları için ortak olay modeli.
/// </summary>
public sealed record DiagnosticLogEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    [JsonPropertyName("level")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagnosticLevel Level { get; init; } = DiagnosticLevel.Info;

    [JsonPropertyName("source")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagnosticSource Source { get; init; } = DiagnosticSource.PC;

    [JsonPropertyName("module")]
    public string Module { get; init; } = string.Empty;

    [JsonPropertyName("eventCode")]
    public string EventCode { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    /// <summary>
    /// Okunabilir satır formatına dönüştürür.
    /// Format: [yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [SOURCE] [MODULE/EVENT_CODE] Message (CID: xxx)
    /// </summary>
    public string FormatLine()
    {
        var timeStr = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var levelStr = Level.ToString().ToUpperInvariant().PadRight(8);
        var sourceStr = Source.ToString().PadRight(7);
        var cidStr = string.IsNullOrWhiteSpace(CorrelationId) ? string.Empty : $" [CID:{CorrelationId}]";

        var baseLine = $"[{timeStr}] [{levelStr}] [{sourceStr}] [{Module}/{EventCode}]{cidStr} {Message}";

        if (!string.IsNullOrWhiteSpace(Details))
        {
            baseLine += Environment.NewLine + "    " + Details.Replace(Environment.NewLine, Environment.NewLine + "    ");
        }

        return baseLine;
    }

    /// <summary>
    /// JSON formatına dönüştürür.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// JSON metninden model oluşturur.
    /// </summary>
    public static DiagnosticLogEntry? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<DiagnosticLogEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Hassas verileri maskelenmiş yeni bir kopya üretir.
    /// </summary>
    public DiagnosticLogEntry CreateMasked()
    {
        return this with
        {
            Message = DiagnosticDataMasker.Mask(Message),
            Details = string.IsNullOrEmpty(Details) ? null : DiagnosticDataMasker.Mask(Details)
        };
    }
}
