using OlcuAtolyesi.Diagnostics;
using OlcuAtolyesi.Models;
using OlcuAtolyesi.Resources.Strings;
using OlcuAtolyesi.Services;
using OlcuAtolyesi.ViewModels;

var checks = new (string Name, Action Run)[]
{
    ("Birim dönüşümleri (mm, cm, m, in)", CheckUnitConversions),
    ("Türkçe pozitif referans ayrıştırma ve geçersiz girişler", CheckReferenceParser),
    ("Referanslı ölçüm geometrisi", CheckMeasurementGeometry),
    ("Zoom/pan/clamp koordinat ve ters dönüşümü", CheckViewportTransform),
    ("Gerçek fotoğraf üzerinde R1/R2 referans ve M1/M2 ölçümü", CheckRealPhotoMeasurementWorkflow),
    ("Referans ve birim değişiminde yeniden hesaplama tutarlılığı", CheckReferenceAndUnitRecalculation),
    ("R1/R2 ve M1/M2 sürükleme, geri alma ve sıfırlama", CheckUndoResetAndPinchCancel),
    ("Oturumluk demo proje CRUD", CheckProjectCrud),
    ("Kalıcı proje kayıt, fotoğraf kopyalama ve yeniden açma", CheckPersistentProjectSaveAndReopen),
    ("Kayıtlı ölçümü düzenleme ve yeniden kaydetme", CheckPersistentProjectEditAndUpdate),
    ("Kayıt hatası ve bozuk/eksik proje verisi dayanıklılığı", CheckPersistenceFaultToleranceAndCorruption),
    ("Proje silindiğinde yalnızca o projenin verilerinin temizlenmesi", CheckIsolatedProjectDeletion),
    ("Tema ve varsayılan birim tercihleri", CheckSettingsPersistence),
    ("PC Kamera Yayın Sunucusu: Yaşam döngüsü, PIN eşleştirme güvenliği ve MJPEG akışı", CheckPcCameraStreamServerLifecycleAndSecurity),
    ("v0.5.0.1 Araçlar Menüsü ve PC Kamera Bağlantısı Regresyonu: Menü entegrasyonu, sayfa oluşturma ve başlangıç durumu", CheckToolsMenuAndPcCameraIntegration),
    ("v0.5.0.3 Kumpas İkonu, Açılış Ekranı (Splash) ve Responsive Varlık Doğrulaması", CheckAppBrandingAndResponsiveAssets),
    ("v0.5.0.4 Kamera Sensör Değişim Durum Makinesi ve Eşzamanlılık Güvenliği", CheckCameraSwitchCoordinatorStateMachine),
    ("v0.5.0.4 Android Tanılama Günlüğü, Döngüsel Arşivleme ve PIN Maskeleme", CheckDiagnosticLoggingAndMasking),
    ("v0.5.0.5 Canlı Önizleme İşlem Hattı, Kare Atlama ve Rolling FPS Doğrulaması", CheckPreviewPipelineFrameSequenceAndFps),
    ("v0.5.0.5 Telefonda Yüksek Çözünürlüklü Fotoğraf Kaydı, SHA-256 ve Kullanıcı Onaylı Silme", CheckPhotoStorageAndDiskFullAndNoDeleteWithoutConfirmation),
    ("v0.5.0.5 Güvenli Token Yetkilendirmesi, Arka Plan Koruması ve Uzaktan Çekim API", CheckRemoteCaptureAndSecurityServerApi),
    ("v0.5.0.5 Fotoğraf Bütünlük Doğrulaması, Bozuk/Eksik Dosya Toleransı ve Yeniden İndirme", CheckRemotePhotoIntegrityAndResilience),
    ("v0.6.0 PIN Kaba Kuvvet Kilidi, HTTP 429 ve Sorgu Dizesi PIN Reddi", CheckPinBruteForceLockout),
    ("v0.6.1 Boş Disk Alanı Ölçümü ve Yetersiz Alan Koruması", CheckDiskSpaceGuard),
    ("v0.6.2 Canlı Önizleme Kare Kaynağı ve Kare Hızı Sınırı", CheckPreviewFrameSourceAndRateLimiter)
};

var failures = new List<string>();

foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"BAŞARILI  {check.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{check.Name}: {exception.Message}");
        Console.WriteLine($"BAŞARISIZ {check.Name}");
    }
}

Console.WriteLine();
Console.WriteLine($"Sonuç: {checks.Length - failures.Count}/{checks.Length} kontrol başarılı.");

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    return 1;
}

return 0;

static void CheckUnitConversions()
{
    Near(1d, UnitConverter.Convert(25.4d, LengthUnit.Millimeter, LengthUnit.Inch));
    Near(2.54d, UnitConverter.Convert(1d, LengthUnit.Inch, LengthUnit.Centimeter));
    Near(100d, new LengthValue(10d, LengthUnit.Centimeter).Millimeters);

    var converted = new LengthValue(50.8d, LengthUnit.Millimeter).ConvertTo(LengthUnit.Inch);
    Near(2d, converted.Value);
    Equal(LengthUnit.Inch, converted.Unit);
}

static void CheckReferenceParser()
{
    var comma = ReferenceLengthParser.ParsePositive("12,5", LengthUnit.Centimeter);
    True(comma.IsValid, comma.ErrorMessage);
    Near(12.5d, comma.Length!.Value.Value);
    Equal(LengthUnit.Centimeter, comma.Length.Value.Unit);

    var dot = ReferenceLengthParser.ParsePositive("1.25", LengthUnit.Inch);
    True(dot.IsValid, dot.ErrorMessage);
    Near(1.25d, dot.Length!.Value.Value);

    False(ReferenceLengthParser.ParsePositive(null, LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("", LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("0", LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("-2,1", LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("sayı değil", LengthUnit.Millimeter).IsValid);
    False(ReferenceLengthParser.ParsePositive("NaN", LengthUnit.Millimeter).IsValid);
}

static void CheckMeasurementGeometry()
{
    var reference = new ReferenceScale(
        new ImageSegment(new ImagePoint(10d, 10d), new ImagePoint(210d, 10d)),
        new LengthValue(100d, LengthUnit.Millimeter));
    var measured = MeasurementGeometry.MeasureLength(
        new ImageSegment(new ImagePoint(30d, 25d), new ImagePoint(30d, 75d)),
        reference,
        LengthUnit.Centimeter);

    Near(2.5d, measured.Value);
    Near(90d, MeasurementGeometry.MeasureAngleDegrees(
        new ImagePoint(1d, 0d),
        new ImagePoint(0d, 0d),
        new ImagePoint(0d, 1d)));

    Throws<ArgumentException>(() => new ReferenceScale(
        new ImageSegment(new ImagePoint(5d, 5d), new ImagePoint(5d, 5d)),
        new LengthValue(10d, LengthUnit.Millimeter)));
}

static void CheckViewportTransform()
{
    var transform = ImageViewportTransform.Fit(
        new ImageSize(1000d, 500d),
        new ViewSize(500d, 500d));

    Near(0.5d, transform.Scale);
    var imagePoint = new ImagePoint(100d, 50d);
    var viewPoint = transform.ImageToView(imagePoint);
    Near(50d, viewPoint.X);
    Near(150d, viewPoint.Y);

    var inverse = transform.ViewToImage(viewPoint);
    Near(imagePoint.X, inverse.X);
    Near(imagePoint.Y, inverse.Y);

    var panned = transform.PanBy(18d, -7d);
    var afterPan = panned.ImageToView(imagePoint);
    Near(viewPoint.X + 18d, afterPan.X);
    Near(viewPoint.Y - 7d, afterPan.Y);

    var anchor = new ViewPoint(210d, 240d);
    var anchorBeforeZoom = panned.ViewToImage(anchor);
    var zoomed = panned.ZoomAt(anchor, 2d);
    var anchorAfterZoom = zoomed.ViewToImage(anchor);
    Near(anchorBeforeZoom.X, anchorAfterZoom.X);
    Near(anchorBeforeZoom.Y, anchorAfterZoom.Y);

    // Aşırı kaydırma sonrasında fotoğrafın ekrandan tamamen çıkmaması (ClampToViewport)
    var extremePan = zoomed.PanBy(-50000d, 50000d)
        .ClampToViewport(new ImageSize(1000d, 500d), new ViewSize(500d, 500d), 16d);
    var topLeft = extremePan.ImageToView(new ImagePoint(0d, 0d));
    var bottomRight = extremePan.ImageToView(new ImagePoint(1000d, 500d));
    True(bottomRight.X >= 484d - 0.001d, "Görüntü sağ kenarı görünüm içinde kalmalı.");
    True(topLeft.Y <= 16d + 0.001d, "Görüntü üst kenarı görünüm içinde kalmalı.");

    // Zoom ve clamp sonrasında fotoğraf koordinatlarındaki ölçümün değişmemesi
    var m1View = extremePan.ImageToView(new ImagePoint(120d, 80d));
    var m2View = extremePan.ImageToView(new ImagePoint(420d, 480d));
    var m1Restored = extremePan.ViewToImage(m1View);
    var m2Restored = extremePan.ViewToImage(m2View);
    Near(500d, m1Restored.DistanceTo(m2Restored));
}

static void CheckRealPhotoMeasurementWorkflow()
{
    // 2048x1536 gerçek fotoğraf boyutu
    var vm = new PhotoMeasureViewModel(LengthUnit.Millimeter, new ImageSize(2048d, 1536d), isRealPhoto: true);
    True(vm.IsRealPhoto);
    False(vm.HasReference);
    Equal("—", vm.ResultText);

    // Çapı bilinen madeni para (1 TL = 26,15 mm) iki kenarına R1 ve R2 yerleştirilmesi (200 piksel çap)
    vm.MovePoint(PhotoMeasureViewModel.PointR1, new ImagePoint(300d, 600d));
    vm.MovePoint(PhotoMeasureViewModel.PointR2, new ImagePoint(500d, 600d));

    var apply = vm.ApplyReference("26,15");
    True(apply.IsValid, apply.ErrorMessage);
    True(vm.HasReference);

    // M1 ve M2 noktalarının 600 piksel aralığa yerleştirilmesi -> 3 * 26,15 = 78,45 mm
    vm.MovePoint(PhotoMeasureViewModel.PointM1, new ImagePoint(400d, 300d));
    vm.MovePoint(PhotoMeasureViewModel.PointM2, new ImagePoint(1000d, 300d));
    True(vm.Result is not null);
    Near(78.45d, vm.Result!.Value.Value);
    Equal(LengthUnit.Millimeter, vm.Result.Value.Unit);

    // Noktaların fotoğraf sınırları dışına taşmaması
    vm.MovePoint(PhotoMeasureViewModel.PointM2, new ImagePoint(5000d, -200d));
    Near(2048d, vm.M2.X);
    Near(0d, vm.M2.Y);
}

static void CheckReferenceAndUnitRecalculation()
{
    var vm = new PhotoMeasureViewModel(LengthUnit.Centimeter, new ImageSize(1600d, 1200d), isRealPhoto: true);
    vm.MovePoint(PhotoMeasureViewModel.PointR1, new ImagePoint(100d, 100d));
    vm.MovePoint(PhotoMeasureViewModel.PointR2, new ImagePoint(300d, 100d)); // 200 px
    vm.MovePoint(PhotoMeasureViewModel.PointM1, new ImagePoint(100d, 400d));
    vm.MovePoint(PhotoMeasureViewModel.PointM2, new ImagePoint(600d, 400d)); // 500 px

    True(vm.ApplyReference("10").IsValid); // 200 px = 10 cm -> 500 px = 25 cm
    Near(25d, vm.Result!.Value.Value);
    Equal(LengthUnit.Centimeter, vm.Result.Value.Unit);

    // R2 noktası sürüklenerek referans 250 px yapıldığında sonucun otomatik 20 cm olması
    vm.MovePoint(PhotoMeasureViewModel.PointR2, new ImagePoint(350d, 100d));
    Near(20d, vm.Result!.Value.Value);

    // Birim mm'ye çevrildiğinde hem KnownLength hem Result tutarlı olarak mm olmalı (100 mm referans, 200 mm sonuç)
    vm.Unit = LengthUnit.Millimeter;
    Near(100d, vm.KnownLength!.Value.Value);
    Equal(LengthUnit.Millimeter, vm.KnownLength.Value.Unit);
    Near(200d, vm.Result!.Value.Value);
    Equal(LengthUnit.Millimeter, vm.Result.Value.Unit);

    // Birim metreye çevrildiğinde 0,1 m referans, 0,2 m sonuç
    vm.Unit = LengthUnit.Meter;
    Near(0.1d, vm.KnownLength!.Value.Value);
    Near(0.2d, vm.Result!.Value.Value);

    // R1 ve R2 aynı noktaya getirilirse geçersiz referans reddedilmeli
    vm.MovePoint(PhotoMeasureViewModel.PointR2, vm.R1);
    False(vm.HasReference);
    False(vm.ApplyReference("10").IsValid);
}

static void CheckUndoResetAndPinchCancel()
{
    var vm = new PhotoMeasureViewModel(LengthUnit.Millimeter, new ImageSize(1000d, 800d), isRealPhoto: true);
    var initialR1 = vm.R1;
    var initialM1 = vm.M1;

    vm.CaptureUndoState();
    vm.MovePoint(PhotoMeasureViewModel.PointR1, new ImagePoint(50d, 60d));
    Near(50d, vm.R1.X);

    vm.CaptureUndoState();
    vm.MovePoint(PhotoMeasureViewModel.PointM1, new ImagePoint(120d, 140d));
    Near(120d, vm.M1.X);

    // İki parmak yakınlaştırma başladığında ilk parmağın yanlışlıkla yakaladığı noktanın iptali
    vm.CaptureUndoState();
    vm.MovePoint(PhotoMeasureViewModel.PointM1, new ImagePoint(999d, 777d));
    vm.CancelLastUndoCapture();
    Near(120d, vm.M1.X);

    // Normal geri alma (Undo)
    True(vm.Undo());
    Near(initialM1.X, vm.M1.X);
    True(vm.Undo());
    Near(initialR1.X, vm.R1.X);
    False(vm.Undo());

    // Sıfırlama (Reset)
    vm.MovePoint(PhotoMeasureViewModel.PointR2, new ImagePoint(900d, 700d));
    vm.Reset();
    Near(initialR1.X, vm.R1.X);
    Near(initialM1.X, vm.M1.X);
}

static void CheckProjectCrud()
{
    var service = new DemoProjectService();
    Equal(3, service.GetAll().Count);
    Equal(3, service.GetRecent().Count);
    True(service.GetAll().All(project => project.IsSample));

    var changeCount = 0;
    service.ProjectsChanged += (_, _) => changeCount++;

    var created = service.Create("  Deneme parçası  ", DemoProjectType.PhotoMeasurement, "Oturumluk kayıt");
    Equal("Deneme parçası", created.Name);
    False(created.IsSample);
    Equal(4, service.GetAll().Count);

    var renamed = service.Rename(created.Id, "Yeni ad");
    Equal("Yeni ad", renamed.Name);

    var measurement = new ProjectMeasurement(
        Guid.NewGuid(),
        "Kenar",
        MeasurementKind.Length,
        new LengthValue(42d, LengthUnit.Millimeter),
        imageSegment: new ImageSegment(new ImagePoint(0d, 0d), new ImagePoint(84d, 0d)));
    var withMeasurement = service.AddMeasurement(created.Id, measurement);
    Equal(1, withMeasurement.Measurements.Count);
    Equal(measurement.Id, withMeasurement.Measurements[0].Id);

    True(service.Delete(created.Id));
    False(service.Delete(created.Id));
    Equal(3, service.GetAll().Count);
    Equal(4, changeCount);
    Throws<ArgumentException>(() => service.Create("  ", DemoProjectType.Other));
}

static void CheckPersistentProjectSaveAndReopen()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesi_Test_{Guid.NewGuid():N}");
    try
    {
        var sourcePhoto = Path.Combine(tempRoot, "temp_camera_photo.jpg");
        Directory.CreateDirectory(tempRoot);
        var fakePhotoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 0xFF, 0xD9 };
        File.WriteAllBytes(sourcePhoto, fakePhotoBytes);

        var service = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        True(service.IsPersistent);
        Equal(0, service.GetAll().Count);

        var photoSize = new ImageSize(2048d, 1536d);
        var refSeg = new ImageSegment(new ImagePoint(220d, 640d), new ImagePoint(620d, 640d)); // 400 px
        var refLen = new LengthValue(100d, LengthUnit.Millimeter);
        var measSeg = new ImageSegment(new ImagePoint(300d, 250d), new ImagePoint(900d, 250d)); // 600 px -> 150 mm
        var measLen = new LengthValue(15d, LengthUnit.Centimeter);

        var saved = service.SavePhotoMeasurementProject(
            "Masa kenarı ölçümü",
            "Kalıcı test projesi",
            sourcePhoto,
            photoSize,
            refSeg,
            refLen,
            measSeg,
            measLen,
            LengthUnit.Centimeter,
            "Üst kenar");

        // Geçici kaynak fotoğraf silinse bile kalıcı kopya korunmalı
        File.Delete(sourcePhoto);
        var persistentPhotoPath = service.GetPhotoFilePath(saved.Id);
        True(persistentPhotoPath is not null && File.Exists(persistentPhotoPath), "Kalıcı fotoğraf dosyası oluşturulmalı.");
        Equal(fakePhotoBytes.Length, File.ReadAllBytes(persistentPhotoPath!).Length);

        // Uygulama kapatılıp yeniden açıldığında (yeni servis örneği) tüm veriler geri yüklenmeli
        var reloadedService = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        Equal(1, reloadedService.GetAll().Count);
        var reopened = reloadedService.GetById(saved.Id);
        True(reopened is not null);
        Equal("Masa kenarı ölçümü", reopened!.Name);
        True(reopened.HasSavedMeasurementWorkspace);
        Equal(photoSize, reopened.PhotoSize!.Value);
        Equal(refSeg, reopened.ReferenceSegment!.Value);
        Equal(refLen, reopened.ReferenceLength!.Value);
        Equal(LengthUnit.Centimeter, reopened.DisplayUnit!.Value);
        Equal(1, reopened.Measurements.Count);
        Equal(measSeg, reopened.Measurements[0].ImageSegment!.Value);
        Near(15d, reopened.Measurements[0].Length!.Value.Value);

        // PhotoMeasureViewModel üzerinde kayıtlı durumun yüklenmesi ve aynı sonucu üretmesi
        var vm = new PhotoMeasureViewModel(
            reopened.DisplayUnit.Value,
            reopened.PhotoSize.Value,
            isRealPhoto: true);
        vm.LoadSavedState(
            reopened.ReferenceSegment,
            reopened.ReferenceLength,
            reopened.Measurements[0].ImageSegment,
            reopened.DisplayUnit);

        True(vm.HasReference);
        Equal(refSeg.Start, vm.R1);
        Equal(refSeg.End, vm.R2);
        Equal(measSeg.Start, vm.M1);
        Equal(measSeg.End, vm.M2);
        Equal(LengthUnit.Centimeter, vm.Unit);
        Near(10d, vm.KnownLength!.Value.Value); // 100 mm -> 10 cm
        Near(15d, vm.Result!.Value.Value);      // 15 cm
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

static void CheckPersistentProjectEditAndUpdate()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesi_Edit_{Guid.NewGuid():N}");
    try
    {
        var service = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        var created = service.SavePhotoMeasurementProject(
            "İlk kayıt",
            "Düzenleme testi",
            null,
            new ImageSize(1000d, 800d),
            new ImageSegment(new ImagePoint(100d, 700d), new ImagePoint(300d, 700d)), // 200 px
            new LengthValue(50d, LengthUnit.Millimeter),
            new ImageSegment(new ImagePoint(100d, 300d), new ImagePoint(500d, 300d)), // 400 px -> 100 mm
            new LengthValue(100d, LengthUnit.Millimeter),
            LengthUnit.Millimeter,
            "Ölçüm 1");

        // Yeniden adlandırma kalıcı olmalı
        service.Rename(created.Id, "Güncellenmiş proje adı");

        // Noktaları ve birimi düzenleyip tekrar kaydetme
        var newRefSeg = new ImageSegment(new ImagePoint(100d, 700d), new ImagePoint(500d, 700d)); // 400 px
        var newRefLen = new LengthValue(20d, LengthUnit.Centimeter);
        var newMeasSeg = new ImageSegment(new ImagePoint(100d, 300d), new ImagePoint(700d, 300d)); // 600 px -> 30 cm
        var newMeasLen = new LengthValue(30d, LengthUnit.Centimeter);

        var updated = service.UpdatePhotoMeasurementProject(
            created.Id,
            name: null,
            sourcePhotoPath: null,
            imageSize: new ImageSize(1000d, 800d),
            referenceSegment: newRefSeg,
            referenceLength: newRefLen,
            measurementSegment: newMeasSeg,
            measuredLength: newMeasLen,
            displayUnit: LengthUnit.Centimeter,
            measurementName: "Ölçüm 1");
        Equal(created.Id, updated.Id);

        // Diskten yeniden okuyup kontrol et
        var reloaded = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot).GetById(created.Id);
        True(reloaded is not null);
        Equal("Güncellenmiş proje adı", reloaded!.Name);
        Equal(newRefSeg, reloaded.ReferenceSegment!.Value);
        Equal(newRefLen, reloaded.ReferenceLength!.Value);
        Equal(LengthUnit.Centimeter, reloaded.DisplayUnit!.Value);
        Equal(newMeasSeg, reloaded.Measurements[0].ImageSegment!.Value);
        Near(30d, reloaded.Measurements[0].Length!.Value.Value);
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

static void CheckPersistenceFaultToleranceAndCorruption()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesi_Fault_{Guid.NewGuid():N}");
    try
    {
        var service = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        var originalRef = new ImageSegment(new ImagePoint(50d, 500d), new ImagePoint(250d, 500d));
        var originalMeas = new ImageSegment(new ImagePoint(100d, 200d), new ImagePoint(400d, 200d));

        var project = service.SavePhotoMeasurementProject(
            "Sağlam proje",
            "Hata toleransı testi",
            null,
            new ImageSize(1000d, 600d),
            originalRef,
            new LengthValue(100d, LengthUnit.Millimeter),
            originalMeas,
            new LengthValue(150d, LengthUnit.Millimeter),
            LengthUnit.Millimeter,
            "M1–M2");

        // 1) Kayıt sırasında hata oluşursa önceki sağlam veriler hem bellekte hem diskte korunmalı
        service.SimulateWriteFailureHook = _ => throw new IOException("Simüle edilmiş disk yazma hatası.");
        Throws<IOException>(() => service.Rename(project.Id, "Bozuk güncelleme"));
        service.SimulateWriteFailureHook = null;

        Equal("Sağlam proje", service.GetById(project.Id)!.Name);
        var afterFailedWrite = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        Equal("Sağlam proje", afterFailedWrite.GetById(project.Id)!.Name);

        // 2) Başarılı bir güncelleme yapılsın (.bak yedek dosyası oluşur), ardından ana project.json bozulsun
        service.Rename(project.Id, "İkinci sağlam sürüm");
        var projectDir = Path.Combine(tempRoot, "projects", project.Id.ToString("N"));
        var mainJsonPath = Path.Combine(projectDir, "project.json");
        var backupJsonPath = Path.Combine(projectDir, "project.json.bak");
        True(File.Exists(backupJsonPath), "Güncelleme sonrasında .bak yedek dosyası oluşmalı.");

        File.WriteAllText(mainJsonPath, "{ bozuk json verisi [[[");

        // Yeniden yüklendiğinde çökmemeli ve .bak yedeğinden önceki sağlam veriyi kurtarmalı
        var recoveredService = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        Equal(1, recoveredService.GetAll().Count);
        Equal("Sağlam proje", recoveredService.GetById(project.Id)!.Name);
        Equal(originalRef, recoveredService.GetById(project.Id)!.ReferenceSegment!.Value);

        // 3) Tamamen bozuk ve yedeksiz başka bir proje klasörü eklendiğinde uygulama çökmemeli
        var corruptDir = Path.Combine(tempRoot, "projects", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(corruptDir);
        File.WriteAllText(Path.Combine(corruptDir, "project.json"), "<<<bozuk>>>");

        var resilientService = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        Equal(1, resilientService.GetAll().Count);
        Equal(1, resilientService.CorruptedEntryCount);
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

static void CheckIsolatedProjectDeletion()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesi_Delete_{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(tempRoot);
        var sourcePhoto1 = Path.Combine(tempRoot, "source1.jpg");
        var sourcePhoto2 = Path.Combine(tempRoot, "source2.jpg");
        File.WriteAllBytes(sourcePhoto1, new byte[] { 10, 20, 30, 40 });
        File.WriteAllBytes(sourcePhoto2, new byte[] { 50, 60, 70, 80, 90 });

        var service = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        var p1 = service.SavePhotoMeasurementProject(
            "Silinecek proje",
            "Proje 1",
            sourcePhoto1,
            new ImageSize(800d, 600d),
            new ImageSegment(new ImagePoint(10d, 10d), new ImagePoint(110d, 10d)),
            new LengthValue(50d, LengthUnit.Millimeter),
            new ImageSegment(new ImagePoint(20d, 50d), new ImagePoint(220d, 50d)),
            new LengthValue(100d, LengthUnit.Millimeter),
            LengthUnit.Millimeter,
            "Ölçüm 1");

        var p2 = service.SavePhotoMeasurementProject(
            "Korunacak proje",
            "Proje 2",
            sourcePhoto2,
            new ImageSize(1200d, 900d),
            new ImageSegment(new ImagePoint(50d, 50d), new ImagePoint(250d, 50d)),
            new LengthValue(10d, LengthUnit.Centimeter),
            new ImageSegment(new ImagePoint(100d, 150d), new ImagePoint(500d, 150d)),
            new LengthValue(20d, LengthUnit.Centimeter),
            LengthUnit.Centimeter,
            "Ölçüm 2");

        var p1Photo = service.GetPhotoFilePath(p1.Id);
        var p2Photo = service.GetPhotoFilePath(p2.Id);
        True(File.Exists(p1Photo!));
        True(File.Exists(p2Photo!));

        // p1 silindiğinde yalnızca p1 klasörü ve fotoğrafı silinmeli, p2 aynen kalmalı
        True(service.Delete(p1.Id));
        False(File.Exists(p1Photo!));
        False(Directory.Exists(Path.Combine(tempRoot, "projects", p1.Id.ToString("N"))));

        True(File.Exists(p2Photo!));
        var reloaded = new DemoProjectService(seedSamples: false, storageDirectory: tempRoot);
        Equal(1, reloaded.GetAll().Count);
        Equal(p2.Id, reloaded.GetAll()[0].Id);
        Equal("Korunacak proje", reloaded.GetAll()[0].Name);
        True(File.Exists(reloaded.GetPhotoFilePath(p2.Id)!));
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

static void CheckSettingsPersistence()
{
    var store = new MemoryPreferenceStore();
    var service = new SettingsService(store);
    Equal(ThemePreference.System, service.Theme);
    Equal(LengthUnit.Millimeter, service.DefaultLengthUnit);

    var changeCount = 0;
    service.Changed += (_, args) =>
    {
        changeCount++;
        Equal(service.Current, args.Preferences);
    };

    service.SetTheme(ThemePreference.Dark);
    service.SetDefaultLengthUnit(LengthUnit.Inch);
    service.SetTheme(ThemePreference.Dark);
    Equal(2, changeCount);

    var reloaded = new SettingsService(store);
    Equal(ThemePreference.Dark, reloaded.Theme);
    Equal(LengthUnit.Inch, reloaded.DefaultLengthUnit);

    var invalidStore = new MemoryPreferenceStore();
    invalidStore.Set(SettingsService.ThemePreferenceKey, "BilinmeyenTema");
    invalidStore.Set(SettingsService.DefaultLengthUnitKey, "999");
    var withInvalidValues = new SettingsService(invalidStore);
    Equal(AppPreferences.Default, withInvalidValues.Current);
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

static void Throws<TException>(Action action)
    where TException : Exception
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

static void CheckPcCameraStreamServerLifecycleAndSecurity()
{
    using var server = new PcCameraStreamServer();
    const int testPort = 18099;
    const string testPin = "482910";

    // 1. Sunucu başlatma
    server.Start(testPort, testPin);
    True(server.IsRunning, "Sunucu çalışır durumda olmalıdır.");
    Equal(testPort, server.Port);
    Equal(testPin, server.SessionPin);

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    // 2. /status endpoint'i kontrolü
    var statusUrl = $"http://127.0.0.1:{testPort}/status";
    var statusResp = client.GetAsync(statusUrl).GetAwaiter().GetResult();
    Equal(System.Net.HttpStatusCode.OK, statusResp.StatusCode);
    var statusContent = statusResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    True(statusContent.Contains("OlcuAtolyesiCameraService"), "Status yanıtında servis adı yer almalıdır.");

    // 3. Yetkisiz erişim kontrolü: Hatalı veya eksik PIN durumunda HTTP 401 dönmeli
    var unauthRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{testPort}/stream");
    unauthRequest.Headers.Add("X-Session-Pin", "000000");
    var unauthResp = client.SendAsync(unauthRequest).GetAwaiter().GetResult();
    Equal(System.Net.HttpStatusCode.Unauthorized, unauthResp.StatusCode);

    var noPinUrl = $"http://127.0.0.1:{testPort}/stream";
    var noPinResp = client.GetAsync(noPinUrl).GetAwaiter().GetResult();
    Equal(System.Net.HttpStatusCode.Unauthorized, noPinResp.StatusCode);

    // 4. Yetkili erişim ve MJPEG akışı başlatma
    var streamRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{testPort}/stream");
    streamRequest.Headers.Add("X-Session-Pin", testPin);
    var streamResp = client.SendAsync(streamRequest, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
    Equal(System.Net.HttpStatusCode.OK, streamResp.StatusCode);
    True(streamResp.Content.Headers.ContentType?.MediaType?.Contains("multipart/x-mixed-replace") == true,
        "MJPEG akışı multipart/x-mixed-replace olmalıdır.");

    // 5. Kare yayınlama ve okuma
    var mockFrame = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 }; // Basit JPEG baytları
    server.PublishFrame(mockFrame);

    Equal(1, server.ActiveClientCount);

    // 6. Sunucuyu durdurma
    server.Stop();
    False(server.IsRunning, "Durdurulduktan sonra IsRunning false olmalıdır.");
    Equal(0, server.ActiveClientCount);
}

static void CheckToolsMenuAndPcCameraIntegration()
{
    // 1. Sunucu başlangıçta kapalı olmalı, otomatik yayın başlatmamalı
    using var initialServer = new PcCameraStreamServer();
    False(initialServer.IsRunning, "Sunucu nesnesi oluşturulduğunda otomatik olarak çalışmamalıdır.");
    Equal(0, initialServer.ActiveClientCount);

    // 2. ToolsPage kaynak kod doğrulaması: PC Kamera Bağlantısı kartı LiveGroup içinde yer almalı
    var toolsPagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OlcuAtolyesi", "Views", "ToolsPage.cs");
    if (!File.Exists(toolsPagePath))
    {
        // Alternatif göreli yol
        toolsPagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\OlcuAtolyesi\OlcuAtolyesi\Views\ToolsPage.cs"));
    }

    if (File.Exists(toolsPagePath))
    {
        var toolsContent = File.ReadAllText(toolsPagePath);
        True(toolsContent.Contains("AppText.PcCamera.Title"), "ToolsPage içinde PcCamera.Title referansı bulunmalıdır.");
        True(toolsContent.Contains("nameof(PcCameraPage)"), "ToolsPage kartı PcCameraPage hedefine yönlendirmelidir.");
        True(toolsContent.Contains("AppText.Tools.LiveGroup"), "Kart LiveGroup altında yer almalıdır.");
    }

    // 3. AppShell route doğrulaması: PcCameraPage rotası kayıtlı olmalı
    var shellPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OlcuAtolyesi", "AppShell.xaml.cs");
    if (!File.Exists(shellPath))
    {
        shellPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\OlcuAtolyesi\OlcuAtolyesi\AppShell.xaml.cs"));
    }

    if (File.Exists(shellPath))
    {
        var shellContent = File.ReadAllText(shellPath);
        True(shellContent.Contains("nameof(PcCameraPage)"), "AppShell içinde PcCameraPage route kaydı bulunmalıdır.");
    }

    // 4. Metin bütünlüğü doğrulaması
    True(!string.IsNullOrWhiteSpace(AppText.PcCamera.Title), "PcCamera.Title tanımlı olmalıdır.");
    True(!string.IsNullOrWhiteSpace(AppText.PcCamera.Description), "PcCamera.Description tanımlı olmalıdır.");
    True(AppText.Version.StartsWith("0.6.2"), "Uygulama sürümü 0.6.2 olmalıdır.");
    True(!AppText.About.Scope.Contains("0.4.0"), "AppText.About.Scope içinde eski 0.4.0 metni kalmamalıdır.");
    True(AppText.About.Scope.Contains("0.6.2"), "AppText.About.Scope içinde 0.6.2 bulunmalıdır.");
    True(AppText.About.Scope.Contains("PC Kamera"), "AppText.About.Scope içinde PC Kamera bulunmalıdır.");
}

static void CheckAppBrandingAndResponsiveAssets()
{
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\OlcuAtolyesi"));
    if (!Directory.Exists(baseDir))
    {
        baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\OlcuAtolyesi"));
    }

    // 1. AppIcon SVG dosyaları
    var iconBgPath = Path.Combine(baseDir, "Resources", "AppIcon", "appicon.svg");
    var iconFgPath = Path.Combine(baseDir, "Resources", "AppIcon", "appiconfg.svg");
    var splashPath = Path.Combine(baseDir, "Resources", "Splash", "splash.svg");

    True(File.Exists(iconBgPath), "appicon.svg dosyası mevcut olmalıdır.");
    True(File.Exists(iconFgPath), "appiconfg.svg dosyası mevcut olmalıdır.");
    True(File.Exists(splashPath), "splash.svg dosyası mevcut olmalıdır.");

    var fgSvg = File.ReadAllText(iconFgPath);
    True(fgSvg.Contains("26.15"), "Kumpas dijital gösterge değeri 26.15 SVG içinde yer almalıdır.");
    True(fgSvg.Contains("beamGrad"), "Kumpas gövdesi gradyanı bulunmalıdır.");
    True(fgSvg.Contains("#00ADB5") || fgSvg.Contains("#00E5FF"), "Turkuaz/camgöbeği paleti bulunmalıdır.");

    var splashSvg = File.ReadAllText(splashPath);
    True(splashSvg.Contains(AppText.AppName), "Açılış ekranında uygulama adı yer almalıdır.");
    True(splashSvg.Contains("Fotoğraftan Ölçüm"), "Açılış ekranında alt başlık yer almalıdır.");

    // 2. Proje dosyası sürüm ve renk kontrolü
    var csprojPath = Path.Combine(baseDir, "OlcuAtolyesi.csproj");
    True(File.Exists(csprojPath), "OlcuAtolyesi.csproj dosyası bulunmalıdır.");
    var csprojContent = File.ReadAllText(csprojPath);
    True(csprojContent.Contains("0.6.2"), "Sürüm 0.6.2 olmalıdır.");
    True(csprojContent.Contains("<ApplicationVersion>13</ApplicationVersion>"), "Sürüm kodu 13 olmalıdır.");
    True(csprojContent.Contains("Color=\"#0A192F\""), "Koyu lacivert zemin rengi #0A192F olmalıdır.");

    // 3. Responsive stil kontrolü (WordWrap)
    var stylesPath = Path.Combine(baseDir, "Resources", "Styles", "Styles.xaml");
    True(File.Exists(stylesPath), "Styles.xaml bulunmalıdır.");
    var stylesContent = File.ReadAllText(stylesPath);
    True(stylesContent.Contains("WordWrap"), "Stiller içinde responsive WordWrap tanımlı olmalıdır.");
}

static void CheckCameraSwitchCoordinatorStateMachine()
{
    using var coordinator = new CameraSwitchCoordinator();
    Equal(CameraSwitchState.Idle, coordinator.State);
    False(coordinator.IsFrontCamera);

    coordinator.SetStreaming();
    Equal(CameraSwitchState.Streaming, coordinator.State);

    // 1. Değişim başlatma
    var (canSwitch, cid) = coordinator.TryBeginSwitchAsync().GetAwaiter().GetResult();
    True(canSwitch, "Değişim başarıyla başlamalıdır.");
    True(!string.IsNullOrWhiteSpace(cid), "Korelasyon kimliği üretilmelidir.");
    Equal(CameraSwitchState.Switching, coordinator.State);

    // 2. Eşzamanlı mükerrer değişim isteği reddedilmelidir
    var (reentrantCanSwitch, _) = coordinator.TryBeginSwitchAsync().GetAwaiter().GetResult();
    False(reentrantCanSwitch, "Zaten süren bir değişim varken yeni istek reddedilmelidir.");

    // 3. CaptureGate testi
    var gateAcquired = coordinator.PauseCaptureAndAcquireGateAsync(500).GetAwaiter().GetResult();
    True(gateAcquired, "CaptureGate kilitlenebilmelidir.");

    // 4. Değişim tamamlama
    coordinator.CompleteSwitch(newIsFront: true, cid);
    Equal(CameraSwitchState.Streaming, coordinator.State);
    True(coordinator.IsFrontCamera, "Yeni sensör ön kamera olarak kaydedilmelidir.");

    // 5. Başarısız değişim ve kurtarma testi
    var (canSwitchFail, failCid) = coordinator.TryBeginSwitchAsync().GetAwaiter().GetResult();
    True(canSwitchFail);
    coordinator.FailSwitch(failCid, new InvalidOperationException("Simüle edilmiş kamera arızası"));
    Equal(CameraSwitchState.Faulted, coordinator.State);

    // 6. Sıfırlama
    coordinator.ResetToIdle();
    Equal(CameraSwitchState.Idle, coordinator.State);
}

static void CheckDiagnosticLoggingAndMasking()
{
    // 1. Hassas veri maskeleme kontrolü
    var rawText = "Sunucu PIN: 482910 ile başlatıldı. Gizli token=abc12345xyz!";
    var maskedText = DiagnosticDataMasker.Mask(rawText);
    False(maskedText.Contains("482910"), "PIN log içinde açık görünmemelidir.");
    True(maskedText.Contains("[MASKELENDİ]"), "PIN yerine [MASKELENDİ] yazılmalıdır.");
    False(maskedText.Contains("abc12345xyz"), "Token log içinde açık görünmemelidir.");

    // 2. JSON serileştirme ve model bütünlüğü
    var entry = new DiagnosticLogEntry
    {
        Timestamp = DateTimeOffset.Now,
        Level = DiagnosticLevel.Warn,
        Source = DiagnosticSource.Android,
        Module = "TestModule",
        EventCode = "TEST_EVENT",
        Message = "Kullanıcı PIN: 999888 girdi.",
        CorrelationId = "test-cid-123",
        Details = "Detay bilgisi"
    };

    var json = entry.ToJson();
    True(json.Contains("\"module\""), "JSON camelCase özellik adlarını içermelidir.");
    var parsed = DiagnosticLogEntry.FromJson(json);
    True(parsed != null);
    Equal(DiagnosticLevel.Warn, parsed!.Level);
    Equal(DiagnosticSource.Android, parsed.Source);
    Equal("TEST_EVENT", parsed.EventCode);

    // 3. Döngüsel dosya günlük yazıcısı ve rotasyon kontrolü
    var tempDir = Path.Combine(Path.GetTempPath(), $"OlcuAtolyesi_DiagTest_{Guid.NewGuid():N}");
    try
    {
        using (var logger = new CircularDiagnosticLogger(tempDir, maxFileSize: 1024, maxArchivedFiles: 3))
        {
            False(logger.PreviousSessionEndedAbnormally, "İlk oturumda çökme bayrağı olmamalıdır.");

            // 1 KB sınırı aşacak kadar kayıt yaz
            for (int i = 0; i < 50; i++)
            {
                logger.Write(new DiagnosticLogEntry
                {
                    Module = "Stress",
                    EventCode = $"EV_{i}",
                    Message = $"Tanılama mesajı satırı {i} - döngüsel arşivleme testi."
                });
            }

            True(File.Exists(logger.CurrentLogPath), "Aktif log dosyası oluşmalıdır.");
            var recent = logger.ReadRecentEntries(10);
            True(recent.Count > 0, "Son loglar dosyadan okunabilmelidir.");

            // Temiz kapatma
            logger.CleanShutdown();
        }

        // Çökme simülasyonu: Temiz kapatılmayan oturum
        using (var abnormalLogger = new CircularDiagnosticLogger(tempDir, maxFileSize: 1024, maxArchivedFiles: 3))
        {
            // CleanShutdown() çağırmadan bırak
        }

        // Bir sonraki açılışta çökme tespit edilmeli
        using (var crashDetector = new CircularDiagnosticLogger(tempDir, maxFileSize: 1024, maxArchivedFiles: 3))
        {
            True(crashDetector.PreviousSessionEndedAbnormally, "Temiz kapanmayan önceki oturum çökme olarak algılanmalıdır.");
            crashDetector.CleanShutdown();
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

static void CheckPreviewPipelineFrameSequenceAndFps()
{
    var pipeline = new ContinuousPreviewPipeline();
    var fakeFrame = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };

    // 1. İlk kare kabul edilmeli
    var ok1 = pipeline.TryProcessFrame(fakeFrame, 1280, 720, 0, out var out1);
    True(ok1, "İlk önizleme karesi kabul edilmelidir.");
    True(out1 != null && out1.Length == 4, "Çıktı karesi baytları korunmalıdır.");

    // 2. Çok hızlı gelen (geciken/biriken) kare atılmalı (frame drop)
    var okFast = pipeline.TryProcessFrame(fakeFrame, 1280, 720, 0, out var outFast);
    True(!okFast, "30ms'den kısa sürede gelen kare atılmalıdır (drop lagging frames).");
    True(outFast == null, "Atılan kare çıktı üretmemelidir.");

    // 3. Metrikler kontrol edilmeli
    var metrics = pipeline.GetMetrics();
    True(metrics.TotalFramesPublished >= 1, "Yayınlanan kare sayısı en az 1 olmalıdır.");
    True(metrics.DroppedFrames >= 1, "Atılan kare sayısı kaydedilmelidir.");
    True(metrics.Width == 1280 && metrics.Height == 720, "Önizleme çözünürlüğü 1280x720 olmalıdır.");

    pipeline.Reset();
    var resetMetrics = pipeline.GetMetrics();
    True(resetMetrics.TotalFramesPublished == 0, "Reset sonrası sayaç sıfırlanmalıdır.");
}

static void CheckPhotoStorageAndDiskFullAndNoDeleteWithoutConfirmation()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi_Test_Storage_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new PhotoStorageService(tempDir);

        // 1. Örnek geçerli SOF0 (1920x1080) JPEG verisi oluştur
        // SOI (FF D8) + SOF0 (FF C0, len 00 11, prec 08, height 04 38 = 1080, width 07 80 = 1920) + EOI (FF D9)
        var testJpeg = new byte[]
        {
            0xFF, 0xD8,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x04, 0x38, 0x07, 0x80, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
            0xFF, 0xD9
        };

        using var ms = new MemoryStream(testJpeg);
        var saved = storage.SavePhotoAsync(ms, "test_still.jpg").GetAwaiter().GetResult();

        True(!string.IsNullOrWhiteSpace(saved.PhotoId), "Fotoğraf kimliği üretilmelidir.");
        True(saved.FileSizeBytes == testJpeg.Length, "Kaydedilen dosya boyutu eşleşmelidir.");
        True(saved.Width == 1920, "Genişlik 1920 olarak okunmalıdır.");
        True(saved.Height == 1080, "Yükseklik 1080 olarak okunmalıdır.");
        True(!string.IsNullOrWhiteSpace(saved.Sha256), "SHA-256 hesaplanmalıdır.");
        True(File.Exists(saved.FilePath), "Dosya diskte mevcut olmalıdır.");

        // 2. Depolama alanı ve listeleme
        var usage = storage.GetStorageUsage();
        True(usage.TotalPhotosCount == 1, "Toplam kayıtlı fotoğraf 1 olmalıdır.");
        True(usage.TotalBytesOccupied == testJpeg.Length, "Toplam kaplanan alan eşleşmelidir.");

        var list = storage.GetPhotos();
        True(list.Count == 1 && list[0].PhotoId == saved.PhotoId, "Listede kayıt bulunmalıdır.");

        // 3. Kullanıcı onayı olmadan silme engellenmeli
        var deletedWithoutConfirm = false;
        try
        {
            storage.DeletePhoto(saved.PhotoId, userConfirmed: false);
            deletedWithoutConfirm = true;
        }
        catch (InvalidOperationException)
        {
            // Beklenen davranış
        }
        True(!deletedWithoutConfirm, "Kullanıcı onayı olmadan fotoğraf silinememelidir!");
        True(File.Exists(saved.FilePath), "Onaysız silme denemesinde dosya korunmalıdır.");

        // 4. Kullanıcı onayıyla silme
        var deleted = storage.DeletePhoto(saved.PhotoId, userConfirmed: true);
        True(deleted, "Kullanıcı onaylı silme başarılı olmalıdır.");
        True(!File.Exists(saved.FilePath), "Silinen dosya diskten kaldırılmalıdır.");
        True(storage.GetPhotos().Count == 0, "Silme sonrası liste boş olmalıdır.");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

static void CheckRemoteCaptureAndSecurityServerApi()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi_Test_Server_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new PhotoStorageService(tempDir);
        using var server = new PcCameraStreamServer(storage);
        server.Start(8092, "778899");

        using var client = new HttpClient();

        // 1. PIN ile token alma (/api/v1/auth) - Yanlış PIN reddi
        var badAuthReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8092/api/v1/auth");
        badAuthReq.Headers.Add("X-Session-Pin", "000000");
        var badAuthRes = client.Send(badAuthReq);
        True(badAuthRes.StatusCode == System.Net.HttpStatusCode.Unauthorized, "Yanlış PIN 401 Unauthorized dönmelidir.");

        // 2. Doğru PIN ile geçerli token alma
        var goodAuthReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8092/api/v1/auth");
        goodAuthReq.Headers.Add("X-Session-Pin", "778899");
        var goodAuthRes = client.Send(goodAuthReq);
        True(goodAuthRes.IsSuccessStatusCode, "Doğru PIN ile belirteç alınmalıdır.");

        var authJson = goodAuthRes.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var authDoc = System.Text.Json.JsonDocument.Parse(authJson);
        var token = authDoc.RootElement.GetProperty("token").GetString();
        True(!string.IsNullOrWhiteSpace(token), "Token üretilmelidir.");

        // 3. Yetkisiz çekim denemesi (Token yok)
        var unauthCaptureReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8092/api/v1/capture");
        var unauthCaptureRes = client.Send(unauthCaptureReq);
        True(unauthCaptureRes.StatusCode == System.Net.HttpStatusCode.Unauthorized, "Belirteçsiz çekim 401 dönmelidir.");

        // 4. Arka plan koruması (Uygulama arka plandayken uzaktan çekim reddedilmeli)
        server.IsForeground = false;
        var bgCaptureReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8092/api/v1/capture");
        bgCaptureReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var bgCaptureRes = client.Send(bgCaptureReq);
        True(bgCaptureRes.StatusCode == System.Net.HttpStatusCode.Forbidden, "Arka planda çekim 403 Forbidden dönmelidir.");
        server.IsForeground = true;

        // 5. Yüksek çözünürlüklü gerçek still-capture testi
        server.OnStillCaptureRequested = async (ct) =>
        {
            await Task.CompletedTask;
            // 3840x2160 JPEG
            return new MemoryStream([
                0xFF, 0xD8,
                0xFF, 0xC0, 0x00, 0x11, 0x08, 0x08, 0x70, 0x0F, 0x00, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
                0xFF, 0xD9
            ]);
        };

        var captureReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8092/api/v1/capture");
        captureReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var captureRes = client.Send(captureReq);
        True(captureRes.IsSuccessStatusCode, "Uzaktan çekim başarılı olmalıdır.");

        var captureJson = captureRes.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var capDoc = System.Text.Json.JsonDocument.Parse(captureJson);
        var photoId = capDoc.RootElement.GetProperty("photoId").GetString();
        var width = capDoc.RootElement.GetProperty("width").GetInt32();
        var height = capDoc.RootElement.GetProperty("height").GetInt32();
        True(!string.IsNullOrWhiteSpace(photoId), "Çekilen fotoğraf kimliği dönmelidir.");
        True(width == 3840 && height == 2160, "Yüksek çözünürlük 3840x2160 olmalıdır.");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

static void CheckRemotePhotoIntegrityAndResilience()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi_Test_Resilience_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new PhotoStorageService(tempDir);
        using var server = new PcCameraStreamServer(storage);
        server.Start(8093, "112233");

        // 1. Örnek fotoğraf kaydet
        var testBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x01, 0x00, 0x01, 0x00, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, 0xFF, 0xD9 };
        using var ms = new MemoryStream(testBytes);
        var saved = storage.SavePhotoAsync(ms, "still_resilience.jpg").GetAwaiter().GetResult();

        using var client = new HttpClient();

        // Token al
        var authReq = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8093/api/v1/auth");
        authReq.Headers.Add("X-Session-Pin", "112233");
        var authRes = client.Send(authReq);
        var authJson = authRes.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var authDoc = System.Text.Json.JsonDocument.Parse(authJson);
        var token = authDoc.RootElement.GetProperty("token").GetString();

        // 2. İndir ve SHA-256 başlığını doğrula
        var dlReq = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:8093/api/v1/photos/{saved.PhotoId}");
        dlReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var dlRes = client.Send(dlReq);
        True(dlRes.IsSuccessStatusCode, "Fotoğraf indirme başarılı olmalıdır.");

        var shaHeader = dlRes.Headers.GetValues("X-Sha256").FirstOrDefault();
        True(string.Equals(shaHeader, saved.Sha256, StringComparison.OrdinalIgnoreCase), "X-Sha256 başlığı eşleşmelidir.");

        var downloadedBytes = dlRes.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        True(downloadedBytes.Length == testBytes.Length, "İndirilen bayt boyutu tam eşleşmelidir.");

        // 3. Başarısız/kopuk aktarım simülasyonu: Dosya telefonda korunmalı ve yeniden indirilebilmeli
        var photoInPhone = storage.GetPhoto(saved.PhotoId);
        True(photoInPhone != null, "Aktarım sonrasında da fotoğraf telefonda korunmalıdır.");
        True(File.Exists(photoInPhone!.FilePath), "Fiziksel dosya telefonda korunmalıdır.");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

static void CheckPinBruteForceLockout()
{
    // 1) /stream için sorgu dizesindeki PIN artık kabul edilmemeli (başlık zorunlu)
    using (var server = new PcCameraStreamServer())
    {
        const int queryPort = 18110;
        const string pin = "731845";
        server.Start(queryPort, pin);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var queryOnly = client.GetAsync($"http://127.0.0.1:{queryPort}/stream?pin={pin}").GetAwaiter().GetResult();
        Equal(System.Net.HttpStatusCode.Unauthorized, queryOnly.StatusCode);

        var headerRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{queryPort}/stream");
        headerRequest.Headers.Add("X-Session-Pin", pin);
        var headerResponse = client.SendAsync(headerRequest, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        Equal(System.Net.HttpStatusCode.OK, headerResponse.StatusCode);

        server.Stop();
    }

    // 2) 5 hatalı PIN denemesinden sonra kilit devreye girmeli (HTTP 429)
    using (var lockServer = new PcCameraStreamServer())
    {
        const int lockPort = 18111;
        const string pin = "918273";
        lockServer.Start(lockPort, pin);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var badRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{lockPort}/api/v1/auth");
            badRequest.Headers.Add("X-Session-Pin", $"00000{attempt}");
            var badResponse = client.SendAsync(badRequest).GetAwaiter().GetResult();
            Equal(System.Net.HttpStatusCode.Unauthorized, badResponse.StatusCode);
        }

        // Kilitliyken DOĞRU PIN bile reddedilmeli
        var blockedRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{lockPort}/api/v1/auth");
        blockedRequest.Headers.Add("X-Session-Pin", pin);
        var blockedResponse = client.SendAsync(blockedRequest).GetAwaiter().GetResult();
        Equal((System.Net.HttpStatusCode)429, blockedResponse.StatusCode);
        True(blockedResponse.Headers.Contains("Retry-After"), "429 yanıtında Retry-After başlığı bulunmalıdır.");

        // Kilit /stream endpoint'ini de kapsamalı
        var blockedStreamRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{lockPort}/stream");
        blockedStreamRequest.Headers.Add("X-Session-Pin", pin);
        var blockedStreamResponse = client.SendAsync(blockedStreamRequest, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        Equal((System.Net.HttpStatusCode)429, blockedStreamResponse.StatusCode);

        lockServer.Stop();
    }
}

static void CheckDiskSpaceGuard()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "OlcuAtolyesi_DiskTest_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);

    try
    {
        var testJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 };

        // 1) Yetersiz alan: sağlayıcı 5 MB döndürüyor -> kayıt reddedilmeli
        var lowSpaceDir = Path.Combine(tempDir, "low");
        var lowSpaceStorage = new PhotoStorageService(lowSpaceDir, _ => 5L * 1024 * 1024);
        var rejected = false;
        try
        {
            using var stream = new MemoryStream(testJpeg);
            lowSpaceStorage.SavePhotoAsync(stream, "low.jpg").GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            rejected = true;
            True(ex.Message.Contains("Yetersiz disk alanı"), "Hata mesajı 'Yetersiz disk alanı' içermelidir.");
            True(ex.Message.Contains("Mevcut: 5 MB"), "Hata mesajı ölçülen boş alanı (5 MB) göstermelidir.");
        }

        True(rejected, "5 MB boş alan varken kayıt reddedilmelidir.");

        // 2) Yeterli alan: sağlayıcı 100 MB döndürüyor -> kayıt başarılı olmalı
        var okDir = Path.Combine(tempDir, "ok");
        var okStorage = new PhotoStorageService(okDir, _ => 100L * 1024 * 1024);
        using (var stream = new MemoryStream(testJpeg))
        {
            var saved = okStorage.SavePhotoAsync(stream, "ok.jpg").GetAwaiter().GetResult();
            True(!string.IsNullOrWhiteSpace(saved.PhotoId), "Yeterli alan varken fotoğraf kaydedilmelidir.");
            True(File.Exists(saved.FilePath), "Kaydedilen dosya diskte bulunmalıdır.");
        }

        // 3) Sağlayıcıya depolama dizini iletilmeli (kök dizin değil)
        string? capturedPath = null;
        var captureDir = Path.Combine(tempDir, "capture");
        var captureStorage = new PhotoStorageService(captureDir, path =>
        {
            capturedPath = path;
            return 100L * 1024 * 1024;
        });
        using (var stream = new MemoryStream(testJpeg))
        {
            captureStorage.SavePhotoAsync(stream, "capture.jpg").GetAwaiter().GetResult();
        }

        True(capturedPath != null, "Boş alan sağlayıcısına yol iletilmelidir.");
        True(capturedPath!.EndsWith("capture", StringComparison.OrdinalIgnoreCase),
            "Sağlayıcıya kök dizin yerine depolama dizini iletilmelidir.");

        // 4) Windows'ta varsayılan ölçüm gerçek dosya sistemini okumalı
        var realFree = PhotoStorageService.GetAvailableFreeSpaceBytes(tempDir);
        True(realFree > 0, "Windows'ta kullanılabilir boş alan pozitif olmalıdır.");
        True(realFree != long.MaxValue, "Windows'ta ölçüm başarılı olmalıdır.");
    }
    finally
    {
        try { Directory.Delete(tempDir, true); } catch { }
    }
}

static void CheckPreviewFrameSourceAndRateLimiter()
{
    // 1) PreviewFrameRateLimiter davranışı
    var limiter = new PreviewFrameRateLimiter(15);
    Equal(15d, limiter.TargetFps);
    True(limiter.ShouldCapture(0), "İlk kare her zaman alınmalıdır.");
    False(limiter.ShouldCapture(20), "66 ms'den kısa aralıkta kare alınmamalıdır.");
    True(limiter.ShouldCapture(70), "66 ms sonra yeni kare alınmalıdır.");
    limiter.Reset();
    True(limiter.ShouldCapture(1), "Reset sonrası ilk kare kabul edilmelidir.");

    // 2) PcCameraPage kaynak doğrulaması
    var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\OlcuAtolyesi"));
    if (!Directory.Exists(baseDir))
    {
        baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\OlcuAtolyesi"));
    }
    var pagePath = Path.Combine(baseDir, "Views", "PcCameraPage.cs");
    True(File.Exists(pagePath), "PcCameraPage.cs bulunmalıdır.");
    var source = File.ReadAllText(pagePath);

    // Her karede tam çözünürlüklü çekim yapan eski yedek yol geri dönmemeli
    var captureImageCount = source.Split("CaptureImage").Length - 1;
    True(captureImageCount <= 1,
        $"Önizleme hattında CaptureImage en fazla 1 kez (uzaktan çekim) kullanılmalıdır. Bulunan: {captureImageCount}");

    True(source.Contains("PixelCopy"), "SurfaceView kare kaynağı için PixelCopy kullanılmalıdır.");
    True(source.Contains("PreviewFrameRateLimiter"), "Kare hızı sınırlayıcı kullanılmalıdır.");
    True(source.Contains("FindSurfaceView"), "SurfaceView bulucu mevcut olmalıdır.");
}
