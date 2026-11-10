using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Services;

namespace OlcuAtolyesi.PC.ViewModels;

public sealed class HelpViewModel : PageViewModelBase
{
    public HelpViewModel(ModuleDescriptor descriptor, ModuleCatalogService catalog)
        : base(descriptor)
    {
        OpenSourceCandidates = catalog.GetOpenSourceCandidates();
        SharedLibraryNotes =
        [
            "Ortak Geometri ve Birim Çekirdeği (OlcuAtolyesi.Core): Mobil uygulamadan salt okunur incelenen LengthUnit, LengthValue, ImagePoint, ImageSegment, ReferenceScale, ReferenceLengthParser ve MeasurementGeometry yapıları bağımsız bir .NET 10 sınıf kütüphanesi olarak ayrıştırılmıştır.",
            "Yeniden Kullanım Önerisi: İlerleyen sürümlerde hem Android (.NET MAUI) hem Windows Masaüstü (WPF) çözümleri aynı OlcuAtolyesi.Core projesini referans alarak ölçüm matematiği, proje JSON şeması ve DXF/STL dışa aktarma kodlarını %100 ortak kullanabilir.",
            "Lisans Güvenliği: Apache-2.0 (OpenCV) ve MIT (ezdxf) doğrudan kullanılabilir; LGPL-2.1 (Open CASCADE / FreeCAD) dinamik kütüphane veya CLI olarak bağlanmalı; GPL-3.0 (OpenScan) ve AGPL-3.0 (OrcaSlicer / PrusaSlicer) projelerinden kesinlikle kaynak kod kopyalanmamalı, yalnızca ayrı süreç (CLI) veya ağ protokolü üzerinden haberleşilmelidir."
        ];
    }

    public IReadOnlyList<OpenSourceCandidate> OpenSourceCandidates { get; }

    public IReadOnlyList<string> SharedLibraryNotes { get; }
}
