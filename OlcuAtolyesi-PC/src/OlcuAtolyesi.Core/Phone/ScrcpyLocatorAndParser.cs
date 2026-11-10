using System.Globalization;
using System.Text.RegularExpressions;

namespace OlcuAtolyesi.Core.Phone;

public interface IScrcpyLocator
{
    /// <summary>Bilgisayarda kurulu scrcpy.exe dosyasının tam yolunu bulur; bulunamazsa null döner.</summary>
    string? FindScrcpyExecutablePath();
}

/// <summary>
/// WinGet paket dizini, PATH ortam değişkeni, Scoop, Chocolatey ve bilinen dizinlerde
/// güvenli biçimde scrcpy.exe arar.
/// </summary>
public sealed class ScrcpyLocator : IScrcpyLocator
{
    private readonly string? _customScrcpyPath;
    private readonly IReadOnlyList<string>? _customSearchDirectories;
    private readonly bool _useSystemPaths;

    public ScrcpyLocator(
        string? customScrcpyPath = null,
        IReadOnlyList<string>? customSearchDirectories = null,
        bool useSystemPaths = true)
    {
        _customScrcpyPath = customScrcpyPath;
        _customSearchDirectories = customSearchDirectories;
        _useSystemPaths = useSystemPaths;
    }

    public string? FindScrcpyExecutablePath()
    {
        var exeName = OperatingSystem.IsWindows() ? "scrcpy.exe" : "scrcpy";

        if (!string.IsNullOrWhiteSpace(_customScrcpyPath))
        {
            try
            {
                var full = Path.GetFullPath(_customScrcpyPath);
                if (File.Exists(full))
                {
                    return full;
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
                if (TryFindInDirectory(dir, exeName, out var foundCustom))
                {
                    return foundCustom;
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

        // 1. WinGet paket dizinleri (Genymobile.scrcpy)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
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

        // 2. PATH ortam değişkeni
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

        // 3. Scoop ve Chocolatey paket dizinleri
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            list.Add(Path.Combine(userProfile, "scoop", "apps", "scrcpy", "current"));
            list.Add(Path.Combine(userProfile, "scoop", "shims"));
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            list.Add(Path.Combine(programData, "chocolatey", "bin"));
            list.Add(Path.Combine(programData, "chocolatey", "lib", "scrcpy", "tools"));
        }

        // 4. Standart kurulum dizinleri
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            list.Add(Path.Combine(programFiles, "scrcpy"));
        }

        list.Add(@"C:\scrcpy");
        list.Add(@"D:\scrcpy");
        list.Add(@"C:\tools\scrcpy");

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

/// <summary>scrcpy komut çıktılarından sürüm, kamera listesi ve çözünürlükleri ayrıştırır.</summary>
public static class ScrcpyOutputParser
{
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1B\[[0-9;]*[a-zA-Z]",
        RegexOptions.Compiled);

    private static readonly Regex VersionRegex = new(
        @"scrcpy\s+v?(\d+(?:\.\d+)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CameraLineRegex = new(
        @"--camera-id=[""']?([^""'\s\(\),]+)[""']?\s*(?:\(([^)]*)\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CameraSizeLineRegex = new(
        @"--camera-size=([0-9]{3,5})x([0-9]{3,5})\s*(?:\(([^)]*)\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResolutionRegex = new(
        @"\b(\d{3,5})x(\d{3,5})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FpsGroupRegex = new(
        @"fps=[{\[]([^}\]]+)[}\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ZoomRangeRegex = new(
        @"zoom-range=\[([^\]]+)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string StripAnsi(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : AnsiEscapeRegex.Replace(text, string.Empty);

    public static ScrcpyVersionInfo ParseVersion(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return ScrcpyVersionInfo.NotFound;
        }

        var clean = StripAnsi(output);
        var match = VersionRegex.Match(clean);
        if (!match.Success)
        {
            return new ScrcpyVersionInfo(clean.Trim(), "Bilinmeyen Sürüm", IsFound: false, IsSupported: false);
        }

        var versionText = match.Groups[1].Value;
        var isSupported = false;
        if (Version.TryParse(versionText, out var parsed))
        {
            isSupported = parsed.Major >= 2;
        }
        else if (int.TryParse(versionText.Split('.')[0], out var major))
        {
            isSupported = major >= 2;
        }

        return new ScrcpyVersionInfo(clean.Trim(), versionText, IsFound: true, IsSupported: isSupported);
    }

    public static IReadOnlyList<ScrcpyCameraInfo> ParseCameraList(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var cameras = new List<ScrcpyCameraInfo>();
        var clean = StripAnsi(output);
        var lines = clean.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var match = CameraLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var id = match.Groups[1].Value.Trim().Trim('"').Trim('\'');
            var details = match.Groups[2].Value.Trim();

            var facing = ScrcpyCameraFacing.Unknown;
            if (details.Contains("back", StringComparison.OrdinalIgnoreCase))
            {
                facing = ScrcpyCameraFacing.Back;
            }
            else if (details.Contains("front", StringComparison.OrdinalIgnoreCase))
            {
                facing = ScrcpyCameraFacing.Front;
            }
            else if (details.Contains("external", StringComparison.OrdinalIgnoreCase))
            {
                facing = ScrcpyCameraFacing.External;
            }

            var nativeWidth = 0;
            var nativeHeight = 0;
            var resMatch = ResolutionRegex.Match(details);
            if (resMatch.Success)
            {
                int.TryParse(resMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeWidth);
                int.TryParse(resMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out nativeHeight);
            }

            var fpsList = new List<int>();
            var fpsMatch = FpsGroupRegex.Match(details);
            if (fpsMatch.Success)
            {
                var fpsTokens = fpsMatch.Groups[1].Value.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var tok in fpsTokens)
                {
                    if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fpsVal) && fpsVal > 0)
                    {
                        fpsList.Add(fpsVal);
                    }
                }
            }

            var minZoom = 1.0;
            var maxZoom = 1.0;
            var zoomMatch = ZoomRangeRegex.Match(details);
            if (zoomMatch.Success)
            {
                ParseZoomRange(zoomMatch.Groups[1].Value, out minZoom, out maxZoom);
            }

            cameras.Add(new ScrcpyCameraInfo(
                Id: id,
                Facing: facing,
                NativeWidth: nativeWidth,
                NativeHeight: nativeHeight,
                SupportedFps: fpsList,
                MinZoom: minZoom,
                MaxZoom: maxZoom,
                RawDetails: details));
        }

        return cameras;
    }

    public static IReadOnlyList<ScrcpyCameraSizeOption> ParseCameraSizes(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var sizes = new List<ScrcpyCameraSizeOption>();
        var clean = StripAnsi(output);
        var lines = clean.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[server]", StringComparison.OrdinalIgnoreCase) && !line.Contains('x'))
            {
                continue;
            }

            int width = 0;
            int height = 0;
            string details = string.Empty;

            var sizeMatch = CameraSizeLineRegex.Match(line);
            if (sizeMatch.Success)
            {
                _ = int.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                _ = int.TryParse(sizeMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
                details = sizeMatch.Groups[3].Value.Trim();
            }
            else
            {
                var resMatch = ResolutionRegex.Match(line);
                if (!resMatch.Success)
                {
                    continue;
                }

                _ = int.TryParse(resMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                _ = int.TryParse(resMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
                details = line;
            }

            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var arLabel = CalculateAspectRatio(width, height);
            var isHighSpeed = line.Contains("high-speed", StringComparison.OrdinalIgnoreCase) ||
                              details.Contains("high-speed", StringComparison.OrdinalIgnoreCase);

            var fpsList = new List<int>();
            var fpsMatch = FpsGroupRegex.Match(line);
            if (fpsMatch.Success)
            {
                var fpsTokens = fpsMatch.Groups[1].Value.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var tok in fpsTokens)
                {
                    if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fpsVal) && fpsVal > 0)
                    {
                        fpsList.Add(fpsVal);
                    }
                }
            }

            if (!sizes.Any(s => s.Width == width && s.Height == height && s.IsHighSpeed == isHighSpeed))
            {
                sizes.Add(new ScrcpyCameraSizeOption(width, height, arLabel, fpsList, isHighSpeed));
            }
        }

        return sizes;
    }

    private static void ParseZoomRange(string zoomContent, out double minZoom, out double maxZoom)
    {
        minZoom = 1.0;
        maxZoom = 1.0;
        if (string.IsNullOrWhiteSpace(zoomContent))
        {
            return;
        }

        // Örnek: "0,55, 8" veya "0.55, 8" veya "1, 8"
        // Türkçe çıktıda virgül ondalık ayracı olarak gelebilir ("0,55, 8")
        // Sondaki token son sınırdır
        var tokens = zoomContent.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var numbers = new List<double>();

        var buffer = string.Empty;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct))
            {
                numbers.Add(direct);
                buffer = string.Empty;
                continue;
            }

            // "0," ve "55" ayrılmış olabilir
            if (!string.IsNullOrEmpty(buffer))
            {
                var combined = buffer + "." + token;
                if (double.TryParse(combined, NumberStyles.Float, CultureInfo.InvariantCulture, out var cval))
                {
                    numbers.Add(cval);
                    buffer = string.Empty;
                    continue;
                }
            }

            if (token.Contains(','))
            {
                var replaced = token.Replace(',', '.');
                if (double.TryParse(replaced, NumberStyles.Float, CultureInfo.InvariantCulture, out var rval))
                {
                    numbers.Add(rval);
                    buffer = string.Empty;
                    continue;
                }
            }

            buffer = token;
        }

        if (numbers.Count >= 2)
        {
            minZoom = numbers[0];
            maxZoom = numbers[^1];
        }
        else if (numbers.Count == 1)
        {
            minZoom = 1.0;
            maxZoom = numbers[0];
        }
    }

    private static string CalculateAspectRatio(int width, int height)
    {
        var gcd = GreatestCommonDivisor(width, height);
        var x = width / gcd;
        var y = height / gcd;

        if (x == 16 && y == 9) return "16:9";
        if (x == 4 && y == 3) return "4:3";
        if (x == 1 && y == 1) return "1:1";
        if (x == 19 && y == 9) return "19:9";
        if (x == 20 && y == 9) return "20:9";

        var ratio = (double)width / height;
        if (Math.Abs(ratio - 16.0 / 9.0) < 0.05) return "16:9";
        if (Math.Abs(ratio - 4.0 / 3.0) < 0.05) return "4:3";

        return $"{x}:{y}";
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }

        return Math.Max(1, a);
    }
}
