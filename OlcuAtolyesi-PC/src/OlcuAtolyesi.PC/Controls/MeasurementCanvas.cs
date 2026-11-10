using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Measurement;

namespace OlcuAtolyesi.PC.Controls;

/// <summary>
/// Fotoğrafı, R1–R2 referans ve M1–M2 ölçüm noktalarını çizen etkileşimli çalışma alanı.
/// Noktalar <see cref="MeasurementSession"/> içinde fotoğraf piksel koordinatlarında tutulur;
/// yakınlaştırma ve kaydırma yalnızca <see cref="ImageViewportTransform"/> değerini değiştirir.
/// </summary>
public sealed class MeasurementCanvas : FrameworkElement
{
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session),
        typeof(MeasurementSession),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSessionChanged));

    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
        nameof(Image),
        typeof(ImageSource),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FitRequestProperty = DependencyProperty.Register(
        nameof(FitRequest),
        typeof(int),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(0, OnFitRequestChanged));

    public static readonly DependencyProperty MagnifierEnabledProperty = DependencyProperty.Register(
        nameof(MagnifierEnabled),
        typeof(bool),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(
            true,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MagnifierZoomProperty = DependencyProperty.Register(
        nameof(MagnifierZoom),
        typeof(double),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(
            4d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisplaySettingsProperty = DependencyProperty.Register(
        nameof(DisplaySettings),
        typeof(ImageDisplaySettings),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(null, OnDisplaySettingsChanged));

    public ImageDisplaySettings? DisplaySettings
    {
        get => (ImageDisplaySettings?)GetValue(DisplaySettingsProperty);
        set => SetValue(DisplaySettingsProperty, value);
    }

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public static readonly DependencyProperty ShowRulerProperty = DependencyProperty.Register(
        nameof(ShowRuler), typeof(bool), typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool ShowRuler
    {
        get => (bool)GetValue(ShowRulerProperty);
        set => SetValue(ShowRulerProperty, value);
    }

    public static readonly DependencyProperty ScreenMillimetresPerDipProperty = DependencyProperty.Register(
        nameof(ScreenMillimetresPerDip), typeof(double), typeof(MeasurementCanvas),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double ScreenMillimetresPerDip
    {
        get => (double)GetValue(ScreenMillimetresPerDipProperty);
        set => SetValue(ScreenMillimetresPerDipProperty, value);
    }

    private static void OnDisplaySettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (MeasurementCanvas)d;
        canvas._hasFit = false;
        canvas.FitView();
        canvas.InvalidateVisual();
    }

    private const double HandleHitRadius = 14d;
    private const double HandleRadius = 7d;
    private const double ViewportPadding = 16d;
    private const double ArrowLength = 10d;
    private const double ArrowHalfWidth = 4.5d;
    private const double EndTickLength = 9d;
    private const double ScaleBarTargetPixels = 110d;

    private static readonly Brush ReferenceBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));
    private static readonly Brush MeasureBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)));
    private static readonly Brush LabelBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x16, 0x1E)));
    private static readonly Brush LabelForeground = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Pen WhitePen = FreezePen(new Pen(Brushes.White, 1.5d));
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private ImageViewportTransform _transform = new(1d, 0d, 0d);
    private ImageViewportTransform _panStart = new(1d, 0d, 0d);
    private double _fitScale = 1d;
    private bool _hasFit;
    private bool _isFitted;
    private bool _panning;
    private Point _panOrigin;
    private MeasurePoint? _dragPoint;
    private MeasurePoint? _hoverPoint;
    private ImagePoint? _mouseImagePoint;
    private Point? _cursorViewPoint;

    public MeasurementCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
        Loaded += (_, _) =>
        {
            if (Session is { } session)
            {
                session.Changed -= OnSessionStateChanged;
                session.Changed += OnSessionStateChanged;
            }

            InvalidateVisual();
        };
        Unloaded += (_, _) =>
        {
            if (Session is { } session)
            {
                session.Changed -= OnSessionStateChanged;
            }
        };
    }

    public MeasurementSession? Session
    {
        get => (MeasurementSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public ImageSource? Image
    {
        get => (ImageSource?)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public int FitRequest
    {
        get => (int)GetValue(FitRequestProperty);
        set => SetValue(FitRequestProperty, value);
    }

    public bool MagnifierEnabled
    {
        get => (bool)GetValue(MagnifierEnabledProperty);
        set => SetValue(MagnifierEnabledProperty, value);
    }

    public double MagnifierZoom
    {
        get => (double)GetValue(MagnifierZoomProperty);
        set => SetValue(MagnifierZoomProperty, value);
    }

    public void ToggleMagnifier() => MagnifierEnabled = !MagnifierEnabled;

    public void CycleMagnifierZoom() => MagnifierZoom = NextMagnifierZoom(MagnifierZoom);

    private ImageDisplayTransform? _displayTransform;

    private ImageDisplayTransform DisplayTransform
    {
        get
        {
            if (Session is not { } session)
            {
                return new ImageDisplayTransform(new ImageSize(1d, 1d), null);
            }

            var settings = DisplaySettings ?? new ImageDisplaySettings();
            if (_displayTransform is null ||
                !ReferenceEquals(_displayTransform.Settings, settings) ||
                _displayTransform.OriginalSize != session.ImageSize)
            {
                _displayTransform = new ImageDisplayTransform(session.ImageSize, settings);
            }

            return _displayTransform;
        }
    }

    /// <summary>Geçerli görünüm dönüşümü (yalnızca görüntüleme).</summary>
    public ImageViewportTransform ViewTransform => _transform;

    /// <summary>
    /// Ölçek çubuğu için "yuvarlak" bir milimetre değeri seçer (1, 2, 5, 10, 20, 50, 100, 200, 500).
    /// Böylece çubuk ekranda hedef piksel genişliğine en yakın okunabilir değerde çizilir.
    /// </summary>
    public static double PickScaleBarMillimetres(double millimetresPerPixel, double targetPixels)
    {
        if (!double.IsFinite(millimetresPerPixel) || millimetresPerPixel <= 0d ||
            !double.IsFinite(targetPixels) || targetPixels <= 0d)
        {
            return 0d;
        }

        var ideal = millimetresPerPixel * targetPixels;
        double[] candidates = [1d, 2d, 5d, 10d, 20d, 50d, 100d, 200d, 500d];

        var best = candidates[0];
        var bestDelta = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var delta = Math.Abs(candidate - ideal);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Büyüteç merkezini imlecin çapraz üstüne yerleştirir; böylece yerleştirilecek nokta
    /// büyütecin altında kalmaz. Kenarlara taşarsa ters tarafa geçer ve tuval içinde kalır.
    /// </summary>
    public static Point ComputeMagnifierCenter(Point cursor, double radius, double width, double height)
    {
        const double gap = 26d;
        var offset = radius + gap;

        var x = cursor.X + offset;
        var y = cursor.Y - offset;

        // Sağ kenara taşıyorsa sola geç
        if (x + radius > width)
        {
            x = cursor.X - offset;
        }

        // Üst kenara taşıyorsa alta geç
        if (y - radius < 0d)
        {
            y = cursor.Y + offset;
        }

        x = Math.Clamp(x, radius + 2d, Math.Max(radius + 2d, width - radius - 2d));
        y = Math.Clamp(y, radius + 2d, Math.Max(radius + 2d, height - radius - 2d));

        return new Point(x, y);
    }

    /// <summary>Büyüteç yakınlaştırma kademeleri: 2x -> 4x -> 8x -> 2x</summary>
    public static double NextMagnifierZoom(double current)
    {
        if (current < 3d)
        {
            return 4d;
        }

        if (current < 6d)
        {
            return 8d;
        }

        return 2d;
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (MeasurementCanvas)d;
        if (e.OldValue is MeasurementSession oldSession)
        {
            oldSession.Changed -= canvas.OnSessionStateChanged;
        }

        if (e.NewValue is MeasurementSession newSession && canvas.IsLoaded)
        {
            newSession.Changed -= canvas.OnSessionStateChanged;
            newSession.Changed += canvas.OnSessionStateChanged;
        }

        canvas._dragPoint = null;
        canvas._panning = false;
        canvas._hasFit = false;
        canvas.FitView();
    }

    private static void OnFitRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MeasurementCanvas)d).FitView();

    private void OnSessionStateChanged(object? sender, EventArgs e) => InvalidateVisual();

    public void FitView()
    {
        if (Session is not { } session || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        _transform = ImageViewportTransform.Fit(DisplayTransform.DisplaySize, new ViewSize(ActualWidth, ActualHeight), ViewportPadding);
        _fitScale = _transform.Scale;
        _hasFit = true;
        _isFitted = true;
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_hasFit || _isFitted)
        {
            FitView();
        }
        else if (Session is { } session)
        {
            _transform = _transform.ClampToViewport(DisplayTransform.DisplaySize, new ViewSize(ActualWidth, ActualHeight), ViewportPadding);
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640d : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 420d : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(0d, 0d, ActualWidth, ActualHeight);
        dc.DrawRectangle(FindBrush("CardAltBackgroundBrush", Brushes.DimGray), null, bounds);

        if (Session is not { } session || !_hasFit)
        {
            return;
        }

        var t = _transform;
        var topLeft = t.ImageToView(new ImagePoint(0d, 0d));
        var displaySize = DisplayTransform.DisplaySize;
        var imageRect = new Rect(topLeft.X, topLeft.Y, displaySize.Width * t.Scale, displaySize.Height * t.Scale);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (Image is { } image)
        {
            dc.DrawImage(image, imageRect);
        }
        else
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4A)), null, imageRect);
            DrawLabel(dc, "Demo çalışma alanı — fotoğraf açılmadı", new Point(imageRect.Left + 10d, imageRect.Top + 8d), dpi, 12d);
        }

        if (ShowGrid)
        {
            DrawGrid(dc, imageRect);
        }

        if (ShowRuler)
        {
            DrawObjectRuler(dc, session, imageRect, dpi);
            DrawScreenRuler(dc, dpi);
        }

        dc.DrawRectangle(null, new Pen(FindBrush("BorderSubtleBrush", Brushes.Gray), 1d), imageRect);

        var reference = session.ReferenceSegment;
        var measurement = session.Measurement;
        var r1 = ToPoint(t.ImageToView(DisplayTransform.ToDisplay(reference.Start)));
        var r2 = ToPoint(t.ImageToView(DisplayTransform.ToDisplay(reference.End)));
        var m1 = ToPoint(t.ImageToView(DisplayTransform.ToDisplay(measurement.Start)));
        var m2 = ToPoint(t.ImageToView(DisplayTransform.ToDisplay(measurement.End)));

        var referencePen = new Pen(ReferenceBrush, 2d) { DashStyle = DashStyles.Dash };
        var measurePen = new Pen(MeasureBrush, 2.5d);

        DrawDimension(dc, r1, r2, referencePen, ReferenceBrush);
        DrawDimension(dc, m1, m2, measurePen, MeasureBrush);

        if (session.KnownLength is { } known)
        {
            DrawLabel(dc, $"REF {known.Format()}", Midpoint(r1, r2, 0d, 10d), dpi, 12d);
        }

        DrawLabel(dc, session.Result?.Format() ?? "Ölçüm için referans gerekli", Midpoint(m1, m2, 0d, -26d), dpi, 13d);

        DrawHandle(dc, MeasurePoint.R1, r1, ReferenceBrush, session.ArmedPoint, dpi);
        DrawHandle(dc, MeasurePoint.R2, r2, ReferenceBrush, session.ArmedPoint, dpi);
        DrawHandle(dc, MeasurePoint.M1, m1, MeasureBrush, session.ArmedPoint, dpi);
        DrawHandle(dc, MeasurePoint.M2, m2, MeasureBrush, session.ArmedPoint, dpi);

        var zoom = t.Scale / Math.Max(0.0001d, _fitScale);
        DrawLabel(dc, $"Yakınlaştırma: {zoom.ToString("0.00", CultureInfo.GetCultureInfo("tr-TR"))}×", new Point(8d, 8d), dpi, 11.5d);

        if (_mouseImagePoint is { } mouse)
        {
            var text = $"X: {mouse.X:0} Y: {mouse.Y:0} px";
            DrawLabel(dc, text, new Point(8d, Math.Max(8d, ActualHeight - 28d)), dpi, 11.5d);
        }

        DrawScaleBar(dc, session, dpi);
        DrawMagnifier(dc, session, imageRect, dpi, r1, r2, m1, m2);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Session is not { } session || !_hasFit)
        {
            return;
        }

        Focus();
        var position = e.GetPosition(this);
        if (session.ArmedPoint is { } armed)
        {
            session.BeginPlace(ToImage(position));
            _dragPoint = armed;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (HitTestHandle(session, position) is { } hit)
        {
            session.CaptureUndo();
            _dragPoint = hit;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        _panning = true;
        _panOrigin = position;
        _panStart = _transform;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Session is not { } session || !_hasFit)
        {
            return;
        }

        var position = e.GetPosition(this);
        _mouseImagePoint = ToImage(position);
        _cursorViewPoint = position;

        if (_dragPoint is { } dragPoint)
        {
            var target = ToImage(position);

            // Shift basılıyken yatay veya dikey eksende kilitle (düz çizgi çekmek için)
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                var anchor = dragPoint switch
                {
                    MeasurePoint.M1 => session.M2,
                    MeasurePoint.M2 => session.M1,
                    MeasurePoint.R1 => session.R2,
                    MeasurePoint.R2 => session.R1,
                    _ => target
                };

                var dx = Math.Abs(target.X - anchor.X);
                var dy = Math.Abs(target.Y - anchor.Y);
                target = dx >= dy
                    ? new ImagePoint(target.X, anchor.Y)
                    : new ImagePoint(anchor.X, target.Y);
            }

            session.MovePoint(dragPoint, target);
        }
        else if (_panning)
        {
            _transform = _panStart
                .PanBy(position.X - _panOrigin.X, position.Y - _panOrigin.Y)
                .ClampToViewport(session.ImageSize, new ViewSize(ActualWidth, ActualHeight), ViewportPadding);
            _isFitted = false;
        }
        else
        {
            _hoverPoint = HitTestHandle(session, position);
            Cursor = session.ArmedPoint is not null
                ? Cursors.Cross
                : _hoverPoint is not null ? Cursors.Hand : Cursors.Arrow;
        }

        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragPoint is not null && Session is { } session && session.ArmedPoint is not null)
        {
            session.AdvanceArm();
        }

        EndInteraction();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndInteraction();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _mouseImagePoint = null;
        _cursorViewPoint = null;
        _hoverPoint = null;
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Session?.CancelArm();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Session?.CancelArm();
            e.Handled = true;
        }

        if (Session is not { } activeSession)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.R:
                activeSession.ArmReference();
                e.Handled = true;
                break;
            case Key.M:
                activeSession.ArmMeasure();
                e.Handled = true;
                break;
            case Key.F:
                FitView();
                e.Handled = true;
                break;
            case Key.Delete:
                activeSession.Reset();
                e.Handled = true;
                break;
            case Key.Z when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                activeSession.Undo();
                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;
        if (Session is not { } session || !_hasFit)
        {
            return;
        }

        var position = e.GetPosition(this);
        var factor = Math.Pow(1.0015d, e.Delta);
        var minScale = _fitScale * 0.5d;
        var maxScale = Math.Max(_fitScale * 40d, 16d);
        _transform = _transform
            .ZoomAt(new ViewPoint(position.X, position.Y), factor, minScale, maxScale)
            .ClampToViewport(DisplayTransform.DisplaySize, new ViewSize(ActualWidth, ActualHeight), ViewportPadding);
        _isFitted = false;
        _mouseImagePoint = ToImage(position);
        InvalidateVisual();
    }

    private void EndInteraction()
    {
        _dragPoint = null;
        _panning = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        Cursor = Session?.ArmedPoint is not null ? Cursors.Cross : Cursors.Arrow;
    }

    private MeasurePoint? HitTestHandle(MeasurementSession session, Point position)
    {
        MeasurePoint? best = null;
        var bestDistance = HandleHitRadius;
        var probe = new ViewPoint(position.X, position.Y);
        foreach (var point in new[] { MeasurePoint.M1, MeasurePoint.M2, MeasurePoint.R1, MeasurePoint.R2 })
        {
            var distance = _transform.ImageToView(DisplayTransform.ToDisplay(session.GetPoint(point))).DistanceTo(probe);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best;
    }

    private ImagePoint ToImage(Point position) =>
        DisplayTransform.ToOriginal(_transform.ViewToImage(new ViewPoint(position.X, position.Y)));

    private static void DrawGrid(DrawingContext dc, Rect imageRect)
    {
        const int divisions = 10;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), 1d);

        for (var i = 1; i < divisions; i++)
        {
            var x = imageRect.Left + (imageRect.Width * i / divisions);
            var y = imageRect.Top + (imageRect.Height * i / divisions);
            dc.DrawLine(pen, new Point(x, imageRect.Top), new Point(x, imageRect.Bottom));
            dc.DrawLine(pen, new Point(imageRect.Left, y), new Point(imageRect.Right, y));
        }
    }

    /// <summary>
    /// A cetveli: fotoğrafın üst ve sol kenarında, NESNENİN gerçek ölçüsünde taksimat.
    /// Referans yoksa piksel taksimatı çizer.
    /// </summary>
    private void DrawObjectRuler(DrawingContext dc, MeasurementSession session, Rect imageRect, double dpi)
    {
        const double rulerThickness = 18d;
        var known = session.KnownLength;
        var hasReference = known is { IsPositive: true } &&
                           session.ReferenceSegment.ImageLength > 0.0001d;

        var topStrip = new Rect(imageRect.Left, imageRect.Top, imageRect.Width, rulerThickness);
        var leftStrip = new Rect(imageRect.Left, imageRect.Top, rulerThickness, imageRect.Height);

        var background = new SolidColorBrush(Color.FromArgb(0xB0, 0x0F, 0x17, 0x2A));
        background.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xDD, 0x00, 0xE5, 0xFF)), 1d);
        pen.Freeze();

        dc.DrawRectangle(background, null, topStrip);
        dc.DrawRectangle(background, null, leftStrip);

        string unit;
        RulerTickStep step;
        double pixelsPerUnit;   // ekranda 1 birim = kaç piksel

        if (hasReference)
        {
            var mmPerPixel = known!.Value.Millimeters / session.ReferenceSegment.ImageLength;
            var screenPixelsPerMm = _transform.Scale / mmPerPixel;
            step = RulerTickCalculator.ChooseMillimetreStep(screenPixelsPerMm);
            unit = "mm";
            pixelsPerUnit = screenPixelsPerMm;
        }
        else
        {
            step = RulerTickCalculator.ChoosePixelStep(_transform.Scale);
            unit = "px";
            pixelsPerUnit = _transform.Scale;
        }

        if (step.StepValue <= 0d || step.StepPixels < 4d)
        {
            return;
        }

        var originX = imageRect.Left;
        var originY = imageRect.Top;
        var index = 0;

        while (true)
        {
            var offset = index * step.StepPixels;
            if (offset > imageRect.Width + 0.5d || offset > imageRect.Height + 0.5d)
            {
                break;
            }

            var isMajor = index % 5 == 0;
            var tickHeight = isMajor ? rulerThickness : rulerThickness * 0.45d;

            // üst kenar
            var x = originX + offset;
            if (x <= imageRect.Right)
            {
                dc.DrawLine(pen, new Point(x, originY), new Point(x, originY + tickHeight));
                if (isMajor && offset < imageRect.Width - 30d)
                {
                    DrawLabel(dc, $"{step.StepValue * index:0.##}", new Point(x + 2d, originY + rulerThickness + 1d), dpi, 10d);
                }
            }

            // sol kenar (dikey taksimat birimi mm ise mm, px ise px)
            var y = originY + offset;
            if (y <= imageRect.Bottom)
            {
                dc.DrawLine(pen, new Point(originX, y), new Point(originX + tickHeight, y));
                if (isMajor && offset < imageRect.Height - 30d)
                {
                    DrawLabel(dc, $"{step.StepValue * index:0.##}", new Point(originX + rulerThickness + 1d, y + 1d), dpi, 10d);
                }
            }

            index++;
            if (index > 500)
            {
                break;
            }
        }

        DrawLabel(dc, unit, new Point(imageRect.Right - 26d, imageRect.Top + 2d), dpi, 10d);
    }

    /// <summary>
    /// B cetveli: ekranın FİZİKSEL milimetre cetveli. Yalnızca kalibrasyon geçerliyse çizilir.
    /// Sağ alt köşede yatay bir şerit olarak gösterilir.
    /// </summary>
    private void DrawScreenRuler(DrawingContext dc, double dpi)
    {
        if (ScreenMillimetresPerDip <= 0.05d || ScreenMillimetresPerDip >= 2.0d)
        {
            return;
        }

        var calibration = new ScreenCalibration(ScreenMillimetresPerDip, "ekran");
        var stepMm = RulerTickCalculator.ChooseMillimetreStep(1d / ScreenMillimetresPerDip).StepValue;
        if (stepMm <= 0d)
        {
            return;
        }

        var stepDips = calibration.ToDips(stepMm);
        var totalMm = stepMm * 10d;
        var totalDips = calibration.ToDips(totalMm);

        var right = ActualWidth - 18d;
        var bottom = ActualHeight - 18d;
        var left = Math.Max(18d, right - totalDips);
        if (left >= right)
        {
            return;
        }

        var strip = new Rect(left, bottom - 16d, right - left, 16d);
        var background = new SolidColorBrush(Color.FromArgb(0xA0, 0x0F, 0x17, 0x2A));
        background.Freeze();
        dc.DrawRectangle(background, null, strip);

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xEE, 0xF5, 0x9E, 0x0B)), 1d);
        pen.Freeze();

        dc.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));

        for (var i = 0; i <= 10; i++)
        {
            var x = left + (i * stepDips);
            var tick = i % 5 == 0 ? 12d : 7d;
            dc.DrawLine(pen, new Point(x, bottom), new Point(x, bottom - tick));

            if (i % 5 == 0)
            {
                DrawLabel(dc, $"{stepMm * i:0}", new Point(x - 4d, bottom - 30d), dpi, 9.5d);
            }
        }

        DrawLabel(dc, "ekran (mm)", new Point(left, bottom - 48d), dpi, 9.5d);
    }

    private static Point ToPoint(ViewPoint point) => new(point.X, point.Y);

    private static Point Midpoint(Point a, Point b, double offsetX, double offsetY) =>
        new(((a.X + b.X) / 2d) + offsetX, ((a.Y + b.Y) / 2d) + offsetY);

    /// <summary>
    /// İki nokta arasına CAD tipi ölçü çizgisi çizer: çizgi + iki uçta ok başı + uç tikleri.
    /// </summary>
    private void DrawDimension(DrawingContext dc, Point start, Point end, Pen pen, Brush brush)
    {
        dc.DrawLine(pen, start, end);

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1d)
        {
            return;
        }

        var ux = dx / length;
        var uy = dy / length;
        var nx = -uy;
        var ny = ux;

        DrawArrowHead(dc, start, ux, uy, brush);
        DrawArrowHead(dc, end, -ux, -uy, brush);

        dc.DrawLine(pen,
            new Point(start.X + (nx * EndTickLength / 2d), start.Y + (ny * EndTickLength / 2d)),
            new Point(start.X - (nx * EndTickLength / 2d), start.Y - (ny * EndTickLength / 2d)));
        dc.DrawLine(pen,
            new Point(end.X + (nx * EndTickLength / 2d), end.Y + (ny * EndTickLength / 2d)),
            new Point(end.X - (nx * EndTickLength / 2d), end.Y - (ny * EndTickLength / 2d)));
    }

    /// <summary>Uç noktaya, verilen yöne BAKAN dolu üçgen ok başı çizer.</summary>
    private static void DrawArrowHead(DrawingContext dc, Point tip, double dirX, double dirY, Brush brush)
    {
        var baseX = tip.X + (dirX * ArrowLength);
        var baseY = tip.Y + (dirY * ArrowLength);
        var perpX = -dirY;
        var perpY = dirX;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(tip, isFilled: true, isClosed: true);
            ctx.LineTo(new Point(baseX + (perpX * ArrowHalfWidth), baseY + (perpY * ArrowHalfWidth)), true, false);
            ctx.LineTo(new Point(baseX - (perpX * ArrowHalfWidth), baseY - (perpY * ArrowHalfWidth)), true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    /// <summary>Sağ altta, milimetre cinsinden ölçek çubuğu çizer.</summary>
    private void DrawScaleBar(DrawingContext dc, MeasurementSession session, double dpi)
    {
        if (session.KnownLength is not { IsPositive: true } known ||
            session.ReferenceSegment.ImageLength <= 0.0001d)
        {
            return;
        }

        var mmPerPixel = known.Millimeters / session.ReferenceSegment.ImageLength;
        var barMillimetres = PickScaleBarMillimetres(mmPerPixel, ScaleBarTargetPixels);
        if (barMillimetres <= 0d)
        {
            return;
        }

        var barPixels = barMillimetres / mmPerPixel;
        if (!double.IsFinite(barPixels) || barPixels <= 0d || barPixels > ActualWidth)
        {
            return;
        }

        var right = ActualWidth - 18d;
        var bottom = ActualHeight - 22d;
        var left = right - barPixels;

        var pen = new Pen(ReferenceBrush, 2d);
        dc.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));
        dc.DrawLine(pen, new Point(left, bottom - 4d), new Point(left, bottom + 4d));
        dc.DrawLine(pen, new Point(right, bottom - 4d), new Point(right, bottom + 4d));

        DrawLabel(dc, $"{barMillimetres:0} mm", new Point(left, bottom - 24d), dpi, 11d);
    }

    /// <summary>
    /// İmlecin çapraz üstünde, içinde fotoğraf + ölçüm çizgileri + tutamaçlar görünen büyüteç çizer.
    /// Zoom kademesi `MagnifierZoom` (2x/4x/8x) ile belirlenir; `MagnifierEnabled` ile kapatılabilir.
    /// </summary>
    private void DrawMagnifier(
        DrawingContext dc,
        MeasurementSession session,
        Rect imageRect,
        double dpi,
        Point r1,
        Point r2,
        Point m1,
        Point m2)
    {
        if (!MagnifierEnabled || _cursorViewPoint is not { } cursor)
        {
            return;
        }

        const double radius = 58d;
        var magnification = Math.Clamp(MagnifierZoom, 2d, 8d);

        var center = ComputeMagnifierCenter(cursor, radius, ActualWidth, ActualHeight);

        // 1) Arka plan (kırpmadan ÖNCE)
        dc.DrawEllipse(FindBrush("CardAltBackgroundBrush", Brushes.Black), null, center, radius, radius);

        // 2) İmleçten büyütece ince bağlantı çizgisi (hangi noktayı gösterdiği anlaşılsın)
        var connectorPen = new Pen(FindBrush("AccentBrush", Brushes.DeepSkyBlue), 1d)
        {
            DashStyle = DashStyles.Dot
        };
        dc.DrawLine(connectorPen, cursor, center);

        // 3) Büyütülmüş içerik
        dc.PushClip(new EllipseGeometry(center, radius, radius));

        var matrix = new Matrix(
            magnification, 0d,
            0d, magnification,
            center.X - (magnification * cursor.X),
            center.Y - (magnification * cursor.Y));

        dc.PushTransform(new MatrixTransform(matrix));

        if (Image is { } image)
        {
            dc.DrawImage(image, imageRect);
        }

        // Ölçüm geometrisi: çizgiler büyütmeye göre inceltilir ki kalın görünmesin
        var referencePen = new Pen(ReferenceBrush, 2d / magnification) { DashStyle = DashStyles.Dash };
        var measurePen = new Pen(MeasureBrush, 2.5d / magnification);
        dc.DrawLine(referencePen, r1, r2);
        dc.DrawLine(measurePen, m1, m2);

        var handleOutline = new Pen(Brushes.White, 1.5d / magnification);
        var handleRadius = HandleRadius / magnification;
        dc.DrawEllipse(ReferenceBrush, handleOutline, r1, handleRadius, handleRadius);
        dc.DrawEllipse(ReferenceBrush, handleOutline, r2, handleRadius, handleRadius);
        dc.DrawEllipse(MeasureBrush, handleOutline, m1, handleRadius, handleRadius);
        dc.DrawEllipse(MeasureBrush, handleOutline, m2, handleRadius, handleRadius);

        dc.Pop();
        dc.Pop();

        // 4) SADECE kenarlık (dolgu YOK)
        var borderPen = new Pen(FindBrush("AccentBrush", Brushes.DeepSkyBlue), 1.5d);
        dc.DrawEllipse(null, borderPen, center, radius, radius);

        // Büyütme oranı etiketi
        DrawLabel(dc, $"{magnification:0}×", new Point(center.X - (radius * 0.45d), center.Y + radius + 6d), dpi, 11d);

        // Merkez artı işareti (imlecin gerçek konumu)
        var centerPen = new Pen(FindBrush("AccentBrush", Brushes.DeepSkyBlue), 1d);
        dc.DrawEllipse(null, centerPen, cursor, 5d, 5d);

        // Tam ekran crosshair
        var crossPen = new Pen(FindBrush("MutedTextBrush", Brushes.Gray), 1d) { DashStyle = DashStyles.Dot };
        dc.DrawLine(crossPen, new Point(cursor.X, 0d), new Point(cursor.X, ActualHeight));
        dc.DrawLine(crossPen, new Point(0d, cursor.Y), new Point(ActualWidth, cursor.Y));
    }

    private void DrawHandle(DrawingContext dc, MeasurePoint point, Point center, Brush brush, MeasurePoint? armed, double dpi)
    {
        var emphasized = armed == point || _dragPoint == point || _hoverPoint == point;
        var radius = emphasized ? HandleRadius + 3d : HandleRadius;
        dc.DrawEllipse(brush, WhitePen, center, radius, radius);
        DrawLabel(dc, point.ToString(), new Point(center.X + 10d, center.Y - 24d), dpi, 11.5d);
    }

    private static void DrawLabel(DrawingContext dc, string text, Point origin, double dpi, double size)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight,
            LabelTypeface,
            size,
            LabelForeground,
            dpi);
        var rect = new Rect(origin.X - 4d, origin.Y - 2d, formatted.Width + 8d, formatted.Height + 4d);
        dc.DrawRoundedRectangle(LabelBackground, null, rect, 4d, 4d);
        dc.DrawText(formatted, origin);
    }

    private Brush FindBrush(string key, Brush fallback) =>
        TryFindResource(key) as Brush ?? fallback;

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
