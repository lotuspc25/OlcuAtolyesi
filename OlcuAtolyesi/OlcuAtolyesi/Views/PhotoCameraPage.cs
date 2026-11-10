using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Media;
using OlcuAtolyesi.Resources.Strings;
#if ANDROID
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;
#endif

namespace OlcuAtolyesi.Views;

public sealed class PhotoCameraPage : PrototypePage
{
    private readonly Label status = ViewFactory.Secondary(PhotoText.Opening);
    private readonly Grid preview = new() { MinimumHeightRequest = 160, IsClippedToBounds = true };
    private readonly VerticalStackLayout controls = ViewFactory.Stack(6);
    private readonly Button capture, accept, retake, retry, permission, settings;
    private TemporaryPhoto? pending;
    private readonly Image review = new() { Aspect = Aspect.AspectFit };
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? operation = null;
    private bool visible, foreground = true, ready, closing;
    private bool wasAttached;
    private Window? hostWindow;
#if ANDROID
    private CameraView? camera;
#endif

    public PhotoCameraPage() : base(PhotoText.CameraTitle, "photo")
    {
        capture = ViewFactory.PrimaryButton(PhotoText.Capture, async (_, _) => await Run(CaptureAsync));
        accept = ViewFactory.PrimaryButton(PhotoText.Use, async (_, _) => await Run(AcceptAsync));
        retake = ViewFactory.SecondaryButton(PhotoText.Retake, async (_, _) => await Run(async () => {
            review.Source = null; pending?.Dispose(); pending = null; preview.Clear(); await StartAsync(false);
        }));
        retry = ViewFactory.SecondaryButton(PhotoText.Retry, async (_, _) => await Run(() => StartAsync(false)));
        permission = ViewFactory.SecondaryButton(PhotoText.Permission, async (_, _) => await Run(() => StartAsync(true)));
        settings = ViewFactory.SecondaryButton(PhotoText.Settings, (_, _) => AppInfo.Current.ShowSettingsUI());
        controls.Add(capture); controls.Add(accept); controls.Add(retake); controls.Add(retry); controls.Add(permission); controls.Add(settings);
        controls.Add(ViewFactory.SecondaryButton(PhotoText.Cancel, async (_, _) => {
            if (closing) return;
            closing = true;
            try { operation?.Cancel(); CloseCamera(); await Navigation.PopAsync(); }
            catch (Exception) { status.Text = PhotoText.Failed; }
            finally { closing = false; }
        }));
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, RowSpacing = 8 };
        layout.Add(status, 0, 0); layout.Add(preview, 0, 1);
        layout.Add(new ScrollView { Content = controls, MaximumHeightRequest = 240 }, 0, 2);
        Content = PageBody(layout);
        UpdateControls();
    }

    private void UpdateControls()
    {
        capture.IsVisible = pending is null; capture.IsEnabled = ready;
        accept.IsVisible = retake.IsVisible = pending is not null;
        retry.IsVisible = !ready && pending is null;
        permission.IsVisible = settings.IsVisible = !ready && pending is null;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing(); visible = true; foreground = true;
        hostWindow = Window;
        if (hostWindow is not null) { hostWindow.Stopped += OnStopped; hostWindow.Resumed += OnResumed; }
        _ = Run(() => pending is null ? StartAsync(false) : Task.CompletedTask);
    }
    protected override void OnDisappearing()
    {
        visible = false; operation?.Cancel(); CloseCamera();
        if (hostWindow is not null) { hostWindow.Stopped -= OnStopped; hostWindow.Resumed -= OnResumed; }
        base.OnDisappearing();
    }
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is not null) wasAttached = true;
        else if (wasAttached) { review.Source = null; pending?.Dispose(); pending = null; }
    }
    private void OnStopped(object? sender, EventArgs e) { foreground = false; operation?.Cancel(); CloseCamera(); }
    private void OnResumed(object? sender, EventArgs e) {
        foreground = true;
        if (visible && pending is null) _ = ResumeAsync();
    }
    private async Task ResumeAsync() {
        // Wait for an interrupted capture/start to unwind before reopening.
        await gate.WaitAsync(); gate.Release();
        if (visible && foreground && pending is null && !ready) await Run(() => StartAsync(false));
    }

    private async Task Run(Func<Task> action)
    {
        if (!await gate.WaitAsync(0)) return;
        foreach (var button in new[] { capture, accept, retake, retry, permission, settings }) button.IsEnabled = false;
        try { await action(); }
        catch (OperationCanceledException) { if (visible) status.Text = PhotoText.Failed; CloseCamera(); }
        catch (Exception) { status.Text = PhotoText.Failed; CloseCamera(); }
        finally { foreach (var button in new[] { accept, retake, retry, permission, settings }) button.IsEnabled = true; UpdateControls(); gate.Release(); }
    }

    private async Task StartAsync(bool explicitPermission)
    {
#if ANDROID
        if (!visible || !foreground) return;
        var granted = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (granted != PermissionStatus.Granted) {
            var asked = Preferences.Default.Get("camera_permission_requested_v020", false);
            if (!asked || (explicitPermission && Permissions.ShouldShowRationale<Permissions.Camera>())) {
                Preferences.Default.Set("camera_permission_requested_v020", true);
                granted = await Permissions.RequestAsync<Permissions.Camera>();
            }
            if (granted != PermissionStatus.Granted) {
                status.Text = Permissions.ShouldShowRationale<Permissions.Camera>() ? PhotoText.Denied : PhotoText.Blocked;
                return;
            }
        }
        if (!visible || !foreground) return;
        CloseCamera(); preview.Clear();
        operation?.Dispose(); operation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token = operation.Token;
        var newCamera = new CameraView();
        camera = newCamera;
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        newCamera.Loaded += (_, _) => loaded.TrySetResult();
        preview.Add(newCamera);
        await loaded.Task.WaitAsync(token);
        var cameras = await newCamera.GetAvailableCameras(token);
        var rear = cameras.FirstOrDefault(x => x.Position == CameraPosition.Rear)
            ?? throw new InvalidOperationException("Arka kamera bulunamadı.");
        newCamera.SelectedCamera = rear;
        // Smallest available capture at least ~1 MP, capped where the device supports it.
        var resolution = rear.SupportedResolutions.Where(x => x.Width * x.Height <= 4_000_000)
            .OrderByDescending(x => x.Width * x.Height).FirstOrDefault();
        if (resolution.Width <= 0)
        {
            resolution = rear.SupportedResolutions
                .OrderBy(x => x.Width * x.Height)
                .FirstOrDefault();
        }
        if (resolution.Width > 0) newCamera.ImageCaptureResolution = resolution;
        await newCamera.StartCameraPreview(token);
        if (!visible || !foreground || camera != newCamera) { CloseCamera(); return; }
        operation.CancelAfter(Timeout.InfiniteTimeSpan);
        ready = true; status.Text = PhotoText.Ready;
#else
        status.Text = PhotoText.Unsupported;
        await Task.CompletedTask;
#endif
    }

    private async Task CaptureAsync()
    {
#if ANDROID
        var activeCamera = camera;
        if (!ready || activeCamera is null || activeCamera.IsBusy || !foreground) return;
        operation?.Dispose(); operation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token = operation.Token;
        using var image = await activeCamera.CaptureImage(token);
        var photo = await TemporaryPhoto.ImportAsync(image, token);
        if (!visible || !foreground) { photo.Dispose(); return; }
        pending?.Dispose(); pending = photo;
        CloseCamera(); preview.Clear();
        review.Source = ImageSource.FromFile(photo.Path); preview.Add(review);
        status.Text = PhotoText.Review;
#else
        await Task.CompletedTask;
#endif
    }
    private async Task AcceptAsync()
    {
        if (pending is null) return;
        var photo = pending;
        // Push while the camera page is still attached; a popped page's NavigationProxy
        // no longer has a navigation host and can silently discard the next push.
        var navigation = Shell.Current.Navigation;
        await navigation.PushAsync(new PhotoMeasurePage(photo));
        pending = null;
        review.Source = null;
        if (navigation.NavigationStack.Contains(this)) navigation.RemovePage(this);
    }

    private void CloseCamera()
    {
        ready = false;
#if ANDROID
        var previous = camera; camera = null;
        if (previous is not null) {
            try { previous.StopCameraPreview(); } catch (Exception) { }
            preview.Children.Remove(previous);
            try { ((IView)previous).Handler?.DisconnectHandler(); previous.Dispose(); } catch (Exception) { }
        }
#endif
    }
}



