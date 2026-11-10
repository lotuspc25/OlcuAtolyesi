using System.Collections.Concurrent;
using System.Text;

namespace OlcuAtolyesi.Diagnostics;

/// <summary>
/// Android mobil uygulamasında tanılama olaylarını yöneten merkezi servis.
/// </summary>
public sealed class DiagnosticLogHub
{
    private static readonly Lazy<DiagnosticLogHub> LazyInstance = new(() =>
    {
        var logDir = GetDefaultLogDir();
        return new DiagnosticLogHub(new CircularDiagnosticLogger(logDir));
    });

    public static DiagnosticLogHub Instance => LazyInstance.Value;

    private readonly object _lock = new();
    private readonly CircularDiagnosticLogger _fileLogger;
    private readonly ConcurrentQueue<DiagnosticLogEntry> _recentBuffer = new();
    private const int MaxInMemoryBuffer = 1000;

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
                "Önceki Android uygulama oturumu beklenmedik biçimde sonlanmış görünüyor (olası çökme veya sistem tarafından sonlandırma).");
        }
    }

    private static string GetDefaultLogDir()
    {
        try
        {
            var fileSystemType = Type.GetType("Microsoft.Maui.Storage.FileSystem, Microsoft.Maui.Essentials")
                                 ?? Type.GetType("Microsoft.Maui.Storage.FileSystem, Microsoft.Maui");
            if (fileSystemType != null)
            {
                var prop = fileSystemType.GetProperty("AppDataDirectory");
                var dir = prop?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return Path.Combine(dir, "Logs");
                }
            }
        }
        catch
        {
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OlcuAtolyesi",
            "Logs");
    }

    public DiagnosticLogEntry Log(
        DiagnosticLevel level,
        string module,
        string eventCode,
        string message,
        string? correlationId = null,
        string? details = null,
        DiagnosticSource source = DiagnosticSource.Android)
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

        return masked;
    }

    public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 200)
    {
        var array = _recentBuffer.ToArray();
        if (array.Length <= maxCount)
        {
            return array;
        }

        return array.Skip(array.Length - maxCount).ToArray();
    }

    public void CleanShutdown()
    {
        Log(DiagnosticLevel.Event, "AppLifecycle", "APP_CLEAN_SHUTDOWN", "Android oturumu temiz şekilde sonlandırıldı.");
        _fileLogger.CleanShutdown();
    }
}
