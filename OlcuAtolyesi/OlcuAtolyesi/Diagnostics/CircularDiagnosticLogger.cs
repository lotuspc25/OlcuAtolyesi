using System.Text;

namespace OlcuAtolyesi.Diagnostics;

/// <summary>
/// Android uygulama içi döngüsel tanılama günlük yazıcısı.
/// </summary>
public sealed class CircularDiagnosticLogger : IDisposable
{
    private const string CurrentLogFileName = "diagnostic_current.log";
    private const string MarkerFileName = "session_active.marker";

    private readonly object _syncRoot = new();
    private readonly string _logDirectory;
    private readonly long _maxFileSize;
    private readonly int _maxArchivedFiles;
    private readonly string _currentLogPath;
    private readonly string _markerPath;
    private bool _isDisposed;

    public string LogDirectory => _logDirectory;
    public string CurrentLogPath => _currentLogPath;
    public bool PreviousSessionEndedAbnormally { get; private set; }

    public CircularDiagnosticLogger(
        string logDirectory,
        long maxFileSize = 2 * 1024 * 1024,
        int maxArchivedFiles = 5)
    {
        _logDirectory = logDirectory;
        _maxFileSize = maxFileSize;
        _maxArchivedFiles = maxArchivedFiles;

        Directory.CreateDirectory(_logDirectory);

        _currentLogPath = Path.Combine(_logDirectory, CurrentLogFileName);
        _markerPath = Path.Combine(_logDirectory, MarkerFileName);

        CheckAndInitSession();
    }

    private void CheckAndInitSession()
    {
        lock (_syncRoot)
        {
            try
            {
                if (File.Exists(_markerPath))
                {
                    PreviousSessionEndedAbnormally = true;
                }

                var sessionInfo = $"SessionStart={DateTimeOffset.Now:O};PID={Environment.ProcessId}";
                File.WriteAllText(_markerPath, sessionInfo, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    public void CleanShutdown()
    {
        lock (_syncRoot)
        {
            try
            {
                if (File.Exists(_markerPath))
                {
                    File.Delete(_markerPath);
                }
            }
            catch
            {
            }
        }
    }

    public void Write(DiagnosticLogEntry entry)
    {
        if (_isDisposed) return;

        var masked = entry.CreateMasked();
        var line = masked.ToJson();

        lock (_syncRoot)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_currentLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_currentLogPath))
        {
            return;
        }

        var fileInfo = new FileInfo(_currentLogPath);
        if (fileInfo.Length < _maxFileSize)
        {
            return;
        }

        for (int i = _maxArchivedFiles - 1; i >= 1; i--)
        {
            var src = Path.Combine(_logDirectory, $"diagnostic_{i}.log");
            var dst = Path.Combine(_logDirectory, $"diagnostic_{i + 1}.log");

            if (File.Exists(src))
            {
                try
                {
                    if (File.Exists(dst))
                    {
                        File.Delete(dst);
                    }
                    File.Move(src, dst);
                }
                catch
                {
                }
            }
        }

        var archiveFirst = Path.Combine(_logDirectory, "diagnostic_1.log");
        try
        {
            if (File.Exists(archiveFirst))
            {
                File.Delete(archiveFirst);
            }
            File.Move(_currentLogPath, archiveFirst);
        }
        catch
        {
        }
    }

    public IReadOnlyList<DiagnosticLogEntry> ReadRecentEntries(int maxEntries = 500)
    {
        lock (_syncRoot)
        {
            var result = new List<DiagnosticLogEntry>();
            if (!File.Exists(_currentLogPath))
            {
                return result;
            }

            try
            {
                var lines = File.ReadAllLines(_currentLogPath, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - maxEntries);

                for (int i = start; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var entry = DiagnosticLogEntry.FromJson(line);
                    if (entry != null)
                    {
                        result.Add(entry);
                    }
                }
            }
            catch
            {
            }

            return result;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
    }
}
