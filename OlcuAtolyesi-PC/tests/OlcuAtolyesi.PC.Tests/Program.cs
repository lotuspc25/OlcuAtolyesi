using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OlcuAtolyesi.Core.Diagnostics;
using OlcuAtolyesi.Core.Export;
using OlcuAtolyesi.Core.Geometry;
using OlcuAtolyesi.Core.Measurement;
using OlcuAtolyesi.Core.Models;
using OlcuAtolyesi.Core.Phone;
using OlcuAtolyesi.Core.Services;
using OlcuAtolyesi.PC;
using OlcuAtolyesi.PC.Controls;
using OlcuAtolyesi.PC.Media;
using OlcuAtolyesi.PC.Services;
using OlcuAtolyesi.PC.Themes;
using OlcuAtolyesi.PC.ViewModels;
using OlcuAtolyesi.PC.Views;

var checks = new (string Name, Action Run)[]
{
    ("Ortak Geometri Çekirdeği: Birim dönüşümleri ve referans ölçeği", CheckCoreGeometryAndUnits),
    ("Modül Kataloğu: 9 sol menü modülü ve 9 açık kaynak entegrasyon adayı", CheckModuleCatalogAndOpenSourceList),
    ("Masaüstü Ayarlar Servisi: Tema ve birim kalıcılığı / bozuk JSON dayanıklılığı", CheckDesktopSettingsPersistence),
    ("Ana Pencere ViewModel: Başlangıç durumu ve 9 sayfa arasında navigasyon", CheckNavigationWorkflow),
    ("Fotoğraftan Ölçüm ViewModel: R1–R2 referans ve M1–M2 etkileşimli hesaplama", CheckPhotoMeasurementViewModel),
    ("Görünüm dönüşümü: ölçek, yakınlaştırma, kaydırma, sınırlama ve ters dönüşüm", CheckViewportTransformMath),
    ("Ölçüm oturumu: piksel koordinatları, sınırlama, referans/birim yeniden hesaplama", CheckMeasurementSessionMath),
    ("Ölçüm oturumu: geri al, sıfırla ve R1→R2 / M1→M2 nokta seçim zinciri", CheckMeasurementSessionUndoAndArming),
    ("Büyütme/kaydırma sırasında ölçüm sonucunun değişmemesi", CheckMeasurementInvariantUnderViewChanges),
    ("Fotoğraf yükleyici: JPG/PNG/BMP, EXIF yönü, büyük görsel ve hatalı dosyalar", CheckPhotoLoader),
    ("Fotoğraftan Ölçüm ViewModel: fotoğraf yükleme, referans/birim ve hata yönetimi", CheckPhotoViewModelWithRealFile),
    ("Kalıcı Proje Yönetimi: oluşturma, kaydetme, yeniden adlandırma, silme ve diğer projelerin korunması", CheckPersistentProjectCrudAndIsolation),
    ("Kayıpsız Ölçüm Geri Yükleme: fotoğraf, R1/R2, M1/M2, referans ve birimlerin yeniden başlatma sonrası korunması", CheckLosslessProjectReloadAndZoomInvariance),
    ("Hata Dayanıklılığı: atomik yazım, .bak yedeğinden kurtarma, bozuk JSON ve eksik fotoğraf senaryoları", CheckCorruptedJsonBackupRecoveryAndMissingPhoto),
    ("Mobil v0.4.0 Uyumluluğu: project.json + photo.jpg içe aktarma, kaynak koruma ve çakışma politikaları", CheckMobileV040ImportAndConflictHandling),
    ("Taşınabilir Proje Paketi (.zip ve klasör): dışa/içe aktarma, göreli yol ve Zip Slip güvenlik kontrolü", CheckPortablePackageExportImportAndSecurity),
    ("Projeler ve Fotoğraftan Ölçüm ViewModel Entegrasyonu: Kaydet, Farklı Kaydet ve yeniden açıp düzenleme", CheckProjectsAndPhotoMeasurementIntegration),
    ("ADB Bulucu ve Kimlik Doğrulayıcı: ADB bulunamaması, özel yol bulma ve geçersiz cihaz kimliği reddi", CheckAdbLocatorAndSerialValidation),
    ("ADB Tek Cihaz ve Bağlı Cihaz Yok: device durumu, Android sürümü sorgusu, seçim ve boş liste", CheckSingleDeviceAndNoDeviceScenarios),
    ("ADB Çoklu Cihaz, Yetkisiz ve Çevrimdışı Ayrımı: unauthorized/offline cihazlara komut göndermeme ve oturum ayırma", CheckMultiDeviceUnauthorizedOfflineAndDisconnect),
    ("ADB Zaman Aşımı, İptal ve Bozuk Çıktı Dayanıklılığı: timeout, cancellation ve hatalı adb devices -l satırları", CheckAdbTimeoutCancellationAndMalformedOutput),
    ("Gelecek Bağlantı Kanalları ve Kamera Mimarisi Sözleşmeleri: USB etkin, Wi-Fi/Bluetooth planlandı ve 9 kamera kontrolü", CheckTransportOptionsAndRemoteCameraContracts),
    ("Telefon Bağlantısı ViewModel İş Akışı: Cihazları Tara, Cihaz Seç, Bağlantıyı Kontrol Et, Yenile ve Ayır", CheckPhoneConnectionViewModelWorkflow),
    ("scrcpy Çıktı Ayrıştırıcı: Sürüm, --list-cameras, --list-camera-sizes ve bozuk çıktı toleransı", CheckScrcpyOutputParser),
    ("scrcpy Süreç Yöneticisi: Yaşam döngüsü, argüman güvenliği, zaman aşımı ve hata izolasyonu", CheckScrcpyProcessManagerLifecycleAndSafety),
    ("Kamera ve Kalibrasyon ViewModel: Cihaz kontrolü, kamera tarama, oturum başlatma/durdurma iş akışı", CheckCameraCalibrationViewModelWorkflow),
    ("v0.4.2 Kamera Sensörleri ve Dinamik Mod Seçimi: ANSI temizliği, sensör tespiti, FPS/çözünürlük dinamik eşleşmesi", CheckCameraSensorsAndDynamicModes),
    ("v0.5.0 Bağımsız Wi-Fi Kamera İstemcisi: HTTP MJPEG akış ayrıştırma, PIN doğrulaması ve kopma toleransı", CheckWifiCameraStreamClient),
    ("v0.5.0 Kamera Mod Seçimi ve WPF Canlı Önizleme: USB vs. Wi-Fi geçişi, canlı kare yakalama ve ölçüm aktarımı", CheckCameraModeSwitchingAndLivePreview),
    ("v0.5.0.1 Wi-Fi MJPEG Dayanıklılık: Bölünmüş paketler, bozuk kare izolasyonu, yaşam döngüsü ve buton durumları", CheckWifiMjpegRobustStreamingAndErrorScenarios),
    ("v0.5.0.2 Kamera Karesi Yakalama ve Ölçüm Aktarımı: Geçerli/bozuk kare, demo/fotoğraf geçişi, referans ve navigasyon", CheckCapturedFrameTransferAndMeasurementWorkflow),
    ("WPF STA: pencere başlatma, tema geçişi ve fotoğraf/proje/telefon/kamera çalışma alanı yerleşimi (etkileşimsiz)", CheckWpfWindowAndThemeStartup),
    ("v0.5.0.3 Kumpas Temalı İkon, Açılış Ekranı (Splash) ve Responsive Tasarım Doğrulaması", CheckBrandedAssetsAndResponsiveLayout),
    ("v0.5.0.4 Merkezi Tanılama Konsolu ve Uzaktan Log Alma Entegrasyonu", CheckDiagnosticConsoleAndRemoteLogIngestion),
    ("v0.5.0.4 Tanılama Konsolu Penceresi STA Başlatma ve Düzen Doğrulaması", CheckDiagnosticConsoleWindowStaStartup),
    ("v0.5.0.5 Uzaktan Yüksek Çözünürlüklü Çekim ve SHA-256 Doğrulamalı İndirme", CheckRemoteHighResCaptureAndSha256Download),
    ("v0.5.0.5 Yüksek Çözünürlüklü Fotoğrafın Ölçüm Modülüne Aktarımı ve Kalibrasyon Sıfırlaması", CheckHighResPhotoMeasurementIntegrationAndCalibrationReset),
    ("v0.5.0.6 Oturum Belirteci Yenileme ve 401 Kurtarma", CheckTokenRefreshOnUnauthorized),
    ("v0.6.0 DXF Üretimi, Katmanlar ve Birim Dönüşümü", CheckDxfExportStructureAndUnits),
    ("v0.6.0 DXF Y Ekseni Ters Çevirme, ASCII Metin ve Hata Yolları", CheckDxfCoordinateFlipAndValidation),
    ("v0.6.1 Adım Rehberi Metinleri ve Ölçek Çubuğu Uzunluğu", CheckToolStateAdvisorAndScaleBar),
    ("v0.6.1 Profesyonel Çizim Kaynağı (ok başı, büyüteç, kısayol)", CheckProfessionalDrawingSource),
    ("v0.6.2 Büyüteç Merkez Hesabı ve Zoom Kademeleri", CheckMagnifierCenterAndZoom),
    ("v0.6.2 Büyüteç Açma-Kapama Kaynağı", CheckMagnifierToggleSource),
    ("v0.6.3 Büyüteç Görüntü ve Kısayol Düzeltmesi", CheckMagnifierRenderingAndShortcuts),
    ("v0.6.4 Ayrı Ölçüm Penceresi ve Paylaşılan Oturum", CheckMeasurementWindowSource),
    ("v0.6.5 Görüntü Dönüşümü Tersinirlik (döndür/aynala/kırp)", CheckImageDisplayTransformRoundTrip),
    ("v0.6.5 Görüntü Filtreleri (parlaklık, gri, negatif)", CheckPhotoFilterPipeline),
    ("v0.6.5 Sekmeli Panel ve Canvas Dönüşüm Bağlantısı", CheckImageTabsAndCanvasBinding),
    ("v0.6.6 Sekmelerde İkonlar ve Pencere Ayrımı", CheckTabIconsAndWindowSplit),
    ("v0.6.7 Cetvel Taksimat Seçimi (mm ve px)", CheckRulerTickCalculator),
    ("v0.6.7 Referans Nesne Kütüphanesi", CheckReferenceObjectLibrary),
    ("v0.6.7 Ekran Kalibrasyonu Hesabı", CheckScreenCalibration),
    ("v0.6.7 Sade Ana Pencere ve Cetvel Bağlantıları", CheckSimplifiedMainWindowSource)
};

var failures = new List<string>();

foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"BAŞARILI  {check.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{check.Name}: {ex.Message}");
        Console.WriteLine($"BAŞARISIZ {check.Name} -> {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Sonuç: {checks.Length - failures.Count}/{checks.Length} kontrol başarılı.");

return failures.Count == 0 ? 0 : 1;

static void CheckCoreGeometryAndUnits()
{
    Near(25.4d, new LengthValue(1d, LengthUnit.Inch).Millimeters);
    Near(1000d, new LengthValue(1d, LengthUnit.Meter).Millimeters);

    var parsed = ReferenceLengthParser.ParsePositive("26,15", LengthUnit.Millimeter);
    True(parsed.IsValid, parsed.ErrorMessage);
    Near(26.15d, parsed.Length!.Value.Value);

    False(ReferenceLengthParser.ParsePositive("0", LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("-15", LengthUnit.Centimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("abc", LengthUnit.Millimeter).IsValid);

    var scale = new ReferenceScale(
        new ImageSegment(new ImagePoint(200d, 500d), new ImagePoint(800d, 500d)), // 600 px
        new LengthValue(120d, LengthUnit.Millimeter));                           // 0.2 mm/px

    var measured = MeasurementGeometry.MeasureLength(
        new ImageSegment(new ImagePoint(250d, 205d), new ImagePoint(700d, 205d)), // 450 px -> 90 mm
        scale,
        LengthUnit.Centimeter);

    Near(9d, measured.Value);
    Equal(LengthUnit.Centimeter, measured.Unit);

    Near(90d, MeasurementGeometry.MeasureAngleDegrees(
        new ImagePoint(10d, 0d),
        new ImagePoint(0d, 0d),
        new ImagePoint(0d, 10d)));
}

static void CheckModuleCatalogAndOpenSourceList()
{
    var catalog = new ModuleCatalogService();
    var modules = catalog.GetAllModules();
    Equal(9, modules.Count);

    var expectedTitles = new[]
    {
        "Ana Panel",
        "Projeler",
        "Telefon Bağlantısı",
        "Kamera ve Kalibrasyon",
        "Fotoğraftan Ölçüm",
        "CAD / DXF / STEP / STL",
        "3B Baskı",
        "Ayarlar",
        "Yardım"
    };

    for (var i = 0; i < expectedTitles.Length; i++)
    {
        Equal(expectedTitles[i], modules[i].Title);
    }

    var candidates = catalog.GetOpenSourceCandidates();
    Equal(9, candidates.Count);
    True(candidates.Any(c => c.Name.Contains("OpenCV", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("OpenScan", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("Meshroom", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("AliceVision", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("FreeCAD", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("Open CASCADE", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("ezdxf", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("OrcaSlicer", StringComparison.OrdinalIgnoreCase)));
    True(candidates.Any(c => c.Name.Contains("PrusaSlicer", StringComparison.OrdinalIgnoreCase)));
}

static void CheckDesktopSettingsPersistence()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Test_{Guid.NewGuid():N}");
    try
    {
        var settings = new DesktopSettingsService(tempDir);
        Equal(AppThemeMode.Dark, settings.Theme);
        Equal(LengthUnit.Millimeter, settings.DefaultUnit);

        settings.SetTheme(AppThemeMode.Light);
        settings.SetDefaultUnit(LengthUnit.Centimeter);

        var reloaded = new DesktopSettingsService(tempDir);
        Equal(AppThemeMode.Light, reloaded.Theme);
        Equal(LengthUnit.Centimeter, reloaded.DefaultUnit);

        // Bozuk JSON yazıldığında varsayılanlara güvenli dönüş
        File.WriteAllText(Path.Combine(tempDir, "settings.json"), "{ bozuk json [[[");
        var resilient = new DesktopSettingsService(tempDir);
        Equal(AppThemeMode.Dark, resilient.Theme);
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static void CheckNavigationWorkflow()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Nav_{Guid.NewGuid():N}");
    try
    {
        var settings = new DesktopSettingsService(tempDir);
        var vm = new MainWindowViewModel(settings, new ModuleCatalogService());

        Equal(ModuleId.Dashboard, vm.CurrentPage.Id);
        Equal("Ana Panel", vm.CurrentModuleTitle);
        Equal(9, vm.NavigationItems.Count);
        True(vm.NavigationItems[0].IsSelected);

        foreach (var moduleId in Enum.GetValues<ModuleId>())
        {
            vm.NavigateTo(moduleId);
            Equal(moduleId, vm.CurrentPage.Id);
            Equal(1, vm.NavigationItems.Count(item => item.IsSelected));
            Equal(moduleId, vm.NavigationItems.First(item => item.IsSelected).Id);
        }

        // Dashboard kartından modüle geçiş komutu kontrolü
        vm.NavigateTo(ModuleId.Dashboard);
        var dashboard = (DashboardViewModel)vm.CurrentPage;
        Equal(8, dashboard.ModuleCards.Count);
        dashboard.ModuleCards.First(c => c.Id == ModuleId.CadExport).OpenCommand.Execute(null);
        Equal(ModuleId.CadExport, vm.CurrentPage.Id);
        True(vm.CurrentPage.IsPlanned);
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static void CheckPhotoMeasurementViewModel()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Photo_{Guid.NewGuid():N}");
    try
    {
        var settings = new DesktopSettingsService(tempDir);
        var catalog = new ModuleCatalogService();
        var photoVm = new PhotoMeasurementViewModel(catalog.GetModule(ModuleId.PhotoMeasurement), settings);

        True(photoVm.HasValidReference);
        Near(90d, photoVm.CalculatedResult!.Value.Value);
        Equal(LengthUnit.Millimeter, photoVm.CalculatedResult.Value.Unit);

        photoVm.SelectedUnit = LengthUnit.Centimeter;
        Near(9d, photoVm.CalculatedResult!.Value.Value);

        photoVm.ReferenceInput = "-5";
        False(photoVm.HasValidReference);
        Equal("—", photoVm.CalculatedResultText);

        photoVm.ResetToDefaults();
        True(photoVm.HasValidReference);
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static void CheckWpfWindowAndThemeStartup()
{
    Exception? threadException = null;
    var thread = new Thread(() =>
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Wpf_{Guid.NewGuid():N}");
            try
            {
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                ThemeManager.ApplyTheme(app.Resources, AppThemeMode.Dark);
                True(app.Resources.MergedDictionaries.Count >= 1);

                ThemeManager.ApplyTheme(app.Resources, AppThemeMode.Light);
                True(app.Resources.MergedDictionaries.Count >= 1);

                ThemeManager.ApplyTheme(app.Resources, AppThemeMode.System);
                True(app.Resources.MergedDictionaries.Count >= 1);

                var settings = new DesktopSettingsService(tempDir);
                var mainVm = new MainWindowViewModel(settings, new ModuleCatalogService());
                var window = new MainWindow(mainVm);

                Equal("Ölçü Atölyesi PC — v0.5.0.5", window.Title);

                var root = (FrameworkElement)window.Content;

                // Tüm 9 modül için sırayla DataTemplate yükleme, layout ve regresyon kontrolü
                foreach (var moduleId in Enum.GetValues<ModuleId>())
                {
                    mainVm.NavigateTo(moduleId);
                    Equal(moduleId, window.ViewModel.CurrentPage.Id);

                    // XAML DataTemplate, StaticResource ve Binding doğrulaması için zorunlu layout güncellemesi
                    root.Measure(new Size(1180d, 760d));
                    root.Arrange(new Rect(0d, 0d, 1180d, 760d));
                    root.UpdateLayout();
                }

                // Kamera ve Kalibrasyon sayfası özel visual tree ve kontrol doğrulaması
                mainVm.NavigateTo(ModuleId.CameraCalibration);
                root.Measure(new Size(1180d, 760d));
                root.Arrange(new Rect(0d, 0d, 1180d, 760d));
                root.UpdateLayout();

                var cameraVm = (CameraCalibrationViewModel)mainVm.CurrentPage;
                True(cameraVm.HasNoConnectedPhone, "Telefon bağlı değilken HasNoConnectedPhone doğru olmalıdır.");

                VerifyPhotoWorkspaceLayout(window, mainVm, tempDir);
                window.Close();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            threadException = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (threadException is not null)
    {
        throw new InvalidOperationException($"WPF pencere testi hatası: {threadException.Message}", threadException);
    }
}

static void CheckBrandedAssetsAndResponsiveLayout()
{
    Exception? threadException = null;
    var thread = new Thread(() =>
    {
        try
        {
            var pcDir = @"D:\proje\OlcuAtolyesi-PC";
            var androidDir = @"D:\proje\OlcuAtolyesi";

            // 1. Vektör ve görsel kaynakları üret ve diske yaz
            BrandedAssetGenerator.GenerateAllAssets(pcDir, androidDir);

            // 2. Android dosyalarının varlığını ve geçerliliğini doğrula
            var androidAppIcon = Path.Combine(androidDir, "OlcuAtolyesi", "Resources", "AppIcon", "appicon.svg");
            var androidAppIconFg = Path.Combine(androidDir, "OlcuAtolyesi", "Resources", "AppIcon", "appiconfg.svg");
            var androidSplash = Path.Combine(androidDir, "OlcuAtolyesi", "Resources", "Splash", "splash.svg");

            True(File.Exists(androidAppIcon), "Android appicon.svg bulunmalıdır.");
            True(File.Exists(androidAppIconFg), "Android appiconfg.svg bulunmalıdır.");
            True(File.Exists(androidSplash), "Android splash.svg bulunmalıdır.");

            var fgContent = File.ReadAllText(androidAppIconFg);
            True(fgContent.Contains("26.15"), "Android foreground SVG kumpas dijital göstergesini içermelidir.");
            True(fgContent.Contains("beamGrad"), "Android foreground SVG kumpas cetvel gradyanını içermelidir.");

            var splashContent = File.ReadAllText(androidSplash);
            True(splashContent.Contains("Ölçü Atölyesi"), "Android splash SVG marka başlığını içermelidir.");
            True(splashContent.Contains("Fotoğraftan Ölçüm"), "Android splash SVG alt başlığını içermelidir.");

            // 3. PC dosyalarının varlığını ve geçerliliğini doğrula
            var pcIco = Path.Combine(pcDir, "src", "OlcuAtolyesi.PC", "Resources", "app.ico");
            var pcPng = Path.Combine(pcDir, "src", "OlcuAtolyesi.PC", "Resources", "app_icon.png");
            var pcSplash = Path.Combine(pcDir, "src", "OlcuAtolyesi.PC", "Resources", "splash.png");
            var pcSvg = Path.Combine(pcDir, "src", "OlcuAtolyesi.PC", "Resources", "appicon.svg");

            True(File.Exists(pcIco), "PC app.ico dosyası oluşturulmalıdır.");
            True(File.Exists(pcPng), "PC app_icon.png dosyası oluşturulmalıdır.");
            True(File.Exists(pcSplash), "PC splash.png dosyası oluşturulmalıdır.");
            True(File.Exists(pcSvg), "PC appicon.svg dosyası oluşturulmalıdır.");

            // ICO başlık ve boyut kontrolü
            var icoBytes = File.ReadAllBytes(pcIco);
            True(icoBytes.Length > 2000, "ICO dosyası çoklu çözünürlükleri içermelidir.");
            Equal(0, BitConverter.ToUInt16(icoBytes, 0)); // Reserved
            Equal(1, BitConverter.ToUInt16(icoBytes, 2)); // Type 1 = ICO
            Equal(7, BitConverter.ToUInt16(icoBytes, 4)); // 7 kare: 16, 24, 32, 48, 64, 128, 256

            // Splash PNG boyut ve format kontrolü
            var splashBytes = File.ReadAllBytes(pcSplash);
            True(splashBytes.Length > 5000, "Splash PNG dosyası geçerli büyüklükte olmalıdır.");
            Equal((byte)0x89, splashBytes[0]); // PNG magic bytes
            Equal((byte)0x50, splashBytes[1]);
            Equal((byte)0x4E, splashBytes[2]);
            Equal((byte)0x47, splashBytes[3]);

            // 4. Responsive Düzen Doğrulaması (1366x768 ve 1920x1080 ekranlar)
            var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Resp_{Guid.NewGuid():N}");
            try
            {
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var settings = new DesktopSettingsService(tempDir);
                var mainVm = new MainWindowViewModel(settings, new ModuleCatalogService());
                var window = new MainWindow(mainVm);

                var resolutions = new[]
                {
                    new Size(920d, 600d),   // Minimum izin verilen pencere boyutu
                    new Size(1366d, 768d),  // HD dizüstü bilgisayar standardı
                    new Size(1920d, 1080d)  // Full HD masaüstü standardı
                };

                var root = (FrameworkElement)window.Content;

                foreach (var res in resolutions)
                {
                    foreach (var moduleId in Enum.GetValues<ModuleId>())
                    {
                        mainVm.NavigateTo(moduleId);
                        root.Measure(res);
                        root.Arrange(new Rect(0d, 0d, res.Width, res.Height));
                        root.UpdateLayout();

                        True(root.ActualWidth <= res.Width, $"Modül {moduleId} {res.Width} genişliğinde taşmamalıdır.");
                    }
                }

                window.Close();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            threadException = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (threadException is not null)
    {
        throw new InvalidOperationException($"Marka ve responsive layout testi hatası: {threadException.Message}", threadException);
    }
}

static void CheckDiagnosticConsoleAndRemoteLogIngestion()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Diag_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
        // 1. PIN ve Hassas Veri Maskeleme Kontrolü
        var rawText = "Telefon PIN: 582910 ile eşleşti. Gizli token=sec_9876543210!";
        var masked = DiagnosticDataMasker.Mask(rawText);
        False(masked.Contains("582910"), "PIN log çıktısında görünmemelidir.");
        True(masked.Contains("[MASKELENDİ]"), "PIN maskelenmiş olmalıdır.");
        False(masked.Contains("sec_9876543210"), "Token gizlenmiş olmalıdır.");
        True(masked.Contains("[GİZLENDİ]"), "Token maskelenmiş olmalıdır.");

        // 2. Hub ve Döngüsel Günlükleyici Başlatma
        var logger = new CircularDiagnosticLogger(tempDir, maxFileSize: 2048, maxArchivedFiles: 3);
        var hub = new DiagnosticLogHub(logger);

        // PC olayları kaydet
        hub.Log(DiagnosticLevel.Info, "CoreModule", "INIT_OK", "Çekirdek başarıyla yüklendi.");
        hub.Log(DiagnosticLevel.Warn, "CameraModule", "CAM_FPS_LOW", "Kamera FPS 15 altına düştü.", correlationId: "cid-101");
        hub.Log(DiagnosticLevel.Error, "NetworkModule", "NET_TIMEOUT", "Ağ zaman aşımı oluştu.", details: "Timeout stack trace mock");

        // 3. ViewModel Başlatma (senkron test modu)
        using var vm = new DiagnosticConsoleViewModel(hub, isSync: true);
        Equal(3, vm.TotalCount);
        Equal(3, vm.DisplayedCount);

        // 4. Seviye Filtresi Doğrulaması
        vm.SelectedLevelFilter = "WARN";
        Equal(3, vm.TotalCount);
        Equal(1, vm.DisplayedCount);
        var warnItem = vm.FilteredView.Cast<DiagnosticLogEntry>().Single();
        Equal("CAM_FPS_LOW", warnItem.EventCode);

        // Filtreyi sıfırla
        vm.SelectedLevelFilter = "Tümü";
        Equal(3, vm.DisplayedCount);

        // 5. Metin Arama Filtresi Doğrulaması
        vm.SearchText = "zaman aşımı";
        Equal(1, vm.DisplayedCount);
        var errItem = vm.FilteredView.Cast<DiagnosticLogEntry>().Single();
        Equal("NET_TIMEOUT", errItem.EventCode);
        vm.SearchText = string.Empty;
        Equal(3, vm.DisplayedCount);

        // 6. Seçim ve Detay Metni Doğrulaması
        vm.SelectedEntry = warnItem;
        True(vm.HasSelectedEntry);
        True(vm.SelectedEntryDetailText.Contains("CAM_FPS_LOW"));
        True(vm.SelectedEntryDetailText.Contains("cid-101"));

        // 7. Uzaktan (Android) Log Alma Simülasyonu
        const int testPort = 19389;
        const string testPin = "482910";
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
        listener.Start();

        var serverCts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            while (!serverCts.Token.IsCancellationRequested)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    var req = ctx.Request;
                    var res = ctx.Response;

                    var pin = req.QueryString["pin"];
                    if (pin != testPin)
                    {
                        res.StatusCode = 401;
                        res.ContentType = "application/json";
                        var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"Yetkisiz\"}");
                        res.ContentLength64 = errBytes.Length;
                        await res.OutputStream.WriteAsync(errBytes);
                        res.Close();
                        continue;
                    }

                    var androidEntries = new List<DiagnosticLogEntry>
                    {
                        new()
                        {
                            Timestamp = DateTimeOffset.Now.AddSeconds(-5),
                            Level = DiagnosticLevel.Info,
                            Source = DiagnosticSource.Android,
                            Module = "AndroidCamera",
                            EventCode = "CAM_HARDWARE_READY",
                            Message = "Camera2 donanım oturumu hazırlandı.",
                            CorrelationId = "cam-switch-001"
                        },
                        new()
                        {
                            Timestamp = DateTimeOffset.Now.AddSeconds(-2),
                            Level = DiagnosticLevel.Event,
                            Source = DiagnosticSource.Android,
                            Module = "CameraSwitch",
                            EventCode = "CAM_SWITCH_COMPLETED",
                            Message = "Sensör ön kameraya başarıyla geçirildi.",
                            CorrelationId = "cam-switch-001"
                        }
                    };

                    res.StatusCode = 200;
                    res.ContentType = "application/json";
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(androidEntries, jsonOptions);
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    res.ContentLength64 = jsonBytes.Length;
                    await res.OutputStream.WriteAsync(jsonBytes);
                    res.Close();
                }
                catch (Exception) when (serverCts.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Diğer isteği dinlemeye devam et
                }
            }
        });

        try
        {
            // 7a. Yetkisiz deneme (hatalı PIN)
            var unauthCount = vm.FetchAndroidLogsAsync($"http://127.0.0.1:{testPort}", "000000").GetAwaiter().GetResult();
            Equal(0, unauthCount);
            True(vm.StatusMessage.Contains("Yetkisiz") || vm.StatusMessage.Contains("PIN"));

            // 7b. Başarılı log çekme
            var successCount = vm.FetchAndroidLogsAsync($"http://127.0.0.1:{testPort}", testPin).GetAwaiter().GetResult();
            Equal(2, successCount);
            True(vm.TotalCount >= 5, "Android logları eklendikten sonra toplam kayıt artmalıdır.");

            // 8. Kaynak Filtresi Doğrulaması (Android)
            vm.SelectedSourceFilter = "Android";
            True(vm.DisplayedCount >= 2, "Android filtresinde en az 2 kayıt görünmelidir.");
            foreach (var item in vm.FilteredView.Cast<DiagnosticLogEntry>())
            {
                Equal(DiagnosticSource.Android, item.Source);
            }

            // Kaynak Filtresi Doğrulaması (PC)
            vm.SelectedSourceFilter = "PC";
            foreach (var item in vm.FilteredView.Cast<DiagnosticLogEntry>())
            {
                Equal(DiagnosticSource.PC, item.Source);
            }
            vm.SelectedSourceFilter = "Tümü";

            // 9. Rapor Dışa Aktarma
            var reportPath = Path.Combine(tempDir, "diagnostic_report.txt");
            hub.ExportReport(reportPath, vm.FilteredView.Cast<DiagnosticLogEntry>());
            True(File.Exists(reportPath), "Rapor dosyası oluşturulmalıdır.");
            var reportContent = File.ReadAllText(reportPath);
            True(reportContent.Contains("ÖLÇÜ ATÖLYESİ TANILAMA RAPORU"));
            True(reportContent.Contains("CAM_SWITCH_COMPLETED"));

            // 10. Temizleme ve Yeniden Yükleme
            vm.ClearView();
            Equal(0, vm.TotalCount);
            Equal(0, vm.DisplayedCount);

            vm.LoadRecentEntries();
            True(vm.TotalCount >= 5, "Yeniden yüklendiğinde hub'daki kayıtlar geri gelmelidir.");
        }
        finally
        {
            serverCts.Cancel();
            listener.Stop();
            listener.Close();
        }
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

static void CheckDiagnosticConsoleWindowStaStartup()
{
    Exception? threadException = null;
    var thread = new Thread(() =>
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_DiagWindow_{Guid.NewGuid():N}");
            try
            {
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                ThemeManager.ApplyTheme(app.Resources, AppThemeMode.Dark);

                var logger = new CircularDiagnosticLogger(tempDir);
                var hub = new DiagnosticLogHub(logger);
                hub.Log(DiagnosticLevel.Info, "StartupTest", "STA_WIN_INIT", "STA pencere testi başlatıldı.");

                var vm = new DiagnosticConsoleViewModel(hub);
                var window = new DiagnosticConsoleWindow(vm);

                True(window.Title.Contains("Merkezi Tanılama Konsolu"), "Pencere başlığı doğru olmalıdır.");
                True(window.Title.Contains("v0.5.0.5"), "Pencere başlığı v0.5.0.5 içermelidir.");

                var root = (FrameworkElement)window.Content;
                // 1366x768 ve 1920x1080 ekran ölçüm/düzen testi
                root.Measure(new Size(1366d, 768d));
                root.Arrange(new Rect(0d, 0d, 1366d, 768d));
                root.UpdateLayout();

                True(root.ActualWidth <= 1366d, "Konsol genişliği 1366 taşmamalıdır.");
                True(root.ActualHeight <= 768d, "Konsol yüksekliği 768 taşmamalıdır.");

                // MainWindow üzerinden OpenDiagnosticConsoleCommand açılış testi
                var settings = new DesktopSettingsService(tempDir);
                var mainVm = new MainWindowViewModel(settings, new ModuleCatalogService());
                mainVm.OpenDiagnosticConsoleCommand.Execute(null);

                // İkinci tıklama durumunda minimize edilmişse normalleştirilip aktive edilmeli
                mainVm.OpenDiagnosticConsoleCommand.Execute(null);

                mainVm.Dispose();
                window.Close();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            threadException = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (threadException is not null)
    {
        throw new InvalidOperationException($"Tanılama Konsolu STA pencere testi hatası: {threadException.Message}", threadException);
    }
}

static void CheckRemoteHighResCaptureAndSha256Download()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_RemoteCapture_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    const int testPort = 19499;
    const string testPin = "741852";
    const string validToken = "bearer-token-secret-xyz";

    var testJpegBytes = CreateTestJpegBytes(1600, 1200);
    var validSha256 = Convert.ToHexString(SHA256.HashData(testJpegBytes)).ToLowerInvariant();

    var corruptJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
    var corruptSha256 = Convert.ToHexString(SHA256.HashData(corruptJpegBytes)).ToLowerInvariant();

    using var listener = new System.Net.HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
    listener.Start();

    var serverCts = new CancellationTokenSource();
    var simulateBackground = false;

    var serverTask = Task.Run(async () =>
    {
        while (!serverCts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var req = context.Request;
                var res = context.Response;

                var path = req.Url?.AbsolutePath ?? string.Empty;

                // 1. Auth Endpoint: /api/v1/auth
                if (path.Equals("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    var body = await reader.ReadToEndAsync();
                    var pinFromHeader = req.Headers["X-Session-Pin"];

                    var pinFromBody = string.Empty;
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("pin", out var p))
                        {
                            pinFromBody = p.GetString();
                        }
                    }
                    catch { }

                    var pin = !string.IsNullOrWhiteSpace(pinFromBody) ? pinFromBody : pinFromHeader;
                    if (pin == testPin)
                    {
                        res.StatusCode = 200;
                        res.ContentType = "application/json";
                        var json = JsonSerializer.Serialize(new { success = true, token = validToken, expiresIn = 3600 });
                        await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(json));
                        res.Close();
                    }
                    else
                    {
                        res.StatusCode = 401;
                        res.ContentType = "application/json";
                        var json = JsonSerializer.Serialize(new { success = false, error = "Geçersiz PIN" });
                        await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(json));
                        res.Close();
                    }
                    continue;
                }

                // Bearer auth check for other endpoints
                var authHeader = req.Headers["Authorization"] ?? string.Empty;
                if (!authHeader.Equals($"Bearer {validToken}", StringComparison.OrdinalIgnoreCase))
                {
                    res.StatusCode = 401;
                    res.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(new { success = false, error = "Yetkisiz erişim: Geçersiz veya eksik Bearer token." });
                    await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(json));
                    res.Close();
                    continue;
                }

                // 2. Capture Endpoint: /api/v1/capture
                if (path.Equals("/api/v1/capture", StringComparison.OrdinalIgnoreCase) && req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    if (simulateBackground)
                    {
                        res.StatusCode = 403;
                        res.ContentType = "application/json";
                        var json = JsonSerializer.Serialize(new { success = false, error = "Uygulama arka planda veya durdurulmuş durumda; uzaktan çekim yapılamaz." });
                        await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(json));
                        res.Close();
                        continue;
                    }

                    res.StatusCode = 200;
                    res.ContentType = "application/json";
                    var captureJson = JsonSerializer.Serialize(new
                    {
                        success = true,
                        photoId = "photo_test_100",
                        fileName = "photo_test_100.jpg",
                        fileSizeBytes = (long)testJpegBytes.Length,
                        sha256 = validSha256,
                        width = 1600,
                        height = 1200,
                        capturedAt = DateTimeOffset.UtcNow
                    });
                    await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(captureJson));
                    res.Close();
                    continue;
                }

                // 3. List Photos Endpoint: /api/v1/photos
                if (path.Equals("/api/v1/photos", StringComparison.OrdinalIgnoreCase) && req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    res.StatusCode = 200;
                    res.ContentType = "application/json";
                    var listJson = JsonSerializer.Serialize(new object[]
                    {
                        new
                        {
                            photoId = "photo_test_100",
                            fileName = "photo_test_100.jpg",
                            fileSizeBytes = (long)testJpegBytes.Length,
                            sha256 = validSha256,
                            width = 1600,
                            height = 1200,
                            createdAt = DateTimeOffset.UtcNow,
                            isTransferredToPc = false
                        }
                    });
                    await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(listJson));
                    res.Close();
                    continue;
                }

                // 4. Download Photo Endpoint: /api/v1/photos/{id}
                if (path.StartsWith("/api/v1/photos/", StringComparison.OrdinalIgnoreCase) && req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    var id = path.Substring("/api/v1/photos/".Length);
                    if (id == "photo_test_100")
                    {
                        res.StatusCode = 200;
                        res.ContentType = "image/jpeg";
                        res.Headers.Add("X-Sha256", validSha256);
                        res.Headers.Add("X-Photo-Width", "1600");
                        res.Headers.Add("X-Photo-Height", "1200");
                        res.ContentLength64 = testJpegBytes.Length;
                        await res.OutputStream.WriteAsync(testJpegBytes);
                        res.Close();
                    }
                    else if (id == "photo_tampered")
                    {
                        res.StatusCode = 200;
                        res.ContentType = "image/jpeg";
                        res.Headers.Add("X-Sha256", corruptSha256);
                        res.ContentLength64 = corruptJpegBytes.Length;
                        await res.OutputStream.WriteAsync(corruptJpegBytes);
                        res.Close();
                    }
                    else
                    {
                        res.StatusCode = 404;
                        res.Close();
                    }
                    continue;
                }

                res.StatusCode = 404;
                res.Close();
            }
            catch
            {
                break;
            }
        }
    });

    try
    {
        using var client = new RemotePhotoClient();
        var serverUrl = $"http://127.0.0.1:{testPort}";

        // 1. Yanlış PIN ile kimlik doğrulama başarısız olmalıdır
        var wrongPinResult = client.AuthenticateAsync(serverUrl, "000000").GetAwaiter().GetResult();
        False(wrongPinResult, "Yanlış PIN ile kimlik doğrulama başarısız olmalıdır.");
        False(client.IsAuthenticated, "IsAuthenticated false kalmalıdır.");

        // 2. Doğru PIN ile kimlik doğrulama başarılı olmalıdır
        var authOk = client.AuthenticateAsync(serverUrl, testPin).GetAwaiter().GetResult();
        True(authOk, "Doğru PIN ile kimlik doğrulama başarılı olmalıdır.");
        True(client.IsAuthenticated, "IsAuthenticated true olmalıdır.");
        Equal(validToken, client.SessionToken);

        // 3. Arka plan koruması: Arka planda uzaktan çekim reddedilmelidir
        simulateBackground = true;
        var bgCaptureResult = client.TriggerCaptureAsync().GetAwaiter().GetResult();
        False(bgCaptureResult.Success, "Arka planda uzaktan çekim reddedilmelidir.");
        True(bgCaptureResult.ErrorMessage != null && bgCaptureResult.ErrorMessage.Contains("arka plan"), "Arka plan hata mesajı dönmelidir.");
        simulateBackground = false;

        // 4. Ön planda yüksek çözünürlüklü çekim başarılı olmalıdır
        var captureResult = client.TriggerCaptureAsync().GetAwaiter().GetResult();
        True(captureResult.Success, "Ön planda çekim başarılı olmalıdır.");
        Equal("photo_test_100", captureResult.PhotoId);
        Equal("photo_test_100.jpg", captureResult.FileName);
        Equal(validSha256, captureResult.Sha256);
        Equal(1600, captureResult.Width);
        Equal(1200, captureResult.Height);
        Equal((long)testJpegBytes.Length, captureResult.FileSizeBytes);

        // 5. Dosyayı indir ve SHA-256 bütünlüğünü doğrula
        var downloadTarget = Path.Combine(tempDir, "Downloads");
        var downloadedPath = client.DownloadPhotoAsync(
            captureResult.PhotoId!,
            captureResult.Sha256!,
            downloadTarget).GetAwaiter().GetResult();

        True(File.Exists(downloadedPath), "İndirilen dosya diskte bulunmalıdır.");
        var downloadedBytes = File.ReadAllBytes(downloadedPath);
        Equal(testJpegBytes.Length, downloadedBytes.Length);
        Equal(validSha256, Convert.ToHexString(SHA256.HashData(downloadedBytes)).ToLowerInvariant());

        // 6. Önbellek kontrolü (Cache-hit): Aynı dosya tekrar istendiğinde gereksiz kopya oluşturulmaz
        var cachePath = client.DownloadPhotoAsync(
            captureResult.PhotoId!,
            captureResult.Sha256!,
            downloadTarget).GetAwaiter().GetResult();
        Equal(downloadedPath, cachePath);

        // 7. Bozuk / Değiştirilmiş SHA-256 bütünlük kontrolü (Tamper / Mismatch rejection)
        var shaMismatchThrown = false;
        try
        {
            client.DownloadPhotoAsync(
                "photo_tampered",
                validSha256,
                downloadTarget).GetAwaiter().GetResult();
        }
        catch (InvalidDataException ex)
        {
            shaMismatchThrown = true;
            True(ex.Message.Contains("SHA-256"), "Hata mesajı SHA-256 doğrulamasıyla ilgili olmalıdır.");
        }
        True(shaMismatchThrown, "SHA-256 uyuşmazlığında InvalidDataException fırlatılmalıdır.");
        False(File.Exists(Path.Combine(downloadTarget, "photo_tampered.jpg")), "Bozuk dosya hedef konuma taşınmamalıdır.");

        // 8. Fotoğrafları Listeleme (ListPhotosAsync)
        var photos = client.ListPhotosAsync().GetAwaiter().GetResult();
        Equal(1, photos.Count);
        Equal("photo_test_100", photos[0].PhotoId);
        Equal(validSha256, photos[0].Sha256);

        // 9. ResetAuth testi
        client.ResetAuth();
        False(client.IsAuthenticated, "ResetAuth sonrası IsAuthenticated false olmalıdır.");
        True(client.SessionToken == null, "ResetAuth sonrası SessionToken null olmalıdır.");
    }
    finally
    {
        serverCts.Cancel();
        listener.Stop();
        listener.Close();
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }
}

static void CheckHighResPhotoMeasurementIntegrationAndCalibrationReset()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_HighResIntegration_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
        // 1. Test ortamı ve bağımlılıkların oluşturulması
        var settings = new DesktopSettingsService(tempDir);
        var projectService = new DesktopProjectService(tempDir, seedSamples: false);
        var catalog = new ModuleCatalogService();
        var fakeDialog = new FakePhotoDialog(null);
        var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe");
        var phoneService = new UsbPhoneConnectionService(fakeAdb);
        var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");
        var fakeWifi = new FakeCameraStreamClient();

        var photoDescriptor = catalog.GetModule(ModuleId.PhotoMeasurement);
        var photoVm = new PhotoMeasurementViewModel(photoDescriptor, settings, fakeDialog, projectService);

        // Başlangıçta demo ortamında geçerli bir referans olmalıdır:
        True(photoVm.HasValidReference, "Başlangıçta geçerli bir referans olmalıdır.");

        // 2. Yüksek Çözünürlüklü Test Fotoğrafı Oluştur (2400 × 1600)
        var highResBytes = CreateTestJpegBytes(2400, 1600);
        var highResSha = Convert.ToHexString(SHA256.HashData(highResBytes)).ToLowerInvariant();
        var highResSourcePath = Path.Combine(tempDir, "highres_source.jpg");
        File.WriteAllBytes(highResSourcePath, highResBytes);

        var fakeRemoteClient = new FakeRemotePhotoClient
        {
            MockCapturedFilePath = highResSourcePath,
            MockCaptureResult = new RemoteCaptureResult(
                true,
                "highres_meas_1",
                "highres_meas_1.jpg",
                highResBytes.Length,
                highResSha,
                2400,
                1600,
                null)
        };

        var cameraDescriptor = catalog.GetModule(ModuleId.CameraCalibration);
        ModuleId? navigatedModule = null;

        var cameraVm = new CameraCalibrationViewModel(
            cameraDescriptor,
            phoneService,
            fakeScrcpy,
            navigateAction: mod => navigatedModule = mod,
            streamClient: fakeWifi,
            openCapturedPhotoAction: path =>
            {
                var opened = photoVm.LoadCapturedPhoto(path);
                if (opened)
                {
                    navigatedModule = ModuleId.PhotoMeasurement;
                }
                return opened;
            },
            remotePhotoClient: fakeRemoteClient);

        // 3. Wi-Fi bağlantısı ve uzaktan çekim düğmesi kontrolleri
        cameraVm.IsWifiMode = true;
        cameraVm.WifiPairingPin = "123456";
        False(cameraVm.CanCaptureHighResPhoto, "Wi-Fi bağlı değilken uzaktan yüksek çözünürlüklü çekim yapılamamalıdır.");

        cameraVm.ConnectWifiCommand.Execute(null);
        True(cameraVm.IsWifiConnected, "Wi-Fi bağlantısı aktif olmalıdır.");
        True(cameraVm.CanCaptureHighResPhoto, "Wi-Fi bağlıyken uzaktan çekim aktif olmalıdır.");

        // 4. Yüksek çözünürlüklü fotoğraf çekimi ve aktarımı
        cameraVm.CaptureHighResPhotoAsync().GetAwaiter().GetResult();

        // İndirme ve aktarım sonrasında Fotoğraftan Ölçüm modülüne geçilmiş olmalıdır
        Equal(ModuleId.PhotoMeasurement, navigatedModule);
        True(cameraVm.CapturedPhotoPath != null, "CapturedPhotoPath atanmış olmalıdır.");
        True(File.Exists(cameraVm.CapturedPhotoPath), "İndirilen yüksek çözünürlüklü fotoğraf mevcut olmalıdır.");
        True(cameraVm.StatusMessage.Contains("2400x1600"), "Durum mesajında yüksek çözünürlük bilgisi yer almalıdır.");

        // 5. Fotoğraftan Ölçüm Modülü Durum Doğrulaması (Kritik Gereksinimler)
        True(photoVm.PhotoImage != null, "Görsel nesnesi yüklenmiş olmalıdır.");
        Equal(2400d, photoVm.Session.ImageSize.Width);
        Equal(1600d, photoVm.Session.ImageSize.Height);

        // (A) Çözünürlük ve Yüksek Çözünürlüklü etiketi
        True(photoVm.PhotoInfoText.Contains("2400 × 1600"), "PhotoInfoText 2400 × 1600 içermelidir.");
        True(photoVm.PhotoInfoText.Contains("Yüksek Çözünürlüklü Fotoğraf"), "PhotoInfoText yüksek çözünürlük rozetini içermelidir.");

        // (B) Önceki referans kesinlikle GEÇERSİZ kılınmalıdır (yeni kalibrasyon zorunluluğu)
        False(photoVm.HasValidReference, "Yeni yüksek çözünürlüklü fotoğrafta eski referans iptal edilmelidir.");
        True(photoVm.Session.ReferenceScale == null, "Session referans ölçeği geçersiz olmalıdır.");
        True(photoVm.StatusMessage.Contains("referans"), "Kullanıcıya referans belirlemesi gerektiği bildirilmelidir.");
        True(photoVm.ProjectSaveFeedback!.Contains("Yeni kalibrasyon gereklidir"), "Geri bildirim yeni kalibrasyon gerektiğini belirtmelidir.");

        // 6. Hata Senaryoları Dayanıklılığı
        // (A) Uzaktan çekim donanım hatası
        fakeRemoteClient.SimulateCaptureFailure = true;
        navigatedModule = null;
        cameraVm.CaptureHighResPhotoAsync().GetAwaiter().GetResult();

        True(cameraVm.HasError, "Çekim hatasında HasError true olmalıdır.");
        True(cameraVm.ErrorDetails != null && cameraVm.ErrorDetails.Contains("Kamera donanım hatası"), "Hata detayı bildirilmelidir.");
        True(navigatedModule == null, "Hata durumunda modül geçişi yapılmamalıdır.");
        fakeRemoteClient.SimulateCaptureFailure = false;

        // (B) İndirme / SHA-256 bütünlük hatası
        fakeRemoteClient.SimulateDownloadFailure = true;
        cameraVm.CaptureHighResPhotoAsync().GetAwaiter().GetResult();

        True(cameraVm.HasError, "Bütünlük hatasında HasError true olmalıdır.");
        True(cameraVm.ErrorDetails != null && cameraVm.ErrorDetails.Contains("SHA-256"), "Bütünlük hatası SHA-256 bilgisi içermelidir.");
        fakeRemoteClient.SimulateDownloadFailure = false;

        // (C) Bekleyen fotoğrafları listeleme (LoadPendingPhotosAsync)
        fakeRemoteClient.MockPhotos.Add(new RemotePhotoItem("p_pending_1", "p_pending_1.jpg", 4096, highResSha, 1920, 1080, DateTimeOffset.UtcNow, false));
        cameraVm.LoadPendingPhotosAsync().GetAwaiter().GetResult();
        True(cameraVm.HasPendingRemotePhotos, "Bekleyen fotoğraflar listelenmiş olmalıdır.");
        Equal(1, cameraVm.PendingRemotePhotos.Count);
        Equal("p_pending_1", cameraVm.PendingRemotePhotos[0].PhotoId);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }
}

static void CheckViewportTransformMath()
{
    var image = new ImageSize(1000d, 500d);
    var view = new ViewSize(500d, 500d);

    var fit = ImageViewportTransform.Fit(image, view);
    Near(0.5d, fit.Scale);
    Near(0d, fit.OffsetX);
    Near(125d, fit.OffsetY);
    Near(0.468d, ImageViewportTransform.Fit(image, view, 16d).Scale);
    Near(1d, ImageViewportTransform.Fit(image, new ViewSize(0d, 0d)).Scale);

    var imagePoint = new ImagePoint(100d, 50d);
    var viewPoint = fit.ImageToView(imagePoint);
    Near(50d, viewPoint.X);
    Near(150d, viewPoint.Y);
    var inverse = fit.ViewToImage(viewPoint);
    Near(imagePoint.X, inverse.X);
    Near(imagePoint.Y, inverse.Y);

    var panned = fit.PanBy(18d, -7d);
    var afterPan = panned.ImageToView(imagePoint);
    Near(viewPoint.X + 18d, afterPan.X);
    Near(viewPoint.Y - 7d, afterPan.Y);

    // Yakınlaştırmada imlecin altındaki fotoğraf noktası sabit kalmalı
    var anchor = new ViewPoint(210d, 240d);
    var before = panned.ViewToImage(anchor);
    var zoomed = panned.ZoomAt(anchor, 2d, 0.1d, 50d);
    var after = zoomed.ViewToImage(anchor);
    Near(before.X, after.X);
    Near(before.Y, after.Y);
    Near(1d, zoomed.Scale);

    // Ölçek sınırları ve geçersiz çarpan
    Near(5d, panned.ZoomAt(anchor, 1000d, 0.1d, 5d).Scale);
    Near(0.25d, panned.ZoomAt(anchor, 0.0001d, 0.25d, 5d).Scale);
    Equal(panned, panned.ZoomAt(anchor, double.NaN, 0.1d, 5d));
    Equal(panned, panned.ZoomAt(anchor, -2d, 0.1d, 5d));

    // Aşırı kaydırma sonrası fotoğraf görünümden tamamen çıkmamalı
    var extreme = zoomed.PanBy(-50000d, 50000d).ClampToViewport(image, view, 16d);
    var topLeft = extreme.ImageToView(new ImagePoint(0d, 0d));
    var bottomRight = extreme.ImageToView(new ImagePoint(image.Width, image.Height));
    True(bottomRight.X >= 484d - 0.001d, "Sağ kenar görünüm içinde kalmalı.");
    True(topLeft.Y <= 16d + 0.001d, "Üst kenar görünüm içinde kalmalı.");

    var extremeOpposite = zoomed.PanBy(50000d, -50000d).ClampToViewport(image, view, 16d);
    True(extremeOpposite.ImageToView(new ImagePoint(0d, 0d)).X <= 16d + 0.001d, "Sol kenar görünüm içinde kalmalı.");
    True(extremeOpposite.ImageToView(new ImagePoint(image.Width, image.Height)).Y >= 484d - 0.001d, "Alt kenar görünüm içinde kalmalı.");

    // Görünümden küçük fotoğraf, kaydırılsa da tamamen görünür kalmalı
    var small = new ImageViewportTransform(0.2d, 100d, 100d).PanBy(10000d, 10000d).ClampToViewport(image, view, 16d);
    True(small.ImageToView(new ImagePoint(image.Width, image.Height)).X <= 484d + 0.001d);
    True(small.ImageToView(new ImagePoint(image.Width, image.Height)).Y <= 484d + 0.001d);
}

static void CheckMeasurementSessionMath()
{
    var session = new MeasurementSession(new ImageSize(2048d, 1536d), LengthUnit.Millimeter);
    var changed = 0;
    session.Changed += (_, _) => changed++;

    True(session.ReferenceScale is null);
    True(session.Result is null);
    Near(409.6d, session.R1.X);
    Near(1075.2d, session.R1.Y);

    // 1 TL (26,15 mm) madeni paranın iki kenarı: 200 piksel
    session.MovePoint(MeasurePoint.R1, new ImagePoint(300d, 600d));
    session.MovePoint(MeasurePoint.R2, new ImagePoint(500d, 600d));
    session.SetKnownLength(new LengthValue(26.15d, LengthUnit.Millimeter));
    session.MovePoint(MeasurePoint.M1, new ImagePoint(400d, 300d));
    session.MovePoint(MeasurePoint.M2, new ImagePoint(1000d, 300d)); // 600 piksel = 3 × 26,15
    Near(78.45d, session.Result!.Value.Value);
    Equal(LengthUnit.Millimeter, session.Result.Value.Unit);
    True(changed > 0);

    // Fotoğraf sınırları dışına taşma ve geçersiz koordinat
    session.MovePoint(MeasurePoint.M2, new ImagePoint(5000d, -200d));
    Near(2048d, session.M2.X);
    Near(0d, session.M2.Y);
    var m1Before = session.M1;
    False(session.MovePoint(MeasurePoint.M1, new ImagePoint(double.NaN, 5d)));
    Equal(m1Before, session.M1);

    // Geçersiz referans uzunlukları reddedilir
    session.SetKnownLength(null);
    True(session.Result is null);
    session.SetKnownLength(new LengthValue(26.15d, LengthUnit.Millimeter));
    True(session.Result is not null);
    session.SetKnownLength(new LengthValue(0d, LengthUnit.Millimeter));
    True(session.KnownLength is null);
    True(session.Result is null);

    // Referans ve birim değiştiğinde yeniden hesaplama
    var s2 = new MeasurementSession(new ImageSize(1600d, 1200d), LengthUnit.Centimeter);
    s2.MovePoint(MeasurePoint.R1, new ImagePoint(100d, 100d));
    s2.MovePoint(MeasurePoint.R2, new ImagePoint(300d, 100d)); // 200 px
    s2.MovePoint(MeasurePoint.M1, new ImagePoint(100d, 400d));
    s2.MovePoint(MeasurePoint.M2, new ImagePoint(600d, 400d)); // 500 px
    s2.SetKnownLength(new LengthValue(10d, LengthUnit.Centimeter));
    Near(25d, s2.Result!.Value.Value);

    s2.MovePoint(MeasurePoint.R2, new ImagePoint(350d, 100d)); // 250 px
    Near(20d, s2.Result!.Value.Value);

    s2.Unit = LengthUnit.Millimeter;
    Near(100d, s2.KnownLength!.Value.Value);
    Near(200d, s2.Result!.Value.Value);
    Equal(LengthUnit.Millimeter, s2.Result.Value.Unit);

    s2.Unit = LengthUnit.Meter;
    Near(0.1d, s2.KnownLength!.Value.Value);
    Near(0.2d, s2.Result!.Value.Value);

    s2.Unit = LengthUnit.Inch;
    Near(200d / 25.4d, s2.Result!.Value.Value);

    // R1 ve R2 aynı noktaya getirilirse referans geçersiz olur
    s2.MovePoint(MeasurePoint.R2, s2.R1);
    True(s2.ReferenceScale is null);
    True(s2.Result is null);

    Throws<ArgumentException>(() => new MeasurementSession(new ImageSize(0d, 100d), LengthUnit.Millimeter));
}

static void CheckMeasurementSessionUndoAndArming()
{
    var session = new MeasurementSession(new ImageSize(1000d, 800d), LengthUnit.Millimeter);
    var initialR1 = session.R1;
    var initialM1 = session.M1;

    False(session.CanUndo);
    False(session.Undo());

    session.CaptureUndo();
    session.MovePoint(MeasurePoint.R1, new ImagePoint(50d, 60d));
    session.CaptureUndo();
    session.MovePoint(MeasurePoint.M1, new ImagePoint(120d, 140d));
    True(session.CanUndo);

    True(session.Undo());
    Equal(initialM1, session.M1);
    Near(50d, session.R1.X);
    True(session.Undo());
    Equal(initialR1, session.R1);
    False(session.Undo());

    // Değişiklik yapmayan yakalama geri alma adımı sayılmaz
    session.CaptureUndo();
    False(session.CanUndo);
    False(session.Undo());

    // Sıfırlama geri alınabilir
    session.MovePoint(MeasurePoint.R2, new ImagePoint(900d, 700d));
    session.Reset();
    Near(800d, session.R2.X);
    True(session.Undo());
    Near(900d, session.R2.X);

    // R1→R2 ve M1→M2 nokta seçim zinciri
    var armed = new MeasurementSession(new ImageSize(1000d, 800d), LengthUnit.Millimeter);
    False(armed.BeginPlace(new ImagePoint(10d, 10d)));
    armed.ArmReference();
    Equal<MeasurePoint?>(MeasurePoint.R1, armed.ArmedPoint);
    True(armed.BeginPlace(new ImagePoint(100d, 100d)));
    Near(100d, armed.R1.X);
    armed.AdvanceArm();
    Equal<MeasurePoint?>(MeasurePoint.R2, armed.ArmedPoint);
    True(armed.BeginPlace(new ImagePoint(300d, 100d)));
    armed.AdvanceArm();
    True(armed.ArmedPoint is null);

    armed.ArmMeasure();
    Equal<MeasurePoint?>(MeasurePoint.M1, armed.ArmedPoint);
    armed.CancelArm();
    True(armed.ArmedPoint is null);

    armed.ArmPoint(MeasurePoint.M2);
    Equal<MeasurePoint?>(MeasurePoint.M2, armed.ArmedPoint);
    armed.AdvanceArm();
    True(armed.ArmedPoint is null);

    // Yerleştirmeler geri alınabilir
    True(armed.Undo());
    Near(800d, armed.R2.X);
}

static void CheckMeasurementInvariantUnderViewChanges()
{
    var session = new MeasurementSession(new ImageSize(4000d, 3000d), LengthUnit.Millimeter);
    session.MovePoint(MeasurePoint.R1, new ImagePoint(500d, 2500d));
    session.MovePoint(MeasurePoint.R2, new ImagePoint(1500d, 2500d)); // 1000 px
    session.SetKnownLength(new LengthValue(50d, LengthUnit.Millimeter));
    session.MovePoint(MeasurePoint.M1, new ImagePoint(700d, 800d));
    session.MovePoint(MeasurePoint.M2, new ImagePoint(3100d, 1900d));
    var expected = session.Result!.Value.Value;
    var m1 = session.M1;
    var m2 = session.M2;

    var viewSize = new ViewSize(900d, 600d);
    var transform = ImageViewportTransform.Fit(session.ImageSize, viewSize, 16d);
    var minScale = transform.Scale * 0.5d;
    var maxScale = Math.Max(transform.Scale * 40d, 16d);
    var anchors = new[] { new ViewPoint(100d, 80d), new ViewPoint(450d, 300d), new ViewPoint(880d, 590d) };

    for (var i = 0; i < 30; i++)
    {
        var anchor = anchors[i % anchors.Length];
        transform = transform
            .ZoomAt(anchor, i % 2 == 0 ? 1.4d : 0.6d, minScale, maxScale)
            .PanBy((i * 37d) % 200d - 100d, (i * 53d) % 160d - 80d)
            .ClampToViewport(session.ImageSize, viewSize, 16d);

        // Noktaları ekran koordinatlarına taşıyıp geri dönüştürmek ölçümü değiştirmemeli
        var backM1 = transform.ViewToImage(transform.ImageToView(m1));
        var backM2 = transform.ViewToImage(transform.ImageToView(m2));
        Near(m1.X, backM1.X, 1e-6d);
        Near(m2.Y, backM2.Y, 1e-6d);
        var recomputed = MeasurementGeometry.MeasureLength(
            new ImageSegment(backM1, backM2),
            session.ReferenceScale!,
            LengthUnit.Millimeter);
        Near(expected, recomputed.Value, 1e-6d);
    }

    // Görünüm dönüşümü oturum verisini hiç değiştirmez
    Equal(m1, session.M1);
    Equal(m2, session.M2);
    Near(expected, session.Result!.Value.Value);
}

static void RunSta(Action action)
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
    {
        throw new InvalidOperationException(failure.Message, failure);
    }
}

static string WriteTestImage(string directory, string fileName, int width, int height, BitmapEncoder encoder, BitmapMetadata? metadata = null)
{
    var pixels = new byte[width * height * 4];
    for (var i = 0; i < pixels.Length; i += 4)
    {
        pixels[i] = (byte)(i % 251);
        pixels[i + 1] = (byte)((i / 4) % 200);
        pixels[i + 2] = 120;
        pixels[i + 3] = 255;
    }

    var source = BitmapSource.Create(width, height, 96d, 96d, PixelFormats.Bgra32, null, pixels, width * 4);
    encoder.Frames.Add(metadata is null
        ? BitmapFrame.Create(source)
        : BitmapFrame.Create(source, null, metadata, null));

    var path = Path.Combine(directory, fileName);
    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
    encoder.Save(stream);
    return path;
}

static void CheckPhotoLoader()
{
    var directory = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Loader_{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        RunSta(() =>
        {
            var png = PhotoLoader.Load(WriteTestImage(directory, "a.png", 40, 30, new PngBitmapEncoder()));
            True(png.Success, png.ErrorMessage);
            Equal(new ImageSize(40d, 30d), png.OriginalSize);
            Equal(40, png.Image!.PixelWidth);

            var bmp = PhotoLoader.Load(WriteTestImage(directory, "b.BMP", 20, 10, new BmpBitmapEncoder()));
            True(bmp.Success, bmp.ErrorMessage);
            Equal(new ImageSize(20d, 10d), bmp.OriginalSize);

            var jpg = PhotoLoader.Load(WriteTestImage(directory, "c.jpg", 40, 30, new JpegBitmapEncoder()));
            True(jpg.Success, jpg.ErrorMessage);
            Equal(new ImageSize(40d, 30d), jpg.OriginalSize);

            var jpeg = PhotoLoader.Load(WriteTestImage(directory, "d.jpeg", 16, 16, new JpegBitmapEncoder()));
            True(jpeg.Success, jpeg.ErrorMessage);

            // EXIF yönü 6 (90° saat yönü): ölçüm koordinat sistemi döndürülmüş görüntünün boyutunu kullanmalı
            var metadata = new BitmapMetadata("jpg");
            metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
            var rotated = PhotoLoader.Load(WriteTestImage(directory, "e.jpg", 40, 30, new JpegBitmapEncoder(), metadata));
            True(rotated.Success, rotated.ErrorMessage);
            Equal(new ImageSize(30d, 40d), rotated.OriginalSize);
            Equal(30, rotated.Image!.PixelWidth);
            Equal(40, rotated.Image.PixelHeight);

            // Çok geniş görsel: görüntüleme küçültülür, ölçüm boyutu özgün kalır
            var wide = PhotoLoader.Load(WriteTestImage(directory, "wide.png", 12000, 20, new PngBitmapEncoder()));
            True(wide.Success, wide.ErrorMessage);
            Equal(new ImageSize(12000d, 20d), wide.OriginalSize);
            Equal(PhotoLoader.MaxDisplayLongSide, wide.Image!.PixelWidth);

            // Hatalı girdiler istisna fırlatmadan reddedilmeli
            File.WriteAllBytes(Path.Combine(directory, "empty.png"), []);
            File.WriteAllBytes(Path.Combine(directory, "bozuk.png"), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
            File.WriteAllText(Path.Combine(directory, "metin.txt"), "merhaba");
            File.WriteAllBytes(Path.Combine(directory, "resim.gif"), [71, 73, 70]);
            foreach (var bad in new string?[]
                     {
                         null, "", "   ",
                         Path.Combine(directory, "yok.png"),
                         Path.Combine(directory, "empty.png"),
                         Path.Combine(directory, "bozuk.png"),
                         Path.Combine(directory, "metin.txt"),
                         Path.Combine(directory, "resim.gif")
                     })
            {
                var result = PhotoLoader.Load(bad);
                False(result.Success);
                True(!string.IsNullOrWhiteSpace(result.ErrorMessage), "Hata mesajı bulunmalı.");
                True(result.Image is null);
            }
        });
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void CheckPhotoViewModelWithRealFile()
{
    var directory = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_PhotoVm_{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        RunSta(() =>
        {
            var settings = new DesktopSettingsService(Path.Combine(directory, "ayar"));
            var catalog = new ModuleCatalogService();
            var photoPath = WriteTestImage(directory, "foto.png", 2000, 1000, new PngBitmapEncoder());
            var dialog = new FakePhotoDialog(photoPath);
            var vm = new PhotoMeasurementViewModel(catalog.GetModule(ModuleId.PhotoMeasurement), settings, dialog);

            False(vm.HasPhoto);
            Equal(MeasurementSession.DemoImageSize, vm.Session.ImageSize);

            vm.OpenPhotoCommand.Execute(null);
            True(vm.HasPhoto);
            False(vm.HasPhotoError);
            Equal(new ImageSize(2000d, 1000d), vm.Session.ImageSize);
            True(vm.PhotoInfoText.Contains("2000"), vm.PhotoInfoText);

            var fitBefore = vm.FitRequest;
            vm.FitViewCommand.Execute(null);
            Equal(fitBefore + 1, vm.FitRequest);

            // 500 piksel = 100 mm → 0,2 mm/piksel; 1000 piksel = 200 mm
            vm.ReferenceInput = "100";
            vm.Session.MovePoint(MeasurePoint.R1, new ImagePoint(100d, 500d));
            vm.Session.MovePoint(MeasurePoint.R2, new ImagePoint(600d, 500d));
            vm.Session.MovePoint(MeasurePoint.M1, new ImagePoint(100d, 100d));
            vm.Session.MovePoint(MeasurePoint.M2, new ImagePoint(1100d, 100d));
            True(vm.HasValidReference);
            Near(200d, vm.CalculatedResult!.Value.Value);

            // Referans değişince sonuç yeniden hesaplanır
            vm.ReferenceInput = "50";
            Near(100d, vm.CalculatedResult!.Value.Value);

            // Birim değişince değerler dönüştürülür ve sonuç tutarlı kalır
            vm.SelectedUnit = LengthUnit.Centimeter;
            Equal("5", vm.ReferenceInput);
            Near(10d, vm.CalculatedResult!.Value.Value);
            Equal(LengthUnit.Centimeter, vm.CalculatedResult.Value.Unit);

            // Geçersiz referans uzunlukları reddedilir
            foreach (var invalid in new[] { "0", "-3", "abc", "", "NaN" })
            {
                vm.ReferenceInput = invalid;
                False(vm.HasValidReference);
                Equal("—", vm.CalculatedResultText);
            }

            vm.ReferenceInput = "5";
            True(vm.HasValidReference);

            // Araç düğmeleri, geri al ve sıfırla
            vm.ReferenceToolCommand.Execute(null);
            Equal<MeasurePoint?>(MeasurePoint.R1, vm.Session.ArmedPoint);
            True(vm.ToolHint.Contains("R1"));
            vm.CancelToolCommand.Execute(null);
            True(vm.Session.ArmedPoint is null);
            vm.MeasureToolCommand.Execute(null);
            Equal<MeasurePoint?>(MeasurePoint.M1, vm.Session.ArmedPoint);
            vm.CancelToolCommand.Execute(null);

            var m2Before = vm.Session.M2;
            vm.Session.CaptureUndo();
            vm.Session.MovePoint(MeasurePoint.M2, new ImagePoint(1500d, 300d));
            vm.UndoCommand.Execute(null);
            Equal(m2Before, vm.Session.M2);
            vm.ResetCommand.Execute(null);
            Near(1600d, vm.Session.R2.X);

            // Hatalı dosya: mevcut fotoğraf ve ölçümler korunur
            var sessionBefore = vm.Session;
            False(vm.LoadPhotoFromPath(Path.Combine(directory, "yok.png")));
            True(vm.HasPhotoError);
            True(vm.HasPhoto);
            True(ReferenceEquals(sessionBefore, vm.Session));
            True(vm.LoadPhotoFromPath(photoPath));
            False(vm.HasPhotoError);
        });
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void VerifyPhotoWorkspaceLayout(MainWindow window, MainWindowViewModel mainVm, string tempDir)
{
    mainVm.NavigateTo(ModuleId.PhotoMeasurement);
    var photoVm = (PhotoMeasurementViewModel)mainVm.GetPage(ModuleId.PhotoMeasurement);

    var root = (FrameworkElement)window.Content;
    root.Measure(new Size(1180d, 760d));
    root.Arrange(new Rect(0d, 0d, 1180d, 760d));
    root.UpdateLayout();

    var canvas = FindVisualChild<MeasurementCanvas>(root);
    True(canvas is not null, "Ölçüm çalışma alanı şablondan oluşturulmalı.");
    True(canvas!.ActualWidth > 100d && canvas.ActualHeight > 100d, "Çalışma alanı yerleşmeli.");
    True(ReferenceEquals(canvas.Session, photoVm.Session), "Oturum çalışma alanına bağlanmalı.");
    True(canvas.Image is null, "Fotoğraf açılmadan görüntü olmamalı.");
    True(canvas.ViewTransform.Scale > 0d, "Görünüm sığdırılmalı.");

    RenderOffscreen(canvas);

    var photoPath = WriteTestImage(tempDir, "layout.png", 800, 600, new PngBitmapEncoder());
    True(photoVm.LoadPhotoFromPath(photoPath));
    root.UpdateLayout();
    True(canvas.Image is not null, "Fotoğraf çalışma alanına bağlanmalı.");
    True(ReferenceEquals(canvas.Session, photoVm.Session), "Yeni oturum çalışma alanına bağlanmalı.");
    Equal(new ImageSize(800d, 600d), canvas.Session!.ImageSize);
    RenderOffscreen(canvas);

    // Programatik sığdırma (fare/klavye simülasyonu yok)
    canvas.FitView();
    True(canvas.ViewTransform.Scale > 0d);
}

static void CheckPersistentProjectCrudAndIsolation()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Crud_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var photoA = WriteTestImage(tempDir, "parcaA.png", 1600, 1200, new PngBitmapEncoder());
            var photoB = WriteTestImage(tempDir, "parcaB.jpg", 1920, 1080, new JpegBitmapEncoder());

            var service = new DesktopProjectService(tempDir, seedSamples: false);
            Equal(0, service.GetAll().Count);

            var createdEmpty = service.Create("Boş Taslak", ProjectType.Other, "İlk açıklama");
            Equal(1, service.GetAll().Count);

            var projA = service.SavePhotoMeasurementProject(
                "Motor Braketi",
                "Alüminyum braket ölçümü",
                photoA,
                new ImageSize(1600d, 1200d),
                new ImageSegment(new ImagePoint(125.5d, 310.25d), new ImagePoint(625.5d, 310.25d)),
                new LengthValue(100d, LengthUnit.Millimeter),
                new ImageSegment(new ImagePoint(200.75d, 600.125d), new ImagePoint(950.75d, 600.125d)),
                new LengthValue(150d, LengthUnit.Millimeter),
                LengthUnit.Millimeter,
                "parcaA.png");

            var projB = service.SavePhotoMeasurementProject(
                "Rulman Kapağı",
                "İkinci proje",
                photoB,
                new ImageSize(1920d, 1080d),
                new ImageSegment(new ImagePoint(100d, 200d), new ImagePoint(500d, 200d)),
                new LengthValue(8d, LengthUnit.Centimeter),
                new ImageSegment(new ImagePoint(300d, 400d), new ImagePoint(1100d, 400d)),
                new LengthValue(16d, LengthUnit.Centimeter),
                LengthUnit.Centimeter,
                "parcaB.jpg");

            Equal(3, service.GetAll().Count);

            // Her projenin kendi ayrı klasörü ve photo.jpg + project.json dosyası olmalı
            var pathA = service.GetPhotoFilePath(projA.Id);
            var pathB = service.GetPhotoFilePath(projB.Id);
            True(pathA is not null && File.Exists(pathA), "Proje A fotoğrafı kalıcı klasörde olmalı.");
            True(pathB is not null && File.Exists(pathB), "Proje B fotoğrafı kalıcı klasörde olmalı.");
            False(string.Equals(Path.GetDirectoryName(pathA), Path.GetDirectoryName(pathB), StringComparison.OrdinalIgnoreCase));

            // Yeniden adlandırma
            var renamedA = service.Rename(projA.Id, "Motor Braketi Rev2", "Güncellenmiş açıklama");
            Equal("Motor Braketi Rev2", renamedA.Name);
            Equal("Güncellenmiş açıklama", renamedA.Description);

            // Boş Taslak projesini silince A ve B projeleri ve klasörleri korunmalı
            True(service.Delete(createdEmpty.Id));
            Equal(2, service.GetAll().Count);
            True(File.Exists(pathA!));
            True(File.Exists(pathB!));

            // Uygulama kapatılıp yeniden açıldığında (yeni servis örneği) veriler eksiksiz yüklenmeli
            var reloaded = new DesktopProjectService(tempDir, seedSamples: false);
            Equal(2, reloaded.GetAll().Count);
            var loadedA = reloaded.GetById(projA.Id);
            True(loadedA is not null);
            Equal("Motor Braketi Rev2", loadedA!.Name);
            Equal("parcaA.png", loadedA.OriginalPhotoFileName);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckLosslessProjectReloadAndZoomInvariance()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Lossless_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var photoPath = WriteTestImage(tempDir, "hassas.png", 2400, 1800, new PngBitmapEncoder());
            var service = new DesktopProjectService(tempDir, seedSamples: false);

            var r1 = new ImagePoint(142.375d, 884.625d);
            var r2 = new ImagePoint(942.375d, 884.625d); // 800 px
            var m1 = new ImagePoint(310.125d, 420.875d);
            var m2 = new ImagePoint(1510.125d, 420.875d); // 1200 px
            var refLen = new LengthValue(160d, LengthUnit.Millimeter); // 0.2 mm/px -> 1200 px = 240 mm = 24 cm

            var saved = service.SavePhotoMeasurementProject(
                "Hassas Plaka",
                "Kayıpsız koordinat testi",
                photoPath,
                new ImageSize(2400d, 1800d),
                new ImageSegment(r1, r2),
                refLen,
                new ImageSegment(m1, m2),
                null,
                LengthUnit.Centimeter,
                "hassas.png");

            // Kaynak fotoğraf silinse bile projedeki kopyası korunmalı
            File.Delete(photoPath);

            // Yeniden başlatma simülasyonu
            var settings = new DesktopSettingsService(tempDir);
            var catalog = new ModuleCatalogService();
            var restartedService = new DesktopProjectService(tempDir, seedSamples: false);
            var photoVm = new PhotoMeasurementViewModel(
                catalog.GetModule(ModuleId.PhotoMeasurement),
                settings,
                new FakePhotoDialog(null),
                restartedService);

            True(photoVm.LoadProject(saved.Id));
            True(photoVm.HasPhoto, "Kayıtlı fotoğraf geri yüklenmeli.");
            False(photoVm.HasPhotoError);
            Equal(new ImageSize(2400d, 1800d), photoVm.Session.ImageSize);
            Near(r1.X, photoVm.Session.R1.X);
            Near(r1.Y, photoVm.Session.R1.Y);
            Near(r2.X, photoVm.Session.R2.X);
            Near(r2.Y, photoVm.Session.R2.Y);
            Near(m1.X, photoVm.Session.M1.X);
            Near(m1.Y, photoVm.Session.M1.Y);
            Near(m2.X, photoVm.Session.M2.X);
            Near(m2.Y, photoVm.Session.M2.Y);
            Equal(LengthUnit.Centimeter, photoVm.SelectedUnit);
            Near(24d, photoVm.CalculatedResult!.Value.Value);

            // Farklı ekran boyutu, yakınlaştırma ve kaydırma dönüşümlerinde ölçüm sonucu değişmemeli
            var viewA = ImageViewportTransform.Fit(photoVm.Session.ImageSize, new ViewSize(800d, 500d));
            var viewB = viewA.ZoomAt(new ViewPoint(350d, 220d), 4.5d, 0.05d, 40d).PanBy(120d, -85d);
            var viewC = ImageViewportTransform.Fit(photoVm.Session.ImageSize, new ViewSize(1920d, 1080d)).ZoomAt(new ViewPoint(100d, 100d), 0.4d, 0.05d, 40d);
            True(viewB.Scale != viewC.Scale);
            Near(24d, photoVm.Session.Result!.Value.Value);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckCorruptedJsonBackupRecoveryAndMissingPhoto()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_Resilience_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var photoPath = WriteTestImage(tempDir, "ornek.png", 1000, 800, new PngBitmapEncoder());
            var service = new DesktopProjectService(tempDir, seedSamples: false);

            var p1 = service.SavePhotoMeasurementProject(
                "Kurtarılacak Proje",
                "İlk kayıt",
                photoPath,
                new ImageSize(1000d, 800d),
                new ImageSegment(new ImagePoint(100d, 100d), new ImagePoint(600d, 100d)),
                new LengthValue(50d, LengthUnit.Millimeter),
                new ImageSegment(new ImagePoint(100d, 300d), new ImagePoint(900d, 300d)),
                null,
                LengthUnit.Millimeter);

            // İkinci güncelleme .bak yedeği oluşturur
            service.Rename(p1.Id, "Kurtarılacak Proje v2", "Yedekli kayıt");

            var p1Dir = Path.Combine(tempDir, DesktopProjectService.ProjectsFolderName, p1.Id.ToString("N"));
            var primaryJson = Path.Combine(p1Dir, DesktopProjectService.ProjectFileName);
            var backupJson = Path.Combine(p1Dir, DesktopProjectService.BackupFileName);
            True(File.Exists(primaryJson) && File.Exists(backupJson));

            // 1. Ana project.json bozulduğunda .bak yedeğinden otomatik kurtarılmalı
            File.WriteAllText(primaryJson, "{ bozuk json içerik [[[", Encoding.UTF8);

            // 2. Tamamen bozuk başka bir proje klasörü eklendiğinde uygulama çökmemeli
            var corruptDir = Path.Combine(tempDir, DesktopProjectService.ProjectsFolderName, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(corruptDir);
            File.WriteAllText(Path.Combine(corruptDir, DesktopProjectService.ProjectFileName), "<<<bozuk>>>", Encoding.UTF8);

            var recoveredService = new DesktopProjectService(tempDir, seedSamples: false);
            Equal(1, recoveredService.GetAll().Count);
            Equal(1, recoveredService.RecoveredFromBackupCount);
            Equal(1, recoveredService.CorruptedEntryCount);

            // 3. Kayıtlı projenin fotoğraf dosyası diskten silindiğinde proje yine açılmalı ve uyarı göstermeli
            var savedPhotoPath = recoveredService.GetPhotoFilePath(p1.Id);
            True(savedPhotoPath is not null);
            File.Delete(savedPhotoPath!);

            var settings = new DesktopSettingsService(tempDir);
            var catalog = new ModuleCatalogService();
            var photoVm = new PhotoMeasurementViewModel(
                catalog.GetModule(ModuleId.PhotoMeasurement),
                settings,
                new FakePhotoDialog(null),
                recoveredService);

            True(photoVm.LoadProject(p1.Id));
            False(photoVm.HasPhoto);
            True(photoVm.HasPhotoError, "Eksik fotoğraf için anlaşılır uyarı gösterilmeli.");
            Near(80d, photoVm.CalculatedResult!.Value.Value);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckMobileV040ImportAndConflictHandling()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_MobileImport_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var mobilePackageDir = Path.Combine(tempDir, "mobile_pkg");
            var mobileProjectId = Guid.Parse("77396967-5211-4a26-b75d-1e0a204b0400");
            var mobileMeasurementId = Guid.Parse("88396967-5211-4a26-b75d-1e0a204b0401");
            Directory.CreateDirectory(mobilePackageDir);

            var mobilePhotoPath = WriteTestImage(mobilePackageDir, "photo.jpg", 1500, 1000, new JpegBitmapEncoder());
            var mobileJsonPath = Path.Combine(mobilePackageDir, "project.json");

            // Mobil v0.4.0 DemoProjectService.ProjectDto ile birebir aynı JSON yapısı
            var mobileJson = $$"""
            {
              "Id": "{{mobileProjectId}}",
              "Name": "Mobil Saha Ölçümü",
              "Type": 3,
              "Description": "Android v0.4.0 uygulamasından kaydedildi",
              "CreatedAt": "2026-11-08T12:00:00+03:00",
              "UpdatedAt": "2026-11-08T12:15:00+03:00",
              "IsSample": false,
              "PhotoRelativePath": "projects/{{mobileProjectId:N}}/photo.jpg",
              "PhotoWidth": 1500.0,
              "PhotoHeight": 1000.0,
              "ReferenceStartX": 150.0,
              "ReferenceStartY": 800.0,
              "ReferenceEndX": 650.0,
              "ReferenceEndY": 800.0,
              "ReferenceLengthValue": 250.0,
              "ReferenceLengthUnit": 0,
              "DisplayUnit": 0,
              "Measurements": [
                {
                  "Id": "{{mobileMeasurementId}}",
                  "Name": "M1–M2 mesafe ölçümü",
                  "Kind": 0,
                  "LengthValue": 450.0,
                  "LengthUnit": 0,
                  "AngleDegrees": null,
                  "StartX": 200.0,
                  "StartY": 400.0,
                  "EndX": 1100.0,
                  "EndY": 400.0
                }
              ]
            }
            """;
            File.WriteAllText(mobileJsonPath, mobileJson, Encoding.UTF8);

            var originalJsonBytes = File.ReadAllBytes(mobileJsonPath);
            var originalPhotoBytes = File.ReadAllBytes(mobilePhotoPath);

            var storageDir = Path.Combine(tempDir, "pc_storage");
            var service = new DesktopProjectService(storageDir, seedSamples: false);

            // 1. İlk içe aktarma
            var import1 = service.ImportProjectPackage(mobilePackageDir, ProjectConflictPolicy.ImportAsCopy);
            True(import1.Success, import1.Message);
            False(import1.WasConflictResolved);
            Equal(mobileProjectId, import1.Project!.Id);
            Equal("Mobil Saha Ölçümü", import1.Project.Name);
            Near(450d, import1.Project.MeasuredLength!.Value.Value);
            Near(200d, import1.Project.MeasurementSegment.Start.X);
            Near(1100d, import1.Project.MeasurementSegment.End.X);

            // Kaynak dosyalar kesinlikle değiştirilmemiş olmalı
            True( originalJsonBytes.SequenceEqual(File.ReadAllBytes(mobileJsonPath)), "Kaynak project.json değişmemeli.");
            True(originalPhotoBytes.SequenceEqual(File.ReadAllBytes(mobilePhotoPath)), "Kaynak photo.jpg değişmemeli.");

            // 2. Aynı paketin tekrar içe aktarılması: ImportAsCopy (yeni kimlik ve kopya adıyla ekler)
            var importCopy = service.ImportProjectPackage(mobilePackageDir, ProjectConflictPolicy.ImportAsCopy);
            True(importCopy.Success, importCopy.Message);
            True(importCopy.WasConflictResolved);
            True(importCopy.Project!.Id != mobileProjectId);
            Equal(2, service.GetAll().Count);

            // 3. Aynı paketin SkipIfExists ile içe aktarılması (atlar, sayı değişmez)
            var importSkip = service.ImportProjectPackage(mobilePackageDir, ProjectConflictPolicy.SkipIfExists);
            True(importSkip.Success);
            True(importSkip.WasConflictResolved);
            Equal(2, service.GetAll().Count);

            // 4. Aynı paketin OverwriteExisting ile içe aktarılması (mevcut kaydı günceller, sayı değişmez)
            service.Rename(mobileProjectId, "Geçici Ad");
            var importOverwrite = service.ImportProjectPackage(mobilePackageDir, ProjectConflictPolicy.OverwriteExisting);
            True(importOverwrite.Success);
            True(importOverwrite.WasConflictResolved);
            Equal(2, service.GetAll().Count);
            Equal("Mobil Saha Ölçümü", service.GetById(mobileProjectId)!.Name);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckPortablePackageExportImportAndSecurity()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_ExportSec_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var sourceStorage = Path.Combine(tempDir, "source_ws");
            var targetStorage = Path.Combine(tempDir, "target_ws");
            var photoPath = WriteTestImage(tempDir, "kapak.png", 1280, 960, new PngBitmapEncoder());

            var sourceService = new DesktopProjectService(sourceStorage, seedSamples: false);
            var saved = sourceService.SavePhotoMeasurementProject(
                "Taşınabilir Kapak",
                "Paket testi",
                photoPath,
                new ImageSize(1280d, 960d),
                new ImageSegment(new ImagePoint(100d, 700d), new ImagePoint(500d, 700d)), // 400 px
                new LengthValue(80d, LengthUnit.Millimeter),                                // 0.2 mm/px
                new ImageSegment(new ImagePoint(200d, 300d), new ImagePoint(850d, 300d)), // 650 px -> 130 mm
                null,
                LengthUnit.Millimeter);

            var zipPath = Path.Combine(tempDir, "exported_project.zip");
            var exportResult = sourceService.ExportProjectPackageZip(saved.Id, zipPath);
            True(exportResult.Success, exportResult.Message);
            True(File.Exists(zipPath));

            // Paket içindeki project.json mutlak dosya yolu içermemeli, göreli yol kullanmalı
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var jsonEntry = zip.GetEntry(DesktopProjectService.ProjectFileName);
                True(jsonEntry is not null);
                using var reader = new StreamReader(jsonEntry!.Open(), Encoding.UTF8);
                var exportedJson = reader.ReadToEnd();
                False(exportedJson.Contains(sourceStorage, StringComparison.OrdinalIgnoreCase));
                True(exportedJson.Contains("projects/", StringComparison.Ordinal));
            }

            // Başka bir çalışma alanına .zip paketinden kayıpsız içe aktarma
            var targetService = new DesktopProjectService(targetStorage, seedSamples: false);
            var importZip = targetService.ImportProjectPackage(zipPath);
            True(importZip.Success, importZip.Message);
            Near(130d, importZip.Project!.MeasuredLength!.Value.Value);
            Near(200d, importZip.Project.MeasurementSegment.Start.X);
            Near(850d, importZip.Project.MeasurementSegment.End.X);
            True(targetService.GetPhotoFilePath(importZip.Project.Id) is not null);

            // Güvenlik testi 1: Zip Slip (.. içeren arşiv girdisi) reddedilmeli
            var maliciousZip = Path.Combine(tempDir, "malicious.zip");
            using (var badArchive = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
            {
                var badEntry = badArchive.CreateEntry("../evil.txt");
                using var writer = new StreamWriter(badEntry.Open());
                writer.Write("saldiri");
            }

            var badZipResult = targetService.ImportProjectPackage(maliciousZip);
            False(badZipResult.Success);

            // Güvenlik testi 2: project.json içinde PhotoRelativePath = "../../secret.jpg" reddedilmeli
            var traversalDir = Path.Combine(tempDir, "traversal_pkg");
            Directory.CreateDirectory(traversalDir);
            File.WriteAllText(
                Path.Combine(traversalDir, "project.json"),
                $$"""
                {
                  "Id": "{{Guid.NewGuid()}}",
                  "Name": "Saldırı Projesi",
                  "Type": 3,
                  "PhotoRelativePath": "../../disari.jpg",
                  "PhotoWidth": 800,
                  "PhotoHeight": 600
                }
                """,
                Encoding.UTF8);
            var traversalResult = targetService.ImportProjectPackage(traversalDir);
            False(traversalResult.Success);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckProjectsAndPhotoMeasurementIntegration()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_VmFlow_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        RunSta(() =>
        {
            var photoPath = WriteTestImage(tempDir, "flans.png", 1000, 1000, new PngBitmapEncoder());
            var settings = new DesktopSettingsService(tempDir);
            var catalog = new ModuleCatalogService();
            var projectService = new DesktopProjectService(settings.WorkspaceDirectory, seedSamples: false);
            var mainVm = new MainWindowViewModel(settings, catalog, projectService, new FakePhotoDialog(photoPath));

            var photoVm = (PhotoMeasurementViewModel)mainVm.GetPage(ModuleId.PhotoMeasurement);
            var projectsVm = (ProjectsViewModel)mainVm.GetPage(ModuleId.Projects);

            // 1. Fotoğraftan Ölçüm ekranında fotoğraf aç, noktaları ayarla ve "Projeyi Kaydet"
            photoVm.OpenPhotoCommand.Execute(null);
            True(photoVm.HasPhoto);
            photoVm.ProjectNameInput = "Flanş Ölçümü 1";
            photoVm.ReferenceInput = "100";
            photoVm.Session.MovePoint(MeasurePoint.R1, new ImagePoint(100d, 800d));
            photoVm.Session.MovePoint(MeasurePoint.R2, new ImagePoint(600d, 800d)); // 500 px = 100 mm
            photoVm.Session.MovePoint(MeasurePoint.M1, new ImagePoint(100d, 300d));
            photoVm.Session.MovePoint(MeasurePoint.M2, new ImagePoint(850d, 300d)); // 750 px = 150 mm

            photoVm.SaveProjectCommand.Execute(null);
            True(photoVm.HasActiveProject);
            var firstId = photoVm.ActiveProjectId!.Value;
            Equal(1, projectsVm.ProjectCount);

            // 2. Ölçümü değiştirip "Farklı Kaydet"
            photoVm.Session.MovePoint(MeasurePoint.M2, new ImagePoint(600d, 300d)); // 500 px = 100 mm
            photoVm.SaveAsNewProjectCommand.Execute(null);
            True(photoVm.ActiveProjectId != firstId);
            Equal(2, projectsVm.ProjectCount);

            // 3. Projeler ekranından ilk projeyi yeniden aç -> 150 mm ve M2.X = 850 geri gelmeli
            var firstItem = projectsVm.Projects.First(p => p.Id == firstId);
            firstItem.OpenInMeasurementCommand.Execute(null);
            Equal(ModuleId.PhotoMeasurement, mainVm.CurrentPage.Id);
            Equal(firstId, photoVm.ActiveProjectId);
            Near(850d, photoVm.Session.M2.X);
            Near(150d, photoVm.CalculatedResult!.Value.Value);
        });
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckAdbLocatorAndSerialValidation()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesiPC_AdbLoc_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
        // 1. ADB bulunamadığında null dönmeli ve servis Türkçe uyarı üretmeli
        var missingLocator = new AdbLocator(customAdbPath: Path.Combine(tempDir, "olmayan_adb.exe"), useSystemPaths: false);
        True(missingLocator.FindAdbExecutablePath() is null);

        var missingRunner = new FakeAdbCommandRunner(adbExecutablePath: null);
        var missingService = new UsbPhoneConnectionService(missingRunner);
        False(missingService.IsAdbAvailable);
        var scanMissing = missingService.ScanDevices();
        False(scanMissing.Success);
        False(scanMissing.AdbAvailable);
        True(scanMissing.Message.Contains("ADB", StringComparison.OrdinalIgnoreCase));
        Equal(0, missingRunner.ExecutedCommands.Count);

        // 2. Özel SDK dizininde adb.exe bulunduğunda yolu döndürmeli
        var fakeExePath = Path.Combine(tempDir, OperatingSystem.IsWindows() ? "adb.exe" : "adb");
        File.WriteAllText(fakeExePath, "sahte_adb");
        var customLocator = new AdbLocator(customSearchDirectories: [tempDir], useSystemPaths: false);
        Equal(fakeExePath, customLocator.FindAdbExecutablePath());

        // 3. Seri numarası doğrulama: güvenli kimlikler kabul edilir, enjeksiyon/boşluk/bayrak reddedilir
        True(AdbInputValidator.IsValidSerial("R5CWC19VLNJ"));
        True(AdbInputValidator.IsValidSerial("emulator-5554"));
        True(AdbInputValidator.IsValidSerial("192.168.1.42:5555"));

        foreach (var badSerial in new string?[]
                 {
                     null, "", "   ",
                     "-s", "--help", "-a",
                     "R5CW; rm -rf /",
                     "R5CW && calc.exe",
                     "R5CW|whoami",
                     "cihaz bosluklu",
                     new string('A', 200)
                 })
        {
            False(AdbInputValidator.IsValidSerial(badSerial));
            var sel = missingService.SelectDevice(badSerial);
            False(sel.Success);
            var cmd = missingService.RunDeviceCommandAsync(badSerial, ["get-state"]).GetAwaiter().GetResult();
            False(cmd.IsSuccess);
        }
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

static void CheckSingleDeviceAndNoDeviceScenarios()
{
    var runner = new FakeAdbCommandRunner(@"C:\Sdk\platform-tools\adb.exe");

    // 1. Bağlı cihaz yok
    runner.DevicesOutput = "List of devices attached\r\n\r\n";
    var service = new UsbPhoneConnectionService(runner);
    var emptyScan = service.ScanDevices();
    True(emptyScan.Success);
    True(emptyScan.AdbAvailable);
    Equal(0, emptyScan.Devices.Count);
    False(service.HasConnectedDevice);

    // 2. Tek yetkili cihaz (device) bağlı
    runner.DevicesOutput =
        "List of devices attached\n" +
        "* daemon not running; starting now at tcp:5037\n" +
        "* daemon started successfully\n" +
        "R5CWC19VLNJ          device product:dm3qxxx model:SM_S918B device:dm3q transport_id:1\n";
    runner.SetDeviceProperty("R5CWC19VLNJ", "ro.build.version.release", "14");
    runner.SetDeviceProperty("R5CWC19VLNJ", "ro.product.manufacturer", "Samsung");
    runner.SetDeviceProperty("R5CWC19VLNJ", "ro.product.model", "SM-S918B");

    var singleScan = service.ScanDevices();
    True(singleScan.Success);
    Equal(1, singleScan.Devices.Count);

    var device = singleScan.Devices[0];
    Equal("R5CWC19VLNJ", device.SerialId);
    Equal(AdbDeviceState.Online, device.State);
    True(device.IsReady);
    Equal("14", device.AndroidVersion);
    Equal("Samsung", device.Manufacturer);
    Equal("Samsung SM-S918B", device.DisplayTitle);

    // Cihaza gönderilen tüm getprop komutlarında '-s R5CWC19VLNJ' açıkça belirtilmiş olmalı
    var deviceCommands = runner.ExecutedCommands.Where(c => c.Contains("getprop")).ToList();
    True(deviceCommands.Count >= 2);
    foreach (var args in deviceCommands)
    {
        Equal("-s", args[0]);
        Equal("R5CWC19VLNJ", args[1]);
    }

    // Cihazı seç ve bağlantıyı kontrol et
    var selectRes = service.SelectDevice("R5CWC19VLNJ");
    True(selectRes.Success);
    True(service.HasConnectedDevice);
    Equal("R5CWC19VLNJ", service.ConnectedDevice!.SerialId);

    var checkRes = service.CheckConnection();
    True(checkRes.Success);
    True(runner.ExecutedCommands.Last().SequenceEqual(["-s", "R5CWC19VLNJ", "get-state"]));
}

static void CheckMultiDeviceUnauthorizedOfflineAndDisconnect()
{
    var runner = new FakeAdbCommandRunner(@"C:\Sdk\platform-tools\adb.exe")
    {
        DevicesOutput =
            "List of devices attached\n" +
            "SERI_HAZIR_1         device product:p1 model:Pixel_8 device:shiba transport_id:1\n" +
            "SERI_HAZIR_2         device product:p2 model:Galaxy_A54 device:a54x transport_id:2\n" +
            "SERI_YETKISIZ        unauthorized usb:1-2 transport_id:3\n" +
            "SERI_CEVRIMDISI      offline usb:1-3 transport_id:4\n"
    };
    runner.SetDeviceProperty("SERI_HAZIR_1", "ro.build.version.release", "15");
    runner.SetDeviceProperty("SERI_HAZIR_2", "ro.build.version.release", "14");

    var service = new UsbPhoneConnectionService(runner);
    var scan = service.ScanDevices();
    True(scan.Success);
    Equal(4, scan.Devices.Count);

    var d1 = scan.Devices.Single(d => d.SerialId == "SERI_HAZIR_1");
    var d2 = scan.Devices.Single(d => d.SerialId == "SERI_HAZIR_2");
    var unauth = scan.Devices.Single(d => d.SerialId == "SERI_YETKISIZ");
    var offline = scan.Devices.Single(d => d.SerialId == "SERI_CEVRIMDISI");

    Equal(AdbDeviceState.Online, d1.State);
    Equal(AdbDeviceState.Online, d2.State);
    Equal(AdbDeviceState.Unauthorized, unauth.State);
    Equal(AdbDeviceState.Offline, offline.State);

    // KURAL: Yetkisiz veya çevrimdışı cihazlara tarama sırasında veya sonrasında hiçbir ADB komutu gönderilmemeli!
    False(runner.ExecutedCommands.Any(cmd => cmd.Contains("SERI_YETKISIZ")), "Yetkisiz cihaza ADB komutu gönderilmemeli.");
    False(runner.ExecutedCommands.Any(cmd => cmd.Contains("SERI_CEVRIMDISI")), "Çevrimdışı cihaza ADB komutu gönderilmemeli.");

    // Yetkisiz ve çevrimdışı cihaz seçilmeye çalışıldığında reddedilmeli ve açıklama dönmeli
    var selUnauth = service.SelectDevice("SERI_YETKISIZ");
    False(selUnauth.Success);
    False(service.HasConnectedDevice);
    True(selUnauth.Message.Contains("yetkisiz", StringComparison.OrdinalIgnoreCase));

    var selOffline = service.SelectDevice("SERI_CEVRIMDISI");
    False(selOffline.Success);
    False(service.HasConnectedDevice);
    True(selOffline.Message.Contains("çevrimdışı", StringComparison.OrdinalIgnoreCase));

    var blockedCmd1 = service.RunDeviceCommandAsync("SERI_YETKISIZ", ["shell", "id"]).GetAwaiter().GetResult();
    False(blockedCmd1.IsSuccess);
    var blockedCmd2 = service.RunDeviceCommandAsync("SERI_CEVRIMDISI", ["shell", "id"]).GetAwaiter().GetResult();
    False(blockedCmd2.IsSuccess);
    False(runner.ExecutedCommands.Any(cmd => cmd.Contains("SERI_YETKISIZ") || cmd.Contains("SERI_CEVRIMDISI")));

    // Hazır 1. cihazı seç, ardından 2. cihaza geç
    True(service.SelectDevice("SERI_HAZIR_1").Success);
    Equal("SERI_HAZIR_1", service.ConnectedDevice!.SerialId);

    True(service.SelectDevice("SERI_HAZIR_2").Success);
    Equal("SERI_HAZIR_2", service.ConnectedDevice!.SerialId);

    // Bağlantıyı Ayır: yalnızca uygulama oturumunu sonlandırmalı, 'kill-server' veya 'disconnect' komutu çalıştırmamalı
    var cmdCountBeforeDisconnect = runner.ExecutedCommands.Count;
    var disc = service.DisconnectSelectedDevice();
    True(disc.Success);
    False(service.HasConnectedDevice);
    Equal(cmdCountBeforeDisconnect, runner.ExecutedCommands.Count);
    False(runner.ExecutedCommands.Any(cmd => cmd.Contains("kill-server")));
    Equal(4, service.Devices.Count);
}

static void CheckAdbTimeoutCancellationAndMalformedOutput()
{
    // 1. Hatalı / bozuk ADB çıktısı satırları uygulamayı çökertmemeli
    var malformedOutput =
        "List of devices attached\n" +
        "tek_kelime_bozuk_satir\n" +
        "gecersiz;seri!no   device model:Test\n" +
        "BOZUK_SATIR   bilinmeyen_durum_tok\n" +
        "SAGLAM_CIHAZ_01   device model:Galaxy_S23 transport_id:9\n";

    var parsed = AdbOutputParser.ParseDevicesOutput(malformedOutput);
    Equal(1, parsed.Devices.Count);
    Equal("SAGLAM_CIHAZ_01", parsed.Devices[0].SerialId);
    Equal(3, parsed.MalformedLineCount);

    // 2. Komut zaman aşımı (Timeout)
    var timeoutRunner = new FakeAdbCommandRunner(@"C:\Sdk\platform-tools\adb.exe")
    {
        SimulateTimeout = true
    };
    var timeoutService = new UsbPhoneConnectionService(timeoutRunner, TimeSpan.FromMilliseconds(100));
    var timeoutScan = timeoutService.ScanDevices();
    False(timeoutScan.Success);
    True(timeoutScan.Message.Contains("zaman aşımı", StringComparison.OrdinalIgnoreCase));

    // 3. Komut iptali (CancellationToken)
    var cancelRunner = new FakeAdbCommandRunner(@"C:\Sdk\platform-tools\adb.exe");
    var cancelService = new UsbPhoneConnectionService(cancelRunner);
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    var cancelledScan = cancelService.ScanDevices(cts.Token);
    False(cancelledScan.Success);
    True(cancelledScan.Message.Contains("iptal", StringComparison.OrdinalIgnoreCase));
}

static void CheckTransportOptionsAndRemoteCameraContracts()
{
    var transports = UsbPhoneConnectionService.GetTransportOptions();
    Equal(3, transports.Count);

    var usb = transports.Single(t => t.Kind == PhoneTransportKind.Usb);
    var wifi = transports.Single(t => t.Kind == PhoneTransportKind.WiFi);
    var bt = transports.Single(t => t.Kind == PhoneTransportKind.Bluetooth);

    True(usb.IsActiveInCurrentVersion);
    False(wifi.IsActiveInCurrentVersion);
    False(bt.IsActiveInCurrentVersion);
    Equal("Planlandı", wifi.StatusBadge);
    Equal("Planlandı", bt.StatusBadge);

    var cameraControls = RemoteCameraContractCatalog.GetPlannedControls();
    Equal(9, cameraControls.Count);
    True(cameraControls.All(c => c.StatusBadge == "Planlandı"));
    True(cameraControls.Any(c => c.Title.Contains("Kamera Seçimi", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Canlı Görüntü", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Zoom", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Çözünürlük", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("FPS", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Odaklama", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Pozlama", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Beyaz Dengesi", StringComparison.OrdinalIgnoreCase)));
    True(cameraControls.Any(c => c.Title.Contains("Flaş", StringComparison.OrdinalIgnoreCase)));
}

static void CheckPhoneConnectionViewModelWorkflow()
{
    var runner = new FakeAdbCommandRunner(@"C:\Sdk\platform-tools\adb.exe")
    {
        DevicesOutput =
            "List of devices attached\n" +
            "USB_TEL_101   device model:Pixel_7_Pro device:cheetah transport_id:5\n" +
            "USB_TEL_102   unauthorized transport_id:6\n"
    };
    runner.SetDeviceProperty("USB_TEL_101", "ro.build.version.release", "14");
    runner.SetDeviceProperty("USB_TEL_101", "ro.product.manufacturer", "Google");

    var catalog = new ModuleCatalogService();
    var service = new UsbPhoneConnectionService(runner);
    var vm = new PhoneConnectionViewModel(catalog.GetModule(ModuleId.PhoneConnection), service);

    // Başlangıçta otomatik tarama yapılmaz (kullanıcı tetiklemesi beklenir)
    Equal(0, runner.ExecutedCommands.Count);
    True(vm.IsAdbAvailable);
    False(vm.HasConnectedDevice);

    // 1. Cihazları Tara
    vm.ScanDevicesCommand.Execute(null);
    Equal(2, vm.DeviceCount);
    Equal(1, vm.ReadyDeviceCount);
    True(vm.HasWarningOrError); // 1 yetkisiz cihaz uyarısı

    // 2. Cihaz Seç
    vm.SelectDeviceBySerial("USB_TEL_101");
    True(vm.HasConnectedDevice);
    Equal("USB_TEL_101", vm.ConnectedDeviceSerial);
    True(vm.ConnectedDeviceAndroidVersion.Contains("14"));

    // 3. Bağlantıyı Kontrol Et
    vm.CheckConnectionCommand.Execute(null);
    True(vm.HasConnectedDevice);

    // 4. Cihaz çevrimdışı olduğunda Bağlantıyı Yenile oturumu güvenli biçimde kapatmalı
    runner.DevicesOutput =
        "List of devices attached\n" +
        "USB_TEL_101   offline transport_id:5\n";
    vm.RefreshConnectionCommand.Execute(null);
    False(vm.HasConnectedDevice);

    // 5. Tekrar çevrimiçi olduğunda seç ve Bağlantıyı Ayır
    runner.DevicesOutput =
        "List of devices attached\n" +
        "USB_TEL_101   device model:Pixel_7_Pro transport_id:5\n";
    vm.ScanDevicesCommand.Execute(null);
    vm.SelectDeviceCommand.Execute(null);
    True(vm.HasConnectedDevice);

    vm.DisconnectCommand.Execute(null);
    False(vm.HasConnectedDevice);
    Equal("Bağlı değil", vm.ConnectedDeviceStateText);
}

static void RenderOffscreen(FrameworkElement element)
{
    var width = Math.Max(1, (int)element.ActualWidth);
    var height = Math.Max(1, (int)element.ActualHeight);
    var target = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
    target.Render(element);
    Equal(width, target.PixelWidth);
}

static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
{
    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T match)
        {
            return match;
        }

        if (FindVisualChild<T>(child) is { } nested)
        {
            return nested;
        }
    }

    return null;
}

static void Near(double expected, double actual, double tolerance = 0.000_001d)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"Beklenen {expected}, bulunan {actual}.");
    }
}

static void True(bool condition, string? message = null)
{
    if (!condition)
    {
        throw new InvalidOperationException(message ?? "Koşul doğru olmalıydı.");
    }
}

static void False(bool condition, string? message = null) => True(!condition, message ?? "Koşul yanlış olmalıydı.");

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Beklenen {expected}, bulunan {actual}.");
    }
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{typeof(TException).Name} bekleniyordu.");
}

static void CheckScrcpyOutputParser()
{
    // 1. Sürüm ayrıştırma
    var verResult = ScrcpyOutputParser.ParseVersion("scrcpy 5.0.1 <https://github.com/Genymobile/scrcpy>\nDependencies:\n - SDL 3.2.4");
    True(verResult.IsFound, "scrcpy 5.0.1 sürüm çıktısı bulunamadı.");
    True(verResult.VersionText == "5.0.1", $"scrcpy sürüm metni '5.0.1' olmalıydı, bulunan: {verResult.VersionText}");
    True(verResult.IsSupported, "5.0.1 sürümü desteklenir olmalıdır.");

    var invalidVer = ScrcpyOutputParser.ParseVersion("Bozuk çıktı");
    False(invalidVer.IsFound, "Bozuk çıktı geçerli sürüm olarak kabul edilmemeli.");

    // 2. Kamera listesi ayrıştırma (Türkçe/Avrupa virgüllü ondalık toleranslı)
    var rawCameras = """
        List of cameras:
            --camera-id=0    (back, 4080x3060, fps={15, 20, 24, 30}, zoom-range=[0,55, 8])
            --camera-id=1    (front, 2560x1440, fps={30, 60})
            Bozuk satır örneği
            --camera-id=2    (external, 1920x1080)
        """;

    var cameras = ScrcpyOutputParser.ParseCameraList(rawCameras);
    True(cameras.Count == 3, $"Kamera sayısı 3 olmalıydı, bulunan: {cameras.Count}");
    True(cameras[0].Id == "0", "İlk kamera id 0 olmalı.");
    True(cameras[0].Facing == ScrcpyCameraFacing.Back, "İlk kamera arka olmalı.");
    True(cameras[0].NativeWidth == 4080 && cameras[0].NativeHeight == 3060, "Çözünürlük 4080x3060 olmalı.");
    True(cameras[1].Id == "1" && cameras[1].Facing == ScrcpyCameraFacing.Front, "İkinci kamera ön olmalı.");
    True(cameras[2].Facing == ScrcpyCameraFacing.External, "Üçüncü kamera harici olmalı.");

    // 3. Kamera çözünürlükleri ayrıştırma (Standart + high-speed ayrımı ve ANSI kaçışları)
    var rawSizes = """
        List of camera sizes:
            --camera-size=4080x3060    (fps={15, 20, 24, 30})
            --camera-size=1920x1080    (fps={30, 60})
            --camera-size=1920x1080    (fps={120, 240}, high-speed)
            --camera-size=1280x720     (fps={30, 60, 120})
        """;

    var sizes = ScrcpyOutputParser.ParseCameraSizes(rawSizes);
    True(sizes.Count == 4, $"Çözünürlük sayısı 4 olmalıydı, bulunan: {sizes.Count}");
    True(sizes[0].Width == 4080 && sizes[0].Height == 3060, "İlk çözünürlük boyutu hatalı.");
    True(sizes[1].SizeArgument == "1920x1080" && !sizes[1].IsHighSpeed, "Standart 1080p high-speed olmamalı.");
    True(sizes[2].SizeArgument == "1920x1080" && sizes[2].IsHighSpeed, "İkinci 1080p high-speed olarak işaretlenmeli.");
    True(sizes[2].HasFps(240), "High-speed 240 FPS içermeli.");

    // 4. ANSI renk kaçış kodlu ve tırnaklı kamera listesi
    var ansiOutput = "\x1B[0m\x1B[32mList of cameras:\x1B[0m\n" +
                     "\x1B[33m    --camera-id=\"0\"    (back, 1920x1080, fps={30})\x1B[0m\n" +
                     "    --camera-id='1'    (front, 1280x720, fps={30})\n";
    var ansiCameras = ScrcpyOutputParser.ParseCameraList(ansiOutput);
    True(ansiCameras.Count == 2, $"ANSI çıktısından 2 kamera ayrıştırılmalıydı, bulunan: {ansiCameras.Count}");
    True(ansiCameras[0].Id == "0", "Tırnaklı id temizlenmeli (0).");
    True(ansiCameras[1].Id == "1", "Tek tırnaklı id temizlenmeli (1).");
}

static void CheckScrcpyProcessManagerLifecycleAndSafety()
{
    var fakeRunner = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");

    // 1. Güvenlik: Geçersiz seri numarası (command injection koruması) reddedilmelidir
    try
    {
        var invalidConfig = new ScrcpyCameraSessionConfig(
            SerialId: "serial; rm -rf /",
            CameraId: "0");
        fakeRunner.StartCameraSessionAsync(invalidConfig).GetAwaiter().GetResult();
        throw new InvalidOperationException("Geçersiz seri numarası engellenmedi!");
    }
    catch (ArgumentException)
    {
        // Beklenen koruma
    }

    // 2. Geçerli başlatma, durum geçişi ve PID kontrolü
    var validConfig = new ScrcpyCameraSessionConfig(
        SerialId: "RF8M123456X",
        CameraId: "0",
        CameraSize: "1920x1080",
        VideoBitRate: "20M",
        CameraFps: 30);

    False(fakeRunner.CurrentStatus.IsActive, "Başlangıçta oturum kapalı olmalı.");
    var launchResult = fakeRunner.StartCameraSessionAsync(validConfig).GetAwaiter().GetResult();
    True(launchResult.Success, "Kamera oturumu başlatılamadı.");
    True(fakeRunner.CurrentStatus.IsActive, "Oturum çalışıyor durumda olmalı.");
    True(fakeRunner.CurrentStatus.State == ScrcpySessionState.Running, "Durum Running olmalı.");
    True(fakeRunner.CurrentStatus.ProcessId == 9999, "Mock PID atanmadı.");

    // 3. Güvenli argüman listesi doğrulaması
    var lastArgs = fakeRunner.LastLaunchedArguments;
    True(lastArgs is not null, "Argümanlar kaydedilmemiş.");
    var safeArgs = lastArgs ?? [];
    True(safeArgs.Contains("-s") && safeArgs.Contains("RF8M123456X"), "Cihaz hedefleme -s <serial> eksik.");
    True(safeArgs.Contains("--video-source=camera"), "--video-source=camera argümanı eksik.");
    True(safeArgs.Contains("--camera-id=0"), "--camera-id=0 argümanı eksik.");
    True(safeArgs.Contains("--camera-size=1920x1080"), "--camera-size argümanı eksik.");
    True(safeArgs.Contains("--video-bit-rate=20M"), "--video-bit-rate argümanı eksik.");
    True(safeArgs.Contains("--no-audio"), "--no-audio argümanı eksik.");

    // 4. Temiz durdurma döngüsü
    var stopResult = fakeRunner.StopCameraSessionAsync().GetAwaiter().GetResult();
    True(stopResult, "Oturum durdurulamadı.");
    False(fakeRunner.CurrentStatus.IsActive, "Oturum kapalı olmalı.");
    True(fakeRunner.CurrentStatus.State == ScrcpySessionState.Stopped, "Durum Stopped olmalı.");
    True(fakeRunner.CurrentStatus.ProcessId is null, "Durdurulduktan sonra PID null olmalı.");

    // 5. scrcpy bulunamama durumu
    var missingScrcpyRunner = new FakeScrcpyProcessManager(null);
    var missingLaunch = missingScrcpyRunner.StartCameraSessionAsync(validConfig).GetAwaiter().GetResult();
    False(missingLaunch.Success, "scrcpy yokken oturum başarılı olmamalı.");
    True(missingLaunch.ErrorDetail?.Contains("scrcpy") == true, "Hata detayında scrcpy eksikliği belirtilmeli.");
}

static void CheckCameraCalibrationViewModelWorkflow()
{
    var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe")
    {
        DevicesOutput = "List of devices attached\nRF8M123456X\tdevice product:galaxy_s24 model:SM_S921B device:e1s transport_id:1\n"
    };
    fakeAdb.SetDeviceProperty("RF8M123456X", "ro.build.version.release", "14");
    fakeAdb.SetDeviceProperty("RF8M123456X", "ro.product.manufacturer", "Samsung");
    fakeAdb.SetDeviceProperty("RF8M123456X", "ro.product.model", "Galaxy S24");

    var phoneService = new UsbPhoneConnectionService(fakeAdb);
    var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");

    var catalog = new ModuleCatalogService();
    var descriptor = catalog.GetModule(ModuleId.CameraCalibration);

    var vm = new CameraCalibrationViewModel(descriptor, phoneService, fakeScrcpy);

    // 1. scrcpy durumu kontrolü
    True(vm.IsScrcpyInstalled, "scrcpy kurulu algılanmalı.");
    True(vm.ScrcpyExecutablePath.Contains("scrcpy.exe"), "scrcpy yürütme yolu içermeli.");

    // 2. Başlangıçta telefon seçili değilken
    False(vm.HasConnectedPhone, "Henüz telefon seçili olmamalı.");
    False(vm.CanStartCamera, "Telefon yokken kamera başlatılamaz.");

    // 3. Telefon taranıp seçildiğinde
    phoneService.ScanDevicesAsync().GetAwaiter().GetResult();
    phoneService.SelectDevice("RF8M123456X");

    True(vm.HasConnectedPhone, "Telefon artık bağlı ve hazır olmalı.");
    True(vm.CanStartCamera, "Telefon bağlandıktan sonra kamera başlatılabilir olmalı.");
    True(vm.ConnectedPhoneTitle.Contains("Galaxy S24") || vm.ConnectedPhoneTitle.Contains("S921B"), "Telefon başlığı güncellenmeli.");

    // 4. Kameraları tara
    vm.ScanCamerasAsync().GetAwaiter().GetResult();
    True(vm.HasDetectedCameras, "Kameralar başarıyla taranmalı.");
    True(vm.AvailableCameras.Count >= 2, "En az iki kamera bulunmalı.");
    True(vm.SelectedCamera is not null, "Varsayılan kamera seçilmeli.");
    True(vm.SelectedCamera!.Facing == ScrcpyCameraFacing.Back, "Öncelikli olarak arka kamera seçilmeli.");

    // 5. Çözünürlükleri tara
    vm.ScanResolutionsAsync().GetAwaiter().GetResult();
    True(vm.AvailableResolutions.Count >= 2, "Çözünürlükler listelenmeli.");

    // 6. Canlı kamera oturumunu başlat
    vm.StartCameraAsync().GetAwaiter().GetResult();
    True(vm.IsSessionRunning, "Oturum başlamış olmalı.");
    False(vm.CanStartCamera, "Zaten çalışırken tekrar başlatılamaz.");
    True(vm.CanStopCamera, "Çalışırken durdurulabilir olmalı.");
    True(vm.SessionStatusBadge.Contains("Canlı") || vm.SessionStatusBadge.Contains("CANLI"), "Durum rozeti canlı yayında olmalı.");

    // 7. Canlı kamera oturumunu durdur
    vm.StopCameraAsync().GetAwaiter().GetResult();
    False(vm.IsSessionRunning, "Oturum durdurulmuş olmalı.");
    True(vm.CanStartCamera, "Durdurulduktan sonra yeniden başlatılabilir.");
    False(vm.CanStopCamera, "Kapalıyken durdurulamaz.");
}

static void CheckCameraSensorsAndDynamicModes()
{
    var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe")
    {
        DevicesOutput = "List of devices attached\nSM_S928B_01\tdevice product:galaxy_s24_ultra model:SM_S928B device:e3q transport_id:2\n"
    };
    fakeAdb.SetDeviceProperty("SM_S928B_01", "ro.build.version.release", "14");
    fakeAdb.SetDeviceProperty("SM_S928B_01", "ro.product.manufacturer", "Samsung");
    fakeAdb.SetDeviceProperty("SM_S928B_01", "ro.product.model", "Galaxy S24 Ultra");

    var phoneService = new UsbPhoneConnectionService(fakeAdb);
    var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");

    // Sensörleri ayarla: 0 (Ana Arka), 1 (Ön), 2 (Ultra Geniş Arka)
    fakeScrcpy.MockCameras =
    [
        new ScrcpyCameraInfo("0", ScrcpyCameraFacing.Back, 4080, 3060, [15, 24, 30, 60], 0.6, 10.0, "main back"),
        new ScrcpyCameraInfo("1", ScrcpyCameraFacing.Front, 2560, 1440, [30, 60], 1.0, 1.0, "front selfie"),
        new ScrcpyCameraInfo("2", ScrcpyCameraFacing.Back, 1920, 1080, [30], 0.5, 3.0, "ultra wide")
    ];

    fakeScrcpy.MockSizesByCameraId["0"] =
    [
        new ScrcpyCameraSizeOption(4080, 3060, "4:3", [15, 24, 30]),
        new ScrcpyCameraSizeOption(1920, 1080, "16:9", [30, 60], IsHighSpeed: false),
        new ScrcpyCameraSizeOption(1920, 1080, "16:9", [120, 240], IsHighSpeed: true),
        new ScrcpyCameraSizeOption(1280, 720, "16:9", [30, 60])
    ];

    fakeScrcpy.MockSizesByCameraId["1"] =
    [
        new ScrcpyCameraSizeOption(2560, 1440, "16:9", [30, 60]),
        new ScrcpyCameraSizeOption(1280, 720, "16:9", [30])
    ];

    var catalog = new ModuleCatalogService();
    var descriptor = catalog.GetModule(ModuleId.CameraCalibration);
    var vm = new CameraCalibrationViewModel(descriptor, phoneService, fakeScrcpy);

    // 1. Telefon bağlandığında otomatik kamera taraması gerçekleşmeli
    phoneService.ScanDevicesAsync().GetAwaiter().GetResult();
    phoneService.SelectDevice("SM_S928B_01");

    True(vm.HasConnectedPhone, "Telefon bağlı olmalı.");
    True(vm.HasDetectedCameras, "Kameralar otomatik algılanmalı.");
    Equal(3, vm.AvailableCameras.Count);
    True(vm.SelectedCamera is not null, "Kamera seçili olmalı.");
    Equal("0", vm.SelectedCamera!.Id);
    Equal(ScrcpyCameraFacing.Back, vm.SelectedCamera.Facing);

    // 2. Seçili arka kamera için modlar yüklenmeli
    True(vm.HasDetectedCameraSizes, "Arka kamera için çözünürlük modları listelenmeli.");
    Equal(4, vm.AvailableCameraSizes.Count);
    True(vm.SelectedCameraSize is not null, "Varsayılan boyut seçilmeli.");
    Equal("1920x1080", vm.SelectedCameraSize!.SizeArgument);
    False(vm.SelectedCameraSize.IsHighSpeed, "Varsayılan olarak standart 1080p seçilmeli.");
    True(vm.AvailableFpsOptions.Contains(60), "1080p modu 60 FPS içermeli.");

    // 3. High-speed moda geçiş ve FPS filtreleme doğrulaması
    var hsMode = vm.AvailableCameraSizes.First(s => s.IsHighSpeed);
    vm.SelectedCameraSize = hsMode;
    True(vm.SelectedCameraSize.IsHighSpeed, "High-speed mod seçildi.");
    True(vm.SelectedCameraSize.DisplayText.Contains("[Yüksek Hız / Slow-Motion]"), "High-speed rozeti etikette görünmeli.");
    True(vm.AvailableFpsOptions.Contains(240), "High-speed modunda 240 FPS mevcut olmalı.");
    Equal(240, vm.SelectedFps);

    // 4. Ön kameraya geçildiğinde modların dinamik güncellenmesi
    var frontCam = vm.AvailableCameras.First(c => c.Facing == ScrcpyCameraFacing.Front);
    vm.SelectedCamera = frontCam;
    Equal("1", vm.SelectedCameraId);
    Equal(2, vm.AvailableCameraSizes.Count);
    Equal("2560x1440", vm.SelectedCameraSize!.SizeArgument);

    // 5. Başlatma komutuna doğru parametrelerin iletilmesi
    vm.StartCameraAsync().GetAwaiter().GetResult();
    True(vm.IsSessionRunning, "Oturum açık olmalı.");
    var launchArgs = fakeScrcpy.LastLaunchedArguments;
    True(launchArgs is not null, "Başlatma argümanları kaydedilmeli.");
    True(launchArgs!.Contains("--camera-id=1"), "Ön kamera ID'si (1) argümanlarda yer almalı.");
    True(launchArgs!.Contains("--camera-size=2560x1440"), "Ön kamera çözünürlüğü argümanlarda yer almalı.");

    vm.StopCameraAsync().GetAwaiter().GetResult();
    False(vm.IsSessionRunning, "Oturum durdurulmalı.");

    // 6. Telefon bağlantısı kesildiğinde kamera listesi temizlenmeli
    phoneService.DisconnectSelectedDevice();
    False(vm.HasConnectedPhone, "Telefon ayrıldı.");
    False(vm.HasDetectedCameras, "Kamera listesi sıfırlanmalı.");
    False(vm.HasDetectedCameraSizes, "Mod listesi sıfırlanmalı.");
    True(vm.SelectedCamera is null, "Seçili kamera boş olmalı.");

    // 7. Boş kamera listesi dönüldüğünde asla başarı sayılmamalı
    fakeScrcpy.MockCameras = [];
    phoneService.ScanDevicesAsync().GetAwaiter().GetResult();
    phoneService.SelectDevice("SM_S928B_01");
    vm.ScanCamerasAsync().GetAwaiter().GetResult();
    False(vm.HasDetectedCameras, "Boş kamera listesi algılanmamış olmalı.");
    True(vm.HasError, "Hata durumu tetiklenmeli.");
}

static void CheckWifiCameraStreamClient()
{
    // Minimal JPEG karesi (SOI + JFIF + EOI)
    var minimalJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9 };

    const int testPort = 19188;
    const string validPin = "749201";

    using var listener = new System.Net.HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
    listener.Start();

    // Arka planda gelen istekleri yanıtlayan hafif sahte MJPEG sunucusu
    var serverCts = new CancellationTokenSource();
    var serverTask = Task.Run(async () =>
    {
        while (!serverCts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var req = context.Request;
                var res = context.Response;

                var providedPin = req.QueryString["pin"] ?? req.Headers["X-Session-Pin"];
                if (providedPin != validPin)
                {
                    res.StatusCode = 401;
                    var err = Encoding.UTF8.GetBytes("{\"error\":\"Geçersiz PIN\"}");
                    await res.OutputStream.WriteAsync(err);
                    res.Close();
                    continue;
                }

                res.StatusCode = 200;
                res.ContentType = "multipart/x-mixed-replace; boundary=--frame";

                var headerBytes = Encoding.ASCII.GetBytes($"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {minimalJpeg.Length}\r\n\r\n");
                var footerBytes = Encoding.ASCII.GetBytes("\r\n");

                await res.OutputStream.WriteAsync(headerBytes);
                await res.OutputStream.WriteAsync(minimalJpeg);
                await res.OutputStream.WriteAsync(footerBytes);
                await res.OutputStream.FlushAsync();

                // İkinci kareyi de gönder
                await Task.Delay(30);
                await res.OutputStream.WriteAsync(headerBytes);
                await res.OutputStream.WriteAsync(minimalJpeg);
                await res.OutputStream.WriteAsync(footerBytes);
                await res.OutputStream.FlushAsync();

                res.Close();
            }
            catch
            {
                break;
            }
        }
    });

    try
    {
        using var client = new CameraStreamClient();

        // 1. Hatalı PIN ile bağlantı (401 Unauthorized)
        var invalidResult = client.ConnectAsync($"http://127.0.0.1:{testPort}", "000000").GetAwaiter().GetResult();
        False(invalidResult.Success, "Hatalı PIN ile bağlantı başarılı olmamalı.");
        True(invalidResult.IsUnauthorized, "IsUnauthorized true olmalı.");

        // 2. Doğru PIN ile bağlantı ve MJPEG karesi alma
        byte[]? receivedData = null;
        var frameReceivedEvent = new ManualResetEventSlim(false);
        client.FrameReceived += (_, bytes) =>
        {
            receivedData = bytes;
            frameReceivedEvent.Set();
        };

        var validResult = client.ConnectAsync($"http://127.0.0.1:{testPort}", validPin).GetAwaiter().GetResult();
        True(validResult.Success, "Doğru PIN ile bağlantı başarılı olmalı.");
        True(client.IsConnected, "İstemci bağlı görünmeli.");

        // Karenin gelmesini bekle
        var arrived = frameReceivedEvent.Wait(TimeSpan.FromSeconds(3));
        True(arrived, "En az bir JPEG karesi alınmalıydı.");
        True(receivedData != null && receivedData.Length > 0, "Alınan kare boş olamaz.");
        True(client.ReceivedFrameCount >= 1, "Alınan kare sayısı en az 1 olmalı.");

        // 3. Bağlantıyı kesme
        client.DisconnectAsync().GetAwaiter().GetResult();
        False(client.IsConnected, "Disconnect sonrası IsConnected false olmalı.");
    }
    finally
    {
        serverCts.Cancel();
        listener.Stop();
        listener.Close();
    }
}

static void CheckCapturedFrameTransferAndMeasurementWorkflow()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "OlcuAtolyesiTests_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);

    try
    {
        var settings = new DesktopSettingsService(tempDir);
        var projectService = new DesktopProjectService(tempDir, seedSamples: false);
        var catalog = new ModuleCatalogService();
        var fakeDialog = new FakePhotoDialog(null);
        var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe");
        var phoneService = new UsbPhoneConnectionService(fakeAdb);
        var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");

        // 1. Boş kare yakalama testi
        var fakeWifi = new FakeCameraStreamClient();
        var cameraDescriptor = catalog.GetModule(ModuleId.CameraCalibration);
        var standaloneCameraVm = new CameraCalibrationViewModel(cameraDescriptor, phoneService, fakeScrcpy, navigateAction: null, streamClient: fakeWifi);
        standaloneCameraVm.CaptureCurrentFrameCommand.Execute(null);
        Equal("Yakalanacak aktif bir kamera karesi yok.", standaloneCameraVm.StatusMessage);
        True(standaloneCameraVm.CapturedPhotoPath == null, "Boş karede CapturedPhotoPath null olmalıdır.");

        // 2. Bozuk JPEG baytları ve format bütünlüğü testi
        byte[] malformedBytes = [1, 2, 3, 4, 5];
        var corruptFile = Path.Combine(tempDir, "corrupt.jpg");
        File.WriteAllBytes(corruptFile, malformedBytes);

        var photoDescriptor = catalog.GetModule(ModuleId.PhotoMeasurement);
        var photoVm = new PhotoMeasurementViewModel(photoDescriptor, settings, fakeDialog, projectService);

        // Başlangıç durumu: Demo çalışma alanı
        True(photoVm.PhotoImage == null, "Başlangıçta PhotoImage null (demo modunda) olmalı.");
        Equal(MeasurementSession.DemoImageSize, photoVm.Session.ImageSize);
        False(photoVm.HasPhoto);

        // Bozuk dosya aktarımı denemesi
        var corruptLoadResult = photoVm.LoadCapturedPhoto(corruptFile);
        False(corruptLoadResult, "Bozuk dosya yüklenememeli.");
        True(photoVm.HasPhotoError, "Hata bildirimi aktif olmalı.");
        True(!string.IsNullOrWhiteSpace(photoVm.PhotoError), "Türkçe hata mesajı içermeli.");
        True(photoVm.PhotoImage == null, "Bozuk yükleme sonrası PhotoImage değişmemeli (null kalmalı).");
        Equal(MeasurementSession.DemoImageSize, photoVm.Session.ImageSize);

        // 3. Demo alanından gerçek kareye geçiş ve Referans sıfırlaması
        var frame1Bytes = CreateTestJpegBytes(600, 400);
        var frame1Path = Path.Combine(tempDir, "frame1.jpg");
        File.WriteAllBytes(frame1Path, frame1Bytes);

        var load1Result = photoVm.LoadCapturedPhoto(frame1Path);
        True(load1Result, "Geçerli 600x400 kamera karesi başarıyla yüklenmelidir.");
        False(photoVm.HasPhotoError, "Başarılı yüklemede PhotoError null olmalı.");
        True(photoVm.PhotoImage != null, "PhotoImage dolu olmalı.");
        Equal(600d, photoVm.Session.ImageSize.Width);
        Equal(400d, photoVm.Session.ImageSize.Height);
        True(photoVm.PhotoInfoText.Contains("600 × 400"), "PhotoInfoText güncel çözünürlüğü içermeli.");
        True(photoVm.PhotoInfoText.Contains("Kamera Karesi"), "PhotoInfoText kamera karesi olduğunu belirtmeli.");
        True(photoVm.ActiveProjectId == null, "Yeni kamera karesinde ActiveProjectId null olmalı.");
        True(photoVm.StatusMessage.Contains("referans"), "StatusMessage kullanıcıya referans belirlemesi gerektiğini bildirmeli.");
        True(photoVm.ProjectSaveFeedback!.Contains("aktarıldı"), "ProjectSaveFeedback başarı bildirmeli.");

        // R1 ve R2 koordinatlarının yeni fotoğrafa göre hesaplandığını doğrula (0.20 ve 0.80)
        Near(120d, photoVm.Session.R1.X);
        Near(280d, photoVm.Session.R1.Y);
        Near(480d, photoVm.Session.R2.X);
        Near(280d, photoVm.Session.R2.Y);

        // Kullanıcı noktaları taşısın (özel ölçüm yapsın)
        photoVm.Session.MovePoint(MeasurePoint.R1, new ImagePoint(50d, 50d));
        Near(50d, photoVm.Session.R1.X);

        // 4. Önceden açılmış başka fotoğrafın yeni kamera karesiyle değiştirilmesi
        var frame2Bytes = CreateTestJpegBytes(800, 500);
        var frame2Path = Path.Combine(tempDir, "frame2.jpg");
        File.WriteAllBytes(frame2Path, frame2Bytes);

        var load2Result = photoVm.LoadCapturedPhoto(frame2Path);
        True(load2Result, "İkinci kare başarıyla yüklenmeli.");
        Equal(800d, photoVm.Session.ImageSize.Width);
        Equal(500d, photoVm.Session.ImageSize.Height);
        // Önceki fotoğrafın özel R1 noktası (50, 50) sessizce taşınmamış, yeni kare boyutuna göre sıfırlanmış olmalı (800 * 0.20 = 160)
        Near(160d, photoVm.Session.R1.X);
        Near(350d, photoVm.Session.R1.Y);

        // 5. Başarısız aktarımda mevcut fotoğrafın korunması
        var badFile = Path.Combine(tempDir, "non_existent.jpg");
        var badResult = photoVm.LoadCapturedPhoto(badFile);
        False(badResult, "Olmayan dosya yüklenememeli.");
        Equal(800d, photoVm.Session.ImageSize.Width);
        Equal(500d, photoVm.Session.ImageSize.Height);
        True(photoVm.PhotoImage != null, "Mevcut görsel korunmalı.");

        // 6. Proje koruma: Mevcut kayıtlı proje açıkken kamera karesi yüklenmesi
        var sampleProject = projectService.Create("Masa Projesi");
        True(sampleProject != null);
        var loadedProject = photoVm.LoadProject(sampleProject!.Id);
        True(loadedProject, "Proje yüklenmeli.");
        Equal(sampleProject.Id, photoVm.ActiveProjectId);

        // Şimdi canlı kamera karesi yükle:
        var loadFromCamera = photoVm.LoadCapturedPhoto(frame1Path);
        True(loadFromCamera);
        True(photoVm.ActiveProjectId == null, "Canlı kamera karesi yüklendiğinde eski projenin üzerine yazılmaması için ActiveProjectId sıfırlanmalıdır.");
        // Disk üzerindeki projenin korunduğunu doğrula
        var projectOnDisk = projectService.GetById(sampleProject.Id);
        True(projectOnDisk != null, "Disk üzerindeki proje korunmuş olmalıdır.");
        Equal("Masa Projesi", projectOnDisk!.Name);

        // 7. End-to-End MainWindowViewModel Entegrasyonu ve Navigasyon Sırası
        var mainVm = new MainWindowViewModel(
            settings,
            catalog,
            projectService,
            fakeDialog,
            phoneService,
            fakeScrcpy);

        // Başlangıçta Dashboard
        Equal(ModuleId.Dashboard, mainVm.CurrentPage.Descriptor.Id);

        // Kameraya geç
        mainVm.NavigateTo(ModuleId.CameraCalibration);
        Equal(ModuleId.CameraCalibration, mainVm.CurrentPage.Descriptor.Id);

        var cameraVm = (CameraCalibrationViewModel)mainVm.CurrentPage;
        cameraVm.IsWifiMode = true;

        // Geçersiz / henüz kare yokken yakalama komutu çalıştır: navigasyon OLMAMALI
        cameraVm.CaptureCurrentFrameCommand.Execute(null);
        Equal(ModuleId.CameraCalibration, mainVm.CurrentPage.Descriptor.Id);

        // Başarılı dosya aktarımı testi
        var validCameraFrame = CreateTestJpegBytes(1024, 768);
        var endToEndCapturePath = Path.Combine(tempDir, "e2e_capture.jpg");
        File.WriteAllBytes(endToEndCapturePath, validCameraFrame);

        var e2eSuccess = mainVm.OpenCapturedPhotoInMeasurement(endToEndCapturePath);
        True(e2eSuccess, "MainWindowViewModel üzerinden kare aktarımı başarılı olmalıdır.");
        Equal(ModuleId.PhotoMeasurement, mainVm.CurrentPage.Descriptor.Id);

        var currentMeasureVm = (PhotoMeasurementViewModel)mainVm.CurrentPage;
        True(currentMeasureVm.PhotoImage != null, "Aktarılan görsel PhotoImage içinde olmalıdır.");
        Equal(1024d, currentMeasureVm.Session.ImageSize.Width);
        Equal(768d, currentMeasureVm.Session.ImageSize.Height);

        // Hatalı dosya aktarımında navigasyon yapılmadığını doğrula:
        mainVm.NavigateTo(ModuleId.CameraCalibration);
        Equal(ModuleId.CameraCalibration, mainVm.CurrentPage.Descriptor.Id);
        var failedE2e = mainVm.OpenCapturedPhotoInMeasurement("C:\\not_found_capture.jpg");
        False(failedE2e, "Olmayan dosya aktarımı başarısız dönmeli.");
        Equal(ModuleId.CameraCalibration, mainVm.CurrentPage.Descriptor.Id);

        // 8. CameraCalibrationViewModel'in CaptureCurrentFrame ile tam akışı
        var fakeDirectWifi = new FakeCameraStreamClient();
        var capturedCameraVm = new CameraCalibrationViewModel(
            cameraDescriptor,
            phoneService,
            fakeScrcpy,
            mainVm.NavigateTo,
            fakeDirectWifi,
            mainVm.OpenCapturedPhotoInMeasurement);

        capturedCameraVm.IsWifiMode = true;
        fakeDirectWifi.SimulateReceiveFrame(frame1Bytes);
        True(capturedCameraVm.HasLiveFrame, "Canlı kare alınmış olmalı.");

        mainVm.NavigateTo(ModuleId.CameraCalibration);
        Equal(ModuleId.CameraCalibration, mainVm.CurrentPage.Descriptor.Id);

        capturedCameraVm.CaptureCurrentFrameCommand.Execute(null);
        Equal(ModuleId.PhotoMeasurement, mainVm.CurrentPage.Descriptor.Id);
        True(!string.IsNullOrWhiteSpace(capturedCameraVm.CapturedPhotoPath), "CapturedPhotoPath kaydedilmiş olmalıdır.");
        True(capturedCameraVm.StatusMessage.Contains("aktarıldı"), "Başarılı aktarım mesajı gösterilmelidir.");
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }
}

static void CheckCameraModeSwitchingAndLivePreview()
{
    var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe");
    var phoneService = new UsbPhoneConnectionService(fakeAdb);
    var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");
    var fakeWifi = new FakeCameraStreamClient();

    var catalog = new ModuleCatalogService();
    var descriptor = catalog.GetModule(ModuleId.CameraCalibration);

    var vm = new CameraCalibrationViewModel(descriptor, phoneService, fakeScrcpy, navigateAction: null, streamClient: fakeWifi);

    // 1. Başlangıçta USB scrcpy modu seçilidir
    Equal(CameraConnectionMode.ScrcpyUsb, vm.ActiveMode);
    True(vm.IsUsbMode, "Başlangıç modu USB olmalı.");
    False(vm.IsWifiMode, "Başlangıçta Wi-Fi modu aktif olmamalı.");

    // 2. Wi-Fi moduna geçiş
    vm.IsWifiMode = true;
    Equal(CameraConnectionMode.WifiDirect, vm.ActiveMode);
    True(vm.IsWifiMode);
    False(vm.IsUsbMode);

    // 3. Wi-Fi bağlantısı kurma
    vm.WifiServerAddress = "http://192.168.1.55:8088";
    vm.WifiPairingPin = "123456";

    True(vm.CanConnectWifi, "Başlangıçta CanConnectWifi true olmalı.");
    False(vm.CanDisconnectWifi, "Başlangıçta CanDisconnectWifi false olmalı.");

    vm.ConnectWifiCommand.Execute(null);
    True(vm.IsWifiConnected, "Wi-Fi bağlantısı kurulmuş olmalı.");
    False(vm.CanConnectWifi, "Bağlantı kurulduktan sonra CanConnectWifi false olmalı.");
    True(vm.CanDisconnectWifi, "Bağlantı kurulduktan sonra CanDisconnectWifi true olmalı.");

    // 4. Canlı kare alma simülasyonu (gerçek çözümlenebilir JPEG karesi)
    var testJpeg = CreateTestJpegBytes(120, 90);
    fakeWifi.SimulateReceiveFrame(testJpeg);
    True(vm.HasLiveFrame, "Geçerli kare alındığında HasLiveFrame true olmalı.");
    Equal("120 × 90", vm.WifiResolutionText);
    True(vm.LiveFrameSource != null, "LiveFrameSource dolu olmalı.");

    // 5. Kare dondurma ve fotoğraftan ölçüme aktarma
    vm.CaptureCurrentFrameCommand.Execute(null);
    True(!string.IsNullOrWhiteSpace(vm.CapturedPhotoPath), "Fotoğraf kaydedilmiş olmalı.");
    True(File.Exists(vm.CapturedPhotoPath), "Kaydedilen fotoğraf dosyası mevcut olmalıdır.");

    // 6. Wi-Fi bağlantısını kesme
    vm.DisconnectWifiCommand.Execute(null);
    False(vm.IsWifiConnected, "Bağlantı kesilmiş olmalı.");
    True(vm.CanConnectWifi, "Bağlantı kesildikten sonra CanConnectWifi yeniden true olmalı.");
    False(vm.CanDisconnectWifi, "Bağlantı kesildikten sonra CanDisconnectWifi false olmalı.");
    False(vm.HasLiveFrame, "Bağlantı kesildikten sonra HasLiveFrame false olmalı.");
    Equal("-", vm.WifiResolutionText);
    Equal(0.0, vm.WifiFps);
}

static void CheckWifiMjpegRobustStreamingAndErrorScenarios()
{
    // 1. Gerçek geçerli JPEG karesi oluştur
    var validJpeg1 = CreateTestJpegBytes(160, 120);
    var validJpeg2 = CreateTestJpegBytes(320, 240);
    True(validJpeg1.Length > 100, "Test JPEG 1 geçerli boyutta olmalı.");
    True(validJpeg2.Length > 100, "Test JPEG 2 geçerli boyutta olmalı.");

    const int testPort = 19189;
    const string testPin = "889900";

    using var listener = new System.Net.HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
    listener.Start();

    var serverCts = new CancellationTokenSource();
    var serverTask = Task.Run(async () =>
    {
        while (!serverCts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var req = context.Request;
                var res = context.Response;

                var pin = req.QueryString["pin"] ?? req.Headers["X-Session-Pin"];
                if (pin != testPin)
                {
                    res.StatusCode = 401;
                    res.ContentType = "application/json";
                    var err = Encoding.UTF8.GetBytes("{\"error\":\"Geçersiz PIN\"}");
                    await res.OutputStream.WriteAsync(err);
                    res.Close();
                    continue;
                }

                res.StatusCode = 200;
                res.ContentType = "multipart/x-mixed-replace; boundary=--frame";

                // Başlangıç boundary ve Content-Length ile bölünmüş paket gönderimi simülasyonu
                var header1 = Encoding.ASCII.GetBytes($"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {validJpeg1.Length}\r\n\r\n");
                var footer = Encoding.ASCII.GetBytes("\r\n");

                // Header ve body'yi küçük 40 baytlık parçalarla gönder (ağ bölünmesi testi)
                var combined = new List<byte>();
                combined.AddRange(header1);
                combined.AddRange(validJpeg1);
                combined.AddRange(footer);

                var allBytes = combined.ToArray();
                for (var offset = 0; offset < allBytes.Length; offset += 40)
                {
                    var count = Math.Min(40, allBytes.Length - offset);
                    await res.OutputStream.WriteAsync(allBytes.AsMemory(offset, count));
                    await res.OutputStream.FlushAsync();
                    await Task.Delay(5);
                }

                // İkinci kareyi Content-Length olmadan (boundary ile) gönder
                var header2 = Encoding.ASCII.GetBytes($"--frame\r\nContent-Type: image/jpeg\r\n\r\n");
                await res.OutputStream.WriteAsync(header2);
                await res.OutputStream.WriteAsync(validJpeg2);
                await res.OutputStream.WriteAsync(Encoding.ASCII.GetBytes("\r\n--frame\r\n"));
                await res.OutputStream.FlushAsync();

                // Bekle ve sunucuyu kapatarak kopma simülasyonu yap
                await Task.Delay(50);
                res.Close();
            }
            catch
            {
                break;
            }
        }
    });

    try
    {
        using var client = new CameraStreamClient();
        var receivedFrames = new List<byte[]>();
        var frameEvent = new AutoResetEvent(false);
        var disconnectEvent = new AutoResetEvent(false);

        client.FrameReceived += (_, frame) =>
        {
            lock (receivedFrames)
            {
                receivedFrames.Add(frame);
            }
            frameEvent.Set();
        };

        client.ConnectionStateChanged += (_, isConnected) =>
        {
            if (!isConnected)
            {
                disconnectEvent.Set();
            }
        };

        // 1. Bağlan
        var connectResult = client.ConnectAsync($"http://127.0.0.1:{testPort}", testPin).GetAwaiter().GetResult();
        True(connectResult.Success, "Sunucuya başarıyla bağlanmalı.");
        True(client.IsConnected, "Bağlantı aktif olmalı.");

        // 2. İlk parçalı kareyi bekle
        var gotFirst = frameEvent.WaitOne(TimeSpan.FromSeconds(3));
        True(gotFirst, "İlk parçalı kare başarıyla birleştirilmeli.");

        // 3. İkinci kareyi bekle
        var gotSecond = frameEvent.WaitOne(TimeSpan.FromSeconds(3));
        True(gotSecond, "İkinci kare başarıyla alınmalı.");

        lock (receivedFrames)
        {
            True(receivedFrames.Count >= 2, $"En az 2 kare alınmalıydı, alınan: {receivedFrames.Count}");
            Equal(validJpeg1.Length, receivedFrames[0].Length);
            Equal(validJpeg2.Length, receivedFrames[1].Length);
        }

        // 4. Sunucu kapanınca kopma olayının tetiklenmesini bekle
        var disconnected = disconnectEvent.WaitOne(TimeSpan.FromSeconds(3));
        True(disconnected, "Sunucu bağlantıyı kestiğinde disconnect olayı tetiklenmeli.");
        False(client.IsConnected, "Kopma sonrası IsConnected false olmalı.");

        // 5. ViewModel ile bozuk veri ve durum izolasyonu testi
        var fakeAdb = new FakeAdbCommandRunner("C:\\mock\\adb.exe");
        var phoneService = new UsbPhoneConnectionService(fakeAdb);
        var fakeScrcpy = new FakeScrcpyProcessManager("C:\\mock\\scrcpy.exe");
        var fakeWifi = new FakeCameraStreamClient();
        var catalog = new ModuleCatalogService();
        var descriptor = catalog.GetModule(ModuleId.CameraCalibration);

        var vm = new CameraCalibrationViewModel(descriptor, phoneService, fakeScrcpy, navigateAction: null, streamClient: fakeWifi);
        vm.IsWifiMode = true;
        vm.ConnectWifiCommand.Execute(null);
        True(vm.IsWifiConnected);

        // Bozuk kare gönder
        var corruptBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        fakeWifi.SimulateReceiveFrame(corruptBytes);

        // Bozuk karede FPS artmamalı ve hata bildirilmelidir
        Equal(0.0, vm.WifiFps);
        False(vm.HasLiveFrame, "Bozuk karede HasLiveFrame true olmamalı.");
        True(vm.HasError, "Bozuk karede HasError true olmalı.");
        True(!string.IsNullOrWhiteSpace(vm.ErrorDetails), "Bozuk kare hata detayı içermeli.");

        // Şimdi sağlam kare gönder
        fakeWifi.SimulateReceiveFrame(validJpeg2);
        True(vm.HasLiveFrame, "Sağlam karede HasLiveFrame true olmalı.");
        False(vm.HasError, "Sağlam karede HasError temizlenmeli.");
        Equal("320 × 240", vm.WifiResolutionText);

        // Bağlantıyı kes
        vm.DisconnectWifiCommand.Execute(null);
        False(vm.IsWifiConnected);
        True(vm.CanConnectWifi, "CanConnectWifi true olmalı.");
        False(vm.CanDisconnectWifi, "CanDisconnectWifi false olmalı.");
        Equal("-", vm.WifiResolutionText);
        Equal(0.0, vm.WifiFps);
        False(vm.HasLiveFrame);

        // Yeniden bağlan
        vm.ConnectWifiCommand.Execute(null);
        True(vm.IsWifiConnected, "Yeniden bağlantı başarılı olmalı.");
        False(vm.CanConnectWifi, "Bağlıyken CanConnectWifi false olmalı.");
        True(vm.CanDisconnectWifi, "Bağlıyken CanDisconnectWifi true olmalı.");
    }
    finally
    {
        serverCts.Cancel();
        listener.Stop();
        listener.Close();
    }
}

static byte[] CreateTestJpegBytes(int width = 80, int height = 60)
{
    var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    var encoder = new JpegBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(rtb));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
}

static void CheckTokenRefreshOnUnauthorized()
{
    const int testPort = 19511;
    const string testPin = "556677";
    const string firstToken = "token-eski";
    const string secondToken = "token-yeni";

    var authCallCount = 0;
    var captureCallCount = 0;
    var issuedToken = firstToken;

    var testJpegBytes = CreateTestJpegBytes(800, 600);
    var sha = Convert.ToHexString(SHA256.HashData(testJpegBytes)).ToLowerInvariant();

    using var listener = new System.Net.HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
    listener.Start();

    var cts = new CancellationTokenSource();

    var serverTask = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var req = context.Request;
                var res = context.Response;
                var path = req.Url?.AbsolutePath ?? string.Empty;

                if (path.Equals("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref authCallCount);
                    // Her doğrulamada yeni belirteç üret (telefon davranışı)
                    issuedToken = authCallCount == 1 ? firstToken : secondToken;
                    var body = $"{{\"success\":true,\"token\":\"{issuedToken}\",\"expiresInSeconds\":3600}}";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = bytes.Length;
                    await res.OutputStream.WriteAsync(bytes, cts.Token);
                    res.Close();
                    continue;
                }

                if (path.Equals("/api/v1/capture", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref captureCallCount);
                    var auth = req.Headers["Authorization"] ?? string.Empty;

                    // İlk belirteç artık geçersiz: 401 dön
                    if (auth.Contains(firstToken, StringComparison.Ordinal))
                    {
                        var err = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Yetkisiz erişim. Geçerli bir oturum belirteci gereklidir.\"}");
                        res.StatusCode = 401;
                        res.ContentType = "application/json; charset=utf-8";
                        res.ContentLength64 = err.Length;
                        await res.OutputStream.WriteAsync(err, cts.Token);
                        res.Close();
                        continue;
                    }

                    var ok = $"{{\"success\":true,\"photoId\":\"p1\",\"fileName\":\"p1.jpg\",\"fileSizeBytes\":{testJpegBytes.Length},\"sha256\":\"{sha}\",\"width\":800,\"height\":600,\"createdAt\":\"2026-11-10T00:00:00Z\"}}";
                    var okBytes = Encoding.UTF8.GetBytes(ok);
                    res.StatusCode = 200;
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = okBytes.Length;
                    await res.OutputStream.WriteAsync(okBytes, cts.Token);
                    res.Close();
                    continue;
                }

                res.StatusCode = 404;
                res.Close();
            }
            catch (System.Net.HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    });

    try
    {
        using var client = new RemotePhotoClient();

        // 1) İlk doğrulama: token-eski alınır
        var auth1 = client.AuthenticateAsync($"http://127.0.0.1:{testPort}/", testPin).GetAwaiter().GetResult();
        True(auth1, "İlk kimlik doğrulama başarılı olmalıdır.");
        Equal(1, authCallCount);

        // 2) Çekim: eski belirteç 401 alır -> istemci yeniden doğrular -> ikinci deneme başarılı
        var capture = client.TriggerCaptureAsync().GetAwaiter().GetResult();
        True(capture.Success, $"401 sonrası çekim başarılı olmalıdır. Hata: {capture.ErrorMessage}");
        Equal(2, authCallCount);
        True(captureCallCount >= 2, "Çekim isteği en az iki kez denenmelidir (401 + yeniden deneme).");
        Equal("p1", capture.PhotoId);
    }
    finally
    {
        cts.Cancel();
        try { listener.Stop(); } catch { }
        try { listener.Close(); } catch { }
    }
}

static void CheckDxfExportStructureAndUnits()
{
    var reference = new ImageSegment(new ImagePoint(200, 500), new ImagePoint(800, 500)); // 600 px
    var measurement = new ImageSegment(new ImagePoint(250, 205), new ImagePoint(700, 205));
    var imageSize = new ImageSize(1000, 600);

    // 600 px = 600 mm -> 1 mm/px
    var request = new DxfExportRequest(
        "Ölçü Atölyesi Test",
        imageSize,
        reference,
        new LengthValue(600, LengthUnit.Millimeter),
        measurement,
        new LengthValue(450, LengthUnit.Millimeter));

    var result = DxfBuilder.Build(request);
    True(result.Success, $"DXF üretimi başarılı olmalıdır: {result.Message}");
    var content = result.Content!;

    // Yapı
    True(content.Contains("$ACADVER") && content.Contains("AC1009"), "DXF R12 (AC1009) olmalıdır.");
    True(content.Contains("$INSUNITS\n70\n4"), "Birim milimetre ($INSUNITS=4) olmalıdır.");
    True(content.Contains("0\nSECTION\n2\nENTITIES"), "ENTITIES bölümü bulunmalıdır.");
    True(content.TrimEnd().EndsWith("0\nEOF"), "Dosya EOF ile bitmelidir.");

    // Katmanlar
    True(content.Contains("REFERENCE"), "REFERENCE katmanı bulunmalıdır.");
    True(content.Contains("MEASURE"), "MEASURE katmanı bulunmalıdır.");
    True(content.Contains("LABELS"), "LABELS katmanı bulunmalıdır.");

    // Birim dönüşümü: aynı geometri cm olarak verilse de koordinatlar mm olmalı
    var cmRequest = request with { ReferenceLength = new LengthValue(60, LengthUnit.Centimeter) };
    var cmResult = DxfBuilder.Build(cmRequest);
    True(cmResult.Success, "cm referansı ile de DXF üretilebilmelidir.");
    True(cmResult.Content!.Contains("395"), "cm referansında da mm koordinatı (395) üretilmelidir.");
}

static void CheckDxfCoordinateFlipAndValidation()
{
    var imageSize = new ImageSize(1000, 600);
    var reference = new ImageSegment(new ImagePoint(200, 500), new ImagePoint(800, 500));
    var measurement = new ImageSegment(new ImagePoint(250, 205), new ImagePoint(700, 205));

    var ok = DxfBuilder.Build(new DxfExportRequest(
        "Test Çalışma ĞÜŞİÖÇ",
        imageSize,
        reference,
        new LengthValue(600, LengthUnit.Millimeter),
        measurement,
        new LengthValue(450, LengthUnit.Millimeter)));

    True(ok.Success, ok.Message);
    var content = ok.Content!;

    // Y ters çevirme: 205 px -> 600 - 205 = 395
    True(content.Contains("395"), "Y ekseni ters çevrilmelidir (600-205=395).");
    // Proje adı ASCII'ye çevrilmeli
    True(content.Contains("Test Calisma GUSIOC"), "Türkçe karakterler ASCII'ye çevrilmelidir.");
    True(!content.Contains("Ğ") && !content.Contains("ş"), "DXF içinde Türkçe karakter kalmamalıdır.");

    // Hata yolları
    var noReference = DxfBuilder.Build(new DxfExportRequest(
        "Test", imageSize, reference, null!, measurement, null));
    False(noReference.Success, "Referans uzunluğu yokken DXF üretilmemelidir.");
    True(noReference.Message.Contains("referans"), "Hata mesajı referanstan bahsetmelidir.");

    var zeroLengthReference = new ImageSegment(new ImagePoint(100, 100), new ImagePoint(100, 100));
    var zeroResult = DxfBuilder.Build(new DxfExportRequest(
        "Test", imageSize, zeroLengthReference, new LengthValue(10, LengthUnit.Millimeter), measurement, null));
    False(zeroResult.Success, "Sıfır uzunluklu referansta DXF üretilmemelidir.");

    // DxfExportService: projesiz/geçersiz proje
    var service = new DxfExportService();
    True(service is not null, "DxfExportService oluşturulabilmelidir.");
}

static void CheckToolStateAdvisorAndScaleBar()
{
    // 1) Adım rehberi metinleri
    True(ToolStateAdvisor.GetHint(null, false, false).Contains("Referans Belirle"),
        "Boşta iken başlangıç yönlendirmesi verilmelidir.");

    True(ToolStateAdvisor.GetHint(MeasurePoint.R1, false, false).StartsWith("1/2"),
        "R1 seçiliyken 1/2 adımı gösterilmelidir.");
    True(ToolStateAdvisor.GetHint(MeasurePoint.R1, false, false).Contains("BAŞLANGIÇ"),
        "R1 adımı başlangıç noktasından bahsetmelidir.");

    True(ToolStateAdvisor.GetHint(MeasurePoint.R2, false, true).StartsWith("2/2"),
        "R2 seçiliyken 2/2 adımı gösterilmelidir.");
    True(ToolStateAdvisor.GetHint(MeasurePoint.M1, false, true).Contains("M1"),
        "M1 adımı M1 noktasından bahsetmelidir.");
    True(ToolStateAdvisor.GetHint(MeasurePoint.M2, false, true).Contains("M2"),
        "M2 adımı M2 noktasından bahsetmelidir.");

    // 2) Ölçek çubuğu uzunluğu: 1 mm/px ve 110 px hedef -> ~110 mm -> 100 seçilmeli
    Equal(100d, MeasurementCanvas.PickScaleBarMillimetres(1d, 110d));
    Equal(50d, MeasurementCanvas.PickScaleBarMillimetres(0.5d, 110d));
    Equal(0d, MeasurementCanvas.PickScaleBarMillimetres(0d, 110d));
    Equal(0d, MeasurementCanvas.PickScaleBarMillimetres(double.NaN, 110d));
}

static void CheckProfessionalDrawingSource()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    var canvasPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Controls", "MeasurementCanvas.cs");
    if (!File.Exists(canvasPath))
    {
        canvasPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs"));
    }
    if (!File.Exists(canvasPath))
    {
        canvasPath = @"D:\proje\OlcuAtolyesi-PC\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs";
    }

    True(File.Exists(canvasPath), "MeasurementCanvas.cs bulunmalıdır.");
    var source = File.ReadAllText(canvasPath);

    True(source.Contains("DrawDimension"), "CAD tipi ölçü çizgisi çizimi bulunmalıdır.");
    True(source.Contains("DrawArrowHead"), "Ok başı çizimi bulunmalıdır.");
    True(source.Contains("DrawMagnifier"), "Büyüteç çizimi bulunmalıdır.");
    True(source.Contains("DrawScaleBar"), "Ölçek çubuğu bulunmalıdır.");
    True(source.Contains("ModifierKeys.Shift"), "Shift ile eksen kilitleme bulunmalıdır.");
    True(source.Contains("Key.R") && source.Contains("Key.M"), "R ve M kısayolları bulunmalıdır.");
    True(source.Contains("PickScaleBarMillimetres"), "Ölçek çubuğu hesaplayıcısı bulunmalıdır.");
}

static void CheckMagnifierCenterAndZoom()
{
    // 1) Büyüteç imlecin çapraz üstüne yerleşmeli (üstüne binmemeli)
    var center = MeasurementCanvas.ComputeMagnifierCenter(new Point(300, 300), 58, 800, 600);
    True(center.X > 300, "Büyüteç yatayda imlecin sağında olmalıdır.");
    True(center.Y < 300, "Büyüteç dikeyde imlecin üstünde olmalıdır.");
    True(Math.Abs(center.X - 300) > 58, "Büyüteç imleci kapatmamalıdır (yatay mesafe > yarıçap).");

    // 2) Sağ kenara yakınken sola geçmeli
    var rightEdge = MeasurementCanvas.ComputeMagnifierCenter(new Point(780, 300), 58, 800, 600);
    True(rightEdge.X < 780, "Sağ kenarda büyüteç sola geçmelidir.");

    // 3) Üst kenara yakınken alta geçmeli
    var topEdge = MeasurementCanvas.ComputeMagnifierCenter(new Point(300, 20), 58, 800, 600);
    True(topEdge.Y > 20, "Üst kenarda büyüteç alta geçmelidir.");

    // 4) Her durumda tuval içinde kalmalı
    var corner = MeasurementCanvas.ComputeMagnifierCenter(new Point(795, 595), 58, 800, 600);
    True(corner.X + 58 <= 800 && corner.Y + 58 <= 600, "Büyüteç tuval dışına taşmamalıdır.");

    // 5) Zoom kademeleri: 2 -> 4 -> 8 -> 2
    Equal(4d, MeasurementCanvas.NextMagnifierZoom(2d));
    Equal(8d, MeasurementCanvas.NextMagnifierZoom(4d));
    Equal(2d, MeasurementCanvas.NextMagnifierZoom(8d));
}

static void CheckMagnifierToggleSource()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    var canvasPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Controls", "MeasurementCanvas.cs");
    if (!File.Exists(canvasPath))
    {
        canvasPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs"));
    }
    if (!File.Exists(canvasPath))
    {
        canvasPath = @"D:\proje\OlcuAtolyesi-PC\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs";
    }

    True(File.Exists(canvasPath), "MeasurementCanvas.cs bulunmalıdır.");
    var source = File.ReadAllText(canvasPath);

    True(source.Contains("MagnifierEnabledProperty"), "Büyüteç açma/kapama özelliği bulunmalıdır.");
    True(source.Contains("MagnifierZoomProperty"), "Büyüteç zoom özelliği bulunmalıdır.");
    True(source.Contains("ComputeMagnifierCenter"), "Çapraz yerleşim hesabı bulunmalıdır.");
    True(source.Contains("ToggleMagnifier"), "Açma/kapama metodu bulunmalıdır.");
    True(source.Contains("CycleMagnifierZoom"), "Zoom değiştirme metodu bulunmalıdır.");
    True(!source.Contains("case Key.L:"), "L kısayolu pencere seviyesine taşınmış olmalıdır.");
    True(!source.Contains("_magnifierEnabled"), "Eski özel alan kaldırılmış olmalıdır.");
    // Büyüteç içinde çizgi ve tutamaç çizilmeli
    True(source.Contains("handleRadius"), "Büyüteç içinde tutamaçlar çizilmelidir.");
}

static void CheckMagnifierRenderingAndShortcuts()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    var canvasPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Controls", "MeasurementCanvas.cs");
    if (!File.Exists(canvasPath))
    {
        canvasPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs"));
    }
    if (!File.Exists(canvasPath))
    {
        canvasPath = @"D:\proje\OlcuAtolyesi-PC\src\OlcuAtolyesi.PC\Controls\MeasurementCanvas.cs";
    }

    True(File.Exists(canvasPath), "MeasurementCanvas.cs bulunmalıdır.");
    var canvasSource = File.ReadAllText(canvasPath);

    // 1) Büyüteç kenarlığı dolgusuz olmalı (içerik kapanmamalı)
    True(canvasSource.Contains("dc.DrawEllipse(null, borderPen, center, radius, radius)"),
        "Büyüteç kenarlığı dolgusuz çizilmelidir; aksi halde görüntü kapanır.");

    // 2) Dolgulu daire çağrısı kırpmadan SONRA kalmamalı:
    //    eski hatalı desen: borderPen tanımından sonra dolgulu DrawEllipse
    var fillAfterBorder = canvasSource.Contains(
        "dc.DrawEllipse(FindBrush(\"CardAltBackgroundBrush\", Brushes.Black), borderPen, center, radius, radius)");
    True(!fillAfterBorder, "Opak arka plan dolgusu kenarlıkla birlikte çizilmemelidir.");

    // 3) L ve zoom tuşları canvas'tan kaldırılmış olmalı (pencere seviyesine taşındı)
    True(!canvasSource.Contains("case Key.L:"), "L kısayolu canvas'ta kalmamalıdır.");
    True(!canvasSource.Contains("case Key.OemOpenBrackets:"), "[ kısayolu kaldırılmalıdır.");

    // 4) Pencere seviyesinde kısayollar tanımlı olmalı
    var xamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "MainWindow.xaml");
    if (!File.Exists(xamlPath))
    {
        xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\src\OlcuAtolyesi.PC\MainWindow.xaml"));
    }
    if (!File.Exists(xamlPath))
    {
        xamlPath = @"D:\proje\OlcuAtolyesi-PC\src\OlcuAtolyesi.PC\MainWindow.xaml";
    }

    True(File.Exists(xamlPath), "MainWindow.xaml bulunmalıdır.");
    var xaml = File.ReadAllText(xamlPath);

    True(xaml.Contains("Window.InputBindings"), "Pencere seviyesinde tuş bağlamaları olmalıdır.");
    True(xaml.Contains("Key=\"L\""), "L tuşu pencere seviyesinde bağlanmalıdır.");
    True(xaml.Contains("MagnifierZoom2Command"), "2× komutu bağlanmalıdır.");
    True(xaml.Contains("MagnifierZoom4Command"), "4× komutu bağlanmalıdır.");
    True(xaml.Contains("MagnifierZoom8Command"), "8× komutu bağlanmalıdır.");
    True(xaml.Contains("MagnifierZoomInCommand"), "Yakınlaştırma komutu bağlanmalıdır.");
    True(xaml.Contains("MagnifierZoomOutCommand"), "Uzaklaştırma komutu bağlanmalıdır.");
}

static void CheckMeasurementWindowSource()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\"));
    }
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = @"D:\proje\OlcuAtolyesi-PC";
    }

    var windowXaml = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Views", "MeasurementWindow.xaml");
    True(File.Exists(windowXaml), "MeasurementWindow.xaml oluşturulmalıdır.");
    var xaml = File.ReadAllText(windowXaml);

    True(xaml.Contains("MeasurementCanvas"), "Pencere ölçüm tuvalini içermelidir.");
    True(xaml.Contains("{Binding Session}"), "Pencere paylaşılan oturuma bağlanmalıdır.");
    True(xaml.Contains("WindowState=\"Maximized\""), "Pencere tam ekran açılmalıdır.");
    True(xaml.Contains("ToolHint"), "Adım rehberi pencerede de görünmelidir.");
    True(xaml.Contains("MagnifierEnabled"), "Büyüteç ayarı iki pencerede senkron olmalıdır.");

    var codeBehind = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Views", "MeasurementWindow.xaml.cs");
    True(File.Exists(codeBehind), "MeasurementWindow.xaml.cs oluşturulmalıdır.");
    var code = File.ReadAllText(codeBehind);
    True(code.Contains("PhotoMeasurementViewModel"), "Pencere paylaşılan ViewModel örneğini almalıdır.");
    True(code.Contains("Key.F11"), "F11 tam ekran kısayolu bulunmalıdır.");

    var mainVm = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "ViewModels", "MainWindowViewModel.cs");
    True(File.Exists(mainVm), "MainWindowViewModel.cs bulunmalıdır.");
    var vm = File.ReadAllText(mainVm);
    True(vm.Contains("OpenMeasurementWindowCommand"), "Açma komutu tanımlanmalıdır.");
    True(vm.Contains("_measurementWindow"), "Tek örnek yönetimi bulunmalıdır.");
    True(vm.Contains("IsLoaded"), "Kapalı pencere yeniden açılabilmelidir.");
    True(vm.Contains("_measurementWindow?.Close()"), "Kapanışta referans temizlenmelidir.");
}

static void CheckImageDisplayTransformRoundTrip()
{
    var original = new ImageSize(1000, 800);
    var p1 = new ImagePoint(100, 200);
    var p2 = new ImagePoint(900, 700);

    // 1. Identity (dönüşüm yok)
    var identityTransform = new ImageDisplayTransform(original, new ImageDisplaySettings());
    Equal(1000d, identityTransform.DisplaySize.Width);
    Equal(800d, identityTransform.DisplaySize.Height);
    Equal(p1.X, identityTransform.ToDisplay(p1).X);
    Equal(p1.Y, identityTransform.ToDisplay(p1).Y);
    Equal(p1.X, identityTransform.ToOriginal(identityTransform.ToDisplay(p1)).X);
    Equal(p1.Y, identityTransform.ToOriginal(identityTransform.ToDisplay(p1)).Y);

    // 2. 90 derece saat yönü döndürme (1 çeyrek tur)
    // Orijinal: 1000x800 -> Display: 800x1000
    var rot90 = new ImageDisplayTransform(original, new ImageDisplaySettings(QuarterTurns: 1));
    Equal(800d, rot90.DisplaySize.Width);
    Equal(1000d, rot90.DisplaySize.Height);
    var dispP1 = rot90.ToDisplay(p1);
    var origP1 = rot90.ToOriginal(dispP1);
    Near(p1.X, origP1.X);
    Near(p1.Y, origP1.Y);

    // 3. 270 derece (3 çeyrek) + FlipH + FlipV
    var combo = new ImageDisplayTransform(original, new ImageDisplaySettings(QuarterTurns: 3, FlipHorizontal: true, FlipVertical: true));
    Equal(800d, combo.DisplaySize.Width);
    Equal(1000d, combo.DisplaySize.Height);
    var dispP2 = combo.ToDisplay(p2);
    var origP2 = combo.ToOriginal(dispP2);
    Near(p2.X, origP2.X);
    Near(p2.Y, origP2.Y);

    // 4. Kırpma + 180 derece + Aynalama
    var cropRect = new ImageRect(100, 150, 600, 400);
    var cropTransform = new ImageDisplayTransform(original, new ImageDisplaySettings(QuarterTurns: 2, FlipHorizontal: true, CropRect: cropRect));
    Equal(600d, cropTransform.DisplaySize.Width);
    Equal(400d, cropTransform.DisplaySize.Height);
    var testPointInCrop = new ImagePoint(300, 250);
    var dispPoint = cropTransform.ToDisplay(testPointInCrop);
    var recovered = cropTransform.ToOriginal(dispPoint);
    Near(testPointInCrop.X, recovered.X);
    Near(testPointInCrop.Y, recovered.Y);
}

static void CheckPhotoFilterPipeline()
{
    // 4x4 Bgra32 bitmap oluştur: her piksel (B=100, G=150, R=200, A=255)
    var width = 4;
    var height = 4;
    var stride = width * 4;
    var rawPixels = new byte[stride * height];
    for (var i = 0; i < rawPixels.Length; i += 4)
    {
        rawPixels[i] = 100;     // B
        rawPixels[i + 1] = 150; // G
        rawPixels[i + 2] = 200; // R
        rawPixels[i + 3] = 255; // A
    }
    var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, rawPixels, stride);
    source.Freeze();

    // 1. Identity (varsayılan)
    var identityOutput = PhotoFilterPipeline.Apply(source, new PhotoFilterSettings());
    Equal(source, identityOutput);

    // 2. Parlaklık +50
    var brightOutput = PhotoFilterPipeline.Apply(source, new PhotoFilterSettings(Brightness: 50));
    var brightPixels = new byte[stride * height];
    brightOutput.CopyPixels(brightPixels, stride, 0);
    True(brightPixels[0] > 100, "B artmalı");
    True(brightPixels[1] > 150, "G artmalı");
    True(brightPixels[2] > 200, "R artmalı");

    // 3. Negatif
    var negOutput = PhotoFilterPipeline.Apply(source, new PhotoFilterSettings(Negative: true));
    var negPixels = new byte[stride * height];
    negOutput.CopyPixels(negPixels, stride, 0);
    Equal((byte)(255 - 100), negPixels[0]);
    Equal((byte)(255 - 150), negPixels[1]);
    Equal((byte)(255 - 200), negPixels[2]);

    // 4. Gri Tonlama (R == G == B)
    var grayOutput = PhotoFilterPipeline.Apply(source, new PhotoFilterSettings(Grayscale: true));
    var grayPixels = new byte[stride * height];
    grayOutput.CopyPixels(grayPixels, stride, 0);
    Equal(grayPixels[0], grayPixels[1]);
    Equal(grayPixels[1], grayPixels[2]);
}

static void CheckImageTabsAndCanvasBinding()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\"));
    }
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = @"D:\proje\OlcuAtolyesi-PC";
    }

    var mainXamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "MainWindow.xaml");
    True(File.Exists(mainXamlPath), "MainWindow.xaml bulunmalıdır.");
    var mainXaml = File.ReadAllText(mainXamlPath);

    // Ana pencerede sekme OLMAMALI (sekmeler ayrı pencerede)
    True(!mainXaml.Contains("<TabControl"), "Ana pencerede sekme paneli bulunmamalıdır.");
    True(mainXaml.Contains("DisplaySettings="), "Ana pencere canvas'ı dönüşüme bağlı olmalıdır.");
    True(mainXaml.Contains("ShowGrid="), "Ana pencere canvas'ı ızgaraya bağlı olmalıdır.");

    var windowXamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Views", "MeasurementWindow.xaml");
    True(File.Exists(windowXamlPath), "MeasurementWindow.xaml bulunmalıdır.");
    var windowXaml = File.ReadAllText(windowXamlPath);

    True(windowXaml.Contains("<TabControl"), "Sekmeli panel ayrı pencerede bulunmalıdır.");
    True(windowXaml.Contains("Görüntü"), "Görüntü sekmesi bulunmalıdır.");
    True(windowXaml.Contains("Ölçüm"), "Ölçüm sekmesi bulunmalıdır.");
    True(windowXaml.Contains("Proje"), "Proje sekmesi bulunmalıdır.");
    True(windowXaml.Contains("DXF"), "DXF sekmesi bulunmalıdır.");
    True(windowXaml.Contains("ImageAdjustment.Brightness"), "Parlaklık bağlanmalıdır.");
    True(windowXaml.Contains("DisplaySettings="), "Pencere canvas'ı dönüşüme bağlı olmalıdır.");

    // 2. MeasurementCanvas.cs DependencyProperty kontrolü
    var canvasPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Controls", "MeasurementCanvas.cs");
    True(File.Exists(canvasPath), "MeasurementCanvas.cs bulunmalıdır.");
    var canvasCode = File.ReadAllText(canvasPath);
    True(canvasCode.Contains("DisplaySettingsProperty"), "DisplaySettings DependencyProperty bulunmalıdır.");
    True(canvasCode.Contains("ShowGridProperty"), "ShowGrid DependencyProperty bulunmalıdır.");
    True(canvasCode.Contains("DisplayTransform"), "DisplayTransform özelliği bulunmalıdır.");

    // 3. PhotoMeasurementViewModel.cs ImageAdjustment kontrolü
    var vmPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "ViewModels", "PhotoMeasurementViewModel.cs");
    True(File.Exists(vmPath), "PhotoMeasurementViewModel.cs bulunmalıdır.");
    var vmCode = File.ReadAllText(vmPath);
    True(vmCode.Contains("ImageAdjustmentViewModel ImageAdjustment"), "ImageAdjustment ViewModel özelliği bulunmalıdır.");
    True(vmCode.Contains("RefreshDisplayImage"), "RefreshDisplayImage metodu bulunmalıdır.");
    True(vmCode.Contains("_originalPhotoBitmap"), "Orijinal fotoğraf Bitmap alanı korunmalıdır.");
}

static void CheckTabIconsAndWindowSplit()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\"));
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\"));
    }
    if (!Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = @"D:\proje\OlcuAtolyesi-PC";
    }

    var windowXamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Views", "MeasurementWindow.xaml");
    True(File.Exists(windowXamlPath), "MeasurementWindow.xaml bulunmalıdır.");
    var xaml = File.ReadAllText(windowXamlPath);

    // Doğrulanmış glif kodları (11.11.2026 tarihli görsel doğrulama)
    var requiredGlyphs = new[]
    {
        "&#xE8B9;", // Görüntü
        "&#xE8EF;", // Ölçüm
        "&#xE8B7;", // Proje
        "&#xE8AD;", // DXF / dışa aktar
        "&#xE72C;", // Orijinale dön
        "&#xE706;", // Parlaklık
        "&#xE8A9;", // Izgara
    };

    foreach (var glyph in requiredGlyphs)
    {
        True(xaml.Contains(glyph), $"İkon kodu eksik: {glyph}");
    }

    True(xaml.Contains("Segoe Fluent Icons"), "İkon fontu tanımlanmalıdır.");
    True(xaml.Contains("IconGlyph"), "Ortak ikon stili kullanılmalıdır.");

    // Ana pencerede sekme olmamalı
    var mainXamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "MainWindow.xaml");
    var mainXaml = File.ReadAllText(mainXamlPath);
    True(!mainXaml.Contains("<TabControl"), "Ana pencerede sekme paneli kalmamalıdır.");
}

static void CheckRulerTickCalculator()
{
    // mm taksimatı: 1 mm = 10 px ise, 10 mm adımı = 100 px aralık seçilmeli
    var mmStep = RulerTickCalculator.ChooseMillimetreStep(10d);
    Equal(10d, mmStep.StepValue);
    Near(100d, mmStep.StepPixels);
    Equal(5, mmStep.Subdivisions);

    // piksel taksimatı: %100 ölçekte 50 px adımı
    var pxStep = RulerTickCalculator.ChoosePixelStep(1d);
    Equal(50d, pxStep.StepValue);
    Near(50d, pxStep.StepPixels);
    Equal(5, pxStep.Subdivisions);
}

static void CheckReferenceObjectLibrary()
{
    var all = ReferenceObjectLibrary.All;
    True(all.Count >= 7, "En az 7 referans nesnesi olmalı");

    var card = ReferenceObjectLibrary.FindByName("Kredi Kartı / Kimlik (ISO 7810 ID-1)");
    True(card is not null, "Kredi kartı bulunamadı");
    Near(85.60d, card!.Millimetres);

    var coin = ReferenceObjectLibrary.FindByName("1 TL Madeni Para");
    True(coin is not null, "1 TL bulunamadı");
    Near(26.15d, coin!.Millimetres);

    var manual = ReferenceObjectLibrary.FindByName("Özel (Manuel Giriş)");
    True(manual is not null, "Manuel giriş seçeneği bulunamadı");
}

static void CheckScreenCalibration()
{
    // 100 mm nesne ekranda 378 DIP tuttuysa -> 1 DIP = 100 / 378 mm
    var calib = ScreenCalibration.FromMeasurement(100d, 378d, "Cetvel");
    Near(100d / 378d, calib.MillimetresPerDip);
    True(calib.IsCustom);

    // 378 DIP = 100 mm olmalı
    Near(100d, calib.ToMillimetres(378d));
    Near(378d, calib.ToDips(100d));

    // Standart 96 DPI varsayılanı (yaklaşık 0.264583 mm/DIP)
    var def = ScreenCalibration.Default96Dpi;
    Near(25.4d / 96d, def.MillimetresPerDip);
    False(def.IsCustom);
}

static void CheckSimplifiedMainWindowSource()
{
    var baseDir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(baseDir) && !File.Exists(Path.Combine(baseDir, "OlcuAtolyesi.PC.sln")))
    {
        var parent = Directory.GetParent(baseDir);
        baseDir = parent?.FullName;
    }

    if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(Path.Combine(baseDir, "src")))
    {
        baseDir = @"D:\proje\OlcuAtolyesi-PC";
    }

    var xamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "MainWindow.xaml");
    var xaml = File.ReadAllText(xamlPath);

    // Ana pencerede eski araç düğmeleri OLMAMALI
    if (xaml.Contains("ReferenceToolCommand") || xaml.Contains("MeasureToolCommand"))
    {
        throw new InvalidOperationException(
            "MainWindow.xaml içinde ReferenceToolCommand veya MeasureToolCommand kalmış — " +
            "bu düğmeler ayrı ölçüm penceresine taşınmalı, ana pencereden kaldırılmalıydı.");
    }

    // Yeni buton ve referans kontrolleri OLMALI
    if (!xaml.Contains("OpenMeasurementWindowCommand") ||
        !xaml.Contains("ÖLÇÜM PENCERESİ") ||
        !xaml.Contains("ReferenceObjects") ||
        !xaml.Contains("HasReferenceWarning") ||
        !xaml.Contains("BottomInfoText"))
    {
        throw new InvalidOperationException(
            "MainWindow.xaml içinde v0.6.7 sadeleştirilmiş arayüz elemanları eksik.");
    }

    // Cetvel ve kalibrasyon bağlamaları OLMALI
    if (!xaml.Contains("ShowRuler") ||
        !xaml.Contains("ScreenMillimetresPerDip") ||
        !xaml.Contains("ContextMenu") ||
        !xaml.Contains("Ekran Kalibrasyonu"))
    {
        throw new InvalidOperationException(
            "MainWindow.xaml içinde cetvel veya kalibrasyon XAML bağlamaları eksik.");
    }

    // MeasurementCanvas kaynak kodunda cetvel çizimleri OLMALI
    var canvasPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Controls", "MeasurementCanvas.cs");
    var canvasCode = File.ReadAllText(canvasPath);
    if (!canvasCode.Contains("DrawObjectRuler") ||
        !canvasCode.Contains("DrawScreenRuler") ||
        !canvasCode.Contains("RulerTickCalculator"))
    {
        throw new InvalidOperationException(
            "MeasurementCanvas.cs içinde DrawObjectRuler, DrawScreenRuler veya RulerTickCalculator eksik.");
    }

    // Kalibrasyon penceresi XAML'i mevcut olmalı
    var calibXamlPath = Path.Combine(baseDir, "src", "OlcuAtolyesi.PC", "Views", "ScreenCalibrationWindow.xaml");
    if (!File.Exists(calibXamlPath))
    {
        throw new InvalidOperationException("ScreenCalibrationWindow.xaml dosyası bulunamadı.");
    }
}

file sealed class FakePhotoDialog(string? path) : IPhotoFileDialogService
{
    public string? PickPhotoFile() => path;
    public string? PickDxfExportPath(string? defaultFileName = null) => null;
}

file sealed class FakeAdbCommandRunner(string? adbExecutablePath) : IAdbCommandRunner
{
    private readonly Dictionary<(string Serial, string Prop), string> _properties = new();

    public List<IReadOnlyList<string>> ExecutedCommands { get; } = [];

    public string DevicesOutput { get; set; } = "List of devices attached\n";

    public string DefaultDeviceState { get; set; } = "device";

    public bool SimulateTimeout { get; set; }

    public void SetDeviceProperty(string serial, string prop, string value) =>
        _properties[(serial, prop)] = value;

    public string? GetAdbExecutablePath() => adbExecutablePath;

    public Task<AdbCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ExecutedCommands.Add(arguments.ToArray());

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                IsCancelled: true,
                FailureReason: "ADB komutu iptal edildi."));
        }

        if (SimulateTimeout)
        {
            return Task.FromResult(new AdbCommandResult(
                -1,
                string.Empty,
                string.Empty,
                IsTimedOut: true,
                FailureReason: "ADB komutu zaman aşımına uğradı."));
        }

        if (arguments.Count >= 2 && arguments[0] == "devices" && arguments[1] == "-l")
        {
            return Task.FromResult(new AdbCommandResult(0, DevicesOutput, string.Empty));
        }

        if (arguments.Count >= 3 && arguments[0] == "-s")
        {
            var serial = arguments[1];
            if (arguments[2] == "get-state")
            {
                return Task.FromResult(new AdbCommandResult(0, DefaultDeviceState + "\n", string.Empty));
            }

            if (arguments.Count >= 5 && arguments[2] == "shell" && arguments[3] == "getprop")
            {
                var prop = arguments[4];
                _properties.TryGetValue((serial, prop), out var val);
                return Task.FromResult(new AdbCommandResult(0, (val ?? string.Empty) + "\n", string.Empty));
            }
        }

        return Task.FromResult(new AdbCommandResult(0, string.Empty, string.Empty));
    }
}

file sealed class FakeScrcpyProcessManager(string? executablePath) : IScrcpyProcessManager
{
    private ScrcpySessionStatus _status = ScrcpySessionStatus.Stopped();
    private int? _pid;

    public string? ScrcpyExecutablePath => executablePath;

    public bool IsScrcpyAvailable => !string.IsNullOrWhiteSpace(executablePath);

    public ScrcpySessionStatus CurrentStatus => _status;

    public event EventHandler<ScrcpySessionStatus>? StatusChanged;

    public IReadOnlyList<string>? LastLaunchedArguments { get; private set; }

    public List<ScrcpyCameraInfo>? MockCameras { get; set; }

    public Dictionary<string, List<ScrcpyCameraSizeOption>> MockSizesByCameraId { get; } = new();

    public Task<ScrcpyVersionInfo> GetVersionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(executablePath is not null
            ? new ScrcpyVersionInfo("scrcpy 5.0.1", "5.0.1", IsFound: true, IsSupported: true)
            : ScrcpyVersionInfo.NotFound);

    public Task<ScrcpyLaunchResult> StartCameraSessionAsync(
        ScrcpyCameraSessionConfig config,
        CancellationToken cancellationToken = default)
    {
        if (executablePath is null)
        {
            _status = new ScrcpySessionStatus(ScrcpySessionState.Error, null, null, null, null, "scrcpy bulunamadı.", "Sistemde scrcpy motoru bulunamadı.");
            StatusChanged?.Invoke(this, _status);
            return Task.FromResult(new ScrcpyLaunchResult(
                false,
                null,
                "scrcpy bulunamadı.",
                "Sistemde scrcpy motoru bulunamadı."));
        }

        if (!AdbInputValidator.IsValidSerial(config.SerialId))
        {
            throw new ArgumentException("Geçersiz cihaz seri numarası.");
        }

        LastLaunchedArguments = config.BuildCommandLineArguments();
        _pid = 9999;
        _status = new ScrcpySessionStatus(
            ScrcpySessionState.Running,
            _pid,
            config.SerialId,
            config.CameraId,
            config.CameraSize,
            "Canlı önizleme penceresi açıldı.");
        StatusChanged?.Invoke(this, _status);

        return Task.FromResult(new ScrcpyLaunchResult(
            true,
            _pid,
            "Canlı önizleme penceresi açıldı.",
            null));
    }

    public Task<bool> StopCameraSessionAsync(CancellationToken cancellationToken = default)
    {
        _status = ScrcpySessionStatus.Stopped("Kamera oturumu kullanıcı tarafından durduruldu.");
        _pid = null;
        StatusChanged?.Invoke(this, _status);
        return Task.FromResult(true);
    }

    public Task<ScrcpyCameraQueryScanResult> QueryCamerasAsync(
        string serialId,
        CancellationToken cancellationToken = default)
    {
        if (MockCameras is not null)
        {
            if (MockCameras.Count == 0)
            {
                return Task.FromResult(new ScrcpyCameraQueryScanResult(false, [], "Cihazda kamera bulunamadı.", "Mock hata"));
            }

            return Task.FromResult(new ScrcpyCameraQueryScanResult(true, MockCameras, $"{MockCameras.Count} kamera bulundu."));
        }

        var cameras = new List<ScrcpyCameraInfo>
        {
            new("0", ScrcpyCameraFacing.Back, 4080, 3060, [15, 20, 24, 30], 0.55, 8.0, "mock details"),
            new("1", ScrcpyCameraFacing.Front, 2560, 1440, [30, 60], 1.0, 1.0, "mock details")
        };
        return Task.FromResult(new ScrcpyCameraQueryScanResult(true, cameras, "Kameralar başarıyla listelendi.", null));
    }

    public Task<ScrcpyCameraSizesQueryResult> QueryCameraSizesAsync(
        string serialId,
        string cameraId,
        CancellationToken cancellationToken = default)
    {
        if (MockSizesByCameraId.TryGetValue(cameraId, out var mockList))
        {
            return Task.FromResult(new ScrcpyCameraSizesQueryResult(true, cameraId, mockList, $"{mockList.Count} mod listelendi."));
        }

        var sizes = new List<ScrcpyCameraSizeOption>
        {
            new(4080, 3060, "4:3", [15, 20, 24, 30]),
            new(1920, 1080, "16:9", [30, 60]),
            new(1280, 720, "16:9", [30, 60, 120])
        };
        return Task.FromResult(new ScrcpyCameraSizesQueryResult(true, cameraId, sizes, "Çözünürlükler başarıyla listelendi.", null));
    }

    public void Dispose()
    {
        _status = ScrcpySessionStatus.Stopped();
        _pid = null;
    }
}

file sealed class FakeCameraStreamClient : ICameraStreamClient
{
    private bool _isConnected;
    private double _fps = 25.0;
    private long _frameCount;
    private string? _serverUrl;

    public bool IsConnected => _isConnected;
    public double Fps => _fps;
    public long ReceivedFrameCount => _frameCount;
    public string? CurrentServerUrl => _serverUrl;

    public event EventHandler<byte[]>? FrameReceived;
    public event EventHandler<string>? StatusChanged;
#pragma warning disable CS0067
    public event EventHandler<string>? ErrorOccurred;
#pragma warning restore CS0067
    public event EventHandler<bool>? ConnectionStateChanged;

    public Task<CameraStreamConnectResult> ConnectAsync(string serverUrl, string pin, CancellationToken cancellationToken = default)
    {
        _serverUrl = serverUrl;
        _isConnected = true;
        StatusChanged?.Invoke(this, "Bağlandı.");
        ConnectionStateChanged?.Invoke(this, true);
        return Task.FromResult(new CameraStreamConnectResult(true, "Bağlandı."));
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        StatusChanged?.Invoke(this, "Bağlantı kesildi.");
        ConnectionStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public void SimulateReceiveFrame(byte[] jpegBytes)
    {
        _frameCount++;
        FrameReceived?.Invoke(this, jpegBytes);
    }

    public void Dispose()
    {
        _isConnected = false;
        ConnectionStateChanged?.Invoke(this, false);
    }
}

file sealed class FakeRemotePhotoClient : IRemotePhotoClient
{
    public bool IsAuthenticated { get; set; } = true;
    public string? SessionToken { get; set; } = "mock-token";
    public string? CurrentBaseUrl { get; set; } = "http://127.0.0.1:8088/";

    public string? MockCapturedFilePath { get; set; }
    public RemoteCaptureResult MockCaptureResult { get; set; } = new(true, "mock_p1", "mock_p1.jpg", 10240, "sha-mock", 2400, 1800, null);
    public bool SimulateCaptureFailure { get; set; }
    public bool SimulateDownloadFailure { get; set; }
    public List<RemotePhotoItem> MockPhotos { get; } = new();

    public Task<bool> AuthenticateAsync(string baseUrl, string pin, CancellationToken cancellationToken = default)
    {
        IsAuthenticated = string.IsNullOrWhiteSpace(pin) || pin == "123456";
        return Task.FromResult(IsAuthenticated);
    }

    public Task<RemoteCaptureResult> TriggerCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (SimulateCaptureFailure)
        {
            return Task.FromResult(new RemoteCaptureResult(false, null, null, 0, null, 0, 0, "Kamera donanım hatası"));
        }
        return Task.FromResult(MockCaptureResult);
    }

    public Task<string> DownloadPhotoAsync(
        string photoId,
        string expectedSha256,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (SimulateDownloadFailure)
        {
            throw new InvalidDataException("SHA-256 bütünlük doğrulaması başarısız!");
        }

        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, $"{photoId}.jpg");
        if (!string.IsNullOrWhiteSpace(MockCapturedFilePath) && File.Exists(MockCapturedFilePath))
        {
            File.Copy(MockCapturedFilePath, path, overwrite: true);
        }
        else
        {
            File.WriteAllBytes(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 });
        }
        progress?.Report(1.0);
        return Task.FromResult(path);
    }

    public Task<IReadOnlyList<RemotePhotoItem>> ListPhotosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RemotePhotoItem>>(MockPhotos);

    public void ResetAuth()
    {
        IsAuthenticated = false;
        SessionToken = null;
    }

    public void Dispose() { }
}
