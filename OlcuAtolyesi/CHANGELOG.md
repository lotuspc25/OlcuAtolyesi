# Değişiklik Günlüğü (CHANGELOG.md)

Bu dosya **Ölçü Atölyesi** projesinin sürüm geçmişini ve her sürümde yapılan başlıca geliştirmeleri içerir.

---

## [v0.6.2] — 2026-11-10 — Akıcı Canlı Önizleme Hattı

### Düzeltilen Hatalar
- **Canlı önizleme 1 FPS / siyah ekran (kritik):** Cihazda CameraX `SurfaceView` kullandığı için
  `FindTextureView` boş dönüyor, önizleme döngüsü her karede `CaptureImage` yedeğine düşüp **tam
  çözünürlüklü (4080×3060) fotoğraf** çekiyordu. Kare başına ~1 saniye gecikme ve UI iş
  parçacığında 12 MP JPEG sıkıştırma nedeniyle önizleme 1 FPS'e düşüyor, bazen hiç kare
  gitmiyordu.
- Kare kaynağı `SurfaceView` + **PixelCopy** olarak değiştirildi; JPEG sıkıştırma arka plan
  iş parçacığına taşındı.
- Her karede tam çözünürlüklü çekim yapan yedek yol **tamamen kaldırıldı**; geri dönmesini
  engelleyen otomatik test (Test 25) eklendi.
- Kare hızı `PreviewFrameRateLimiter` ile 15 FPS'e sınırlandı; arayüzde yüzey kopyalama hata
  sayacı gösterilir.

### Testler
- Test 25: kare hızı sınırlayıcı davranışı + `PcCameraPage` kaynak koruması (CaptureImage sayısı ≤ 1,
  PixelCopy/FindSurfaceView/PreviewFrameRateLimiter varlığı).

---

## [v0.6.1] — 2026-11-10 — Android Disk Alanı Düzeltmesi

### Düzeltilen Hatalar
- **"Yetersiz disk alanı" hatası (kritik):** Android'de boş alan `Path.GetPathRoot()` üzerinden
  `/` **sistem bölümünden** ölçülüyordu; bu bölüm küçük ve salt okunur olduğu için cihazda bol yer
  olmasına rağmen yüksek çözünürlüklü çekim reddediliyordu. Ölçüm artık Android'de
  `Java.IO.File.UsableSpace` ile doğrudan **veri bölümünden** yapılır.
- Disk alanı kontrolü test edilebilir hale getirildi: `PhotoStorageService` isteğe bağlı
  `Func<string, long>` boş alan sağlayıcısı kabul eder; varsayılan davranış değişmedi.
- Hata mesajı artık gereken ve mevcut alanı MB cinsinden açıkça gösterir.

### Sürüm ve Testler
- `ApplicationDisplayVersion` `0.6.1`, `ApplicationVersion` `12`.
- Test 24 (`CheckDiskSpaceGuard`): yetersiz alanda red, yeterli alanda kayıt, sağlayıcıya depolama
  dizininin iletilmesi ve Windows'ta gerçek ölçüm doğrulanır.

---

## [v0.6.0] — 2026-11-10 — PIN Kaba Kuvvet Koruması ve Yerel Yayın Sertleştirmesi

### Eklenenler ve Güvenlik
- **PIN deneme sınırı (kaba kuvvet koruması):** Aynı istemci IP'sinden art arda 5 hatalı PIN
  denemesinde oturum 60 saniye kilitlenir; kilitliyken doğru PIN de reddedilir. Yanıt `HTTP 429` +
  `Retry-After` başlığıdır. Sayaç, başarılı kimlik doğrulamada sıfırlanır ve `Stop()` ile temizlenir.
- **PIN sorgu dizesinden kaldırıldı:** `/stream`, `/video`, `/mjpeg` artık PIN'i yalnızca
  `X-Session-Pin` başlığıyla kabul eder. Böylece PIN adres çubuğuna, tarayıcı geçmişine ve ara
  sunucu günlüklerine düşmez. `GET /logs` geriye uyumluluk için sorgu dizesi PIN'ini kabul etmeye
  devam eder (PC tanılama konsolu v0.5.0.5 bu yolu kullanıyor).
- **CORS daraltma:** Joker (`*`) `Access-Control-Allow-*` başlıkları kaldırıldı.
- **Uyumluluk:** PC uygulaması (v0.5.0.5) `CameraStreamClient` üzerinden `X-Session-Pin` başlığını
  zaten gönderdiği için akış bağlantısı bozulmaz; PC tarafında değişiklik gerekmez.

### Sürüm ve Testler
- `ApplicationDisplayVersion` `0.6.0`, `ApplicationVersion` `11`.
- Otomatik testler: `CheckPinBruteForceLockout` (Test 23) eklendi; `/stream` sorgu dizesi PIN'i
  reddi, başlıkla kabulü, 5 hatalı denemede 429 kilidi ve kilidin `/stream`'i kapsaması doğrulanır.

---

## [v0.5.0.5] — 2026-11-09 — Akıcı Canlı Önizleme ve Telefona Kayıpsız Orijinal Fotoğraf Kaydı

### Eklenenler ve Geliştirmeler
- **Akıcı Canlı Önizleme Hattı (`ContinuousPreviewPipeline`):**
  - Tek kamera oturumu üzerinde çalışan, eski kareleri kuyrukta bekletmeden düşüren (`DropOldest`) düşük gecikmeli önizleme döngüsü.
  - 1000ms yuvarlanan pencerede gerçek FPS ve kareler arası gecikme (ms) hesaplaması.
- **Telefonda Güvenli Fotoğraf Depolama (`PhotoStorageService`):**
  - Önizleme akışından bağımsız, telefon donanımının desteklediği yüksek çözünürlükte gerçek fotoğraf çekimi (`OnStillCaptureRequested`).
  - Telefonun özel uygulama depolama alanına (`Captures/`) yeniden JPEG kodlaması yapmadan kayıpsız dosya yazımı.
  - Cihazda en az 20MB boş alan kontrolü ve her fotoğraf için SHA-256 bütünlük özeti hesaplama.
  - Kullanıcı açık onayı olmadan fotoğrafların silinmesi engellendi (`DeletePhoto(..., userConfirmed: true)`).
- **Güvenli Uzaktan API (`PcCameraStreamServer`):**
  - URL query parametresinde PIN taşınması kaldırıldı; `POST /api/v1/auth` ile kısa ömürlü Bearer token takası sağlandı.
  - `POST /api/v1/capture`: Uzaktan donanımsal fotoğraf çekimi.
  - `GET /api/v1/photos`: Kayıtlı fotoğrafları ve depolama alanı kullanımını sorgulama.
  - `GET /api/v1/photos/{id}`: SHA-256 başlıklı dosya akışı.
  - Arka plan koruması: Uygulama arka plandayken (`IsForeground == false`) uzaktan çekim istekleri 403 ile engellenir.
- **Kamera Değiştirme Koruması (SM-S711B):**
  - Canlı yayın süresince donanımsal HAL çökmesini önlemek için kamera değiştirme butonu kilitlendi ("Yayın sırasında kilitli"); varsayılan arka kamera olarak ayarlandı.
- **Arayüz:**
  - `PcCameraPage` içine "Telefonda Saklanan Ölçüm Fotoğrafları" kartı ve "Fotoğrafları İncele" butonu eklendi.
- **Sürüm ve Testler:**
  - `ApplicationDisplayVersion` `0.5.0.5`, `ApplicationVersion` `10`.
  - Otomatik testler: Test 19-22 eklendi, 22/22 test başarılı.

---

## [v0.5.0.4] — 2026-11-09 — Kamera Değiştirme Güvenliği ve Merkezi Tanılama Altyapısı

### Kritik Hata Düzeltmeleri
- **Android Kamera Sensör Değişimi Çökmesi (Samsung SM-S711B, Android 16):**
  - Canlı MJPEG yayını sırasında ön/arka kamera değiştirildiğinde oluşan ölümcül HAL çöküşü kökten çözüldü.
  - `CameraSwitchCoordinator` durum makinesi (`Idle`, `Streaming`, `Switching`, `Faulted`) geliştirildi.
  - Eşzamanlı re-entrancy (çift tıklama) engellendi; arka plan `CaptureImage` istekleri `_captureGate` ile güvenle duraklatıldı.
  - Android 16 Camera2 donanımının oturumu temizlemesi için 300 ms HAL dinlendirme gecikmesi (`SettleHardwareDelayAsync`) eklendi.
  - `MainThread.InvokeOnMainThreadAsync` ile UI iş parçacığı garantisi ve sıfır çökme try/catch bloğu sağlandı.
  - Değişim anında HTTP MJPEG akışının kopmadan devam etmesi sağlandı.

### Eklenenler ve Geliştirmeler
- **Kalıcı Döngüsel Tanılama Altyapısı (`OlcuAtolyesi.Diagnostics`):**
  - `CircularDiagnosticLogger`: 2MB dosya sınırı, 5 arşiv dosyası (10MB maks döngüsel kayıt) ve `session_active.marker` çökme algılama işaretçisi.
  - `DiagnosticDataMasker`: 6 haneli PIN eşleştirme kodlarını (`[MASKELENDİ]`) ve token/anahtarları (`[GİZLENDİ]`) Türkçe/kültür bağımsız maskeleme.
  - `DiagnosticLogHub`: Bellek içi 1500 kayıt tamponu ve tekil dağıtıcı.
  - `PcCameraStreamServer`: PIN korumalı `GET /logs?pin={pin}&limit=200` endpoint'i eklendi (yetkisiz istekler 401 ile reddedilir).
  - `MainActivity.cs`: `AndroidEnvironment.UnhandledExceptionRaiser` ve `AppDomain.UnhandledException` yakalama kancaları.
- **Sürüm Güncellemesi:**
  - `ApplicationDisplayVersion` `0.5.0.4` (kod: 9) olarak güncellendi.
- **Otomatik Testler (`Tests/LogicChecks`):**
  - Test 17 (Kamera Sensör Değişim Durum Makinesi) ve Test 18 (Tanılama Günlüğü ve PIN Maskeleme) eklendi; 18/18 test başarıyla tamamlandı.

---

## [v0.5.0.3] — 2026-11-09 — Uygulama İkonu, Açılış Ekranı ve Responsive Tasarım

### Eklenenler ve Geliştirmeler
- **Kumpas Temalı Kurumsal İkon (`appicon.svg`, `appiconfg.svg`):**
  - Dijital kumpas (sabit/hareketli çeneler, verniyer çizgileri, LCD ekranda "26.15"), optik vizör reticle çemberleri ve R1/R2 lazer referans noktaları içeren modern vektör adaptive icon tasarımı.
  - Koyu lacivert (`#0A192F`), antrasit (`#1E293B`) ve turkuaz/camgöbeği (`#00ADB5` / `#00E5FF`) renk paleti uygulandı.
- **Vektörel Açılış Ekranı (`splash.svg`):**
  - Android 12+ sistem splash standartlarıyla uyumlu merkezi kumpas amblemi ve kurumsal alt başlık ("Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm").
- **Tam Responsive Tasarım & Ekran Uyumu:**
  - `BaseLabelStyle` ve `BaseButtonStyle` içine `LineBreakMode="WordWrap"` tanımlandı; resim içine metin gömülmesi önlendi.
  - Küçük ekranlı (360-390 dp) telefonlarda yatay buton taşmalarını önlemek için `PcCameraPage` içerisindeki buton çubuğu `FlexLayout` (Wrap) yapısına geçirildi.
  - Sayfa sarmalayıcısı dikey kaydırma ve yatay taşma engeliyle güçlendirildi.
- **Sürüm Güncellemesi:**
  - `ApplicationDisplayVersion` `0.5.0.3` (kod: 8) olarak güncellendi.
- **Otomatik Testler (`Tests/LogicChecks`):**
  - `CheckAppBrandingAndResponsiveAssets` eklendi; 16/16 test başarıyla tamamlandı.

---

## [v0.5.0.1] — 2026-11-08 — PC Kamera Bağlantısı Menü ve Navigasyon Düzeltmesi

### Düzeltilenler ve Eklenenler
- **Tam Bağımsız Gömülü APK ve Fast Deployment İptali (`OlcuAtolyesi.csproj`, `MainActivity.cs`):**
  - .NET Android varsayılan Debug modunda derlenen APK'ların içerisine assembly'leri (`.dll`) gömmemesi ve cihazda kalan eski fast-deployment önbelleklerini çalıştırması sorunu kökünden çözüldü.
  - `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` ve `<AndroidEnableFastDeployment>false</AndroidEnableFastDeployment>` eklenerek tüm C# kütüphaneleri (299 adet) doğrudan APK içine (`lib/arm64-v8a/lib_*.dll.so`) gömüldü (APK boyutu ~13 MB'tan ~49.6 MB'a ulaştı).
  - `MainActivity.cs` içerisine açılışta eski fast-deployment kalıntılarını (`files/.__override__`) temizleyen güvenli rutin eklendi.
- **Hakkında Ekranı Metin Düzeltmesi (`AppText.cs`):**
  - `AppText.About.Scope` içerisindeki eski sabit "v0.4.0" ifadesi güncellenerek "v0.5.0.1" ve PC Kamera Bağlantısı bilgileri eklendi.
  - `AppText.PrototypeNote` metni güncellendi.
- **Araçlar Menüsü Entegrasyonu (`ToolsPage.cs`):**
  - "Araçlar" sekmesindeki "Canlı araçlar" grubuna en başa `PC Kamera Bağlantısı` aracı eklendi (Başlık: `PC Kamera Bağlantısı`, Açıklama: `Telefon kamerasını Wi-Fi üzerinden Ölçü Atölyesi PC uygulamasına bağla.`, İkon: `camera.png`, Durum: `Aktif`).
  - Kart tıklandığında `PcCameraPage` sayfasına yönlendirilir; geri dönüşte `ToolsPage` sayfasına sorunsuz dönülür.
  - Mevcut `CameraMeasurePage` ("Kamerayla mesafe (Demo)") ve diğer tüm araçlar eksiksiz korundu.
- **Sensör Butonu Metin Güncellemesi (`PcCameraPage.cs`):**
  - Kamera sensörü değiştirme düğmesi aktif sensör durumunu belirtecek şekilde `Sensör: Arka Kamera` / `Sensör: Ön Kamera` olarak güncellendi.
- **Sürüm ve Paket Yükseltmesi (`OlcuAtolyesi.csproj`, `AppText.cs`):**
  - Gerçek cihaza mevcut kurulum silinmeden doğrudan güncelleme yapılabilmesi için `ApplicationVersion` `6` -> `7`, `ApplicationDisplayVersion` `0.5.0.1` olarak artırıldı. Paket kimliği (`com.companyname.olcuatolyesi`) ve debug imzalama korundu.
- **Yeni Otomatik Mantık Kontrolleri (`Tests/LogicChecks`):**
  - `CheckToolsMenuAndPcCameraIntegration` testi genişletildi: Hakkında ekranı metinlerinin v0.4.0 içermediği, v0.5.0.1 ve PC Kamera ifadelerinin yer aldığı doğrulandı (`15/15` kontrol başarılı).

---

## [v0.5.0] — 2026-11-08 — Bağımsız Android Kamera Servisi ve WPF Canlı Önizleme

### Eklenenler
- **Bağımsız Yerel Ağ Kamera Yayını (`PcCameraStreamServer`):**
  - scrcpy ve ADB gerektirmeyen, doğrudan telefon üzerinde çalışan yerel HTTP MJPEG sunucusu.
  - Sadece yerel ağda (Wi-Fi) çalışır; internet veya harici bulut bağımlılığı bulunmaz.
  - Rastgele 6 haneli geçici PIN kodu (`SessionPin`) ile oturum güvenliği ve eşleştirme mekanizması.
  - Yetkisiz istemcileri `401 Unauthorized` ile derhal reddetme.
  - Düşük gecikme (low-latency) için `DropOldest` tampon politikası (eski kareleri biriktirmeme).
- **PC Kamera Bağlantısı Sayfası (`PcCameraPage`):**
  - Cihazın yerel IP adresi (`NetworkHelper.GetLocalIpAddress`), yayın portu (`8088`) ve 6 haneli PIN gösterimi.
  - Tek tıkla "PIN Yenile" ve "Yayını Başlat / Durdur" kontrolleri.
  - Ön Kamera / Arka Kamera seçimi.
  - Bağlı PC istemci IP adresi ve aktif istemci sayısı gösterimi.
  - Uygulama arka plana geçtiğinde (`Window.Stopped`) kamera ve sunucunun anında kapatılması ile gizlilik/güvenlik koruması.
- **Otomatik Testler (`Tests/LogicChecks`):**
  - Sunucu yaşam döngüsü, PIN eşleştirmesi, 401 yetkisiz erişim kontrolü, MJPEG akış başlıkları ve kare yayınlama testi eklendi (`14/14` başarılı).

---

## [v0.4.0] — 2026-11-08 — Kalıcı Proje ve Ölçüm Kayıt Sistemi

### Eklenenler
- **Cihaz İçi Kalıcı Proje Depolama (`DemoProjectService`):** Projeler artık geçici bellek yerine uygulama veri dizininde (`FileSystem.AppDataDirectory/projects/<projectId:N>/`) her projeye özel izole alt klasörlerde saklanır.
- **Kalıcı Fotoğraf Saklama (`photo.jpg`):** Fotoğraf ölçümü kaydedildiğinde geçici önbellekteki fotoğraf, projenin kendi kalıcı klasörüne atomik olarak kopyalanır (`CopyPhotoToPersistentStorageLocked`).
- **Tam Ölçüm Durumu Kaydı (`DemoProject`):** Her proje için fotoğraf boyutu (`PhotoSize`), `R1–R2` referans segmenti (`ReferenceSegment`), bilinen gerçek referans uzunluğu (`ReferenceLength`), seçili birim (`DisplayUnit`) ve `M1–M2` ölçüm noktaları (`ProjectMeasurement.ImageSegment`) kaydedilir.
- **Kaydedilmiş Projeyi Yeniden Açma ve Düzenleme (`PhotoMeasurePage`, `PhotoMeasureViewModel.LoadSavedState`):** Projeler listesi ve proje ayrıntı ekranına eklenen **"Ölçümü aç / düzenle"** düğmesi ile kayıtlı fotoğraf ve `R1/R2`, `M1/M2` noktaları orijinal koordinatlarında yeniden açılır; noktalar veya referans düzenlenip **"Değişiklikleri kaydet"** (`UpdatePhotoMeasurementProject`) ile güncellenebilir.
- **Atomik Yazma ve Yedekli Kurtarma (`.tmp` + `.bak`):** Kayıt işlemi önce `project.json.tmp` dosyasına yazılıp diske flush edilir (`Flush(flushToDisk: true)`), mevcut sağlam kayıt `project.json.bak` olarak yedeklenir ve ardından `project.json` güncellenir.
- **Bozuk Veri Dayanıklılığı:** Açılışta bozuk `project.json` tespit edilirse otomatik olarak `project.json.bak` yedeğinden geri yüklenir; tamamen bozuk kayıtlar uygulamayı çökertmeden atlanır (`CorruptedEntryCount`).
- **İzole Proje Silme:** Bir proje silindiğinde yalnızca ilgili `projects/<projectId:N>/` klasörü ve içindeki fotoğraf/JSON dosyaları temizlenir; diğer projeler etkilenmez.
- **Yeni Otomatik Testler (`Tests/LogicChecks`):** Kalıcı kayıt, fotoğraf kopyalama, yeniden açma, düzenleme/güncelleme, yazma hatası geri dönüşü, bozuk JSON kurtarma ve izole silme senaryoları eklendi (`13/13` başarılı).

---

## [v0.3.0] — 2026-11-08 — Gerçek Fotoğrafta Referanslı Ölçüm

### Eklenenler ve Düzeltilenler
- **Gerçek Fotoğraf Üzerinde R1–R2 Referans ve M1–M2 Ölçümü:** Kamera veya galeriden alınan gerçek fotoğrafların `PhotoMeasurePage` ve `PhotoPlateDrawable` üzerinde doğrudan açılması ve ölçülmesi sağlandı.
- **Manuel Referans Sistemi (`R1`, `R2`):** Referans noktalarını sürükleyerek veya düğmeyle seçerek belirleme; `mm`, `cm`, `m`, `inç` birimlerinde pozitif referans uzunluğu tanımlama ve referans değiştiğinde ölçüm sonucunu otomatik yeniden hesaplama.
- **Koordinat ve Görüntü Sistemi (`ImageViewportTransform`):** Fotoğrafın ekrandan tamamen kaybolmasını önleyen `ClampToViewport` sınırlandırması eklendi.
- **Hata Düzeltmeleri:**
  - İki parmakla yakınlaştırma (`OnPinchUpdated`) sırasında ölçeğin üstel patlaması ve parmak çakışması düzeltildi (`CancelLastUndoCapture`).
  - Birim değiştirildiğinde referans ölçeğinin ve ölçüm sonucunun tutarsız kalması düzeltildi.
  - Geri alma (`Undo`) yığını 30 adım ile sınırlandırıldı.

---

## [v0.2.0] — Önceki Sürüm — Android Gerçek Fotoğraf Edinme

- Android kamera (`CommunityToolkit.Maui.Camera`) ve sistem fotoğraf seçicisi (`MediaPicker`) ile fotoğraf alma, EXIF yön düzeltmesi ve en fazla 2048 piksele güvenli küçültme (`TemporaryPhoto`).
- Dört sekmeli Türkçe arayüz, ekran cetveli demosu, bağlantı plakası demosu ve ayarlar.
