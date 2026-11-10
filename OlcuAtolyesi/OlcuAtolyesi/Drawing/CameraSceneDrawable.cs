using Microsoft.Maui.Graphics;

namespace OlcuAtolyesi.Drawing;

public sealed class CameraSceneDrawable : IDrawable
{
    public string ResultText { get; set; } = "180 cm";

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var p = ThemePalette.Current;
        canvas.FillColor = p.Surface;
        canvas.FillRoundedRectangle(dirtyRect, 16);

        var margin = 20f;
        var room = new RectF(margin, margin, dirtyRect.Width - (2 * margin), dirtyRect.Height - (2 * margin));
        canvas.StrokeColor = p.Border;
        canvas.StrokeSize = 2;
        canvas.DrawRoundedRectangle(room, 10);

        var floorY = room.Bottom - 58;
        canvas.FillColor = p.Background;
        canvas.FillRectangle(room.Left + 2, floorY, room.Width - 4, room.Bottom - floorY - 2);
        canvas.StrokeColor = p.Border;
        canvas.DrawLine(room.Left, floorY, room.Right, floorY);

        // Çizilmiş bir sehpa ve kutu; fotoğraf/kamera görüntüsü değildir.
        canvas.FillColor = p.Border;
        canvas.FillRoundedRectangle(room.Left + 38, floorY - 72, 92, 72, 5);
        canvas.FillColor = p.Background;
        canvas.FillRectangle(room.Left + 48, floorY - 62, 72, 52);
        canvas.FillColor = p.Primary;
        canvas.FillRoundedRectangle(room.Right - 112, floorY - 108, 70, 108, 6);

        var start = new PointF(room.Left + 82, floorY - 88);
        var end = new PointF(room.Right - 77, floorY - 124);
        DrawTarget(canvas, start, p.Accent, "A");
        DrawTarget(canvas, end, p.Accent, "B");

        canvas.StrokeColor = p.Accent;
        canvas.StrokeSize = 2.5f;
        canvas.StrokeDashPattern = new[] { 8f, 6f };
        canvas.DrawLine(start, end);
        canvas.StrokeDashPattern = null;

        var center = new PointF(dirtyRect.Center.X, dirtyRect.Center.Y - 18);
        canvas.StrokeColor = p.Text;
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(center, 20);
        canvas.DrawLine(center.X - 30, center.Y, center.X - 8, center.Y);
        canvas.DrawLine(center.X + 8, center.Y, center.X + 30, center.Y);
        canvas.DrawLine(center.X, center.Y - 30, center.X, center.Y - 8);
        canvas.DrawLine(center.X, center.Y + 8, center.X, center.Y + 30);

        var labelWidth = Math.Min(170f, room.Width - 30);
        canvas.FillColor = p.Background;
        canvas.FillRoundedRectangle(center.X - (labelWidth / 2), room.Top + 14, labelWidth, 38, 12);
        canvas.FontColor = p.Text;
        canvas.FontSize = 15;
        canvas.DrawString($"DEMO  •  {ResultText}", center.X - (labelWidth / 2), room.Top + 14,
            labelWidth, 38, HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.FontColor = p.Muted;
        canvas.FontSize = 11;
        canvas.DrawString("ÇİZİLMİŞ TANITIM SAHNESİ", room.Left + 12, room.Bottom - 32,
            room.Width - 24, 24, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawTarget(ICanvas canvas, PointF point, Color color, string label)
    {
        canvas.FillColor = color;
        canvas.FillCircle(point, 8);
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2;
        canvas.DrawCircle(point, 4);
        canvas.FontColor = color;
        canvas.FontSize = 12;
        canvas.DrawString(label, point.X - 12, point.Y - 30, 24, 20,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
