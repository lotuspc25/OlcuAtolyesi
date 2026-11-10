using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Mvvm;

namespace OlcuAtolyesi.PC.ViewModels;

public abstract class PageViewModelBase : ObservableObject
{
    protected PageViewModelBase(ModuleDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public ModuleDescriptor Descriptor { get; }

    public ModuleId Id => Descriptor.Id;

    public string Title => Descriptor.Title;

    public string IconGlyph => Descriptor.IconGlyph;

    public string ShortSummary => Descriptor.ShortSummary;

    public string DetailedScope => Descriptor.DetailedScope;

    public ModuleImplementationState State => Descriptor.State;

    public string StateBadge => Descriptor.State.BadgeText();

    public string TargetVersion => Descriptor.TargetVersion;

    public bool IsPlanned => Descriptor.State == ModuleImplementationState.Planned;

    public bool IsActive => Descriptor.State == ModuleImplementationState.Active;

    public bool IsFoundationReady => Descriptor.State == ModuleImplementationState.FoundationReady;
}
