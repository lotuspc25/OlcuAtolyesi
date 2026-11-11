using System.Reflection;

namespace OlcuAtolyesi.Core;

public static class VersionInfo
{
    private static readonly Lazy<string> _displayVersion = new(() =>
    {
        var assembly = typeof(VersionInfo).Assembly;
        var ver = assembly.GetName().Version;
        if (ver != null && (ver.Major > 0 || ver.Minor > 0 || ver.Build > 0))
        {
            return ver.Build >= 0 ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : $"v{ver.Major}.{ver.Minor}";
        }

        var infoVer = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            var cleanVer = infoVer.Split('+')[0];
            return cleanVer.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? cleanVer : $"v{cleanVer}";
        }

        return "v0.6.7";
    });

    public static string DisplayVersion => _displayVersion.Value;
}
