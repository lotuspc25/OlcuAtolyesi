using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Models;

namespace OlcuAtolyesi.Core.Services;

/// <summary>
/// Masaüstü kalıcı proje kayıt, geri yükleme ve mobil (v0.4.0) proje paketi içe/dışa aktarma servisi.
/// Her projeyi kendi alt klasöründe (<c>projects/&lt;id&gt;/project.json</c>, yedek <c>project.json.bak</c>
/// ve <c>photo.jpg</c>) atomik olarak saklar.
/// </summary>
public sealed class DesktopProjectService
{
    public const string ProjectsFolderName = "projects";
    public const string ProjectFileName = "project.json";
    public const string BackupFileName = "project.json.bak";
    public const string TempFileName = "project.json.tmp";
    public const string PhotoFileName = "photo.jpg";
    private const string SeededMarkerFileName = ".pc_samples_seeded_v030";
    private const string DefaultMeasurementName = "M1–M2 mesafe ölçümü";

    private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly List<DesktopProject> _projects = [];
    private readonly TimeProvider _timeProvider;
    private readonly string? _storageRoot;
    private readonly string? _projectsRoot;

    public DesktopProjectService(
        string? storageDirectory = null,
        bool seedSamples = true,
        TimeProvider? timeProvider = null)
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

    public string? StorageRoot => _storageRoot;

    public int CorruptedEntryCount { get; private set; }

    public int RecoveredFromBackupCount { get; private set; }

    /// <summary>Testlerde atomik yazım sırasında kesinti/çökme simülasyonu için kullanılır.</summary>
    public Action<Guid>? SimulateWriteFailureHook { get; set; }

    public IReadOnlyList<DesktopProject> GetAll()
    {
        lock (_gate)
        {
            return Array.AsReadOnly(_projects
                .OrderByDescending(p => p.UpdatedAt)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
        }
    }

    public DesktopProject? GetById(Guid id)
    {
        lock (_gate)
        {
            return _projects.FirstOrDefault(p => p.Id == id);
        }
    }

    /// <summary>
    /// Projeye ait kalıcı fotoğraf dosyasının tam yolunu döndürür.
    /// Dosya eksikse veya yol çalışma alanı dışına çıkıyorsa null döner (çökmez).
    /// </summary>
    public string? GetPhotoFilePath(Guid projectId)
    {
        lock (_gate)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null || _storageRoot is null || _projectsRoot is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(project.PhotoRelativePath))
            {
                if (TryResolveSafeRelativePath(_storageRoot, project.PhotoRelativePath, out var resolved) &&
                    File.Exists(resolved))
                {
                    return resolved;
                }
            }

            var defaultPhoto = Path.Combine(GetProjectDirectoryPath(projectId), PhotoFileName);
            return File.Exists(defaultPhoto) ? defaultPhoto : null;
        }
    }

    /// <summary>Varsayılan koordinatlarla yeni bir proje oluşturur ve kalıcı olarak kaydeder.</summary>
    public DesktopProject Create(
        string name,
        ProjectType type = ProjectType.PhotoMeasurement,
        string description = "",
        LengthUnit displayUnit = LengthUnit.Millimeter)
    {
        var normalizedName = ValidateName(name);
        var now = _timeProvider.GetUtcNow();
        var size = MeasurementSession.DemoImageSize;
        var session = new MeasurementSession(size, displayUnit, useDemoDefaults: true);
        var refLen = new LengthValue(120d, LengthUnit.Millimeter).ConvertTo(displayUnit);
        session.SetKnownLength(refLen);

        var measurementItem = new ProjectMeasurementItem(
            Guid.NewGuid(),
            DefaultMeasurementName,
            MeasurementKind.Length,
            session.Result,
            imageSegment: session.Measurement);

        var project = new DesktopProject(
            Guid.NewGuid(),
            normalizedName,
            type,
            description,
            [measurementItem],
            now,
            now,
            isSample: false,
            photoRelativePath: null,
            photoSize: size,
            referenceSegment: session.ReferenceSegment,
            referenceLength: refLen,
            measurementSegment: session.Measurement,
            measuredLength: session.Result,
            displayUnit: displayUnit,
            originalPhotoFileName: null,
            sourcePlatform: "PC");

        lock (_gate)
        {
            PersistProjectLocked(project);
            _projects.Add(project);
        }

        OnProjectsChanged();
        return project;
    }

    /// <summary>Açık fotoğrafı ve R1/R2, M1/M2 ölçüm bilgilerini yeni bir proje olarak kaydeder.</summary>
    public DesktopProject SavePhotoMeasurementProject(
        string name,
        string description,
        string? sourcePhotoPath,
        ImageSize imageSize,
        ImageSegment referenceSegment,
        LengthValue? referenceLength,
        ImageSegment measurementSegment,
        LengthValue? measuredLength,
        LengthUnit displayUnit,
        string? originalPhotoFileName = null,
        string measurementName = DefaultMeasurementName)
    {
        var normalizedName = ValidateName(name);
        var now = _timeProvider.GetUtcNow();
        var projectId = Guid.NewGuid();

        var effectiveMeasured = CalculateMeasuredLength(referenceSegment, referenceLength, measurementSegment, displayUnit)
                                ?? measuredLength;

        var measurement = new ProjectMeasurementItem(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(measurementName) ? DefaultMeasurementName : measurementName.Trim(),
            MeasurementKind.Length,
            effectiveMeasured,
            imageSegment: measurementSegment);

        lock (_gate)
        {
            var photoRelativePath = CopyPhotoToPersistentStorageLocked(projectId, sourcePhotoPath, existingRelativePath: null);
            var origName = !string.IsNullOrWhiteSpace(originalPhotoFileName)
                ? originalPhotoFileName.Trim()
                : (!string.IsNullOrWhiteSpace(sourcePhotoPath) ? Path.GetFileName(sourcePhotoPath) : null);

            var project = new DesktopProject(
                projectId,
                normalizedName,
                ProjectType.PhotoMeasurement,
                description,
                [measurement],
                now,
                now,
                isSample: false,
                photoRelativePath: photoRelativePath,
                photoSize: imageSize,
                referenceSegment: referenceSegment,
                referenceLength: referenceLength,
                measurementSegment: measurementSegment,
                measuredLength: effectiveMeasured,
                displayUnit: displayUnit,
                originalPhotoFileName: origName,
                sourcePlatform: "PC");

            PersistProjectLocked(project);
            _projects.Add(project);
        }

        OnProjectsChanged();
        return GetById(projectId)!;
    }

    /// <summary>Mevcut bir projenin fotoğrafını, R1/R2 referansını ve M1/M2 ölçüm verilerini günceller.</summary>
    public DesktopProject UpdatePhotoMeasurementProject(
        Guid projectId,
        string? name,
        string? description,
        string? sourcePhotoPath,
        ImageSize imageSize,
        ImageSegment referenceSegment,
        LengthValue? referenceLength,
        ImageSegment measurementSegment,
        LengthValue? measuredLength,
        LengthUnit displayUnit,
        string? originalPhotoFileName = null)
    {
        DesktopProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(projectId);
            var existing = _projects[index];
            var normalizedName = name is not null ? ValidateName(name) : existing.Name;
            var normalizedDesc = description is not null ? description.Trim() : existing.Description;
            var photoRelativePath = CopyPhotoToPersistentStorageLocked(projectId, sourcePhotoPath, existing.PhotoRelativePath);
            var origName = !string.IsNullOrWhiteSpace(originalPhotoFileName)
                ? originalPhotoFileName.Trim()
                : (!string.IsNullOrWhiteSpace(sourcePhotoPath) ? Path.GetFileName(sourcePhotoPath) : existing.OriginalPhotoFileName);

            var effectiveMeasured = CalculateMeasuredLength(referenceSegment, referenceLength, measurementSegment, displayUnit)
                                    ?? measuredLength;

            var targetMeasurementId = existing.Measurements.FirstOrDefault(m => m.Kind == MeasurementKind.Length)?.Id
                                      ?? Guid.NewGuid();
            var targetMeasurementName = existing.Measurements.FirstOrDefault(m => m.Id == targetMeasurementId)?.Name
                                        ?? DefaultMeasurementName;

            var updatedMeasurement = new ProjectMeasurementItem(
                targetMeasurementId,
                targetMeasurementName,
                MeasurementKind.Length,
                effectiveMeasured,
                imageSegment: measurementSegment);

            var measurements = existing.Measurements.ToList();
            var existingIdx = measurements.FindIndex(m => m.Id == targetMeasurementId);
            if (existingIdx >= 0)
            {
                measurements[existingIdx] = updatedMeasurement;
            }
            else
            {
                measurements.Insert(0, updatedMeasurement);
            }

            updated = new DesktopProject(
                existing.Id,
                normalizedName,
                existing.Type,
                normalizedDesc,
                measurements,
                existing.CreatedAt,
                _timeProvider.GetUtcNow(),
                isSample: false,
                photoRelativePath: photoRelativePath,
                photoSize: imageSize,
                referenceSegment: referenceSegment,
                referenceLength: referenceLength,
                measurementSegment: measurementSegment,
                measuredLength: effectiveMeasured,
                displayUnit: displayUnit,
                originalPhotoFileName: origName,
                sourcePlatform: existing.SourcePlatform);

            PersistProjectLocked(updated);
            _projects[index] = updated;
        }

        OnProjectsChanged();
        return updated;
    }

    public DesktopProject Rename(Guid id, string newName, string? newDescription = null)
    {
        var normalizedName = ValidateName(newName);
        DesktopProject updated;

        lock (_gate)
        {
            var index = FindIndexOrThrow(id);
            var existing = _projects[index];
            updated = Copy(
                existing,
                name: normalizedName,
                description: newDescription is not null ? newDescription.Trim() : existing.Description,
                updatedAt: _timeProvider.GetUtcNow(),
                isSample: false);
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
            removed = _projects.RemoveAll(p => p.Id == id) > 0;
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

    /// <summary>
    /// Mobil (v0.4.0) veya PC proje paketini (.zip arşivi, proje klasörü veya project.json dosyası)
    /// kaynak dosyaları hiç değiştirmeden güvenli dönüştürme katmanıyla içe aktarır.
    /// </summary>
    public ProjectImportResult ImportProjectPackage(
        string? sourcePath,
        ProjectConflictPolicy conflictPolicy = ProjectConflictPolicy.ImportAsCopy)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return new ProjectImportResult(false, null, "İçe aktarılacak dosya veya klasör yolu boş.");
        }

        string fullSourcePath;
        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);
        }
        catch (Exception)
        {
            return new ProjectImportResult(false, null, "Geçersiz dosya veya klasör yolu.");
        }

        if (Directory.Exists(fullSourcePath))
        {
            return ImportFromDirectory(fullSourcePath, conflictPolicy);
        }

        if (!File.Exists(fullSourcePath))
        {
            return new ProjectImportResult(false, null, "Seçilen proje dosyası veya klasörü bulunamadı.");
        }

        var ext = Path.GetExtension(fullSourcePath);
        if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".olcupkg", StringComparison.OrdinalIgnoreCase))
        {
            return ImportFromZipArchive(fullSourcePath, conflictPolicy);
        }

        if (string.Equals(Path.GetFileName(fullSourcePath), ProjectFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
        {
            var parentDir = Path.GetDirectoryName(fullSourcePath);
            if (string.IsNullOrWhiteSpace(parentDir))
            {
                return new ProjectImportResult(false, null, "Proje klasörü belirlenemedi.");
            }

            return ImportFromProjectJsonFile(fullSourcePath, parentDir, conflictPolicy);
        }

        return new ProjectImportResult(
            false,
            null,
            "Desteklenmeyen paket biçimi. Lütfen bir proje klasörü, project.json veya .zip proje paketi seçin.");
    }

    /// <summary>
    /// Bir PC projesini mobil v0.4.0 ve PC ile uyumlu taşınabilir .zip proje paketi olarak dışa aktarır.
    /// Paket içinde yalnızca göreli yollar kullanılır.
    /// </summary>
    public ProjectExportResult ExportProjectPackageZip(Guid projectId, string? targetZipPath)
    {
        if (string.IsNullOrWhiteSpace(targetZipPath))
        {
            return new ProjectExportResult(false, null, "Dışa aktarma dosya yolu boş olamaz.");
        }

        string fullZipPath;
        try
        {
            fullZipPath = Path.GetFullPath(targetZipPath);
        }
        catch (Exception)
        {
            return new ProjectExportResult(false, null, "Geçersiz dışa aktarma dosya yolu.");
        }

        DesktopProject? project;
        string? photoPath;
        lock (_gate)
        {
            project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null)
            {
                return new ProjectExportResult(false, null, "Dışa aktarılacak proje bulunamadı.");
            }

            photoPath = GetPhotoFilePath(projectId);
        }

        try
        {
            var parentDir = Path.GetDirectoryName(fullZipPath);
            if (!string.IsNullOrWhiteSpace(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            var tempZipPath = fullZipPath + ".tmp";
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            var relativePhotoEntry = photoPath is not null
                ? $"{ProjectsFolderName}/{project.Id:N}/{PhotoFileName}"
                : null;

            var portableProject = Copy(project, photoRelativePath: relativePhotoEntry);
            var dto = ToDto(portableProject);
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);

            using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
            {
                // Hem kök dizinde project.json hem de mobil klasör yapısıyla uyumlu göreli fotoğraf kaydı
                var jsonEntry = archive.CreateEntry(ProjectFileName, CompressionLevel.Optimal);
                using (var entryStream = jsonEntry.Open())
                {
                    entryStream.Write(jsonBytes, 0, jsonBytes.Length);
                }

                if (photoPath is not null && File.Exists(photoPath))
                {
                    // Kök photo.jpg ve projects/<id>/photo.jpg olarak eklenir
                    archive.CreateEntryFromFile(photoPath, PhotoFileName, CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(photoPath, $"{ProjectsFolderName}/{project.Id:N}/{PhotoFileName}", CompressionLevel.Optimal);
                }
            }

            File.Move(tempZipPath, fullZipPath, overwrite: true);
            return new ProjectExportResult(
                true,
                fullZipPath,
                $"\"{project.Name}\" projesi taşınabilir paket olarak dışa aktarıldı.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new ProjectExportResult(false, null, $"Proje paketi dışa aktarılamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// Bir PC projesini mobil v0.4.0 klasör yapısıyla (project.json + photo.jpg) hedef klasöre dışa aktarır.
    /// </summary>
    public ProjectExportResult ExportProjectFolder(Guid projectId, string? targetDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(targetDirectoryPath))
        {
            return new ProjectExportResult(false, null, "Hedef klasör yolu boş olamaz.");
        }

        string fullTargetDir;
        try
        {
            fullTargetDir = Path.GetFullPath(targetDirectoryPath);
        }
        catch (Exception)
        {
            return new ProjectExportResult(false, null, "Geçersiz hedef klasör yolu.");
        }

        DesktopProject? project;
        string? photoPath;
        lock (_gate)
        {
            project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null)
            {
                return new ProjectExportResult(false, null, "Dışa aktarılacak proje bulunamadı.");
            }

            photoPath = GetPhotoFilePath(projectId);
        }

        try
        {
            Directory.CreateDirectory(fullTargetDir);
            var relativePhoto = photoPath is not null
                ? $"{ProjectsFolderName}/{project.Id:N}/{PhotoFileName}"
                : null;

            var portableProject = Copy(project, photoRelativePath: relativePhoto);
            var dto = ToDto(portableProject);
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);

            var targetJsonPath = Path.Combine(fullTargetDir, ProjectFileName);
            File.WriteAllBytes(targetJsonPath, jsonBytes);

            if (photoPath is not null && File.Exists(photoPath))
            {
                var rootPhotoPath = Path.Combine(fullTargetDir, PhotoFileName);
                File.Copy(photoPath, rootPhotoPath, overwrite: true);

                var nestedDir = Path.Combine(fullTargetDir, ProjectsFolderName, project.Id.ToString("N"));
                Directory.CreateDirectory(nestedDir);
                File.Copy(photoPath, Path.Combine(nestedDir, PhotoFileName), overwrite: true);
            }

            return new ProjectExportResult(
                true,
                fullTargetDir,
                $"\"{project.Name}\" projesi klasör paketi olarak dışa aktarıldı.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProjectExportResult(false, null, $"Klasör paketi dışa aktarılamadı: {ex.Message}");
        }
    }

    private ProjectImportResult ImportFromZipArchive(string zipFilePath, ProjectConflictPolicy conflictPolicy)
    {
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);
        try
        {
            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                if (archive.Entries.Count == 0)
                {
                    return new ProjectImportResult(false, null, "Proje paketi (.zip) boş.");
                }

                var normalizedExtractRoot = Path.GetFullPath(tempExtractDir + Path.DirectorySeparatorChar);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    // Zip Slip / dizin dışına yazma koruması
                    if (entry.FullName.Contains("..", StringComparison.Ordinal) ||
                        Path.IsPathRooted(entry.FullName))
                    {
                        return new ProjectImportResult(
                            false,
                            null,
                            "Güvenlik hatası: Paket içinde geçersiz veya dizin dışına çıkan dosya yolu tespit edildi.");
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(tempExtractDir, entry.FullName));
                    if (!destinationPath.StartsWith(normalizedExtractRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ProjectImportResult(
                            false,
                            null,
                            "Güvenlik hatası: Paket içeriği hedef klasör dışına çıkamaz.");
                    }

                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    var entryDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            return ImportFromDirectory(tempExtractDir, conflictPolicy);
        }
        catch (InvalidDataException)
        {
            return new ProjectImportResult(false, null, "Seçilen .zip proje paketi bozuk veya geçerli bir arşiv değil.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProjectImportResult(false, null, $"Proje paketi okunamadı: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private ProjectImportResult ImportFromDirectory(string directoryPath, ProjectConflictPolicy conflictPolicy)
    {
        var directJson = Path.Combine(directoryPath, ProjectFileName);
        if (File.Exists(directJson))
        {
            return ImportFromProjectJsonFile(directJson, directoryPath, conflictPolicy);
        }

        // Alt klasörlerde tek bir project.json varsa (ör. projects/<id>/project.json)
        var candidates = Directory.EnumerateFiles(directoryPath, ProjectFileName, SearchOption.AllDirectories)
            .Take(2)
            .ToList();

        if (candidates.Count == 1)
        {
            return ImportFromProjectJsonFile(candidates[0], directoryPath, conflictPolicy);
        }

        if (candidates.Count > 1)
        {
            return new ProjectImportResult(
                false,
                null,
                "Seçilen klasörde birden fazla proje bulundu. Lütfen tek bir proje klasörünü veya project.json dosyasını seçin.");
        }

        return new ProjectImportResult(
            false,
            null,
            "Seçilen klasörde project.json proje kayıt dosyası bulunamadı.");
    }

    private ProjectImportResult ImportFromProjectJsonFile(
        string jsonFilePath,
        string packageRootDirectory,
        ProjectConflictPolicy conflictPolicy)
    {
        string json;
        try
        {
            json = File.ReadAllText(jsonFilePath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProjectImportResult(false, null, $"Proje dosyası okunamadı: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProjectImportResult(false, null, "Proje kayıt dosyası (project.json) boş.");
        }

        ProjectDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ProjectDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return new ProjectImportResult(false, null, "Proje kayıt dosyası (project.json) bozuk veya geçersiz JSON içeriyor.");
        }

        if (dto is null)
        {
            return new ProjectImportResult(false, null, "Proje verisi çözümlenemedi.");
        }

        // Güvenlik: PhotoRelativePath içinde dizin dışına çıkma (.. veya kök yol) varsa reddet
        if (!string.IsNullOrWhiteSpace(dto.PhotoRelativePath))
        {
            if (ContainsPathTraversal(dto.PhotoRelativePath))
            {
                return new ProjectImportResult(
                    false,
                    null,
                    "Güvenlik hatası: Proje içindeki fotoğraf yolu geçersiz veya dizin dışına çıkıyor.");
            }
        }

        var converted = TryConvertDtoToProject(dto, defaultSourcePlatform: "Mobil v0.4.0", out var parsedProject, out var validationError);
        if (!converted || parsedProject is null)
        {
            return new ProjectImportResult(false, null, validationError ?? "Proje verileri geçersiz veya desteklenmiyor.");
        }

        // Kaynak fotoğrafı güvenli biçimde bul (kaynak dosyaları hiç değiştirmeden)
        var jsonDir = Path.GetDirectoryName(jsonFilePath) ?? packageRootDirectory;
        var resolvedSourcePhoto = ResolvePackagePhotoPath(jsonDir, packageRootDirectory, dto.PhotoRelativePath);

        if (resolvedSourcePhoto is not null)
        {
            if (!IsValidImageFileHeader(resolvedSourcePhoto, out var photoValidationError))
            {
                return new ProjectImportResult(false, null, photoValidationError ?? "Paketteki fotoğraf dosyası bozuk veya desteklenmiyor.");
            }
        }

        var photoMissingWarning = !string.IsNullOrWhiteSpace(dto.PhotoRelativePath) && resolvedSourcePhoto is null;

        lock (_gate)
        {
            var existingIndex = _projects.FindIndex(p => p.Id == parsedProject.Id);
            var wasConflict = existingIndex >= 0;
            var targetId = parsedProject.Id;
            var targetName = parsedProject.Name;

            if (wasConflict)
            {
                switch (conflictPolicy)
                {
                    case ProjectConflictPolicy.SkipIfExists:
                        return new ProjectImportResult(
                            true,
                            _projects[existingIndex],
                            $"\"{parsedProject.Name}\" projesi zaten mevcut olduğu için atlandı.",
                            WasConflictResolved: true,
                            PhotoMissingWarning: photoMissingWarning);

                    case ProjectConflictPolicy.ImportAsCopy:
                        targetId = Guid.NewGuid();
                        targetName = BuildUniqueCopyNameLocked(parsedProject.Name);
                        break;

                    case ProjectConflictPolicy.OverwriteExisting:
                        targetId = parsedProject.Id;
                        break;
                }
            }

            var storedPhotoRelativePath = resolvedSourcePhoto is not null
                ? CopyPhotoToPersistentStorageLocked(targetId, resolvedSourcePhoto, existingRelativePath: null)
                : null;

            var now = _timeProvider.GetUtcNow();
            var finalProject = new DesktopProject(
                targetId,
                targetName,
                parsedProject.Type,
                parsedProject.Description,
                parsedProject.Measurements,
                parsedProject.CreatedAt == default ? now : parsedProject.CreatedAt,
                now,
                isSample: false,
                photoRelativePath: storedPhotoRelativePath,
                photoSize: parsedProject.PhotoSize,
                referenceSegment: parsedProject.ReferenceSegment,
                referenceLength: parsedProject.ReferenceLength,
                measurementSegment: parsedProject.MeasurementSegment,
                measuredLength: parsedProject.MeasuredLength,
                displayUnit: parsedProject.DisplayUnit,
                originalPhotoFileName: parsedProject.OriginalPhotoFileName ?? (resolvedSourcePhoto is not null ? Path.GetFileName(resolvedSourcePhoto) : null),
                sourcePlatform: string.IsNullOrWhiteSpace(dto.SourcePlatform) ? "Mobil v0.4.0" : dto.SourcePlatform!);

            PersistProjectLocked(finalProject);

            if (wasConflict && conflictPolicy == ProjectConflictPolicy.OverwriteExisting)
            {
                _projects[existingIndex] = finalProject;
            }
            else
            {
                _projects.Add(finalProject);
            }

            OnProjectsChanged();

            var msg = photoMissingWarning
                ? $"\"{finalProject.Name}\" projesi içe aktarıldı (Uyarı: Fotoğraf dosyası pakette bulunamadı, ölçüm koordinatları yüklendi)."
                : (wasConflict && conflictPolicy == ProjectConflictPolicy.ImportAsCopy
                    ? $"Aynı kimlikli proje mevcut olduğu için \"{finalProject.Name}\" kopya olarak içe aktarıldı."
                    : $"\"{finalProject.Name}\" projesi başarıyla içe aktarıldı.");

            return new ProjectImportResult(true, finalProject, msg, wasConflict, photoMissingWarning);
        }
    }

    private string BuildUniqueCopyNameLocked(string baseName)
    {
        var candidate = $"{baseName} (İçe Aktarılan)";
        var counter = 2;
        while (_projects.Any(p => string.Equals(p.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            candidate = $"{baseName} (İçe Aktarılan {counter})";
            counter++;
        }

        return candidate;
    }

    private static string? ResolvePackagePhotoPath(string jsonDir, string packageRootDir, string? photoRelativePath)
    {
        var normalizedJsonDir = Path.GetFullPath(jsonDir);
        var normalizedPackageRoot = Path.GetFullPath(packageRootDir);

        if (!string.IsNullOrWhiteSpace(photoRelativePath))
        {
            if (TryResolveSafeRelativePath(normalizedPackageRoot, photoRelativePath, out var fromPackageRoot) &&
                File.Exists(fromPackageRoot))
            {
                return fromPackageRoot;
            }

            if (TryResolveSafeRelativePath(normalizedJsonDir, photoRelativePath, out var fromJsonDir) &&
                File.Exists(fromJsonDir))
            {
                return fromJsonDir;
            }

            var fileNameOnly = Path.GetFileName(photoRelativePath.Replace('\\', '/'));
            if (!string.IsNullOrWhiteSpace(fileNameOnly) && !ContainsPathTraversal(fileNameOnly))
            {
                var siblingByName = Path.Combine(normalizedJsonDir, fileNameOnly);
                if (File.Exists(siblingByName))
                {
                    return siblingByName;
                }
            }
        }

        // Mobil v0.4.0 standart photo.jpg dosyası aynı klasördeyse
        var defaultSibling = Path.Combine(normalizedJsonDir, PhotoFileName);
        if (File.Exists(defaultSibling))
        {
            return defaultSibling;
        }

        var defaultRoot = Path.Combine(normalizedPackageRoot, PhotoFileName);
        if (File.Exists(defaultRoot))
        {
            return defaultRoot;
        }

        return null;
    }

    private static bool IsValidImageFileHeader(string filePath, out string? error)
    {
        error = null;
        try
        {
            var ext = Path.GetExtension(filePath);
            if (!AllowedPhotoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                error = "Paketteki fotoğraf uzantısı desteklenmiyor (JPG, JPEG, PNG veya BMP olmalıdır).";
                return false;
            }

            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length < 8)
            {
                error = "Paketteki fotoğraf dosyası boş veya bozuk.";
                return false;
            }

            Span<byte> header = stackalloc byte[8];
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var read = fs.Read(header);
            if (read < 4)
            {
                error = "Paketteki fotoğraf başlığı okunamadı.";
                return false;
            }

            // JPEG: FF D8 FF
            var isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            var isPng = read >= 8 &&
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            // BMP: 42 4D ('BM')
            var isBmp = header[0] == 0x42 && header[1] == 0x4D;

            if (!isJpeg && !isPng && !isBmp)
            {
                error = "Paketteki fotoğraf dosyası geçerli bir görüntü (JPG/PNG/BMP) başlığı içermiyor.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Fotoğraf dosyası doğrulanamadı: {ex.Message}";
            return false;
        }
    }

    private static bool ContainsPathTraversal(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith('/') ||
            relativePath.StartsWith('\\') ||
            relativePath.Contains(':'))
        {
            return true;
        }

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => s == "..");
    }

    private static bool TryResolveSafeRelativePath(string rootDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (ContainsPathTraversal(relativePath))
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                  + Path.DirectorySeparatorChar);
            var normalizedRelative = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, normalizedRelative));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void LoadFromDisk(bool seedSamples)
    {
        if (_projectsRoot is null)
        {
            return;
        }

        CorruptedEntryCount = 0;
        RecoveredFromBackupCount = 0;
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
                RecoveredFromBackupCount++;
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
                File.WriteAllText(markerPath, "v0.3.0", Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
        else if (!File.Exists(markerPath) && _projects.Count > 0)
        {
            try
            {
                File.WriteAllText(markerPath, "v0.3.0", Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool TryLoadProjectFile(string filePath, out DesktopProject? project)
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

            return TryConvertDtoToProject(dto, defaultSourcePlatform: "PC", out project, out _);
        }
        catch (Exception)
        {
            project = null;
            return false;
        }
    }

    private void PersistProjectLocked(DesktopProject project)
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
            return $"{ProjectsFolderName}/{projectId:N}/{PhotoFileName}";
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
        var index = _projects.FindIndex(p => p.Id == id);
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

    private static LengthValue? CalculateMeasuredLength(
        ImageSegment referenceSegment,
        LengthValue? referenceLength,
        ImageSegment measurementSegment,
        LengthUnit displayUnit)
    {
        if (referenceLength is { IsPositive: true } validRef &&
            referenceSegment.IsFinite &&
            referenceSegment.ImageLength > 0.0001d &&
            measurementSegment.IsFinite)
        {
            var scale = new ReferenceScale(referenceSegment, validRef);
            return scale.Measure(measurementSegment, displayUnit);
        }

        return null;
    }

    private static DesktopProject Copy(
        DesktopProject source,
        string? name = null,
        string? description = null,
        string? photoRelativePath = null,
        DateTimeOffset? updatedAt = null,
        bool? isSample = null) =>
        new(
            source.Id,
            name ?? source.Name,
            source.Type,
            description ?? source.Description,
            source.Measurements,
            source.CreatedAt,
            updatedAt ?? source.UpdatedAt,
            isSample ?? source.IsSample,
            photoRelativePath ?? source.PhotoRelativePath,
            source.PhotoSize,
            source.ReferenceSegment,
            source.ReferenceLength,
            source.MeasurementSegment,
            source.MeasuredLength,
            source.DisplayUnit,
            source.OriginalPhotoFileName,
            source.SourcePlatform);

    private static ProjectDto ToDto(DesktopProject project) =>
        new()
        {
            SchemaVersion = "0.3.0",
            SourcePlatform = project.SourcePlatform,
            Id = project.Id,
            Name = project.Name,
            Type = project.Type,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsSample = project.IsSample,
            PhotoRelativePath = project.PhotoRelativePath,
            OriginalPhotoFileName = project.OriginalPhotoFileName,
            PhotoWidth = project.PhotoSize.Width,
            PhotoHeight = project.PhotoSize.Height,
            ReferenceStartX = project.ReferenceSegment.Start.X,
            ReferenceStartY = project.ReferenceSegment.Start.Y,
            ReferenceEndX = project.ReferenceSegment.End.X,
            ReferenceEndY = project.ReferenceSegment.End.Y,
            ReferenceLengthValue = project.ReferenceLength?.Value,
            ReferenceLengthUnit = project.ReferenceLength?.Unit,
            MeasurementStartX = project.MeasurementSegment.Start.X,
            MeasurementStartY = project.MeasurementSegment.Start.Y,
            MeasurementEndX = project.MeasurementSegment.End.X,
            MeasurementEndY = project.MeasurementSegment.End.Y,
            MeasuredLengthValue = project.MeasuredLength?.Value,
            MeasuredLengthUnit = project.MeasuredLength?.Unit,
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

    private static bool TryConvertDtoToProject(
        ProjectDto dto,
        string defaultSourcePlatform,
        out DesktopProject? project,
        out string? errorMessage)
    {
        project = null;
        errorMessage = null;

        if (dto.Id == Guid.Empty)
        {
            errorMessage = "Proje kimliği (Id) boş veya geçersiz.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errorMessage = "Proje adı boş olamaz.";
            return false;
        }

        if (!Enum.IsDefined(dto.Type))
        {
            errorMessage = "Desteklenmeyen proje türü.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dto.PhotoRelativePath) && ContainsPathTraversal(dto.PhotoRelativePath))
        {
            errorMessage = "Proje fotoğraf yolu geçersiz.";
            return false;
        }

        ImageSize photoSize = MeasurementSession.DemoImageSize;
        if (dto.PhotoWidth is not null || dto.PhotoHeight is not null)
        {
            if (dto.PhotoWidth is not { } pw || dto.PhotoHeight is not { } ph ||
                !double.IsFinite(pw) || !double.IsFinite(ph) || pw <= 0d || ph <= 0d)
            {
                errorMessage = "Fotoğraf piksel boyutları geçersiz (sıfırdan büyük ve sonlu olmalıdır).";
                return false;
            }

            photoSize = new ImageSize(pw, ph);
        }

        LengthUnit displayUnit = LengthUnit.Millimeter;
        if (dto.DisplayUnit is { } dUnit)
        {
            if (!Enum.IsDefined(dUnit))
            {
                errorMessage = "Projedeki gösterim birimi desteklenmiyor.";
                return false;
            }

            displayUnit = dUnit;
        }

        LengthValue? referenceLength = null;
        if (dto.ReferenceLengthValue is not null || dto.ReferenceLengthUnit is not null)
        {
            if (dto.ReferenceLengthValue is not { } rVal || dto.ReferenceLengthUnit is not { } rUnit ||
                !double.IsFinite(rVal) || rVal <= 0d || !Enum.IsDefined(rUnit))
            {
                errorMessage = "Referans gerçek uzunluğu veya birimi geçersiz.";
                return false;
            }

            referenceLength = new LengthValue(rVal, rUnit);
        }

        var defaultSession = new MeasurementSession(photoSize, displayUnit, useDemoDefaults: photoSize == MeasurementSession.DemoImageSize);
        ImageSegment referenceSegment = defaultSession.ReferenceSegment;

        if (dto.ReferenceStartX is not null || dto.ReferenceStartY is not null ||
            dto.ReferenceEndX is not null || dto.ReferenceEndY is not null)
        {
            if (dto.ReferenceStartX is not { } rx1 || dto.ReferenceStartY is not { } ry1 ||
                dto.ReferenceEndX is not { } rx2 || dto.ReferenceEndY is not { } ry2)
            {
                errorMessage = "Referans koordinatları (R1/R2) eksik.";
                return false;
            }

            var candidateRef = new ImageSegment(new ImagePoint(rx1, ry1), new ImagePoint(rx2, ry2));
            if (!candidateRef.IsFinite || candidateRef.ImageLength <= 0d)
            {
                errorMessage = "Referans noktaları R1 ve R2 geçerli ve birbirinden farklı olmalıdır.";
                return false;
            }

            referenceSegment = candidateRef;
        }

        var measurements = new List<ProjectMeasurementItem>();
        if (dto.Measurements is not null)
        {
            foreach (var m in dto.Measurements)
            {
                if (m is null || m.Id == Guid.Empty || string.IsNullOrWhiteSpace(m.Name) || !Enum.IsDefined(m.Kind))
                {
                    errorMessage = "Proje içindeki ölçüm kayıtlarından biri bozuk veya eksik.";
                    return false;
                }

                LengthValue? length = null;
                if (m.LengthValue is not null || m.LengthUnit is not null)
                {
                    if (m.LengthValue is not { } lv || m.LengthUnit is not { } lu ||
                        !double.IsFinite(lv) || lv < 0d || !Enum.IsDefined(lu))
                    {
                        errorMessage = "Ölçüm uzunluk değeri veya birimi geçersiz.";
                        return false;
                    }

                    length = new LengthValue(lv, lu);
                }

                ImageSegment? seg = null;
                if (m.StartX is not null || m.StartY is not null || m.EndX is not null || m.EndY is not null)
                {
                    if (m.StartX is not { } sx || m.StartY is not { } sy ||
                        m.EndX is not { } ex || m.EndY is not { } ey)
                    {
                        errorMessage = "Ölçüm koordinatları (M1/M2) eksik.";
                        return false;
                    }

                    var candidateSeg = new ImageSegment(new ImagePoint(sx, sy), new ImagePoint(ex, ey));
                    if (!candidateSeg.IsFinite)
                    {
                        errorMessage = "Ölçüm koordinatları sonlu sayılar olmalıdır.";
                        return false;
                    }

                    seg = candidateSeg;
                }

                measurements.Add(new ProjectMeasurementItem(
                    m.Id,
                    m.Name,
                    m.Kind,
                    length,
                    m.AngleDegrees,
                    seg));
            }
        }

        ImageSegment measurementSegment = defaultSession.Measurement;
        if (dto.MeasurementStartX is { } msx && dto.MeasurementStartY is { } msy &&
            dto.MeasurementEndX is { } mex && dto.MeasurementEndY is { } mey)
        {
            var candidateMeas = new ImageSegment(new ImagePoint(msx, msy), new ImagePoint(mex, mey));
            if (!candidateMeas.IsFinite)
            {
                errorMessage = "Ölçüm segmenti koordinatları geçersiz.";
                return false;
            }

            measurementSegment = candidateMeas;
        }
        else if (measurements.FirstOrDefault(m => m.ImageSegment is not null)?.ImageSegment is { } fromList)
        {
            measurementSegment = fromList;
        }

        LengthValue? measuredLength = null;
        if (dto.MeasuredLengthValue is { } mlv && dto.MeasuredLengthUnit is { } mlu &&
            double.IsFinite(mlv) && mlv >= 0d && Enum.IsDefined(mlu))
        {
            measuredLength = new LengthValue(mlv, mlu);
        }
        else if (measurements.FirstOrDefault(m => m.Kind == MeasurementKind.Length && m.Length is not null)?.Length is { } firstLen)
        {
            measuredLength = firstLen;
        }

        if (measurements.Count == 0)
        {
            var computed = CalculateMeasuredLength(referenceSegment, referenceLength, measurementSegment, displayUnit) ?? measuredLength;
            measurements.Add(new ProjectMeasurementItem(
                Guid.NewGuid(),
                DefaultMeasurementName,
                MeasurementKind.Length,
                computed,
                imageSegment: measurementSegment));
        }

        var sourcePlatform = !string.IsNullOrWhiteSpace(dto.SourcePlatform)
            ? dto.SourcePlatform!
            : defaultSourcePlatform;

        project = new DesktopProject(
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
            measurementSegment,
            measuredLength,
            displayUnit,
            dto.OriginalPhotoFileName,
            sourcePlatform);

        return true;
    }

    private void OnProjectsChanged() => ProjectsChanged?.Invoke(this, EventArgs.Empty);

    private static IReadOnlyList<DesktopProject> CreateSamples(DateTimeOffset now)
    {
        var plateMeasurementSeg = new ImageSegment(new ImagePoint(250d, 205d), new ImagePoint(700d, 205d));
        var plateRefSeg = new ImageSegment(new ImagePoint(200d, 500d), new ImagePoint(800d, 500d));
        var plateRefLen = new LengthValue(120d, LengthUnit.Millimeter);
        var plateMeasured = new ReferenceScale(plateRefSeg, plateRefLen).Measure(plateMeasurementSeg, LengthUnit.Millimeter);

        return
        [
            new DesktopProject(
                Guid.Parse("44396967-5211-4a26-b75d-1e0a204b0d01"),
                "Bağlantı plakası (Örnek)",
                ProjectType.ConnectionPlate,
                "İki delikli örnek plaka — kalibre edilmiş R1–R2 ve M1–M2 örnek ölçüm kaydı.",
                [
                    new ProjectMeasurementItem(
                        Guid.Parse("55396967-5211-4a26-b75d-1e0a204b0e01"),
                        "Plaka genişliği (M1–M2)",
                        MeasurementKind.Length,
                        plateMeasured,
                        imageSegment: plateMeasurementSeg)
                ],
                now.AddMinutes(-20),
                now.AddMinutes(-5),
                isSample: true,
                photoRelativePath: null,
                photoSize: new ImageSize(1000d, 600d),
                referenceSegment: plateRefSeg,
                referenceLength: plateRefLen,
                measurementSegment: plateMeasurementSeg,
                measuredLength: plateMeasured,
                displayUnit: LengthUnit.Millimeter,
                originalPhotoFileName: null,
                sourcePlatform: "PC")
        ];
    }

    private sealed class ProjectDto
    {
        public string? SchemaVersion { get; set; }
        public string? SourcePlatform { get; set; }
        public Guid Id { get; set; }
        public string? Name { get; set; }
        [JsonConverter(typeof(LenientEnumConverter<ProjectType>))]
        public ProjectType Type { get; set; } = ProjectType.PhotoMeasurement;
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsSample { get; set; }
        public string? PhotoRelativePath { get; set; }
        public string? OriginalPhotoFileName { get; set; }
        public double? PhotoWidth { get; set; }
        public double? PhotoHeight { get; set; }
        public double? ReferenceStartX { get; set; }
        public double? ReferenceStartY { get; set; }
        public double? ReferenceEndX { get; set; }
        public double? ReferenceEndY { get; set; }
        public double? ReferenceLengthValue { get; set; }
        [JsonConverter(typeof(NullableLenientEnumConverter<LengthUnit>))]
        public LengthUnit? ReferenceLengthUnit { get; set; }
        public double? MeasurementStartX { get; set; }
        public double? MeasurementStartY { get; set; }
        public double? MeasurementEndX { get; set; }
        public double? MeasurementEndY { get; set; }
        public double? MeasuredLengthValue { get; set; }
        [JsonConverter(typeof(NullableLenientEnumConverter<LengthUnit>))]
        public LengthUnit? MeasuredLengthUnit { get; set; }
        [JsonConverter(typeof(NullableLenientEnumConverter<LengthUnit>))]
        public LengthUnit? DisplayUnit { get; set; }
        public List<MeasurementDto>? Measurements { get; set; }
    }

    private sealed class MeasurementDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        [JsonConverter(typeof(LenientEnumConverter<MeasurementKind>))]
        public MeasurementKind Kind { get; set; }
        public double? LengthValue { get; set; }
        [JsonConverter(typeof(NullableLenientEnumConverter<LengthUnit>))]
        public LengthUnit? LengthUnit { get; set; }
        public double? AngleDegrees { get; set; }
        public double? StartX { get; set; }
        public double? StartY { get; set; }
        public double? EndX { get; set; }
        public double? EndY { get; set; }
    }

    private sealed class LenientEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), intVal);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str) && Enum.TryParse<TEnum>(str, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            throw new JsonException($"Geçersiz {typeof(TEnum).Name} değeri.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt32(value));
    }

    private sealed class NullableLenientEnumConverter<TEnum> : JsonConverter<TEnum?>
        where TEnum : struct, Enum
    {
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), intVal);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return null;
                }

                if (Enum.TryParse<TEnum>(str, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            throw new JsonException($"Geçersiz {typeof(TEnum).Name} değeri.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(Convert.ToInt32(value.Value));
        }
    }
}
