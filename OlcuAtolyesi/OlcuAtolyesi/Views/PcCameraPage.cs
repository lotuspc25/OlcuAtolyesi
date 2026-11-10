using System.Text;
using OlcuAtolyesi.Controls;
using OlcuAtolyesi.Diagnostics;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;
#if ANDROID
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;
#endif

namespace OlcuAtolyesi.Views;

public sealed class PcCameraPage : PrototypePage
{
    private readonly PcCameraStreamServer _server = new();
    private readonly CameraSwitchCoordinator _switchCoordinator = new();
    private readonly Label _statusLabel = ViewFactory.Secondary(AppText.PcCamera.ServerStopped);
    private readonly Label _ipLabel = ViewFactory.Body("IP: ...");
    private readonly Label _pinLabel = ViewFactory.SectionTitle("PIN: ------");
    private readonly Label _clientsLabel = ViewFactory.Secondary(AppText.PcCamera.NoClients);
    private readonly Label _metricsLabel = ViewFactory.Secondary("Canlı Akış: 0.0 FPS | 1280x720 | Gecikme: 0 ms");
    private readonly Label _savedPhotosLabel = ViewFactory.Secondary("Kayıtlı Fotoğraflar: 0 adet (0 KB)");
    private readonly Button _toggleButton;
    private readonly Button _refreshPinButton;
    private readonly Button _switchCameraButton;
    private readonly Button _viewPhotosButton;
    private readonly Grid _previewGrid = new() { MinimumHeightRequest = 180, HeightRequest = 220, IsClippedToBounds = true };

    private bool _visible;
    private bool _foreground = true;
    private volatile bool _isCaptureSuspended;
    private int _surfaceFrameFailures;
    private readonly PreviewFrameRateLimiter _frameRateLimiter = new(15);
    private Window? _hostWindow;
    private CancellationTokenSource? _streamCts;
#if ANDROID
    private CameraView? _cameraView;
#endif

    public PcCameraPage() : base(AppText.PcCamera.Title, "general")
    {
        _toggleButton = ViewFactory.PrimaryButton(AppText.PcCamera.StartServer, async (_, _) => await ToggleServerAsync());
        _refreshPinButton = ViewFactory.SecondaryButton(AppText.PcCamera.NewPin, (_, _) => RefreshPin());
        _switchCameraButton = ViewFactory.SecondaryButton("Sensör: Arka Kamera", async (_, _) => await SwitchCameraAsync());
        _viewPhotosButton = ViewFactory.SecondaryButton("Fotoğrafları İncele", async (_, _) => await ViewSavedPhotosAsync());

        _server.ClientConnected += (_, ip) => MainThread.BeginInvokeOnMainThread(UpdateClientStatus);
        _server.ClientDisconnected += (_, ip) => MainThread.BeginInvokeOnMainThread(UpdateClientStatus);

        // Yüksek çözünürlüklü gerçek still-capture geri çağrısı bağla
        _server.OnStillCaptureRequested = async (token) =>
        {
#if ANDROID
            if (_cameraView != null)
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (_cameraView == null || _cameraView.IsBusy) return null;
                    return await _cameraView.CaptureImage(token);
                });
            }
            return null;
#else
            await Task.CompletedTask;
            // Test / Simülasyon JPEG
            return new MemoryStream([0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x0F, 0xA0, 0x14, 0x00, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, 0xFF, 0xD9]);
#endif
        };

        var ipPinStack = ViewFactory.Stack(6);
        ipPinStack.Add(ViewFactory.SectionTitle(AppText.PcCamera.Title));
        ipPinStack.Add(_statusLabel);
        ipPinStack.Add(_ipLabel);
        ipPinStack.Add(_pinLabel);
        ipPinStack.Add(_clientsLabel);
        ipPinStack.Add(_metricsLabel);

        var buttonRow = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Center,
            JustifyContent = FlexJustify.Start
        };
        _toggleButton.Margin = new Thickness(0, 0, 8, 8);
        _refreshPinButton.Margin = new Thickness(0, 0, 8, 8);
        _switchCameraButton.Margin = new Thickness(0, 0, 8, 8);
        buttonRow.Add(_toggleButton);
        buttonRow.Add(_refreshPinButton);
        buttonRow.Add(_switchCameraButton);
        ipPinStack.Add(buttonRow);

        var networkCard = ViewFactory.Card(ipPinStack);

        var previewStack = ViewFactory.Stack(6);
        previewStack.Add(ViewFactory.SectionTitle("Kamera Canlı Önizleme"));
        previewStack.Add(_previewGrid);
        var previewCard = ViewFactory.Card(previewStack);

        var photosStack = ViewFactory.Stack(6);
        photosStack.Add(ViewFactory.SectionTitle("Telefonda Saklanan Ölçüm Fotoğrafları"));
        photosStack.Add(_savedPhotosLabel);
        _viewPhotosButton.Margin = new Thickness(0, 4, 0, 0);
        photosStack.Add(_viewPhotosButton);
        var photosCard = ViewFactory.Card(photosStack);

        var infoCard = ViewFactory.InfoCard(
            "Wi-Fi Canlı Akış & Yüksek Çözünürlüklü Çekim Rehberi",
            $"{AppText.PcCamera.Instruction}\n\n🔒 {AppText.PcCamera.SecurityNotice}\n\n📸 PC'den 'Yüksek Çözünürlüklü Fotoğraf Çek ve Ölç' komutu verildiğinde telefon fotoğrafı orijinal kalitede özel depolama alanına kaydeder ve PC'ye kayıpsız aktarır.");

        var pageStack = ViewFactory.Stack(12);
        pageStack.Add(networkCard);
        pageStack.Add(previewCard);
        pageStack.Add(photosCard);
        pageStack.Add(infoCard);

        Content = PageBody(ViewFactory.Scroll(pageStack));

        UpdateNetworkInfo();
    }

    private void UpdateNetworkInfo()
    {
        var localIp = NetworkHelper.GetLocalIpAddress();
        _ipLabel.Text = $"{AppText.PcCamera.LocalIpLabel} http://{localIp}:{_server.Port}/";
        _pinLabel.Text = $"{AppText.PcCamera.PinLabel} {_server.SessionPin}";
        UpdateClientStatus();
        UpdateStorageStatus();
    }

    private void UpdateStorageStatus()
    {
        var usage = _server.PhotoStorage.GetStorageUsage();
        _savedPhotosLabel.Text = $"Kayıtlı Fotoğraflar: {usage.TotalPhotosCount} adet ({usage.FormattedSize})";
    }

    private void UpdateClientStatus()
    {
        if (_server.ActiveClientCount > 0)
        {
            _clientsLabel.Text = $"{AppText.PcCamera.ActiveClients} {_server.LastClientIp} (Toplam {_server.ActiveClientCount} istemci bağlandı)";
        }
        else
        {
            _clientsLabel.Text = _server.IsRunning ? AppText.PcCamera.NoClients : AppText.PcCamera.ServerStopped;
        }
    }

    private async Task ViewSavedPhotosAsync()
    {
        var usage = _server.PhotoStorage.GetStorageUsage();
        var photos = _server.PhotoStorage.GetPhotos();
        if (photos.Count == 0)
        {
            await DisplayAlert("Kayıtlı Fotoğraflar", "Telefonda henüz kaydedilmiş ölçüm fotoğrafı bulunmuyor.", "Tamam");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Toplam: {usage.TotalPhotosCount} fotoğraf ({usage.FormattedSize})");
        sb.AppendLine("Depolama Alanı: Uygulama Özel Alanı");
        sb.AppendLine("Galeriye Aktarım: Planlandı");
        sb.AppendLine();

        foreach (var p in photos.Take(6))
        {
            sb.AppendLine($"• {p.FileName} ({p.FileSizeBytes / 1024} KB, {p.Width}x{p.Height})");
            sb.AppendLine($"  Tarih: {p.CreatedAt:yyyy-MM-dd HH:mm} | Durum: {(p.IsTransferredToPc ? "PC'ye Aktarıldı" : "Bekliyor")}");
        }

        if (photos.Count > 6)
        {
            sb.AppendLine($"... ve {photos.Count - 6} fotoğraf daha saklanıyor.");
        }

        await DisplayAlert("Telefonda Kayıtlı Fotoğraflar", sb.ToString(), "Tamam");
    }

    private void RefreshPin()
    {
        if (!_server.IsRunning)
        {
            _server.Start(_server.Port, PcCameraStreamServer.GenerateRandomPin());
            _server.Stop();
        }
        else
        {
            _server.Start(_server.Port, PcCameraStreamServer.GenerateRandomPin());
        }
        UpdateNetworkInfo();
    }

    private async Task ToggleServerAsync()
    {
        if (_server.IsRunning)
        {
            StopStreaming();
            _toggleButton.Text = AppText.PcCamera.StartServer;
            _statusLabel.Text = AppText.PcCamera.ServerStopped;
            _switchCameraButton.IsEnabled = true;
            _switchCameraButton.Text = "Sensör: Arka Kamera";
        }
        else
        {
            _server.Start(8088);
            _toggleButton.Text = AppText.PcCamera.StopServer;
            _statusLabel.Text = $"{AppText.PcCamera.ServerRunning} (Port: {_server.Port})";
            UpdateNetworkInfo();
            _switchCoordinator.SetStreaming();

            // v0.5.0.5: Yayın sırasında kamera değiştirme devre dışı bırakılır (ertelenen bilinen sorun)
            _switchCameraButton.IsEnabled = false;
            _switchCameraButton.Text = "Sensör: Arka Kamera (Yayın sırasında kilitli)";

            await StartCameraAndStreamingAsync();
        }
    }

    private async Task SwitchCameraAsync()
    {
        // Yayın sırasında çağrılmasını engelle
        if (_server.IsRunning)
        {
            await DisplayAlert("Bilgi", "Aktif canlı yayın sırasında kamera değiştirme kilitlidir. Lütfen önce yayını durdurun.", "Tamam");
            return;
        }

        var (canSwitch, cid) = await _switchCoordinator.TryBeginSwitchAsync();
        if (!canSwitch)
        {
            return;
        }

        try
        {
            _switchCameraButton.IsEnabled = false;
            _statusLabel.Text = "Kamera sensörü değiştiriliyor...";

            _isCaptureSuspended = true;
            await _switchCoordinator.PauseCaptureAndAcquireGateAsync(2500);

            var newIsFront = !_switchCoordinator.IsFrontCamera;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CloseCamera();
                _previewGrid.Clear();
            });

            await _switchCoordinator.SettleHardwareDelayAsync(300);

            if (_visible && _foreground)
            {
                await SetupAndStartPreviewAsync(newIsFront, cid);
            }

            _switchCoordinator.CompleteSwitch(newIsFront, cid);
            _switchCameraButton.Text = newIsFront ? "Sensör: Ön Kamera" : "Sensör: Arka Kamera";
            _statusLabel.Text = _server.IsRunning ? $"{AppText.PcCamera.ServerRunning} (Port: {_server.Port})" : AppText.PcCamera.ServerStopped;
        }
        catch (Exception ex)
        {
            _switchCoordinator.FailSwitch(cid, ex);
            _statusLabel.Text = $"Kamera değişimi başarısız: {ex.Message}";
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Error,
                "PcCameraPage",
                "SWITCH_CAMERA_EX",
                $"Kamera değişimi sırasında hata: {ex.Message}",
                cid,
                ex.ToString());
        }
        finally
        {
            _isCaptureSuspended = false;
            _switchCoordinator.ReleaseCaptureGate();
            _switchCameraButton.IsEnabled = !_server.IsRunning;
        }
    }

    private async Task StartCameraAndStreamingAsync()
    {
        if (!_visible || !_foreground) return;

#if ANDROID
        var granted = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (granted != PermissionStatus.Granted)
        {
            granted = await Permissions.RequestAsync<Permissions.Camera>();
            if (granted != PermissionStatus.Granted)
            {
                _statusLabel.Text = "Kamera izni verilmedi.";
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Warn,
                    "Camera",
                    "CAMERA_PERMISSION_DENIED",
                    "Kullanıcı kamera izni vermedi.");
                return;
            }
        }

        var cid = _switchCoordinator.CurrentCorrelationId ?? $"cam-init-{Guid.NewGuid().ToString("N")[..8]}";
        await SetupAndStartPreviewAsync(isFront: false, cid); // v0.5.0.5 Arka kamera esas alınır
#else
        _statusLabel.Text = "Bu platformda kamera simülasyonu çalışıyor.";
        _previewGrid.Clear();
        _previewGrid.Add(ViewFactory.Secondary("Masaüstü/Simülatör Önizlemesi"));
        await Task.CompletedTask;
#endif
    }

    private async Task SetupAndStartPreviewAsync(bool isFront, string cid)
    {
#if ANDROID
        CloseCamera();
        _previewGrid.Clear();

        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = new CancellationTokenSource();
        var token = _streamCts.Token;

        CameraView? newCamera = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            newCamera = new CameraView();
            _cameraView = newCamera;

            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            newCamera.Loaded += (_, _) => loaded.TrySetResult();
            _previewGrid.Add(newCamera);

            var timeoutTask = Task.Delay(3000, token);
            var completed = await Task.WhenAny(loaded.Task, timeoutTask);
            if (completed == timeoutTask && !loaded.Task.IsCompleted)
            {
                DiagnosticLogHub.Instance.Log(
                    DiagnosticLevel.Warn,
                    "Camera",
                    "CAMERA_LOADED_TIMEOUT",
                    "CameraView Loaded olayı zaman aşımına uğradı, başlatmaya devam ediliyor.",
                    cid);
            }

            var cameras = await newCamera.GetAvailableCameras(token);
            var targetPosition = isFront ? CameraPosition.Front : CameraPosition.Rear;
            var chosen = cameras.FirstOrDefault(c => c.Position == targetPosition)
                         ?? cameras.FirstOrDefault();

            if (chosen != null)
            {
                newCamera.SelectedCamera = chosen;

                // Yüksek çözünürlüklü gerçek still-capture için sensörün en yüksek çözünürlüğünü ayarla
                var maxStillRes = chosen.SupportedResolutions
                    .OrderByDescending(x => x.Width * x.Height)
                    .FirstOrDefault();

                if (maxStillRes.Width > 0)
                {
                    newCamera.ImageCaptureResolution = maxStillRes;
                }
            }

            await newCamera.StartCameraPreview(token);
        });

        await Task.Delay(250, token);
        StartContinuousCaptureLoop(token, cid);
#else
        await Task.CompletedTask;
#endif
    }

    private void StartContinuousCaptureLoop(CancellationToken token, string cid)
    {
#if ANDROID
        _ = Task.Run(async () =>
        {
            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Debug,
                "CameraCapture",
                "CAPTURE_LOOP_START",
                "Sürekli önizleme döngüsü başlatıldı (Hedef: 1280x720, 20-30 FPS).",
                cid);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (!token.IsCancellationRequested && _server.IsRunning && _foreground)
            {
                if (_isCaptureSuspended)
                {
                    await Task.Delay(40, token).ConfigureAwait(false);
                    continue;
                }

                if (!_frameRateLimiter.ShouldCapture(sw.ElapsedMilliseconds))
                {
                    await Task.Delay(10, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    if (_server.ActiveClientCount > 0 && _cameraView != null)
                    {
                        var startMs = sw.ElapsedMilliseconds;
                        byte[]? frameBytes = null;

                        if (await _switchCoordinator.CaptureGate.WaitAsync(80, token).ConfigureAwait(false))
                        {
                            try
                            {
                                if (!_isCaptureSuspended && _cameraView != null)
                                {
                                    // 1. Doğrudan TextureView yüzeyinden GPU bitişik karesini çekmeyi dene
                                    frameBytes = await MainThread.InvokeOnMainThreadAsync(() =>
                                    {
                                        if (_cameraView?.Handler?.PlatformView is Android.Views.View nativeView)
                                        {
                                            var tv = FindTextureView(nativeView);
                                            if (tv != null && tv.IsAvailable)
                                            {
                                                using var bmp = tv.GetBitmap(1280, 720);
                                                if (bmp != null)
                                                {
                                                    using var ms = new MemoryStream();
                                                    bmp.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 75, ms);
                                                    return ms.ToArray();
                                                }
                                            }
                                        }
                                        return null;
                                    });

                                    // 2. TextureView yoksa SurfaceView üzerinden PixelCopy ile kare al
                                    // (Tam çözünürlüklü donanımsal çekim her karede çağrılmamalı: çekim ~1 sn sürer)
                                    if (frameBytes == null && _cameraView != null)
                                    {
                                        var surfaceFrame = await MainThread.InvokeOnMainThreadAsync(async () =>
                                        {
                                            if (_cameraView?.Handler?.PlatformView is not Android.Views.View native)
                                            {
                                                return (Android.Views.SurfaceView?)null;
                                            }

                                            return FindSurfaceView(native);
                                        });

                                        if (surfaceFrame != null)
                                        {
                                            frameBytes = await TryCaptureSurfaceFrameAsync(surfaceFrame, token);
                                            if (frameBytes == null)
                                            {
                                                Interlocked.Increment(ref _surfaceFrameFailures);
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                _switchCoordinator.CaptureGate.Release();
                            }
                        }

                        if (frameBytes != null && frameBytes.Length > 0)
                        {
                            if (_server.Pipeline.TryProcessFrame(frameBytes, 1280, 720, startMs, out var outFrame) && outFrame != null)
                            {
                                _server.PublishFrame(outFrame);
                            }
                        }

                        var metrics = _server.Pipeline.GetMetrics();
                        var failureCount = Volatile.Read(ref _surfaceFrameFailures);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _metricsLabel.Text = $"Canlı Akış: {metrics.Fps:F1} FPS | {metrics.Width}x{metrics.Height} | Gecikme: {metrics.LatencyMs:F0} ms" +
                                                 (failureCount > 0 ? $" | Yüzey hatası: {failureCount}" : string.Empty);
                        });
                    }

                    await Task.Delay(10, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DiagnosticLogHub.Instance.Log(
                        DiagnosticLevel.Debug,
                        "CameraCapture",
                        "CAPTURE_FRAME_ERR",
                        $"Kare yakalama hatası: {ex.Message}",
                        cid);
                    await Task.Delay(60, token).ConfigureAwait(false);
                }
            }

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Info,
                "CameraCapture",
                "CAPTURE_LOOP_SUMMARY",
                $"Önizleme döngüsü bitti. Toplam yüzey kopyalama hatası: {Volatile.Read(ref _surfaceFrameFailures)}",
                cid);

            DiagnosticLogHub.Instance.Log(
                DiagnosticLevel.Debug,
                "CameraCapture",
                "CAPTURE_LOOP_STOP",
                "Sürekli önizleme döngüsü durduruldu.",
                cid);
        }, token);
#endif
    }

#if ANDROID
    private static Android.Views.TextureView? FindTextureView(Android.Views.View? root)
    {
        if (root is Android.Views.TextureView tv) return tv;
        if (root is Android.Views.ViewGroup group)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                var found = FindTextureView(group.GetChildAt(i));
                if (found != null) return found;
            }
        }
        return null;
    }

    private const int PreviewTargetWidth = 1280;
    private const int PreviewTargetHeight = 720;
    private const int PreviewJpegQuality = 70;

    private static Android.Views.SurfaceView? FindSurfaceView(Android.Views.View? root)
    {
        if (root is Android.Views.SurfaceView sv)
        {
            return sv;
        }

        if (root is Android.Views.ViewGroup group)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                var found = FindSurfaceView(group.GetChildAt(i));
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private sealed class PixelCopyCompletionListener : Java.Lang.Object, Android.Views.PixelCopy.IOnPixelCopyFinishedListener
    {
        private readonly TaskCompletionSource<int> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> Completion => _completion.Task;

        public void OnPixelCopyFinished(int result) => _completion.TrySetResult(result);
    }

    /// <summary>
    /// SurfaceView yüzeyinden PixelCopy ile tek kare alır ve JPEG'e çevirir.
    /// PixelCopy arka planda çalışır; JPEG sıkıştırma UI iş parçacığında YAPILMAZ.
    /// </summary>
    private static async Task<byte[]?> TryCaptureSurfaceFrameAsync(
        Android.Views.SurfaceView surfaceView,
        CancellationToken cancellationToken)
    {
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.N)
        {
            return null;
        }

        try
        {
            var holder = surfaceView.Holder;
            var surface = holder?.Surface;
            if (surface == null || !surface.IsValid)
            {
                return null;
            }

            var width = surfaceView.Width > 0 ? surfaceView.Width : PreviewTargetWidth;
            var height = surfaceView.Height > 0 ? surfaceView.Height : PreviewTargetHeight;

            using var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888!);
            if (bitmap == null)
            {
                return null;
            }

            var listener = new PixelCopyCompletionListener();
            using var handler = new Android.OS.Handler(Android.OS.Looper.MainLooper!);
            Android.Views.PixelCopy.Request(surfaceView, bitmap, listener, handler);

            var copyResult = await listener.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (copyResult != (int)Android.Views.PixelCopyResult.Success)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var output = new MemoryStream();
            await Task.Run(() => bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, PreviewJpegQuality, output), cancellationToken)
                .ConfigureAwait(false);

            return output.Length > 0 ? output.ToArray() : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
#endif

    private void StopStreaming()
    {
        _isCaptureSuspended = true;
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
        _server.Stop();
        CloseCamera();
        _switchCoordinator.ResetToIdle();
        UpdateClientStatus();
        UpdateStorageStatus();
    }

    private void CloseCamera()
    {
#if ANDROID
        var prev = _cameraView;
        _cameraView = null;
        if (prev != null)
        {
            try { prev.StopCameraPreview(); } catch { }
            try { _previewGrid.Children.Remove(prev); } catch { }
            try { ((IView)prev).Handler?.DisconnectHandler(); prev.Dispose(); } catch { }
        }
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        _foreground = true;
        _server.IsForeground = true;
        _hostWindow = Window;
        if (_hostWindow != null)
        {
            _hostWindow.Stopped += OnStopped;
            _hostWindow.Resumed += OnResumed;
        }
        UpdateNetworkInfo();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        StopStreaming();
        if (_hostWindow != null)
        {
            _hostWindow.Stopped -= OnStopped;
            _hostWindow.Resumed -= OnResumed;
        }
        base.OnDisappearing();
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        _foreground = false;
        _server.IsForeground = false;
        StopStreaming();
        _toggleButton.Text = AppText.PcCamera.StartServer;
        _statusLabel.Text = "Uygulama arka plana geçti; kamera yayını ve uzaktan çekim durduruldu.";
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        _foreground = true;
        _server.IsForeground = true;
        UpdateNetworkInfo();
    }
}
