using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Media;
using OlcuAtolyesi.Resources.Strings;

namespace OlcuAtolyesi.Views;

public sealed class PhotoSourcePage : PrototypePage
{
    private readonly Label status = ViewFactory.Secondary(PhotoText.Privacy);
    private readonly VerticalStackLayout actions = ViewFactory.Stack(10);
    private bool busy;
    private CancellationTokenSource lifetime = new();
    private bool visible;
    public PhotoSourcePage() : base(PhotoText.Title, "photo")
    {
        var stack = ViewFactory.Stack(12);
        stack.Add(ViewFactory.Body(PhotoText.Intro));
        actions.Add(ViewFactory.PrimaryButton(PhotoText.Shoot, async (_, _) => await Run(async () =>
            await Navigation.PushAsync(new PhotoCameraPage()))));
        actions.Add(ViewFactory.SecondaryButton(PhotoText.Gallery, async (_, _) => await Run(PickAsync)));
        actions.Add(ViewFactory.SecondaryButton(PhotoText.Demo, async (_, _) => await Run(async () =>
            await Shell.Current.GoToAsync(nameof(PhotoMeasurePage)))));
        stack.Add(actions); stack.Add(status);
#if !ANDROID
        status.Text = PhotoText.Unsupported;
        ((VisualElement)actions.Children[0]).IsEnabled = ((VisualElement)actions.Children[1]).IsEnabled = false;
#endif
        Content = PageBody(ViewFactory.Scroll(stack));
    }
    private async Task Run(Func<Task> operation)
    {
        if (busy || !visible) return;
        busy = true; actions.IsEnabled = false;
        try { await operation(); }
        catch (OperationCanceledException) { }
        catch (Exception) { status.Text = PhotoText.Failed; }
        finally { busy = false; actions.IsEnabled = true;
#if !ANDROID
            ((VisualElement)actions.Children[0]).IsEnabled = ((VisualElement)actions.Children[1]).IsEnabled = false;
#endif
        }
    }
    private async Task PickAsync()
    {
#if ANDROID
        var files = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = PhotoText.Gallery, SelectionLimit = 1 });
        var file = files.FirstOrDefault();
        if (file is null) { status.Text = PhotoText.SelectionCancelled; return; }
        if (!visible) return;
        using var stream = await file.OpenReadAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var photo = await TemporaryPhoto.ImportAsync(stream, timeout.Token);
        try {
            if (!visible) { photo.Dispose(); return; }
            await Shell.Current.Navigation.PushAsync(new PhotoMeasurePage(photo));
        } catch { photo.Dispose(); throw; }
#else
        await Task.CompletedTask;
#endif
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        visible = true;
    }
    protected override void OnDisappearing()
    {
        visible = false;
        lifetime.Cancel();
        base.OnDisappearing();
    }
}




