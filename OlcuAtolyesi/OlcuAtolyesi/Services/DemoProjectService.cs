using System.Text;
using System.Text.Json;
using OlcuAtolyesi.Models;

namespace OlcuAtolyesi.Services;

/// <summary>
/// Proje ve ölçüm kayıt deposu. Depolama dizini verildiğinde
/// her projeyi kendi alt klasöründe (<c>projects/&lt;id&gt;/project.json</c>, yedek <c>.bak</c>
/// ve kalıcı fotoğraf kopyası <c>photo.jpg</c>) atomik olarak saklar.
/// </summary>
public sealed class DemoProjectService
{
    private const string ProjectsFolderName = "projects";
    private const string ProjectFileName = "project.json";
    private const string BackupFileName = "project.json.bak";
    private const string TempFileName = "project.json.tmp";
    private const string PhotoFileName = "photo.jpg";
    private const string SeededMarkerFileName = ".samples_seeded_v040";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly List<DemoProject> _projects = [];
    private readonly TimeProvider _timeProvider;
    private readonly string? _storageRoot;
    private readonly string? _projectsRoot;

    public DemoProjectService(
        bool seedSamples = true,
        TimeProvider? timeProvider = null,
        string? storageDirectory = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (!string.IsNullOrWhiteSpace(storageDirectory))
        {
            _storageRoot = Path.GetFullPath(storageDirectory);
            _projectsRoot = Path.Combine(_storageRoot, ProjectsFolderName);
            Directory.CreateDirectory(_projectsRoot);
            LoadFromDisk(seedSamples);
        }
        else if (seedSamples)
        {
            _projects.AddRange(CreateSamples(_timeProvider.GetUtcNow()));
        }
    }

    public event EventHandler? ProjectsChanged;

    public bool IsPersistent => _projectsRoot is not null;

    public int CorruptedEntryCount { get; private set; }

    /// <summary>Testlerde yazım sırasında hata/kapanma simülasyonu için kullanılır.</summary>
    public Action<Guid>? SimulateWriteFailureHook { get; set; }

    public IReadOnlyList<DemoProject> GetAll()
    {
        lock (_gate)
        {
            return Array.AsReadOnly(_projects
                .OrderByDescending(project => project.UpdatedAt)
                .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
        }
    }

    public IReadOnlyList<DemoProject> GetRecent(int maximumCount = 3)
    {
        if (maximumCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        lock (_gate)
        {
            return Array.AsReadOnly(_projects
                .OrderByDescending(project => project.UpdatedAt)
                .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(maximumCount)
                .ToArray());
        }
    }

    public DemoProject? GetById(Guid id)
    {
        lock (_gate)
        {
            return _projects.FirstOrDefault(project => project.Id == id);
        }
    }

    public string? GetPhotoFilePath(Guid projectId)
    {
        lock (_gate)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null || string.IsNullOrWhiteSpace(project.PhotoRelativePath) || _storageRoot is null)
            {
                return null;
            }

            var normalizedRelative = project.PhotoRelativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalizedRelative));
            if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }
    }

    public DemoProject Create(string name, DemoProjectType type, string description = "")
    {
        var normalizedName = ValidateName(name);
        var now = _timeProvider.GetUtcNow();
        var project = new DemoProject(
            Guid.NewGuid(),
            normalizedName,
            type,
            description,
            [],
            now,
            now,
            isSample: false);

        lock (_gate)
        {
            PersistProjectLocked(project);
            _projects.Add(project);
        }

        OnProjectsChanged();
        return project;
    }

    public DemoProject SavePhotoMeasurementProject(
        string name,
        string description,
        string? sourcePhotoPath,
        ImageSize imageSize,
        ImageSegment referenceSegment,
        LengthValue referenceLength,
        ImageSegment measurementSegment,
        LengthValue measuredLength,
        LengthUnit displayUnit,
        string measurementName = "M1–M2 mesafe ölçümü")
    {
        var normalizedName = ValidateName(name);
        var now = _timeProvider.GetUtcNow();
        var projectId = Guid.NewGuid();
        var measurement = new ProjectMeasurement(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(measurementName) ? "M1–M2 mesafe ölçümü" : measurementName.Trim(),
            MeasurementKind.Length,
            measuredLength,
            imageSegment: measurementSegment);

        lock (_gate)
        {
            var photoRelativePath = CopyPhotoToPersistentStorageLocked(projectId, sourcePhotoPath, existingRelativePath: null);
            var project = new DemoProject(
                projectId,
                normalizedName,
                DemoProjectType.PhotoMeasurement,
                description,
                [measurement],
                now,
                now,
                isSample: false,
                photoRelativePath: photoRelativePath,
                photoSize: imageSize,
                referenceSegment: referenceSegment,
                referenceLength: referenceLength,
                displayUnit: displayUnit);

            PersistProjectLocked(project);
            _projects.Add(project);
            OnProjectsChanged();
            return project;
        }
    }

    public DemoProject UpdatePhotoMeasurementProject(
        Guid projectId,
        string? name,
        string? sourcePhotoPath,
        ImageSize imageSize,
        ImageSegment referenceSegment,
        LengthValue referenceLength,
        ImageSegment measurementSegment,
        LengthValue measuredLength,
        LengthUnit displayUnit,
        Guid? measurementId = null,
        string? measurementName = null)
    {
        DemoProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(projectId);
            var existing = _projects[index];
            var normalizedName = name is not null ? ValidateName(name) : existing.Name;
            var photoRelativePath = CopyPhotoToPersistentStorageLocked(projectId, sourcePhotoPath, existing.PhotoRelativePath);

            var targetMeasurementId = measurementId
                ?? existing.Measurements.FirstOrDefault(m => m.Kind == MeasurementKind.Length)?.Id
                ?? Guid.NewGuid();
            var targetMeasurementName = !string.IsNullOrWhiteSpace(measurementName)
                ? measurementName.Trim()
                : existing.Measurements.FirstOrDefault(m => m.Id == targetMeasurementId)?.Name ?? "M1–M2 mesafe ölçümü";

            var updatedMeasurement = new ProjectMeasurement(
                targetMeasurementId,
                targetMeasurementName,
                MeasurementKind.Length,
                measuredLength,
                imageSegment: measurementSegment);

            var measurements = existing.Measurements.ToList();
            var existingMeasurementIndex = measurements.FindIndex(m => m.Id == targetMeasurementId);
            if (existingMeasurementIndex >= 0)
            {
                measurements[existingMeasurementIndex] = updatedMeasurement;
            }
            else
            {
                measurements.Add(updatedMeasurement);
            }

            updated = new DemoProject(
                existing.Id,
                normalizedName,
                existing.Type,
                existing.Description,
                measurements,
                existing.CreatedAt,
                _timeProvider.GetUtcNow(),
                isSample: false,
                photoRelativePath: photoRelativePath,
                photoSize: imageSize,
                referenceSegment: referenceSegment,
                referenceLength: referenceLength,
                displayUnit: displayUnit);

            PersistProjectLocked(updated);
            _projects[index] = updated;
        }

        OnProjectsChanged();
        return updated;
    }

    public DemoProject Rename(Guid id, string newName)
    {
        var normalizedName = ValidateName(newName);
        DemoProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(id);
            var existing = _projects[index];
            updated = Copy(
                existing,
                name: normalizedName,
                updatedAt: _timeProvider.GetUtcNow());
            PersistProjectLocked(updated);
            _projects[index] = updated;
        }

        OnProjectsChanged();
        return updated;
    }

    public DemoProject UpdateDescription(Guid id, string description)
    {
        DemoProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(id);
            var existing = _projects[index];
            updated = Copy(
                existing,
                description: description?.Trim() ?? string.Empty,
                updatedAt: _timeProvider.GetUtcNow());
            PersistProjectLocked(updated);
            _projects[index] = updated;
        }

        OnProjectsChanged();
        return updated;
    }

    public DemoProject AddMeasurement(Guid projectId, ProjectMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        DemoProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(projectId);
            var existing = _projects[index];

            if (existing.Measurements.Any(item => item.Id == measurement.Id))
            {
                throw new InvalidOperationException("Aynı kimliğe sahip bir ölçüm projede zaten bulunuyor.");
            }

            updated = Copy(
                existing,
                measurements: [.. existing.Measurements, measurement],
                updatedAt: _timeProvider.GetUtcNow());
            PersistProjectLocked(updated);
            _projects[index] = updated;
        }

        OnProjectsChanged();
        return updated;
    }

    public bool Delete(Guid id)
    {
        bool removed;

        lock (_gate)
        {
            removed = _projects.RemoveAll(project => project.Id == id) > 0;
            if (removed && _projectsRoot is not null)
            {
                var dir = GetProjectDirectoryPath(id);
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        if (removed)
        {
            OnProjectsChanged();
        }

        return removed;
    }

    private void LoadFromDisk(bool seedSamples)
    {
        if (_projectsRoot is null)
        {
            return;
        }

        CorruptedEntryCount = 0;
        _projects.Clear();

        foreach (var dir in Directory.EnumerateDirectories(_projectsRoot))
        {
            var primaryPath = Path.Combine(dir, ProjectFileName);
            var backupPath = Path.Combine(dir, BackupFileName);

            if (TryLoadProjectFile(primaryPath, out var project))
            {
                _projects.Add(project!);
                continue;
            }

            if (TryLoadProjectFile(backupPath, out var recovered))
            {
                _projects.Add(recovered!);
                try
                {
                    File.Copy(backupPath, primaryPath, overwrite: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                continue;
            }

            if (File.Exists(primaryPath) || File.Exists(backupPath))
            {
                CorruptedEntryCount++;
            }
        }

        var markerPath = Path.Combine(_projectsRoot, SeededMarkerFileName);
        if (seedSamples && _projects.Count == 0 && CorruptedEntryCount == 0 && !File.Exists(markerPath))
        {
            foreach (var sample in CreateSamples(_timeProvider.GetUtcNow()))
            {
                PersistProjectLocked(sample);
                _projects.Add(sample);
            }

            try
            {
                File.WriteAllText(markerPath, "v0.4.0", Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
        else if (!File.Exists(markerPath) && _projects.Count > 0)
        {
            try
            {
                File.WriteAllText(markerPath, "v0.4.0", Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool TryLoadProjectFile(string filePath, out DemoProject? project)
    {
        project = null;
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var dto = JsonSerializer.Deserialize<ProjectDto>(json, JsonOptions);
            if (dto is null)
            {
                return false;
            }

            project = FromDto(dto);
            return project is not null;
        }
        catch (Exception)
        {
            project = null;
            return false;
        }
    }

    private void PersistProjectLocked(DemoProject project)
    {
        if (_projectsRoot is null)
        {
            return;
        }

        var dir = GetProjectDirectoryPath(project.Id);
        Directory.CreateDirectory(dir);

        var primaryPath = Path.Combine(dir, ProjectFileName);
        var backupPath = Path.Combine(dir, BackupFileName);
        var tempPath = Path.Combine(dir, TempFileName);

        var dto = ToDto(project);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            stream.Flush(flushToDisk: true);
        }

        SimulateWriteFailureHook?.Invoke(project.Id);

        if (File.Exists(primaryPath))
        {
            File.Copy(primaryPath, backupPath, overwrite: true);
        }

        File.Move(tempPath, primaryPath, overwrite: true);
    }

    private string? CopyPhotoToPersistentStorageLocked(Guid projectId, string? sourcePhotoPath, string? existingRelativePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePhotoPath))
        {
            return existingRelativePath;
        }

        if (_projectsRoot is null || _storageRoot is null)
        {
            return sourcePhotoPath;
        }

        if (!File.Exists(sourcePhotoPath))
        {
            throw new FileNotFoundException("Kaydedilecek fotoğraf dosyası bulunamadı.", sourcePhotoPath);
        }

        var dir = GetProjectDirectoryPath(projectId);
        Directory.CreateDirectory(dir);
        var targetPath = Path.Combine(dir, PhotoFileName);
        var fullSourcePath = Path.GetFullPath(sourcePhotoPath);
        var fullTargetPath = Path.GetFullPath(targetPath);

        if (!string.Equals(fullSourcePath, fullTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPhotoPath = Path.Combine(dir, PhotoFileName + ".tmp");
            File.Copy(fullSourcePath, tempPhotoPath, overwrite: true);
            File.Move(tempPhotoPath, fullTargetPath, overwrite: true);
        }

        return $"{ProjectsFolderName}/{projectId:N}/{PhotoFileName}";
    }

    private string GetProjectDirectoryPath(Guid id) =>
        Path.Combine(_projectsRoot!, id.ToString("N"));

    private int FindIndexOrThrow(Guid id)
    {
        var index = _projects.FindIndex(project => project.Id == id);
        return index >= 0
            ? index
            : throw new KeyNotFoundException("Proje bulunamadı.");
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Proje adı boş olamaz.", nameof(name));
        }

        return name.Trim();
    }

    private static DemoProject Copy(
        DemoProject source,
        string? name = null,
        string? description = null,
        IReadOnlyList<ProjectMeasurement>? measurements = null,
        DateTimeOffset? updatedAt = null) =>
        new(
            source.Id,
            name ?? source.Name,
            source.Type,
            description ?? source.Description,
            measurements ?? source.Measurements,
            source.CreatedAt,
            updatedAt ?? source.UpdatedAt,
            source.IsSample,
            source.PhotoRelativePath,
            source.PhotoSize,
            source.ReferenceSegment,
            source.ReferenceLength,
            source.DisplayUnit);

    private static ProjectDto ToDto(DemoProject project) =>
        new()
        {
            Id = project.Id,
            Name = project.Name,
            Type = project.Type,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsSample = project.IsSample,
            PhotoRelativePath = project.PhotoRelativePath,
            PhotoWidth = project.PhotoSize?.Width,
            PhotoHeight = project.PhotoSize?.Height,
            ReferenceStartX = project.ReferenceSegment?.Start.X,
            ReferenceStartY = project.ReferenceSegment?.Start.Y,
            ReferenceEndX = project.ReferenceSegment?.End.X,
            ReferenceEndY = project.ReferenceSegment?.End.Y,
            ReferenceLengthValue = project.ReferenceLength?.Value,
            ReferenceLengthUnit = project.ReferenceLength?.Unit,
            DisplayUnit = project.DisplayUnit,
            Measurements = project.Measurements.Select(m => new MeasurementDto
            {
                Id = m.Id,
                Name = m.Name,
                Kind = m.Kind,
                LengthValue = m.Length?.Value,
                LengthUnit = m.Length?.Unit,
                AngleDegrees = m.AngleDegrees,
                StartX = m.ImageSegment?.Start.X,
                StartY = m.ImageSegment?.Start.Y,
                EndX = m.ImageSegment?.End.X,
                EndY = m.ImageSegment?.End.Y
            }).ToList()
        };

    private static DemoProject? FromDto(ProjectDto dto)
    {
        if (dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Name) || !Enum.IsDefined(dto.Type))
        {
            return null;
        }

        ImageSize? photoSize = null;
        if (dto.PhotoWidth is { } pw && dto.PhotoHeight is { } ph)
        {
            if (!double.IsFinite(pw) || !double.IsFinite(ph) || pw <= 0d || ph <= 0d)
            {
                return null;
            }

            photoSize = new ImageSize(pw, ph);
        }

        ImageSegment? referenceSegment = null;
        if (dto.ReferenceStartX is { } rx1 && dto.ReferenceStartY is { } ry1 &&
            dto.ReferenceEndX is { } rx2 && dto.ReferenceEndY is { } ry2)
        {
            var seg = new ImageSegment(new ImagePoint(rx1, ry1), new ImagePoint(rx2, ry2));
            if (!seg.IsFinite || seg.ImageLength <= 0d)
            {
                return null;
            }

            referenceSegment = seg;
        }

        LengthValue? referenceLength = null;
        if (dto.ReferenceLengthValue is { } rVal && dto.ReferenceLengthUnit is { } rUnit)
        {
            if (!double.IsFinite(rVal) || rVal <= 0d || !Enum.IsDefined(rUnit))
            {
                return null;
            }

            referenceLength = new LengthValue(rVal, rUnit);
        }

        LengthUnit? displayUnit = null;
        if (dto.DisplayUnit is { } dUnit)
        {
            if (!Enum.IsDefined(dUnit))
            {
                return null;
            }

            displayUnit = dUnit;
        }

        var measurements = new List<ProjectMeasurement>();
        if (dto.Measurements is not null)
        {
            foreach (var m in dto.Measurements)
            {
                if (m is null || m.Id == Guid.Empty || string.IsNullOrWhiteSpace(m.Name) || !Enum.IsDefined(m.Kind))
                {
                    return null;
                }

                LengthValue? length = null;
                if (m.LengthValue is { } lv && m.LengthUnit is { } lu)
                {
                    if (!double.IsFinite(lv) || lv < 0d || !Enum.IsDefined(lu))
                    {
                        return null;
                    }

                    length = new LengthValue(lv, lu);
                }

                ImageSegment? seg = null;
                if (m.StartX is { } sx && m.StartY is { } sy && m.EndX is { } ex && m.EndY is { } ey)
                {
                    var candidate = new ImageSegment(new ImagePoint(sx, sy), new ImagePoint(ex, ey));
                    if (!candidate.IsFinite)
                    {
                        return null;
                    }

                    seg = candidate;
                }

                measurements.Add(new ProjectMeasurement(
                    m.Id,
                    m.Name,
                    m.Kind,
                    length,
                    m.AngleDegrees,
                    seg));
            }
        }

        return new DemoProject(
            dto.Id,
            dto.Name,
            dto.Type,
            dto.Description ?? string.Empty,
            measurements,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.IsSample,
            dto.PhotoRelativePath,
            photoSize,
            referenceSegment,
            referenceLength,
            displayUnit);
    }

    private sealed class ProjectDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public DemoProjectType Type { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsSample { get; set; }
        public string? PhotoRelativePath { get; set; }
        public double? PhotoWidth { get; set; }
        public double? PhotoHeight { get; set; }
        public double? ReferenceStartX { get; set; }
        public double? ReferenceStartY { get; set; }
        public double? ReferenceEndX { get; set; }
        public double? ReferenceEndY { get; set; }
        public double? ReferenceLengthValue { get; set; }
        public LengthUnit? ReferenceLengthUnit { get; set; }
        public LengthUnit? DisplayUnit { get; set; }
        public List<MeasurementDto>? Measurements { get; set; }
    }

    private sealed class MeasurementDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public MeasurementKind Kind { get; set; }
        public double? LengthValue { get; set; }
        public LengthUnit? LengthUnit { get; set; }
        public double? AngleDegrees { get; set; }
        public double? StartX { get; set; }
        public double? StartY { get; set; }
        public double? EndX { get; set; }
        public double? EndY { get; set; }
    }

    private static IReadOnlyList<DemoProject> CreateSamples(DateTimeOffset now)
    {
        var plateMeasurements = new[]
        {
            new ProjectMeasurement(
                Guid.Parse("55396967-5211-4a26-b75d-1e0a204b0e01"),
                "Plaka genişliği",
                MeasurementKind.Length,
                new LengthValue(90, LengthUnit.Millimeter),
                imageSegment: new ImageSegment(new ImagePoint(250, 205), new ImagePoint(700, 205))),
            LengthMeasurement("Delik çapı", 18, LengthUnit.Millimeter),
            LengthMeasurement("Delik merkezleri", 80, LengthUnit.Millimeter)
        };
        var tableMeasurements = new[]
        {
            LengthMeasurement("Uzun kenar", 120, LengthUnit.Centimeter),
            LengthMeasurement("Kısa kenar", 60, LengthUnit.Centimeter)
        };
        var gasketMeasurements = new[]
        {
            LengthMeasurement("Dış çap", 80, LengthUnit.Millimeter),
            LengthMeasurement("İç çap", 42, LengthUnit.Millimeter)
        };

        return
        [
            new DemoProject(
                Guid.Parse("44396967-5211-4a26-b75d-1e0a204b0d01"),
                "Bağlantı plakası",
                DemoProjectType.ConnectionPlate,
                "İki delikli örnek plaka — DEMO, gerçek ölçüm değildir.",
                plateMeasurements,
                now.AddMinutes(-25),
                now.AddMinutes(-3),
                isSample: true,
                photoSize: new ImageSize(1000, 600),
                referenceSegment: new ImageSegment(new ImagePoint(200, 500), new ImagePoint(800, 500)),
                referenceLength: new LengthValue(120, LengthUnit.Millimeter),
                displayUnit: LengthUnit.Millimeter),
            new DemoProject(
                Guid.Parse("44396967-5211-4a26-b75d-1e0a204b0d02"),
                "Masa ölçüsü",
                DemoProjectType.TableMeasurement,
                "Dikdörtgen yüzey örneği — DEMO, gerçek ölçüm değildir.",
                tableMeasurements,
                now.AddMinutes(-40),
                now.AddMinutes(-12),
                isSample: true),
            new DemoProject(
                Guid.Parse("44396967-5211-4a26-b75d-1e0a204b0d03"),
                "Conta taslağı",
                DemoProjectType.GasketSketch,
                "İç ve dış çap örneği — DEMO, gerçek ölçüm değildir.",
                gasketMeasurements,
                now.AddMinutes(-60),
                now.AddMinutes(-20),
                isSample: true)
        ];
    }

    private static ProjectMeasurement LengthMeasurement(string name, double value, LengthUnit unit) =>
        new(Guid.NewGuid(), name, MeasurementKind.Length, new LengthValue(value, unit));

    private void OnProjectsChanged() => ProjectsChanged?.Invoke(this, EventArgs.Empty);
}
