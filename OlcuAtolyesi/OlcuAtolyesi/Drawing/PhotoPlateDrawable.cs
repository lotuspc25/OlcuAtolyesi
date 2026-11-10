using Microsoft.Maui.Graphics;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Services;
using OlcuAtolyesi.ViewModels;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace OlcuAtolyesi.Drawing;

public sealed class PhotoPlateDrawable : IDrawable
{
    private static readonly float[] CenterLineDash = [5f, 4f];
    private static readonly float[] ReferenceDash = [7f, 5f];

    public required PhotoMeasureViewModel ViewModel { get; init; }
    public IImage? Image { get; set; }
    public ImageViewportTransform Transform { get; set; } = new(1, 0, 0);
    public int ActivePoint { get; set; } = -1;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var p = ThemePalette.Current;
        canvas.SaveState();
        canvas.ClipRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        canvas.FillColor = p.Background;
        canvas.FillRoundedRectangle(dirtyRect, 16);

        if (Image is not null)
        {
            DrawRealPhoto(canvas, p);
        }
        else
        {
            DrawImageBounds(canvas, p);
            DrawPlate(canvas, p);
        }

        DrawReference(canvas, p);
        DrawMeasurement(canvas, p);
        DrawActiveMagnifier(canvas, p);

        if (Image is null)
        {
            canvas.FontColor = p.Muted;
            canvas.FontSize = 10;
            canvas.DrawString("KODLA ÇİZİLMİŞ DEMO GÖRSELİ", 12, dirtyRect.Height - 28,
                dirtyRect.Width - 24, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        canvas.RestoreState();
    }

    private void DrawRealPhoto(ICanvas canvas, ThemePalette p)
    {
        if (Image is null)
        {
            return;
        }

        var x = (float)Transform.TranslationX;
        var y = (float)Transform.TranslationY;
        var width = (float)(ViewModel.ImageSize.Width * Transform.Scale);
        var height = (float)(ViewModel.ImageSize.Height * Transform.Scale);

        canvas.DrawImage(Image, x, y, width, height);
        canvas.StrokeColor = p.Border;
        canvas.StrokeSize = 1;
        canvas.DrawRectangle(x, y, width, height);
    }

    private void DrawImageBounds(ICanvas canvas, ThemePalette p)
    {
        var topLeft = ToPoint(Transform.ImageToView(new ImagePoint(0, 0)));
        var bottomRight = ToPoint(Transform.ImageToView(new ImagePoint(
            ViewModel.ImageSize.Width,
            ViewModel.ImageSize.Height)));
        var rect = new RectF(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        canvas.FillColor = p.Surface;
        canvas.FillRectangle(rect);
        canvas.StrokeColor = p.Border;
        canvas.StrokeSize = 1;
        canvas.DrawRectangle(rect);

        canvas.StrokeColor = p.Border.WithAlpha(0.35f);
        canvas.StrokeSize = 1;
        for (var x = 100; x < 1000; x += 100)
        {
            var a = ToPoint(Transform.ImageToView(new ImagePoint(x, 0)));
            var b = ToPoint(Transform.ImageToView(new ImagePoint(x, 600)));
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }
        for (var y = 100; y < 600; y += 100)
        {
            var a = ToPoint(Transform.ImageToView(new ImagePoint(0, y)));
            var b = ToPoint(Transform.ImageToView(new ImagePoint(1000, y)));
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }
    }

    private void DrawPlate(ICanvas canvas, ThemePalette p)
    {
        var plateTopLeft = ToPoint(Transform.ImageToView(new ImagePoint(110, 110)));
        var plateBottomRight = ToPoint(Transform.ImageToView(new ImagePoint(890, 455)));
        var plateRect = new RectF(
            plateTopLeft.X,
            plateTopLeft.Y,
            plateBottomRight.X - plateTopLeft.X,
            plateBottomRight.Y - plateTopLeft.Y);

        canvas.FillColor = p.Border;
        canvas.FillRoundedRectangle(plateRect, Math.Max(4, (float)(24 * Transform.Scale)));
        var innerInset = Math.Max(2f, (float)(10 * Transform.Scale));
        canvas.FillColor = p.Background;
        canvas.FillRoundedRectangle(
            plateRect.X + innerInset,
            plateRect.Y + innerInset,
            plateRect.Width - (2 * innerInset),
            plateRect.Height - (2 * innerInset),
            Math.Max(3, (float)(16 * Transform.Scale)));

        DrawHole(canvas, new ImagePoint(260, 282), 55, p);
        DrawHole(canvas, new ImagePoint(740, 282), 55, p);

        var center = ToPoint(Transform.ImageToView(new ImagePoint(500, 282)));
        canvas.StrokeColor = p.Muted;
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = CenterLineDash;
        canvas.DrawLine(center.X, plateRect.Top + 15, center.X, plateRect.Bottom - 15);
        canvas.StrokeDashPattern = null;
    }

    private void DrawHole(ICanvas canvas, ImagePoint centerPoint, double radius, ThemePalette p)
    {
        var center = ToPoint(Transform.ImageToView(centerPoint));
        var scaledRadius = Math.Max(3f, (float)(radius * Transform.Scale));
        canvas.FillColor = p.Surface;
        canvas.FillCircle(center, scaledRadius);
        canvas.StrokeColor = p.Primary;
        canvas.StrokeSize = 2;
        canvas.DrawCircle(center, scaledRadius);
        canvas.DrawLine(center.X - scaledRadius - 8, center.Y, center.X + scaledRadius + 8, center.Y);
        canvas.DrawLine(center.X, center.Y - scaledRadius - 8, center.X, center.Y + scaledRadius + 8);
    }

    private void DrawReference(ICanvas canvas, ThemePalette p)
    {
        var segment = Transform.ImageToView(ViewModel.ReferenceSegment);
        var start = ToPoint(segment.Start);
        var end = ToPoint(segment.End);
        canvas.StrokeColor = p.Accent;
        canvas.StrokeSize = 3;
        canvas.StrokeDashPattern = ReferenceDash;
        canvas.DrawLine(start, end);
        canvas.StrokeDashPattern = null;

        DrawHandle(canvas, start, ActivePoint == PhotoMeasureViewModel.PointR1, p.Accent, "R1", p);
        DrawHandle(canvas, end, ActivePoint == PhotoMeasureViewModel.PointR2, p.Accent, "R2", p);

        DrawLabel(canvas, (start.X + end.X) / 2, Math.Max(start.Y, end.Y) + 12,
            ViewModel.ReferenceText, p.Text, p.Surface, p.Accent);
    }

    private void DrawMeasurement(ICanvas canvas, ThemePalette p)
    {
        var segment = Transform.ImageToView(ViewModel.Measurement);
        var start = ToPoint(segment.Start);
        var end = ToPoint(segment.End);
        canvas.StrokeColor = p.Primary;
        canvas.StrokeSize = 3;
        canvas.DrawLine(start, end);
        DrawHandle(canvas, start, ActivePoint == PhotoMeasureViewModel.PointM1, p.Primary, "M1", p);
        DrawHandle(canvas, end, ActivePoint == PhotoMeasureViewModel.PointM2, p.Primary, "M2", p);

        var prefix = ViewModel.IsRealPhoto ? "ÖLÇÜM" : "DEMO";
        DrawLabel(canvas, (start.X + end.X) / 2, Math.Min(start.Y, end.Y) - 40,
            $"{prefix}  {ViewModel.ResultText}", p.Text, p.Surface, p.Primary);
    }

    private void DrawActiveMagnifier(ICanvas canvas, ThemePalette p)
    {
        switch (ActivePoint)
        {
            case PhotoMeasureViewModel.PointM1:
                DrawPrecisionIndicator(canvas, ToPoint(Transform.ImageToView(ViewModel.M1)), ViewModel.M1, "M1", p.Primary, p);
                break;
            case PhotoMeasureViewModel.PointM2:
                DrawPrecisionIndicator(canvas, ToPoint(Transform.ImageToView(ViewModel.M2)), ViewModel.M2, "M2", p.Primary, p);
                break;
            case PhotoMeasureViewModel.PointR1:
                DrawPrecisionIndicator(canvas, ToPoint(Transform.ImageToView(ViewModel.R1)), ViewModel.R1, "R1", p.Accent, p);
                break;
            case PhotoMeasureViewModel.PointR2:
                DrawPrecisionIndicator(canvas, ToPoint(Transform.ImageToView(ViewModel.R2)), ViewModel.R2, "R2", p.Accent, p);
                break;
        }
    }

    private static void DrawHandle(ICanvas canvas, PointF point, bool active, Color color, string tag, ThemePalette p)
    {
        canvas.FillColor = color;
        canvas.FillCircle(point, active ? 14 : 11);
        canvas.StrokeColor = p.Surface;
        canvas.StrokeSize = 2.5f;
        canvas.DrawCircle(point, active ? 14 : 11);

        canvas.FontColor = Colors.White;
        canvas.FontSize = 9;
        canvas.DrawString(tag, point.X - 12, point.Y - 10, 24, 20,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private void DrawPrecisionIndicator(ICanvas canvas, PointF touch, ImagePoint imagePoint, string tag, Color color, ThemePalette p)
    {
        var centerY = touch.Y > 96 ? touch.Y - 74 : touch.Y + 74;
        var center = new PointF(touch.X, centerY);
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2;
        canvas.DrawLine(touch, new PointF(center.X, center.Y + (centerY < touch.Y ? 36 : -36)));
        canvas.FillColor = p.Surface;
        canvas.FillCircle(center, 36);

        if (Image is not null)
        {
            canvas.SaveState();
            canvas.ClipRectangle(center.X - 26, center.Y - 26, 52, 52);
            var zoomScale = Transform.Scale * 2.2d;
            var imgX = center.X - (float)(imagePoint.X * zoomScale);
            var imgY = center.Y - (float)(imagePoint.Y * zoomScale);
            canvas.DrawImage(
                Image,
                imgX,
                imgY,
                (float)(ViewModel.ImageSize.Width * zoomScale),
                (float)(ViewModel.ImageSize.Height * zoomScale));
            canvas.RestoreState();
        }

        canvas.StrokeColor = color;
        canvas.StrokeSize = 3;
        canvas.DrawCircle(center, 36);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(center.X - 22, center.Y, center.X + 22, center.Y);
        canvas.DrawLine(center.X, center.Y - 22, center.X, center.Y + 22);
        canvas.FillColor = color;
        canvas.FillCircle(center, 3.5f);

        canvas.FillColor = color;
        canvas.FillRoundedRectangle(center.X - 16, center.Y - 46, 32, 18, 6);
        canvas.FontColor = Colors.White;
        canvas.FontSize = 10;
        canvas.DrawString(tag, center.X - 16, center.Y - 46, 32, 18,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawLabel(ICanvas canvas, float centerX, float top, string text, Color textColor, Color fill, Color border)
    {
        const float width = 166;
        canvas.FillColor = fill.WithAlpha(0.92f);
        canvas.FillRoundedRectangle(centerX - (width / 2), top, width, 28, 8);
        canvas.StrokeColor = border;
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(centerX - (width / 2), top, width, 28, 8);
        canvas.FontColor = textColor;
        canvas.FontSize = 11;
        canvas.DrawString(text, centerX - (width / 2) + 4, top + 1, width - 8, 26,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static PointF ToPoint(ViewPoint point) => new((float)point.X, (float)point.Y);
}
