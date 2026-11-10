using System.Diagnostics;
using System.Text;

namespace OlcuAtolyesi.Core.Phone;

/// <summary>Cihaz seri numaralarını ve ADB argümanlarını komut/bayrak enjeksiyonuna karşı doğrular.</summary>
public static class AdbInputValidator
{
    public const int MaxSerialLength = 128;

    /// <summary>
    /// Geçerli bir ADB cihaz seri numarası olup olmadığını kontrol eder.
    /// Boşluk, kabuk meta karakterleri veya '-' ile başlayan bayrak enjeksiyonlarını reddeder.
    /// </summary>
    public static bool IsValidSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return false;
        }

        var trimmed = serial.Trim();
        if (trimmed.Length != serial.Length || trimmed.Length == 0 || trimmed.Length > MaxSerialLength)
        {
            return false;
        }

        if (trimmed.StartsWith('-'))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            var allowed = char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or ':' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}

public interface IAdbLocator
{
    /// <summary>Bilgisayarda kurulu ADB yürütülebilir dosyasının tam yolunu bulur; bulunamazsa null döner.</summary>
    string? FindAdbExecutablePath();
}

/// <summary>
/// PATH ortam değişkeni, ANDROID_HOME / ANDROID_SDK_ROOT ve bilinen Windows Android SDK dizinlerinde
/// güvenli biçimde <c>adb.exe</c> arar.
/// </summary>
public sealed class AdbLocator : IAdbLocator
{
    private readonly string? _customAdbPath;
    private readonly IReadOnlyList<string>? _customSearchDirectories;
    private readonly bool _useSystemPaths;

    public AdbLocator(
        string? customAdbPath = null,
        IReadOnlyList<string>? customSearchDirectories = null,
        bool useSystemPaths = true)
    {
        _customAdbPath = customAdbPath;
        _customSearchDirectories = customSearchDirectories;
        _useSystemPaths = useSystemPaths;
    }

    public string? FindAdbExecutablePath()
    {
        var exeName = OperatingSystem.IsWindows() ? "adb.exe" : "adb";

        if (!string.IsNullOrWhiteSpace(_customAdbPath))
        {
            try
            {
                var fullCustom = Path.GetFullPath(_customAdbPath);
                if (File.Exists(fullCustom))
                {
                    return fullCustom;
                }
            }
            catch
            {
            }
        }

        if (_customSearchDirectories is not null)
        {
            foreach (var dir in _customSearchDirectories)
            {
                if (TryFindInDirectory(dir, exeName, out var foundCustomDir))
                {
                    return foundCustomDir;
                }
            }
        }

        if (!_useSystemPaths)
        {
            return null;
        }

        foreach (var dir in EnumerateCandidateDirectories())
        {
            if (TryFindInDirectory(dir, exeName, out var found))
            {
                return found;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> EnumerateCandidateDirectories()
    {
        var list = new List<string>();

        // 1. ANDROID_HOME ve ANDROID_SDK_ROOT
        foreach (var envVar in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT", "ANDROID_SDK" })
        {
            var sdkRoot = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(sdkRoot))
            {
                list.Add(Path.Combine(sdkRoot, "platform-tools"));
            }
        }

        // 2. PATH ortam değişkenindeki dizinler
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleaned = entry.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    list.Add(cleaned);
                }
            }
        }

        // 3. Bilinen standart Windows Android SDK Platform-Tools dizinleri
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            list.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools"));
            list.Add(Path.Combine(localAppData, "Android", "android-sdk", "platform-tools"));

            var wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetPackages))
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(wingetPackages, "*scrcpy*", SearchOption.AllDirectories))
                    {
                        list.Add(dir);
                    }
                }
                catch
                {
                }
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            list.Add(Path.Combine(userProfile, "AppData", "Local", "Android", "Sdk", "platform-tools"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            list.Add(Path.Combine(programFilesX86, "Android", "android-sdk", "platform-tools"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            list.Add(Path.Combine(programFiles, "Android", "android-sdk", "platform-tools"));
        }

        list.Add(@"C:\Android\Sdk\platform-tools");
        list.Add(@"C:\Android\platform-tools");

        return list;
    }

    private static bool TryFindInDirectory(string? directory, string exeName, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(directory, exeName));
            if (File.Exists(candidate))
            {
                fullPath = candidate;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}

public interface IAdbCommandRunner
{
    /// <summary>ADB yürütülebilir dosyasının tam yolunu döndürür; bulunamazsa null döner.</summary>
    string? GetAdbExecutablePath();

    /// <summary>
    /// ADB komutunu kabuk (shell) kullanmadan güvenli argüman listesiyle, zaman aşımı ve iptal desteğiyle çalıştırır.
    /// </summary>
    Task<AdbCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gerçek <c>adb.exe</c> sürecini <see cref="ProcessStartInfo.ArgumentList"/> kullanarak
/// kabuksuz, güvenli ve zaman aşımlı biçimde çalıştıran uygulama.
/// </summary>
public sealed class ProcessAdbCommandRunner : IAdbCommandRunner
{
    private readonly IAdbLocator _locator;

    public ProcessAdbCommandRunner(IAdbLocator? locator = null)
    {
        _locator = locator ?? new AdbLocator();
    }

    public string? GetAdbExecutablePath() => _locator.FindAdbExecutablePath();

    public async Task<AdbCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var adbPath = _locator.FindAdbExecutablePath();
        if (string.IsNullOrWhiteSpace(adbPath) || !File.Exists(adbPath))
        {
            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                FailureReason: "Bilgisayarda ADB (Android Debug Bridge) yürütülebilir dosyası bulunamadı.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in arguments)
        {
            if (arg is not null)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new AdbCommandResult(-1, string.Empty, string.Empty, FailureReason: "ADB süreci başlatılamadı.");
            }
        }
        catch (Exception ex)
        {
            return new AdbCommandResult(-1, string.Empty, ex.Message, FailureReason: $"ADB çalıştırılamadı: {ex.Message}");
        }

        var effectiveTimeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : timeout;
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new AdbCommandResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            if (cancellationToken.IsCancellationRequested)
            {
                return new AdbCommandResult(
                    -1,
                    string.Empty,
                    string.Empty,
                    IsCancelled: true,
                    FailureReason: "ADB komutu kullanıcı veya sistem tarafından iptal edildi.");
            }

            return new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                IsTimedOut: true,
                FailureReason: $"ADB komutu {effectiveTimeout.TotalSeconds:0.#} saniye içinde yanıt vermedi (zaman aşımı).");
        }
        catch (Exception ex)
        {
            TryKillProcess(process);
            return new AdbCommandResult(-1, string.Empty, ex.Message, FailureReason: $"ADB komutu sırasında hata oluştu: {ex.Message}");
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
