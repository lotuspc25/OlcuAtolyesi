using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OlcuAtolyesi.Diagnostics;

/// <summary>
/// Android tarafında üretilen tanılama olay modeli (PC ile birebir eşdeğer JSON şeması).
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
    public DiagnosticSource Source { get; init; } = DiagnosticSource.Android;

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

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

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

    public DiagnosticLogEntry CreateMasked()
    {
        return this with
        {
            Message = DiagnosticDataMasker.Mask(Message),
            Details = string.IsNullOrEmpty(Details) ? null : DiagnosticDataMasker.Mask(Details)
        };
    }
}
