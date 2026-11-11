using System.Reflection;
using OlcuAtolyesi.Core;

namespace OlcuAtolyesi.PC.Resources.Strings;

public static class PcText
{
    public const string AppTitle = "Ölçü Atölyesi PC";

    private static readonly Lazy<string> _displayVersion = new(() =>
    {
        var assembly = typeof(PcText).Assembly;
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

        return VersionInfo.DisplayVersion;
    });

    public static string DisplayVersion => _displayVersion.Value;

    public static string WindowTitle => $"{AppTitle} — {DisplayVersion}";

    public static string Version => $"{DisplayVersion} — Sade Ana Pencere, Cetveller ve Ekran Kalibrasyonu";
    public const string Subtitle = "Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm";
    public const string PlannedNotice = "PLANLANDI — Bu özellik sonraki sürümlerde etkinleştirilecektir.";
    public const string SharedCoreNote = "Ortak Kütüphane: OlcuAtolyesi.Core (.NET 10) hem masaüstü hem mobil geometri motorunu paylaşmak üzere tasarlanmıştır.";
}
