using System.Collections.Concurrent;
using System.Text;

namespace OlcuAtolyesi.Core.Diagnostics;

/// <summary>
/// Uygulama genelinde tanılama olaylarını toplayan, canlı konsola dağıtan
/// ve diske kalıcı olarak kaydeden merkezi tanılama merkezi.
/// </summary>
public sealed class DiagnosticLogHub
{
    private static readonly Lazy<DiagnosticLogHub> LazyInstance = new(() =>
    {
        var defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OlcuAtolyesi",
            "Logs");
        return new DiagnosticLogHub(new CircularDiagnosticLogger(defaultDir));
    });

    public static DiagnosticLogHub Instance => LazyInstance.Value;

    private readonly object _lock = new();
    private readonly CircularDiagnosticLogger _fileLogger;
    private readonly ConcurrentQueue<DiagnosticLogEntry> _recentBuffer = new();
    private const int MaxInMemoryBuffer = 1500;

    public event EventHandler<DiagnosticLogEntry>? EntryLogged;

    public CircularDiagnosticLogger FileLogger => _fileLogger;
    public string LogDirectory => _fileLogger.LogDirectory;
    public bool PreviousSessionEndedAbnormally => _fileLogger.PreviousSessionEndedAbnormally;

    public DiagnosticLogHub(CircularDiagnosticLogger fileLogger)
    {
        _fileLogger = fileLogger;

        if (_fileLogger.PreviousSessionEndedAbnormally)
        {
            Log(
                DiagnosticLevel.Warn,
                "AppLifecycle",
                "PREV_SESSION_ABNORMAL_TERMINATION",
                "Önceki uygulama oturumu beklenmedik biçimde sonlanmış görünüyor (olası çökme veya zorla kapatma).");
        }
    }

    /// <summary>
    /// Yeni bir tanılama olayı kaydeder ve dağıtır.
    /// </summary>
    public DiagnosticLogEntry Log(
        DiagnosticLevel level,
        string module,
        string eventCode,
        string message,
        string? correlationId = null,
        string? details = null,
        DiagnosticSource source = DiagnosticSource.PC)
    {
        var entry = new DiagnosticLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Source = source,
            Module = module,
            EventCode = eventCode,
            Message = message,
            CorrelationId = correlationId,
            Details = details
        };

        var masked = entry.CreateMasked();

        // 1. Dosyaya yaz
        _fileLogger.Write(masked);

        // 2. Bellek tamponuna ekle
        _recentBuffer.Enqueue(masked);
        while (_recentBuffer.Count > MaxInMemoryBuffer && _recentBuffer.TryDequeue(out _))
        {
        }

        // 3. Canlı dinleyicilere bildir
        try
        {
            EntryLogged?.Invoke(this, masked);
        }
        catch
        {
        }

        return masked;
    }

    /// <summary>
    /// Uzak bir kaynaktan (ör. Android cihaz) gelen log kayıtlarını sisteme dahil eder.
    /// </summary>
    public int IngestRemoteEntries(IEnumerable<DiagnosticLogEntry> entries)
    {
        int count = 0;
        foreach (var item in entries.OrderBy(e => e.Timestamp))
        {
            var remoteItem = item with { Source = DiagnosticSource.Android };
            var masked = remoteItem.CreateMasked();

            _fileLogger.Write(masked);
            _recentBuffer.Enqueue(masked);
            while (_recentBuffer.Count > MaxInMemoryBuffer && _recentBuffer.TryDequeue(out _))
            {
            }

            try
            {
                EntryLogged?.Invoke(this, masked);
            }
            catch
            {
            }

            count++;
        }

        if (count > 0)
        {
            Log(
                DiagnosticLevel.Info,
                "DiagnosticHub",
                "ANDROID_LOGS_INGESTED",
                $"Android cihazından {count} adet tanılama kaydı başarıyla alındı ve konsola aktarıldı.");
        }

        return count;
    }

    /// <summary>
    /// Bellekteki son tanılama kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries()
    {
        return _recentBuffer.ToArray();
    }

    /// <summary>
    /// Tanılama raporunu belirtilen dosya yoluna dışa aktarır.
    /// </summary>
    public void ExportReport(string destinationFilePath, IEnumerable<DiagnosticLogEntry>? specificEntries = null)
    {
        var list = specificEntries?.ToList() ?? _recentBuffer.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($" ÖLÇÜ ATÖLYESİ TANILAMA RAPORU — {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($" Toplam Kayıt: {list.Count}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        foreach (var entry in list)
        {
            sb.AppendLine(entry.FormatLine());
        }

        File.WriteAllText(destinationFilePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Oturumu temiz sonlandırır.
    /// </summary>
    public void CleanShutdown()
    {
        Log(DiagnosticLevel.Event, "AppLifecycle", "APP_CLEAN_SHUTDOWN", "Uygulama oturumu temiz şekilde sonlandırıldı.");
        _fileLogger.CleanShutdown();
    }
}
