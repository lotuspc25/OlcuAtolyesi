using OlcuAtolyesi.Core.Models;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed record PlannedActionItem(string ButtonTitle, string Description);

public sealed class ModulePageViewModel : PageViewModelBase
{
    public ModulePageViewModel(
        ModuleDescriptor descriptor,
        string architectureNote,
        IReadOnlyList<PlannedActionItem> plannedActions)
        : base(descriptor)
    {
        ArchitectureNote = architectureNote;
        PlannedActions = plannedActions;
    }

    public string ArchitectureNote { get; }

    public IReadOnlyList<string> CurrentCapabilities => Descriptor.CurrentCapabilities;

    public IReadOnlyList<string> PlannedCapabilities => Descriptor.PlannedCapabilities;

    public IReadOnlyList<string> OpenSourceCandidates => Descriptor.OpenSourceCandidates;

    public IReadOnlyList<PlannedActionItem> PlannedActions { get; }
}
