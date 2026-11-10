using System.Text;
using System.Text.Json;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Measurement;

namespace OlcuAtolyesi.Core.Services;

public enum AppThemeMode
{
    Light,
    Dark,
    System
}

public static class AppThemeModeExtensions
{
    public static string DisplayName(this AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => "Açık Tema",
        AppThemeMode.Dark => "Koyu Tema",
        AppThemeMode.System => "Sistem Teması",
        _ => "Koyu Tema"
    };
}

public readonly record struct DesktopPreferences(
    AppThemeMode Theme,
    LengthUnit DefaultUnit,
    string WorkspaceDirectory,
    double ScreenMillimetresPerDip = 0d,
    string ScreenCalibrationReference = "",
    double LastReferenceMillimetres = 0d,
    string LastReferenceObject = "")
{
    public static DesktopPreferences CreateDefault(string? workspaceDir = null) =>
        new(
            AppThemeMode.Dark,
            LengthUnit.Millimeter,
            string.IsNullOrWhiteSpace(workspaceDir)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OlcuAtolyesiPC",
                    "Workspace")
                : workspaceDir);
}

public sealed class DesktopSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string? _settingsFilePath;
    private DesktopPreferences _current;

    public DesktopSettingsService(string? storageDirectory = null)
    {
        var defaultWorkspace = string.IsNullOrWhiteSpace(storageDirectory)
            ? null
            : Path.Combine(storageDirectory, "Workspace");
        _current = DesktopPreferences.CreateDefault(defaultWorkspace);

        if (!string.IsNullOrWhiteSpace(storageDirectory))
        {
            Directory.CreateDirectory(storageDirectory);
            _settingsFilePath = Path.Combine(storageDirectory, "settings.json");
            LoadFromDisk();
        }
    }

    public event EventHandler<DesktopPreferences>? PreferencesChanged;

    public DesktopPreferences Current => _current;

    public AppThemeMode Theme => _current.Theme;

    public LengthUnit DefaultUnit => _current.DefaultUnit;

    public string WorkspaceDirectory => _current.WorkspaceDirectory;

    public void SetTheme(AppThemeMode theme)
    {
        if (!Enum.IsDefined(theme) || _current.Theme == theme)
        {
            return;
        }

        _current = _current with { Theme = theme };
        SaveToDisk();
        PreferencesChanged?.Invoke(this, _current);
    }

    public void SetDefaultUnit(LengthUnit unit)
    {
        if (!Enum.IsDefined(unit) || _current.DefaultUnit == unit)
        {
            return;
        }

        _current = _current with { DefaultUnit = unit };
        SaveToDisk();
        PreferencesChanged?.Invoke(this, _current);
    }

    public ScreenCalibration GetScreenCalibration() =>
        new(_current.ScreenMillimetresPerDip, _current.ScreenCalibrationReference);

    public void SetScreenCalibration(ScreenCalibration calibration)
    {
        _current = _current with
        {
            ScreenMillimetresPerDip = calibration?.MillimetresPerDip ?? 0d,
            ScreenCalibrationReference = calibration?.ReferenceName ?? string.Empty
        };
        SaveToDisk();
        PreferencesChanged?.Invoke(this, _current);
    }

    public void SetLastReference(double millimetres, string referenceObject)
    {
        _current = _current with
        {
            LastReferenceMillimetres = millimetres,
            LastReferenceObject = referenceObject ?? string.Empty
        };
        SaveToDisk();
        PreferencesChanged?.Invoke(this, _current);
    }

    private void LoadFromDisk()
    {
        if (_settingsFilePath is null || !File.Exists(_settingsFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
            var dto = JsonSerializer.Deserialize<PreferencesDto>(json, JsonOptions);
            if (dto is null)
            {
                return;
            }

            var theme = Enum.IsDefined(dto.Theme) ? dto.Theme : _current.Theme;
            var unit = Enum.IsDefined(dto.DefaultUnit) ? dto.DefaultUnit : _current.DefaultUnit;
            var workspace = string.IsNullOrWhiteSpace(dto.WorkspaceDirectory)
                ? _current.WorkspaceDirectory
                : dto.WorkspaceDirectory;
            var screenMmPerDip = dto.ScreenMillimetresPerDip;
            var screenCalibRef = dto.ScreenCalibrationReference ?? string.Empty;
            var lastRefMm = dto.LastReferenceMillimetres;
            var lastRefObj = dto.LastReferenceObject ?? string.Empty;

            _current = new DesktopPreferences(theme, unit, workspace, screenMmPerDip, screenCalibRef, lastRefMm, lastRefObj);
        }
        catch
        {
            // Bozuk ayar dosyası durumunda varsayılan değerler korunur.
        }
    }

    private void SaveToDisk()
    {
        if (_settingsFilePath is null)
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = _settingsFilePath + ".tmp";
            var json = JsonSerializer.Serialize(new PreferencesDto
            {
                Theme = _current.Theme,
                DefaultUnit = _current.DefaultUnit,
                WorkspaceDirectory = _current.WorkspaceDirectory,
                ScreenMillimetresPerDip = _current.ScreenMillimetresPerDip,
                ScreenCalibrationReference = _current.ScreenCalibrationReference,
                LastReferenceMillimetres = _current.LastReferenceMillimetres,
                LastReferenceObject = _current.LastReferenceObject
            }, JsonOptions);

            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
        }
        catch
        {
        }
    }

    private sealed class PreferencesDto
    {
        public AppThemeMode Theme { get; set; }
        public LengthUnit DefaultUnit { get; set; }
        public string? WorkspaceDirectory { get; set; }
        public double ScreenMillimetresPerDip { get; set; }
        public string ScreenCalibrationReference { get; set; } = string.Empty;
        public double LastReferenceMillimetres { get; set; }
        public string LastReferenceObject { get; set; } = string.Empty;
    }
}
