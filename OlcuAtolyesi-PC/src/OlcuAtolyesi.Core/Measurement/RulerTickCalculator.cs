namespace OlcuAtolyesi.Core.Measurement;

/// <summary>Cetvel taksimatı: seçilen adım, ekrandaki piksel karşılığı ve alt bölme sayısı.</summary>
public readonly record struct RulerTickStep(double StepValue, double StepPixels, int Subdivisions = 5);

/// <summary>
/// Cetvel taksimat aralığını seçer. Ekranda hedef aralığa (varsayılan 70 px) en yakın
/// "yuvarlak" değeri döndürür. Böylece yakınlaştırmaya göre 1/5/10/50 mm gibi aralıklar seçilir.
/// </summary>
public static class RulerTickCalculator
{
    public static readonly double[] MillimetreCandidates =
        [0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000];

    public static readonly double[] PixelCandidates =
        [5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000];

    /// <summary>Ekranda 1 mm kaç piksel olduğu biliniyorsa milimetre taksimatı seçer.</summary>
    public static RulerTickStep ChooseMillimetreStep(double screenPixelsPerMillimetre, double targetSpacing = 90d)
    {
        if (!double.IsFinite(screenPixelsPerMillimetre) || screenPixelsPerMillimetre <= 0d ||
            !double.IsFinite(targetSpacing) || targetSpacing <= 0d)
        {
            return new RulerTickStep(0d, 0d, 5);
        }

        var best = MillimetreCandidates[0];
        var bestDelta = double.MaxValue;

        foreach (var candidate in MillimetreCandidates)
        {
            var pixels = candidate * screenPixelsPerMillimetre;
            var delta = Math.Abs(pixels - targetSpacing);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = candidate;
            }
        }

        return new RulerTickStep(best, best * screenPixelsPerMillimetre, 5);
    }

    /// <summary>Milimetre bilinmiyorsa piksel taksimatı seçer (ekranda 1 görüntü pikseli = scale px).</summary>
    public static RulerTickStep ChoosePixelStep(double screenPixelsPerImagePixel, double targetSpacing = 60d)
    {
        if (!double.IsFinite(screenPixelsPerImagePixel) || screenPixelsPerImagePixel <= 0d ||
            !double.IsFinite(targetSpacing) || targetSpacing <= 0d)
        {
            return new RulerTickStep(0d, 0d, 5);
        }

        var best = PixelCandidates[0];
        var bestDelta = double.MaxValue;

        foreach (var candidate in PixelCandidates)
        {
            var pixels = candidate * screenPixelsPerImagePixel;
            var delta = Math.Abs(pixels - targetSpacing);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = candidate;
            }
        }

        return new RulerTickStep(best, best * screenPixelsPerImagePixel, 5);
    }
}
