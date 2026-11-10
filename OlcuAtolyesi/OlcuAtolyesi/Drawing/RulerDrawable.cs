using Microsoft.Maui.Graphics;

namespace OlcuAtolyesi.Drawing;

public sealed class RulerDrawable : IDrawable
{
    private const float SidePadding = 24f;

    public double StartPosition { get; set; } = 0.24;
    public double EndPosition { get; set; } = 0.68;
    public string DistanceText { get; set; } = string.Empty;
    public bool IsLocked { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var palette = ThemePalette.Current;
        canvas.FillColor = palette.Surface;
        canvas.FillRoundedRectangle(dirtyRect, 16);

        var left = SidePadding;
        var right = Math.Max(left + 1, dirtyRect.Width - SidePadding);
        var rulerTop = 48f;
        var baseline = Math.Min(dirtyRect.Height - 86f, 172f);
        var rulerWidth = right - left;

        canvas.StrokeColor = palette.Border;
        canvas.StrokeSize = 1;
        canvas.DrawLine(left, baseline, right, baseline);

        for (var index = 0; index <= 100; index++)
        {
            var x = left + (rulerWidth * index / 100f);
            var isMajor = index % 10 == 0;
            var isMedium = index % 5 == 0;
            var height = isMajor ? 32f : isMedium ? 22f : 12f;
            canvas.StrokeColor = isMajor ? palette.Text : palette.Muted;
            canvas.StrokeSize = isMajor ? 1.6f : 1f;
            canvas.DrawLine(x, baseline, x, baseline - height);

            if (isMajor && index < 100)
            {
                canvas.FontColor = palette.Muted;
                canvas.FontSize = 10;
                canvas.DrawString(index.ToString(), x - 12, baseline + 5, 24, 20,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        var startX = PositionToX(dirtyRect.Width, StartPosition);
        var endX = PositionToX(dirtyRect.Width, EndPosition);
        DrawGuide(canvas, startX, rulerTop, baseline + 42, palette.Primary, IsLocked);
        DrawGuide(canvas, endX, rulerTop, baseline + 42, palette.Primary, IsLocked);

        var arrowY = 32f;
        canvas.StrokeColor = palette.Accent;
        canvas.StrokeSize = 2;
        canvas.DrawLine(startX, arrowY, endX, arrowY);
        DrawArrowHead(canvas, startX, arrowY, 1, palette.Accent);
        DrawArrowHead(canvas, endX, arrowY, -1, palette.Accent);

        var labelWidth = Math.Min(170f, Math.Max(108f, Math.Abs(endX - startX)));
        var labelX = ((startX + endX) / 2f) - (labelWidth / 2f);
        canvas.FillColor = palette.Background;
        canvas.FillRoundedRectangle(labelX, arrowY - 16, labelWidth, 32, 12);
        canvas.FontColor = palette.Text;
        canvas.FontSize = 14;
        canvas.DrawString(DistanceText, labelX + 6, arrowY - 14, labelWidth - 12, 28,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.FontColor = palette.Muted;
        canvas.FontSize = 11;
        canvas.DrawString(IsLocked ? "KİLİTLİ" : "SÜRÜKLENEBİLİR", left, dirtyRect.Height - 34,
            rulerWidth, 22, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static float PositionToX(double width, double position)
    {
        var usable = Math.Max(1, width - (2 * SidePadding));
        return SidePadding + ((float)Math.Clamp(position, 0, 1) * (float)usable);
    }

    public static double XToPosition(double width, double x)
    {
        var usable = Math.Max(1, width - (2 * SidePadding));
        return Math.Clamp((x - SidePadding) / usable, 0, 1);
    }

    private static void DrawGuide(ICanvas canvas, float x, float top, float bottom, Color color, bool locked)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = locked ? 2 : 3;
        canvas.DrawLine(x, top, x, bottom);
        canvas.FillColor = color;
        canvas.FillCircle(x, top, 10);
        canvas.FillCircle(x, bottom, 10);
        if (locked)
        {
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 1.5f;
            canvas.DrawRectangle(x - 3.5f, top - 2, 7, 6);
        }
    }

    private static void DrawArrowHead(ICanvas canvas, float x, float y, float direction, Color color)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2;
        canvas.DrawLine(x, y, x + (direction * 8), y - 5);
        canvas.DrawLine(x, y, x + (direction * 8), y + 5);
    }
}
