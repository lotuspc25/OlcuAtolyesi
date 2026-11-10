namespace OlcuAtolyesi.Core.Measurement;

/// <summary>
/// Monitörün fiziksel ölçek kalibrasyonu: 1 DIP'in kaç milimetreye karşılık geldiği.
/// Yalnızca ekran cetveli (fiziksel mm) için kullanılır; fotoğraf ölçümünü ETKİLEMEZ.
/// </summary>
public sealed record ScreenCalibration(double MillimetresPerDip, string ReferenceName, bool IsCustom = true)
{
    public static ScreenCalibration None { get; } = new(0d, string.Empty, false);

    /// <summary>Standart 96 DPI varsayılan monitör ölçeği (25.4 / 96 mm/DIP).</summary>
    public static ScreenCalibration Default96Dpi { get; } = new(25.4d / 96d, "Standart 96 DPI", false);

    /// <summary>0,05–2,0 mm/DIP aralığı dışındaki değerler hatalı kabul edilir.</summary>
    public bool IsValid => double.IsFinite(MillimetresPerDip) &&
                           MillimetresPerDip > 0.05d &&
                           MillimetresPerDip < 2.0d;

    public double ToMillimetres(double dips) => IsValid ? dips * MillimetresPerDip : 0d;

    public double ToDips(double millimetres) => IsValid ? millimetres / MillimetresPerDip : 0d;

    /// <summary>Bilinen mm ve ekranda ölçülen DIP'ten kalibrasyon üretir.</summary>
    public static ScreenCalibration FromMeasurement(double knownMillimetres, double measuredDips, string referenceName = "")
    {
        if (!double.IsFinite(measuredDips) || measuredDips <= 1d) return None;
        if (!double.IsFinite(knownMillimetres) || knownMillimetres <= 0d) return None;

        return new ScreenCalibration(knownMillimetres / measuredDips, referenceName ?? string.Empty, true);
    }
}
