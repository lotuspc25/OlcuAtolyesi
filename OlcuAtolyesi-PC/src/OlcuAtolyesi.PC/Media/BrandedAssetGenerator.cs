using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OlcuAtolyesi.PC.Media;

/// <summary>
/// Ölçü Atölyesi v0.5.0.3 kurumsal kimlik, kumpas temalı ikon, açılış ekranı (splash)
/// ve ölçeklenebilir SVG/ICO/PNG kaynaklarını üreten ve doğrulayan servis.
/// </summary>
public static class BrandedAssetGenerator
{
    public const string BrandTitle = "Ölçü Atölyesi";
    public const string BrandSubtitle = "Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm";

    public static readonly Color NavyDark = Color.FromRgb(0x05, 0x0C, 0x1A);
    public static readonly Color NavyBase = Color.FromRgb(0x0A, 0x19, 0x2F);
    public static readonly Color NavyLight = Color.FromRgb(0x11, 0x22, 0x40);
    public static readonly Color Anthracite = Color.FromRgb(0x1E, 0x29, 0x3B);
    public static readonly Color SlateMuted = Color.FromRgb(0x64, 0x74, 0x8B);
    public static readonly Color Turquoise = Color.FromRgb(0x00, 0xAD, 0xB5);
    public static readonly Color CyanGlow = Color.FromRgb(0x00, 0xE5, 0xFF);
    public static readonly Color SkyBlue = Color.FromRgb(0x38, 0xBD, 0xF8);

    public static string GenerateAppIconBackgroundSvg()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="456" height="456" viewBox="0 0 456 456">
              <defs>
                <radialGradient id="navyGlow" cx="50%" cy="46%" r="65%">
                  <stop offset="0%" stop-color="#112240"/>
                  <stop offset="65%" stop-color="#0A192F"/>
                  <stop offset="100%" stop-color="#050C1A"/>
                </radialGradient>
                <pattern id="gridPattern" width="24" height="24" patternUnits="userSpaceOnUse">
                  <path d="M 24 0 L 0 0 0 24" fill="none" stroke="#1E3A5F" stroke-width="0.8" opacity="0.35"/>
                </pattern>
                <linearGradient id="edgeGlow" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stop-color="#00ADB5" stop-opacity="0.3"/>
                  <stop offset="50%" stop-color="#0A192F" stop-opacity="0"/>
                  <stop offset="100%" stop-color="#38BDF8" stop-opacity="0.2"/>
                </linearGradient>
              </defs>
              <rect width="456" height="456" fill="url(#navyGlow)"/>
              <rect width="456" height="456" fill="url(#gridPattern)"/>
              <rect width="456" height="456" fill="url(#edgeGlow)"/>
            </svg>
            """;
    }

    public static string GenerateAppIconForegroundSvg()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="456" height="456" viewBox="0 0 456 456" fill="none">
              <defs>
                <linearGradient id="beamGrad" x1="0%" y1="0%" x2="100%" y2="0%">
                  <stop offset="0%" stop-color="#1E293B"/>
                  <stop offset="50%" stop-color="#334155"/>
                  <stop offset="100%" stop-color="#1E293B"/>
                </linearGradient>
                <linearGradient id="sliderGrad" x1="0%" y1="0%" x2="0%" y2="100%">
                  <stop offset="0%" stop-color="#1E293B"/>
                  <stop offset="100%" stop-color="#0F172A"/>
                </linearGradient>
                <linearGradient id="laserGrad" x1="0%" y1="0%" x2="100%" y2="0%">
                  <stop offset="0%" stop-color="#00ADB5"/>
                  <stop offset="50%" stop-color="#00E5FF"/>
                  <stop offset="100%" stop-color="#38BDF8"/>
                </linearGradient>
              </defs>

              <!-- 1. Kamera Optik Vizör / Reticle Katmanı -->
              <circle cx="228" cy="228" r="92" stroke="#00ADB5" stroke-width="2" stroke-opacity="0.45" stroke-dasharray="6,4"/>
              <circle cx="228" cy="228" r="54" stroke="#38BDF8" stroke-width="2.5" stroke-opacity="0.8"/>
              
              <!-- Vizör Artı Kıl Kesişimleri -->
              <line x1="228" y1="126" x2="228" y2="140" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
              <line x1="228" y1="316" x2="228" y2="330" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
              <line x1="126" y1="228" x2="140" y2="228" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
              <line x1="316" y1="228" x2="330" y2="228" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>

              <!-- Vizör Köşe Braketleri -->
              <path d="M 126 148 L 126 136 L 138 136" stroke="#00E5FF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M 330 148 L 330 136 L 318 136" stroke="#00E5FF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M 126 308 L 126 320 L 138 320" stroke="#00E5FF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M 330 308 L 330 320 L 318 320" stroke="#00E5FF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>

              <!-- 2. Kumpas Ana Gövde ve Cetvel Bölüntüleri -->
              <rect x="132" y="188" width="196" height="28" rx="2" fill="url(#beamGrad)" stroke="#334155" stroke-width="1.5"/>
              
              <!-- Cetvel Çizgileri -->
              <line x1="150" y1="188" x2="150" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="160" y1="188" x2="160" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="170" y1="188" x2="170" y2="196" stroke="#38BDF8" stroke-width="1.8"/>
              <line x1="180" y1="188" x2="180" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="190" y1="188" x2="190" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="200" y1="188" x2="200" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="210" y1="188" x2="210" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="220" y1="188" x2="220" y2="196" stroke="#38BDF8" stroke-width="1.8"/>
              <line x1="270" y1="188" x2="270" y2="196" stroke="#38BDF8" stroke-width="1.8"/>
              <line x1="280" y1="188" x2="280" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="290" y1="188" x2="290" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="300" y1="188" x2="300" y2="193" stroke="#64748B" stroke-width="1.2"/>
              <line x1="310" y1="188" x2="310" y2="193" stroke="#64748B" stroke-width="1.2"/>

              <!-- 3. Sabit Çene (Sol) -->
              <!-- Üst İç Çap Çenesi -->
              <path d="M 142 188 L 142 142 L 162 188 Z" fill="#334155" stroke="#64748B" stroke-width="1.5"/>
              <!-- Alt Dış Çap Çenesi -->
              <path d="M 142 216 L 142 298 Q 158 280 170 216 Z" fill="#334155" stroke="#64748B" stroke-width="1.5"/>
              <line x1="142" y1="216" x2="142" y2="298" stroke="#00E5FF" stroke-width="2"/>

              <!-- 4. Hareketli Sürgü Gövdesi ve Çenesi (Sağ) -->
              <!-- Üst Hareketli İç Çap Çenesi -->
              <path d="M 222 180 L 222 142 L 238 180 Z" fill="#1E293B" stroke="#00ADB5" stroke-width="1.5"/>
              <!-- Alt Hareketli Dış Çap Çenesi -->
              <path d="M 222 244 L 222 298 Q 242 280 254 244 Z" fill="#1E293B" stroke="#00ADB5" stroke-width="1.5"/>
              <line x1="222" y1="244" x2="222" y2="298" stroke="#00E5FF" stroke-width="2"/>
              
              <!-- Sürgü Ana Kutusu -->
              <rect x="222" y="180" width="64" height="64" rx="5" fill="url(#sliderGrad)" stroke="#00ADB5" stroke-width="2"/>
              
              <!-- Dijital LCD Ekran -->
              <rect x="228" y="194" width="52" height="24" rx="3" fill="#050B14" stroke="#22D3EE" stroke-width="1.2"/>
              <text x="254" y="211" font-family="'Segoe UI', monospace, sans-serif" font-weight="900" font-size="12" fill="#00E5FF" text-anchor="middle" letter-spacing="0.5">26.15</text>

              <!-- 5. İki Ölçüm Noktası (R1 - R2) ve Lazer Boyut Çizgisi -->
              <!-- Lazer Çizgisi -->
              <line x1="142" y1="298" x2="222" y2="298" stroke="url(#laserGrad)" stroke-width="2.5"/>
              <line x1="142" y1="290" x2="142" y2="306" stroke="#00E5FF" stroke-width="2"/>
              <line x1="222" y1="290" x2="222" y2="306" stroke="#00E5FF" stroke-width="2"/>

              <!-- R1 Hedef Noktası -->
              <circle cx="142" cy="298" r="8" fill="#00E5FF" fill-opacity="0.3"/>
              <circle cx="142" cy="298" r="3.5" fill="#FFFFFF"/>

              <!-- R2 Hedef Noktası -->
              <circle cx="222" cy="298" r="8" fill="#00E5FF" fill-opacity="0.3"/>
              <circle cx="222" cy="298" r="3.5" fill="#FFFFFF"/>
            </svg>
            """;
    }

    public static string GenerateSplashSvg()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512" fill="none">
              <defs>
                <radialGradient id="splashBg" cx="50%" cy="40%" r="70%">
                  <stop offset="0%" stop-color="#112240"/>
                  <stop offset="60%" stop-color="#0A192F"/>
                  <stop offset="100%" stop-color="#050C1A"/>
                </radialGradient>
                <linearGradient id="textCyan" x1="0%" y1="0%" x2="100%" y2="0%">
                  <stop offset="0%" stop-color="#FFFFFF"/>
                  <stop offset="100%" stop-color="#22D3EE"/>
                </linearGradient>
              </defs>

              <rect width="512" height="512" fill="url(#splashBg)"/>

              <!-- Merkezi Kumpas ve Kamera Vizörü -->
              <g transform="translate(28, 20)">
                <!-- Kamera Vizörü -->
                <circle cx="228" cy="200" r="96" stroke="#00ADB5" stroke-width="2" stroke-opacity="0.4" stroke-dasharray="8,5"/>
                <circle cx="228" cy="200" r="56" stroke="#38BDF8" stroke-width="2.5" stroke-opacity="0.8"/>
                
                <line x1="228" y1="96" x2="228" y2="112" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
                <line x1="228" y1="288" x2="228" y2="304" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
                <line x1="124" y1="200" x2="140" y2="200" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>
                <line x1="316" y1="200" x2="332" y2="200" stroke="#00E5FF" stroke-width="2.5" stroke-linecap="round"/>

                <!-- Kumpas Cetvel Gövdesi -->
                <rect x="132" y="160" width="196" height="28" rx="2" fill="#1E293B" stroke="#334155" stroke-width="1.5"/>
                <line x1="160" y1="160" x2="160" y2="168" stroke="#38BDF8" stroke-width="2"/>
                <line x1="190" y1="160" x2="190" y2="168" stroke="#38BDF8" stroke-width="2"/>
                <line x1="220" y1="160" x2="220" y2="168" stroke="#38BDF8" stroke-width="2"/>
                <line x1="270" y1="160" x2="270" y2="168" stroke="#38BDF8" stroke-width="2"/>
                <line x1="300" y1="160" x2="300" y2="168" stroke="#38BDF8" stroke-width="2"/>

                <!-- Sabit Çene -->
                <path d="M 142 160 L 142 114 L 162 160 Z" fill="#334155" stroke="#64748B" stroke-width="1.5"/>
                <path d="M 142 188 L 142 270 Q 158 252 170 188 Z" fill="#334155" stroke="#64748B" stroke-width="1.5"/>
                <line x1="142" y1="188" x2="142" y2="270" stroke="#00E5FF" stroke-width="2"/>

                <!-- Hareketli Sürgü ve Çene -->
                <path d="M 222 152 L 222 114 L 238 152 Z" fill="#1E293B" stroke="#00ADB5" stroke-width="1.5"/>
                <path d="M 222 216 L 222 270 Q 242 252 254 216 Z" fill="#1E293B" stroke="#00ADB5" stroke-width="1.5"/>
                <line x1="222" y1="216" x2="222" y2="270" stroke="#00E5FF" stroke-width="2"/>
                <rect x="222" y="152" width="64" height="64" rx="5" fill="#0F172A" stroke="#00ADB5" stroke-width="2"/>
                <rect x="228" y="166" width="52" height="24" rx="3" fill="#050B14" stroke="#22D3EE" stroke-width="1.2"/>
                <text x="254" y="183" font-family="'Segoe UI', monospace, sans-serif" font-weight="900" font-size="12" fill="#00E5FF" text-anchor="middle">26.15</text>

                <!-- Lazer ve Ölçüm Noktaları R1 - R2 -->
                <line x1="142" y1="270" x2="222" y2="270" stroke="#00E5FF" stroke-width="2.5"/>
                <line x1="142" y1="262" x2="142" y2="278" stroke="#00E5FF" stroke-width="2"/>
                <line x1="222" y1="262" x2="222" y2="278" stroke="#00E5FF" stroke-width="2"/>
                <circle cx="142" cy="270" r="7" fill="#00E5FF" fill-opacity="0.3"/>
                <circle cx="142" cy="270" r="3.5" fill="#FFFFFF"/>
                <circle cx="222" cy="270" r="7" fill="#00E5FF" fill-opacity="0.3"/>
                <circle cx="222" cy="270" r="3.5" fill="#FFFFFF"/>
              </g>

              <!-- Başlık ve Alt Başlık -->
              <text x="256" y="380" font-family="'Segoe UI', 'Open Sans', sans-serif" font-weight="bold" font-size="34" fill="url(#textCyan)" text-anchor="middle" letter-spacing="1">Ölçü Atölyesi</text>
              <text x="256" y="415" font-family="'Segoe UI', 'Open Sans', sans-serif" font-weight="600" font-size="13" fill="#38BDF8" text-anchor="middle" letter-spacing="0.5">Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm</text>
            </svg>
            """;
    }

    public static byte[] RenderEmblemPng(int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            DrawEmblem(dc, size, size);
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public static byte[] RenderSplashPng(int width = 700, int height = 380)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            DrawSplashVisual(dc, width, height);
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public static byte[] BuildIco(params int[] sizes)
    {
        if (sizes == null || sizes.Length == 0)
        {
            sizes = [16, 24, 32, 48, 64, 128, 256];
        }

        var frames = new List<(int size, byte[] pngBytes)>();
        foreach (var s in sizes)
        {
            frames.Add((s, RenderEmblemPng(s)));
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((ushort)0); // Reserved
        writer.Write((ushort)1); // Type 1 = ICO
        writer.Write((ushort)frames.Count);

        int offset = 6 + (frames.Count * 16);

        foreach (var (size, pngBytes) in frames)
        {
            byte b = (byte)(size >= 256 ? 0 : size);
            writer.Write(b); // Width
            writer.Write(b); // Height
            writer.Write((byte)0); // Color count
            writer.Write((byte)0); // Reserved
            writer.Write((ushort)1); // Color planes
            writer.Write((ushort)32); // Bits per pixel
            writer.Write((uint)pngBytes.Length);
            writer.Write((uint)offset);
            offset += pngBytes.Length;
        }

        foreach (var (_, pngBytes) in frames)
        {
            writer.Write(pngBytes);
        }

        return ms.ToArray();
    }

    private static void DrawEmblem(DrawingContext dc, double width, double height)
    {
        double scale = width / 456.0;
        dc.PushTransform(new ScaleTransform(scale, scale));

        // 1. Arka plan dairesi
        var bgBrush = new RadialGradientBrush(NavyLight, NavyDark)
        {
            Center = new Point(0.5, 0.46),
            GradientOrigin = new Point(0.5, 0.46),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        dc.DrawRoundedRectangle(bgBrush, new Pen(new SolidColorBrush(Turquoise), 2.0), new Rect(8, 8, 440, 440), 96, 96);

        // 2. Kamera Reticle Çemberleri
        var reticlePen = new Pen(new SolidColorBrush(Turquoise), 2.0)
        {
            DashStyle = new DashStyle([4, 3], 0)
        };
        dc.DrawEllipse(null, reticlePen, new Point(228, 228), 92, 92);

        var innerRingPen = new Pen(new SolidColorBrush(SkyBlue), 2.5);
        dc.DrawEllipse(null, innerRingPen, new Point(228, 228), 54, 54);

        // 3. Vizör Çapraz Ticks
        var cyanPen = new Pen(new SolidColorBrush(CyanGlow), 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(cyanPen, new Point(228, 126), new Point(228, 140));
        dc.DrawLine(cyanPen, new Point(228, 316), new Point(228, 330));
        dc.DrawLine(cyanPen, new Point(126, 228), new Point(140, 228));
        dc.DrawLine(cyanPen, new Point(316, 228), new Point(330, 228));

        // 4. Kumpas Ana Gövdesi (Cetvel)
        var beamBrush = new LinearGradientBrush(Anthracite, Color.FromRgb(0x33, 0x41, 0x55), 0);
        dc.DrawRoundedRectangle(beamBrush, new Pen(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), 1.5), new Rect(132, 188, 196, 28), 3, 3);

        // Cetvel Çizgileri
        var tickPen = new Pen(new SolidColorBrush(SlateMuted), 1.2);
        var majorTickPen = new Pen(new SolidColorBrush(SkyBlue), 1.8);
        for (int x = 145; x <= 320; x += 10)
        {
            bool isMajor = (x == 165 || x == 215 || x == 265 || x == 315);
            dc.DrawLine(isMajor ? majorTickPen : tickPen, new Point(x, 188), new Point(x, isMajor ? 197 : 193));
        }

        // 5. Sabit Çene (Sol)
        var fixedJawGeom = new StreamGeometry();
        using (var ctx = fixedJawGeom.Open())
        {
            ctx.BeginFigure(new Point(142, 188), true, true);
            ctx.LineTo(new Point(142, 142), true, false);
            ctx.LineTo(new Point(162, 188), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), new Pen(new SolidColorBrush(SlateMuted), 1.5), fixedJawGeom);

        var fixedLowerJaw = new StreamGeometry();
        using (var ctx = fixedLowerJaw.Open())
        {
            ctx.BeginFigure(new Point(142, 216), true, true);
            ctx.LineTo(new Point(142, 298), true, false);
            ctx.QuadraticBezierTo(new Point(158, 280), new Point(170, 216), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), new Pen(new SolidColorBrush(SlateMuted), 1.5), fixedLowerJaw);
        dc.DrawLine(new Pen(new SolidColorBrush(CyanGlow), 2.2), new Point(142, 216), new Point(142, 298));

        // 6. Hareketli Sürgü ve Çene (Sağ)
        var slidingUpperJaw = new StreamGeometry();
        using (var ctx = slidingUpperJaw.Open())
        {
            ctx.BeginFigure(new Point(222, 180), true, true);
            ctx.LineTo(new Point(222, 142), true, false);
            ctx.LineTo(new Point(238, 180), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Anthracite), new Pen(new SolidColorBrush(Turquoise), 1.5), slidingUpperJaw);

        var slidingLowerJaw = new StreamGeometry();
        using (var ctx = slidingLowerJaw.Open())
        {
            ctx.BeginFigure(new Point(222, 244), true, true);
            ctx.LineTo(new Point(222, 298), true, false);
            ctx.QuadraticBezierTo(new Point(242, 280), new Point(254, 244), true, false);
        }
        dc.DrawGeometry(new SolidColorBrush(Anthracite), new Pen(new SolidColorBrush(Turquoise), 1.5), slidingLowerJaw);
        dc.DrawLine(new Pen(new SolidColorBrush(CyanGlow), 2.2), new Point(222, 244), new Point(222, 298));

        // Sürgü Gövdesi
        var sliderBrush = new LinearGradientBrush(Anthracite, Color.FromRgb(0x0F, 0x17, 0x2A), 90);
        dc.DrawRoundedRectangle(sliderBrush, new Pen(new SolidColorBrush(Turquoise), 2.0), new Rect(222, 180, 64, 64), 6, 6);

        // Dijital LCD Ekran
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x05, 0x0B, 0x14)), new Pen(new SolidColorBrush(SkyBlue), 1.4), new Rect(228, 194, 52, 24), 3, 3);

        var ft = new FormattedText(
            "26.15",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI, Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            new SolidColorBrush(CyanGlow),
            96)
        {
            TextAlignment = TextAlignment.Center
        };
        dc.DrawText(ft, new Point(254, 198));

        // 7. Lazer Boyut Çizgisi ve R1/R2 Hedef Noktaları
        var laserPen = new Pen(new SolidColorBrush(CyanGlow), 2.5);
        dc.DrawLine(laserPen, new Point(142, 298), new Point(222, 298));
        dc.DrawLine(laserPen, new Point(142, 290), new Point(142, 306));
        dc.DrawLine(laserPen, new Point(222, 290), new Point(222, 306));

        // R1 Noktası
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 229, 255)), null, new Point(142, 298), 8, 8);
        dc.DrawEllipse(Brushes.White, null, new Point(142, 298), 3.5, 3.5);

        // R2 Noktası
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 229, 255)), null, new Point(222, 298), 8, 8);
        dc.DrawEllipse(Brushes.White, null, new Point(222, 298), 3.5, 3.5);

        dc.Pop(); // Restore scale
    }

    private static void DrawSplashVisual(DrawingContext dc, double width, double height)
    {
        // 1. Koyu lacivert zemin
        var bg = new RadialGradientBrush(NavyLight, NavyDark)
        {
            Center = new Point(0.35, 0.45),
            GradientOrigin = new Point(0.35, 0.45),
            RadiusX = 0.85,
            RadiusY = 0.85
        };
        dc.DrawRectangle(bg, new Pen(new SolidColorBrush(Turquoise), 1.5), new Rect(0, 0, width, height));

        // 2. İnce teknik ızgara
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(28, 30, 58, 95)), 0.8);
        for (double x = 0; x <= width; x += 30)
        {
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
        }
        for (double y = 0; y <= height; y += 30)
        {
            dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        // 3. Sol Taraf: Amblem
        dc.PushTransform(new TranslateTransform(40, (height - 200) / 2));
        DrawEmblem(dc, 200, 200);
        dc.Pop();

        // 4. Sağ Taraf: Tipografi
        double textX = 265;
        double textY = 110;

        // Başlık: Ölçü Atölyesi
        var titleFt = new FormattedText(
            BrandTitle,
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            38,
            Brushes.White,
            96);
        dc.DrawText(titleFt, new Point(textX, textY));

        // Kategori / Rozet: v0.5.0.3 Masaüstü İstasyonu
        var badgeBrush = new SolidColorBrush(Color.FromArgb(40, 0, 173, 181));
        dc.DrawRoundedRectangle(badgeBrush, new Pen(new SolidColorBrush(Turquoise), 1.0), new Rect(textX, textY + 54, 170, 24), 4, 4);
        var badgeFt = new FormattedText(
            "v0.5.0.3 • Masaüstü Sürümü",
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            11.5,
            new SolidColorBrush(CyanGlow),
            96);
        dc.DrawText(badgeFt, new Point(textX + 10, textY + 58));

        // Alt Başlık: Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm
        var subtitleFt = new FormattedText(
            BrandSubtitle,
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            13,
            new SolidColorBrush(SkyBlue),
            96)
        {
            MaxTextWidth = width - textX - 25
        };
        dc.DrawText(subtitleFt, new Point(textX, textY + 95));

        // Alt Açıklama: Endüstriyel hassasiyet
        var descFt = new FormattedText(
            "Yüksek hassasiyetli 2B/3B optik geometri ve doğrudan Android kamera akışı",
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            12,
            new SolidColorBrush(SlateMuted),
            96)
        {
            MaxTextWidth = width - textX - 25
        };
        dc.DrawText(descFt, new Point(textX, textY + 128));
    }

    public static void GenerateAllAssets(string pcProjectDir, string androidProjectDir)
    {
        // 1. Android SVG Dosyaları
        var androidAppIconDir = Path.Combine(androidProjectDir, "OlcuAtolyesi", "Resources", "AppIcon");
        var androidSplashDir = Path.Combine(androidProjectDir, "OlcuAtolyesi", "Resources", "Splash");
        Directory.CreateDirectory(androidAppIconDir);
        Directory.CreateDirectory(androidSplashDir);

        File.WriteAllText(Path.Combine(androidAppIconDir, "appicon.svg"), GenerateAppIconBackgroundSvg(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(androidAppIconDir, "appiconfg.svg"), GenerateAppIconForegroundSvg(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(androidSplashDir, "splash.svg"), GenerateSplashSvg(), Encoding.UTF8);

        // 2. PC Kaynak Klasörü
        var pcResourcesDir = Path.Combine(pcProjectDir, "src", "OlcuAtolyesi.PC", "Resources");
        Directory.CreateDirectory(pcResourcesDir);

        File.WriteAllText(Path.Combine(pcResourcesDir, "appicon.svg"), GenerateAppIconForegroundSvg(), Encoding.UTF8);

        // Multi-resolution ICO: 16, 24, 32, 48, 64, 128, 256
        var icoBytes = BuildIco(16, 24, 32, 48, 64, 128, 256);
        File.WriteAllBytes(Path.Combine(pcResourcesDir, "app.ico"), icoBytes);

        // 256x256 PNG ikon
        var png256 = RenderEmblemPng(256);
        File.WriteAllBytes(Path.Combine(pcResourcesDir, "app_icon.png"), png256);

        // Windows WPF Native Splash Screen PNG (700x380)
        var splashPng = RenderSplashPng(700, 380);
        File.WriteAllBytes(Path.Combine(pcResourcesDir, "splash.png"), splashPng);
    }
}
