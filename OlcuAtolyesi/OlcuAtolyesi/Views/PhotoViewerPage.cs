using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Media;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Services;
using OlcuAtolyesi.Resources.Strings;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace OlcuAtolyesi.Views;

/// <summary>Bounded canvas for real photos with direct transition to PhotoMeasurePage.</summary>
#if ANDROID
public sealed class PhotoViewerPage : PrototypePage
{
    private readonly TemporaryPhoto photo;
    private readonly GraphicsView viewport;
    private readonly PhotoDrawable drawable;
    private bool released, wasAttached, photoTransferred;
    private ImageViewportTransform transform = new(1, 0, 0), panStart = new(1, 0, 0);
    private double fitScale = 1;

    public PhotoViewerPage(TemporaryPhoto photo) : base(PhotoText.Title, "photo")
    {
        this.photo = photo;
#if ANDROID
        using var input = File.OpenRead(photo.Path);
        var pixels = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(input);
        drawable = new PhotoDrawable(pixels);
#else
        throw new NotSupportedException(PhotoText.Unsupported);
#endif
#if ANDROID
        viewport = new GraphicsView { Drawable = drawable, MinimumHeightRequest = 160 };
        SemanticProperties.SetDescription(viewport, "Seçilen gerçek fotoğraf.");
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += (_, e) =>
        {
            if (e.Status != GestureStatus.Running) return;
            var target = Math.Clamp(transform.Scale * e.Scale, fitScale, fitScale * 5);
            transform = transform.ZoomAt(new ViewPoint(e.ScaleOrigin.X * viewport.Width, e.ScaleOrigin.Y * viewport.Height),
                target / transform.Scale, fitScale, fitScale * 5);
            UpdateCanvas();
        };
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, e) =>
        {
            if (e.StatusType == GestureStatus.Started) panStart = transform;
            if (e.StatusType == GestureStatus.Running)
            {
                transform = panStart.PanBy(e.TotalX, e.TotalY);
                UpdateCanvas();
            }
        };
        viewport.GestureRecognizers.Add(pinch);
        viewport.GestureRecognizers.Add(pan);
        viewport.SizeChanged += (_, _) => Fit();
        var layout = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            RowSpacing = 8
        };
        layout.Add(ViewFactory.Secondary(PhotoText.Notice), 0, 0);
        layout.Add(viewport, 0, 1);
        layout.Add(ViewFactory.PrimaryButton(PhotoText.MeasureOnPhoto, OpenMeasureAsync), 0, 2);
        layout.Add(ViewFactory.SecondaryButton(PhotoText.Fit, (_, _) => Fit()), 0, 3);
        Content = PageBody(layout);
#endif
    }

    private async void OpenMeasureAsync(object? sender, EventArgs e)
    {
        if (photoTransferred || released) return;
        photoTransferred = true;
        var navigation = Shell.Current.Navigation;
        await navigation.PushAsync(new PhotoMeasurePage(photo));
        if (navigation.NavigationStack.Contains(this)) navigation.RemovePage(this);
    }

    private void Fit()
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        transform = ImageViewportTransform.Fit(new ImageSize(drawable.Image.Width, drawable.Image.Height),
            new ViewSize(viewport.Width, viewport.Height));
        fitScale = transform.Scale;
        UpdateCanvas();
    }

    private void UpdateCanvas()
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        transform = transform.ClampToViewport(
            new ImageSize(drawable.Image.Width, drawable.Image.Height),
            new ViewSize(viewport.Width, viewport.Height));
        drawable.Transform = transform;
        viewport.Invalidate();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is not null) wasAttached = true;
        else if (wasAttached && !released)
        {
            released = true;
            viewport.Drawable = null;
            drawable.Image.Dispose();
            if (!photoTransferred) photo.Dispose();
        }
    }

    private sealed class PhotoDrawable(IImage image) : IDrawable
    {
        public IImage Image { get; } = image;
        public ImageViewportTransform Transform { get; set; } = new(1, 0, 0);

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.ClipRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
            canvas.DrawImage(Image, (float)Transform.TranslationX, (float)Transform.TranslationY,
                (float)(Image.Width * Transform.Scale), (float)(Image.Height * Transform.Scale));
            canvas.RestoreState();
        }
    }
}

#else
public sealed class PhotoViewerPage : PrototypePage
{
    public PhotoViewerPage(TemporaryPhoto photo) : base(PhotoText.Title, "photo")
    {
        photo.Dispose();
        Content = PageBody(ViewFactory.Body(PhotoText.Unsupported));
    }
}
#endif
