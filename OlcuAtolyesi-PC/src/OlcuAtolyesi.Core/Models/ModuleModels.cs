namespace OlcuAtolyesi.Core.Models;

public enum ModuleId
{
    Dashboard,
    Projects,
    PhoneConnection,
    CameraCalibration,
    PhotoMeasurement,
    CadExport,
    Print3D,
    Settings,
    Help
}

public enum ModuleImplementationState
{
    Active,
    FoundationReady,
    Planned
}

public static class ModuleImplementationStateExtensions
{
    public static string BadgeText(this ModuleImplementationState state) => state switch
    {
        ModuleImplementationState.Active => "Etkin",
        ModuleImplementationState.FoundationReady => "Temel Hazır",
        ModuleImplementationState.Planned => "Planlandı",
        _ => "Planlandı"
    };
}

public sealed record ModuleDescriptor(
    ModuleId Id,
    string Title,
    string IconGlyph,
    string ShortSummary,
    string DetailedScope,
    ModuleImplementationState State,
    string TargetVersion,
    IReadOnlyList<string> CurrentCapabilities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> OpenSourceCandidates);

public sealed record OpenSourceCandidate(
    string Name,
    string RepositoryUrl,
    string License,
    string IntegrationStrategy,
    string PurposeInWorkspace);
