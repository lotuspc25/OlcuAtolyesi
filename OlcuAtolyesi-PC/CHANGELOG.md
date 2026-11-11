# Değişiklik Günlüğü (CHANGELOG.md)

Bu dosya **Ölçü Atölyesi PC** (`D:\proje\OlcuAtolyesi-PC`) Windows masaüstü uygulamasının sürüm geçmişini içerir.

---

## [v0.6.9] — 2026-11-11 — Linux Debian ve Windows Çapraz Masaüstü Desteği (Avalonia UI)

### Eklenenler
- **Çapraz Platform Masaüstü Uygulaması (`OlcuAtolyesi.Desktop`):** Avalonia UI 12 ve .NET 10 ile Linux (Debian 10/11/12, Ubuntu vb.) ve Windows üzerinde yerel pencere olarak çalışan responsive masaüstü istemcisi eklendi.
- **Linux Başlatıcı Betikleri (`Calistir.sh` & `Calistir-Linux.sh`):** Linux ortamında `dotnet` SDK kontrolü yapan, eksikse kullanıcı dizinine otomatik yükleyen ve uygulamayı doğrudan başlatan tek tık kabuk betikleri.
- **Linux x64 Bağımsız Paketleme (`Paketle-Linux.bat`):** Windows üzerinden Linux için `self-contained` tek dosya (single-file binary) üreten dağıtım betiği.
- **Linux Debian ve Windows Rehber Güncellemeleri:** Kök `README.md` ve PC `README.md` dosyalarına Debian terminalinden GitHub klonlama ve çalıştırma yönergeleri eklendi.

---

## [v0.6.8] — 2026-11-11 — GitHub İndirme, Dağıtım ve Otomatik Başlatıcı Rehberi

### Eklenenler
- **Doğrudan Başlatıcı Betiği (`Calistir.bat`):** GitHub'dan ZIP olarak indiren veya klonlayan kullanıcıların sisteminde kurulu .NET 10 SDK ile hiçbir karmaşık komut girmeden uygulamayı tek tıkla başlatmasını sağlayan dinamik yollu (`%~dp0`) Türkçe batch başlatıcı.
- **Taşınabilir Bağımsız Paket Üretici (`Paketle.bat`):** Başka bir bilgisayara veya sanal makineye aktarmak üzere .NET SDK gerektirmeyen bağımsız (Self-Contained) tek dosya masaüstü paketini (`Publish/OlcuAtolyesi.PC.exe`) oluşturan betik.
- **GitHub Actions CI/CD Dağıtım Akışı (`.github/workflows/build-and-release.yml`):** Kod değişikliklerinde otomatik derleme ve 54 birim/entegrasyon testini çalıştıran, etiketleme (`v*`) veya manuel tetikleme ile bağımsız `.zip` paketini derleyip GitHub Releases altında yayınlayan sürekli entegrasyon iş akışı.
- **Kapsamlı README.md Hızlı Başlangıç Rehberi:** Son kullanıcılar için GitHub Releases'tan kurulumsuz çalıştırma, kaynak koddan `Calistir.bat` ile tek tıkla çalıştırma ve terminal komutlarını içeren adım adım Türkçe kılavuz eklendi; sabit dosya yolları göreceli yollarla güncellendi.

---

## [v0.6.7] — 2026-11-11 — Sade Ana Pencere, Cetveller ve Ekran Kalibrasyonu

### Eklenenler
- **Sadeleştirilmiş Ana Pencere Arayüzü:** Eski karmaşık butonlar ana pencereden kaldırıldı. Büyük "ÖLÇÜM PENCERESİ (F2)" butonu, hızlı hazır şablon referans seçimi ComboBox'ı, referans uzunluk girişi ve birim seçimi, "Cetvelleri Göster" onay kutusu ve dinamik alt bilgi çubuğu ile profesyonel, temiz bir görünüm sağlandı.
- **Referans Nesne Kütüphanesi (`ReferenceObjectLibrary`):** Kredi kartı, 1 TL, 50 kuruş madeni para, A4 kağıt gibi standart nesnelerin ölçüleri tanımlandı; tek tıkla referans atama desteği eklendi.
- **Dinamik Cetvel Taksimat Sistemi (`MeasurementCanvas`):**
  - **A Cetveli (Nesne Cetveli):** Fotoğrafın üst ve sol kenarında nesnenin gerçek boyutunu (mm) veya referans yoksa piksel değerlerini gösteren akıllı taksimatlı cetvel.
  - **B Cetveli (Ekran Cetveli):** Kalibre edilmiş monitörlerde ekranın sağ-alt köşesinde fiziksel milimetre cetveli. Monitör kalibre edilmemişse asla çizilmez.
- **Ekran Kalibrasyonu Penceresi (`ScreenCalibrationWindow`):** Kullanıcının ekrana fiziksel bir cetvel veya kredi kartı tutarak iki çizgi arasındaki mesafeyi girmesini sağlayan etkileşimli kalibrasyon aracı. Ayarlar `settings.json` içinde kalıcı olarak saklanır.
- **Tuval Sağ Tık Menüsü (ContextMenu):** Tuval üzerinde sağ tıklanarak Ölçüm Penceresini Açma (F2), Cetvelleri Göster/Gizle, Ekran Kalibrasyonu ve Görünümü Sığdır (F) işlemlerine anında erişim sağlandı.
- **Klavye Kısayolları (Pencere Seviyesi):** F2 ile Ölçüm Penceresi, F ile sığdırma, L ile büyüteç açma/kapama, 2/4/8 ile büyüteç oranı, Ctrl+Sol/Sağ ile döndürme, Ctrl+H/V ile aynalama kısayolları ana pencereye tanımlandı.

### Testler
- Test 51 (`CheckRulerTickCalculator`): mm ve px taksimat hesaplamaları ve alt bölme sayısı doğrulanır.
- Test 52 (`CheckReferenceObjectLibrary`): Referans nesneleri listesi ve `FindByName` metodu doğrulanır.
- Test 53 (`CheckScreenCalibration`): Ölçülen DIP ve mm'den kalibrasyon üretimi, birim dönüşümleri ve varsayılan 96 DPI doğrulanır.
- Test 54 (`CheckSimplifiedMainWindowSource`): Ana pencere kaynak kodunda eski butonların olmaması, yeni kontrollerin, cetvel ve kalibrasyon bağlamalarının eksiksiz varlığı denetlenir.

---

## [v0.6.6] — 2026-11-11 — İkonlar ve Pencere Ayrımı

### Eklenenler
- **Sekmelerde ve düğmelerde ikonlar:** Windows `Segoe Fluent Icons` fontu kullanıldı (Windows 10'da
  `Segoe MDL2 Assets`'a düşer). Kodlar görsel doğrulamadan geçirildi.
- **Sekmeler ayrı pencereye taşındı:** `Görüntü / Ölçüm / Proje / DXF` sekmeleri artık yalnızca
  Ölçüm Çalışma Alanı penceresinde. Ana pencere v0.6.4 düzenine döndü.
- Ortak `IconGlyph` stili eklendi; tüm ikonlar tek yerden yönetilir.

### Düzeltmeler
- Ana pencere canvas'ında `DisplaySettings` ve `ShowGrid` bağlamaları korundu; böylece görüntü
  döndürüldüğünde ölçüm noktaları kaymaz.

### Testler
- Test 50 (`CheckTabIconsAndWindowSplit`): doğrulanmış ikon kodları, ikon stili, sekmelerin yalnızca
  ayrı pencerede olması ve ana pencerenin sekmesiz kalması.

---

## [v0.6.5] — 2026-11-11 — Sekmeli Panel ve Görüntü Ayarları

### Eklenenler
- **Sekmeli Araç Paneli:** Ölçüm çalışma alanı üstündeki kalabalık araç çubuğu `TabControl` ile 4 sekmeye ayrıldı: `Görüntü`, `Ölçüm`, `Proje`, `DXF`.
- **Görüntüleme Ayarları (Filtreler):** Parlaklık (-100..+100), kontrast (-100..+100), keskinleştirme (0..100), gri tonlama, negatif ve Sobel kenar vurgusu filtreleri. Filtreler yalnızca ekranda gösterilen görüntüyü işler; ölçüm matematiğini ve DXF ihracını kesinlikle etkilemez.
- **Geometrik Düzenleme:** 90° sol/sağ döndürme, 180° döndürme, yatay aynalama, dikey aynalama ve dikdörtgen kırpma.
- **İki Yönlü Tersinir Dönüşüm (`ImageDisplayTransform`):** Ölçüm noktaları (`R1`, `R2`, `M1`, `M2`) orijinal fotoğraf koordinatlarında saklanır. Görüntü döndürüldüğünde veya aynalandığında çizimler ve fare etkileşimi dinamik olarak ekran koordinatına çevrilir; `ToOriginal` ve `ToDisplay` tam tersinirdir.
- **Izgara (Grid):** İsteğe bağlı 50 px CAD ızgarası görsel hizalama için tuval üzerine çizilebilir.
- **Orijinale Dön Düğmesi:** Tüm filtre ve geometrik dönüşümleri tek tıkla sıfırlar.

### Testler
- Test 47 (`CheckImageDisplayTransformRoundTrip`): Döndürme, aynalama ve kırpma dönüşümünün tersinirliği doğrulanır.
- Test 48 (`CheckPhotoFilterPipeline`): Parlaklık, gri tonlama, negatif ve kimlik filtreleri doğrulanır.
- Test 49 (`CheckImageTabsAndCanvasBinding`): Sekmeli panel yapısı, Canvas bağımlılık özellikleri ve ViewModel entegrasyonu kaynak denetimiyle doğrulanır.

### Dokümantasyon
- `Docs/ROADMAP.md`: uzun vadeli ürün haritası eklendi (sekme yerleşimi, sağ araç şeridi, yeni yeteneklerin gerektirdiği motorlar ve sıralama ilkeleri).

---

## [v0.6.4] — 2026-11-10 — Ayrı Ölçüm Penceresi

### Eklenenler
- **Ayrı ölçüm penceresi (`MeasurementWindow`):** Ölçüm çalışma alanı, bağımsız ve tam ekran
  açılan bir pencerede çalıştırılabilir. Fotoğraf ekranın tamamını kaplar; hassas nokta koymak
  kolaylaşır ve pencere ikinci monitöre taşınabilir.
- **Oturum paylaşımı:** Pencere, ana penceredeki `PhotoMeasurementViewModel` örneğini paylaşır.
  Noktalar, ölçüm sonucu, fotoğraf ve büyüteç ayarları iki pencere arasında anında senkron kalır.
- **Pencere içi araç çubuğu:** Referans, mesafe, geri al, sıfırla, sığdır, iptal, büyüteç,
  proje kaydet ve DXF dışa aktar komutları.
- **F11** ile tam ekran/normal geçişi.
- Mevcut ölçüm sayfası korundu; yalnızca "Ayrı Pencerede Aç" düğmesi eklendi.

### Testler
- Test 46 (`CheckMeasurementWindowSource`): pencere düzeni, paylaşılan oturum, F11, tek örnek
  yönetimi ve kapanış temizliği doğrulanır.

---

## [v0.6.3] — 2026-11-10 — Büyüteç Görüntü ve Kısayol Düzeltmesi

### Düzeltilen Hatalar
- **Büyüteçte görüntü görünmüyordu (kritik):** Büyütecin arka planı, büyütülmüş görüntünün
  ÜSTÜNE opak olarak boyanıyordu; içerik tamamen kapanıyordu. Dolgu kırpmadan önceye alındı,
  kenarlık dolgusuz çiziliyor.
- **`L` tuşu yalnızca resme tıklandıktan sonra çalışıyordu:** Kısayollar canvas'ın klavye
  olayından pencere seviyesine (`Window.InputBindings`) taşındı; artık odak nerede olursa olsun
  çalışır.
- **Büyüteç oranı kısayolları değişti:** `[` ve `]` yerine `2`, `4`, `8` (doğrudan) ve `+`, `-`
  (kademeli) kullanılıyor.

### Testler
- Test 45 (`CheckMagnifierRenderingAndShortcuts`): dolgusuz kenarlık, kısayolların pencere
  seviyesine taşınması ve XAML bağlamaları doğrulanır.

---

## [v0.6.2] — 2026-11-10 — Büyüteç İyileştirmesi

### Düzeltilen ve Eklenenler
- **Büyüteç artık imleci kapatmıyor:** büyüteç merkezi imlecin çapraz üstüne taşındı; kenara
  yaklaşınca otomatik olarak ters tarafa geçiyor ve tuval içinde kalıyor. İmleç ile büyüteç
  arasında ince bağlantı çizgisi çizilir.
- **Büyüteç içinde ölçüm görünüyor:** fotoğrafın yanı sıra R1–R2 ve M1–M2 çizgileri ile dört
  tutamaç da büyütülmüş olarak çizilir (kalınlıkları orana göre inceltilir).
- **Yakınlaştırma ayarlanabilir:** 2× / 4× / 8× kademeleri; `[` ve `]` tuşları veya düğme ile
  değiştirilir.
- **Açma/kapama:** `L` tuşu ve "Büyüteç: Açık/Kapalı" düğmesi. Otomatik gizlenme kaldırıldı;
  kontrol tamamen kullanıcıda.
- **Düğmeler:** araç çubuğuna büyüteç aç/kapat ve oran düğmeleri eklendi.

### Testler
- Test 43 (`CheckMagnifierCenterAndZoom`): çapraz yerleşim, kenar geçişleri, tuval içinde kalma,
  zoom kademeleri (2→4→8→2).
- Test 44 (`CheckMagnifierToggleSource`): açma/kapama, zoom, kısayol ve eski alanın kaldırılması.

---

## [v0.6.1] — 2026-11-10 — Profesyonel Çizim Deneyimi

### Eklenenler
- **CAD tipi ölçü çizgisi:** Uzunluk çizgileri artık iki uçta dolu ok başı ve uç tikleriyle çizilir.
- **Adım rehberi (`ToolStateAdvisor`):** "1/2 — Başlangıç noktasına tıklayın" gibi Türkçe
  yönlendirme her araç değişiminde gösterilir.
- **Büyüteç ve crosshair:** Görüntü küçükken imleç çevresinde 4× büyütme ve tam ekran crosshair.
- **Shift ile eksen kilitleme:** Sürüklerken yatay/dikey düz çizgi.
- **Klavye kısayolları:** R (referans), M (ölçüm), F (sığdır), Delete (sıfırla), Ctrl+Z (geri al),
  Esc (iptal).
- **Ölçek çubuğu:** Sağ altta milimetre cinsinden okunabilir ölçek çubuğu (1–500 mm arası).
- **Düğme iyileştirmeleri:** Kısayol harfi içeren etiketler, açıklayıcı ipuçları ve aktif araç
  vurgusu.

### Testler
- Test 41 (`CheckToolStateAdvisorAndScaleBar`): adım metinleri ve ölçek çubuğu seçimi.
- Test 42 (`CheckProfessionalDrawingSource`): çizim/kısayol/büyüteç kodunun varlığı.

---

## [v0.6.0] — 2026-11-10 — 2B DXF İhracı

### Eklenenler
- **2B DXF dışa aktarma (`DxfBuilder`, `DxfExportService`):** Ölçülen R1–R2 referansı ve M1–M2
  ölçümü, DXF R12 ASCII (AC1009) formatında kaydedilir. AutoCAD, LibreCAD, QCAD ve Fusion 360 uyumlu.
- Koordinatlar her zaman **milimetre** ve `$INSUNITS=4` olarak yazılır; cm/inç seçimleri mm'ye çevrilir.
- Görüntü koordinatındaki Y ekseni DXF'in Y-up sistemine çevrilir (`y = H - y_px`).
- Katmanlar: `REFERENCE` (kırmızı), `MEASURE` (mavi), `LABELS` (beyaz). Noktalar CIRCLE ile
  işaretlenir, uzunluklar TEXT olarak yazılır.
- Türkçe karakterler DXF R12 uyumu için ASCII'ye çevrilir (ı→i, ş→s, ğ→g, ü→u, ö→o, ç→c).
- Fotoğraftan Ölçüm ekranına **"DXF Dışa Aktar"** düğmesi eklendi.

### Testler
- Test 39 (`CheckDxfExportStructureAndUnits`): DXF R12 yapısı, $INSUNITS=4, katmanlar, cm→mm dönüşümü.
- Test 40 (`CheckDxfCoordinateFlipAndValidation`): Y ekseni ters çevirme, ASCII dönüşümü, referans
  yokluğu ve sıfır uzunluklu referans hata yolları.

---

## [v0.5.0.6] — 2026-11-10 — Oturum Belirteci Yenileme ve 401 Kurtarma

### Dokümantasyon
- `Docs/ROADMAP.md` oluşturuldu: v1.0 "tamamlandı" tanımı (zemin kalibrasyonu + perspektif
  düzeltme + 2B DXF ihracı), kabul kriterleri, faz sırası ve kapsam dışı maddeler.
- `README.md`: otomatik test sayısı 28/28 ve 35 ifadeleri gerçek sayı olan **38** ile düzeltildi;
  7 modül rozeti `v0.4.2` → `v0.5.0.6`; rapor listesine v0.5.0.6 raporları eklendi; CAD/3B
  özelliklerinin henüz uygulanmadığını belirten kapsam notu eklendi.
- `PROJECT_STATUS.md`: yol haritası referansı eklendi; Projeler, Telefon Bağlantısı, CAD ve
  3B Baskı modüllerinin hedef sürüm sütunları güncellendi.

### Düzeltilen Hatalar
- **"Yetkisiz erişim. Geçerli bir oturum belirteci gereklidir." hatası:** Telefon sunucusu her
  yeniden başlatıldığında (yayın başlatma, "PIN Yenile", uygulama yeniden açılışı) yeni bir oturum
  belirteci üretiyor; PC istemcisi önbellekteki eski belirteçle çekim istediği için istek
  reddediliyordu. `RemotePhotoClient` artık 401 yanıtında PIN ile **bir kez** yeniden kimlik
  doğrulaması yapıp isteği tekrar deniyor.
- PIN artık istemcide saklanıyor (`_currentPin`), böylece yeniden doğrulama kullanıcı müdahalesi
  olmadan yapılabiliyor.
- Hata mesajları HTTP koduna göre Türkçe ve yönlendirici hale getirildi (401 / 403 / 404 / 429).

### Testler
- Test 38 (`CheckTokenRefreshOnUnauthorized`): 401 sonrası otomatik yeniden doğrulama ve
  başarılı tekrar deneme doğrulanır.

---

## [v0.5.0.5] — 2026-11-09 — Uzaktan Yüksek Çözünürlüklü Çekim ve Kayıpsız Dosya Aktarımı

### Eklenenler ve Geliştirmeler
- **Uzaktan Fotoğraf İstemcisi (`RemotePhotoClient.cs`):**
  - Telefon üzerindeki bağımsız kamera servisine `POST /api/v1/auth` ile güvenli Bearer oturum belirteci alma.
  - `POST /api/v1/capture`: Uzaktan donanımsal still-capture tetikleme komutu.
  - `GET /api/v1/photos/{id}`: SHA-256 bütünlük doğrulamalı kayıpsız dosya indirme (`IncrementalHash`).
  - Önbellek kontrolü (Cache-hit): İndirilmiş ve SHA-256 özeti eşleşen fotoğraflar tekrar indirilmez, doğrudan önbellekten açılır.
  - Bozuk / tahrif edilmiş dosya koruması: Hash uyuşmazlığında indirme iptal edilir (`InvalidDataException`), bozuk geçici dosya temizlenir.
  - `GET /api/v1/photos`: Telefondaki fotoğrafları ve depolama alanı kullanımını sorgulama.
- **Kamera ve Kalibrasyon ViewModel (`CameraCalibrationViewModel.cs`):**
  - "⭐ Yüksek Çözünürlüklü Fotoğraf Çek ve Ölç" butonu (`CaptureHighResPhotoCommand`).
  - 4 aşamalı görsel ilerleme bildirimi ("1/4: Komut gönderiliyor...", "2/4: Telefon çekiyor...", "3/4: PC'ye aktarılıyor...", "4/4: Doğrulanıyor ve açılıyor...").
  - "Telefonda Bekleyen Ölçüm Fotoğrafları" paneli (`PendingRemotePhotos`), `LoadPendingPhotosCommand` ve `DownloadPendingPhotoCommand`.
  - UI iş parçacığı güvenliği (`ExecuteOnUi`).
- **Fotoğraftan Ölçüm Modülü & Kalibrasyon Güvenliği (`PhotoMeasurementViewModel.cs`):**
  - `LoadCapturedPhoto` / `LoadHighResPhoto` ile yüksek çözünürlüklü fotoğraf yükleme.
  - Çözünürlük > 1920x1080 olduğunda "(Yüksek Çözünürlüklü Fotoğraf)" rozeti gösterimi.
  - **Fiziksel Kalibrasyon Güvenliği:** Yeni kamera fotoğrafı yüklendiğinde eski referans ölçeği geçersiz kılınır (`HasValidReference = false`), bilinen uzunluk sıfırlanır ve kullanıcı yeni referans belirlemeye yönlendirilir.
- **Arayüz (`MainWindow.xaml`):**
  - Kamera kontrol paneline "⭐ Yüksek Çözünürlüklü Fotoğraf Çek ve Ölç" butonu, ilerleme rozeti ve bekleyen fotoğraflar Expander'ı eklendi.
- **Otomatik Testler (`OlcuAtolyesi.PC.Tests`):**
  - Test 36: `CheckRemoteHighResCaptureAndSha256Download` (PIN doğrulama, arka plan koruması, SHA-256 doğrulama, cache-hit, bozuk dosya reddi).
  - Test 37: `CheckHighResPhotoMeasurementIntegrationAndCalibrationReset` (ViewModel entegrasyonu, rozet, kalibrasyon sıfırlaması, hata izolasyonu).
  - Tüm 37 test başarıyla geçti (`37/37 BAŞARILI`).

---

## [v0.5.0.4] — 2026-11-09 — Merkezi Tanılama Konsolu ve Uzaktan Log Alma Sistemi

### Eklenenler ve Geliştirmeler
- **Merkezi Tanılama Konsolu Penceresi (`DiagnosticConsoleWindow.xaml`, `DiagnosticConsoleViewModel.cs`):**
  - Sol navigasyon paneline bağımsız modeless "🖥️ Tanılama Konsolu" düğmesi eklendi; ana pencereyi kilitlemeden paralel çalışır, simge durumuna küçültülebilir.
  - Canlı olay akışı, renkli seviye rozetleri (DEBUG: Gri, INFO: Mavi, WARN: Turuncu/Sarı, ERROR: Kırmızı, CRITICAL: Koyu Kırmızı), kaynak etiketleri (PC / Android) ile anlık izleme.
  - Kaynak filtresi (Tümü, Yalnızca PC, Yalnızca Android), Seviye filtresi (Tümü, Bilgi ve Üzeri, Uyarı ve Hata, Yalnızca Kritik), anlık metin arama.
  - Otomatik kaydırma kilidi (Auto-scroll Açık/Kapalı), seçili kaydı panoya kopyalama, konsol temizleme ve UTF-8 formatında dışa aktarma (`.txt`).
  - Thread-safe WPF veri bağlama: `BindingOperations.EnableCollectionSynchronization` ile arka plan iş parçacıklarından gelen kayıtlar UI donmasına neden olmadan güvenle listeye akar.
- **Kalıcı Döngüsel Loglama Altyapısı (`OlcuAtolyesi.Core.Diagnostics`):**
  - `%LOCALAPPDATA%\OlcuAtolyesi\Logs` dizininde 2 MB dosya sınırı, 5 adet arşiv dosyası (maksimum 10 MB toplam disk kullanımı).
  - Uygulama başlatılırken `session_active.marker` kontrolü ile önceki anormal sonlanma ve çökmelerin tespiti.
  - `DiagnosticDataMasker`: 6 haneli PIN kodlarını (`[MASKELENDİ]`) ve gizli anahtarları (`[GİZLENDİ]`) Türkçe ve kültür bağımsız (`RegexOptions.CultureInvariant`) hassasiyetle maskeler.
  - `DiagnosticLogHub`: 1500 bellek içi döngüsel olay tamponu.
- **Telefondan Uzaktan Log Çekme Entegrasyonu (`FetchAndroidLogsAsync`):**
  - PC uygulamasında "Telefondan Logları Al" düğmesi ile bağlı Android telefonun `http://<telefon-ip>:<port>/logs?pin=<pin>&limit=200` endpoint'ine güvenli HTTP GET sorgusu.
  - Alınan Android logları anında ayrıştırılarak merkezi konsola Android etiketli olarak eklenir ve PC üzerinde kalıcı log dosyasına kaydedilir.
  - Yetkisiz (geçersiz PIN) durumlarda HTTP 401 bildirimi konsola işlenir.
- **Evrensel Hata Yakalama (`App.xaml.cs`):**
  - `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException` ve `TaskScheduler.UnobservedTaskException` dinleyicileri eklendi.
  - Kapanışta `_activeMarker` güvenle silinir.
- **Otomatik Testler (`OlcuAtolyesi.PC.Tests`):**
  - Test 34: `CheckDiagnosticConsoleAndMasking` (PIN maskeleme, döngüsel dosya sınırları, marker kontrolü).
  - Test 35: `CheckRemoteLogIngestionAndModel` (JSON ayrıştırma, Android loglarının hub'a aktarımı, UI thread filtreleme doğrulaması).
  - 35/35 test başarıyla tamamlandı.

---

## [v0.5.0.3] — 2026-11-09 — Uygulama İkonu, Açılış Ekranı ve Responsive Tasarım

### Eklenenler ve Geliştirmeler
- **Kumpas Temalı Kurumsal Kimlik & İkon (`BrandedAssetGenerator.cs`, `app.ico`, `app_icon.png`):**
  - Dijital kumpas (çeneler, cetvel bölüntüleri, "26.15" LCD ekran), optik vizör/reticle halkaları ve R1/R2 lazer boyut çizgisi içeren vektörel kurumsal ikon oluşturuldu.
  - PE yürütülebilir dosyası, pencere başlığı ve Windows görev çubuğu için 7 katmanlı (16, 24, 32, 48, 64, 128, 256 px) standart Windows `.ico` formatı derleme kaynağına eklendi.
  - Sol navigasyon paneli başlığına kumpas logosu ve `v0.5.0.3` sürüm rozeti yerleştirildi.
- **WPF Yerel Açılış Ekranı (`splash.png`):**
  - 700x380 px boyutlarında, koyu lacivert (`#0A192F`), kumpas amblemi ve kurumsal alt başlık ("Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm") içeren yerel WPF `SplashScreen` entegre edildi.
  - Çalışma zamanı ve pencere yüklenirken anında endüstriyel karşılama sunar; yapay gecikme içermez.
- **Responsive Tasarım & Ekran Uyumu (920x600, 1366x768, 1920x1080):**
  - Minimum 1366x768 dizüstü ve 1920x1080 masaüstü çözünürlükleri için tüm kontroller esnetildi.
  - Metin bloklarına `TextWrapping="Wrap"` atanarak Türkçe kelimelerin kesilmesi önlendi.
  - Sol menü, çalışma alanı ve durum çubuğu tüm boyutlarda net ve erişilebilir tutuldu.
- **Otomatik Testler (`OlcuAtolyesi.PC.Tests`):**
  - 33. test olarak `CheckBrandedAssetsAndResponsiveLayout` eklendi; tüm çözünürlüklerde arayüz taşma denetimi, ICO binary başlık doğrulaması ve PNG splash testleri başarıyla tamamlandı (`33/33` kontrol başarılı).

---

## [v0.5.0.2] — 2026-11-08 — Canlı Kamera Karesini Fotoğraftan Ölçüm Modülüne Aktarma Hatası Düzeltmesi

### Düzeltilenler ve İyileştirmeler
- **Modüller Arası Kare Aktarımı (`MainWindowViewModel.cs` & `CameraCalibrationViewModel.cs`):**
  - "Bu Kareyi Yakala ve Ölç" tıklandığında yalnızca sayfa navigasyonu yapılıp yakalanan dosyanın ölçüm modülüne hiç bildirilmemesi kök nedeni tespit edildi.
  - `MainWindowViewModel.OpenCapturedPhotoInMeasurement(string photoPath)` köprüsü eklendi; `CameraCalibrationViewModel`'e doğrudan aktarım delegesi (`openCapturedPhotoAction`) bağlandı.
  - Kare başarıyla diske yazılıp doğrulanmadan ve `PhotoMeasurementViewModel` içinde çözümlenmeden navigasyon yapılması engellendi. Hata durumunda kullanıcı kamera ekranında kalır ve Türkçe hata açıklaması gösterilir.
- **Canlı Kare Yükleme ve Bağımsız Ölçüm Oturumu (`PhotoMeasurementViewModel.cs`):**
  - `LoadCapturedPhoto(string path)` yöntemi eklendi:
    - Son geçerli JPEG karesi `PhotoLoader` üzerinden tam çözünürlükte güvenle çözümlenir (`BitmapCacheOption.OnLoad` + `Freeze()`).
    - Demo çalışma alanı veya önceki fotoğraf yeni kamera karesiyle güvenli şekilde değiştirilir.
    - `ActiveProjectId = null` yapılarak, kullanıcının daha önce açtığı kayıtlı projelerin yeni kamera karesiyle ezilmesi engellendi.
    - Yeni görsel boyutlarına göre sıfırdan `MeasurementSession` başlatıldı; eski fotoğrafa ait özel piksel koordinatları (`R1/R2`, `M1/M2`) sessizce yeni fotoğrafa taşınmaz.
    - Kullanıcıya referans noktalarını (`R1` ve `R2`) yeni fotoğraf üzerindeki gerçek referans nesnesine yerleştirmesi gerektiğini belirten açık Türkçe durum mesajı gösterildi.
    - Dosya yükleme başarısız olursa mevcut fotoğraf, oturum ve açık proje eksiksiz korunur.
- **JPEG Bütünlük ve Format Doğrulaması (`CameraCalibrationViewModel.cs`):**
  - Yakalama sırasında `_lastReceivedJpeg` verisinin minimum uzunluğu, JPEG başlangıç (`0xFF, 0xD8` / SOI) ve bitiş (`0xFF, 0xD9` / EOI) belirteçleri doğrulanır.
  - Diske yazılan dosyanın varlığı ve sıfırdan büyük boyutu teyit edilir.
  - `Captures` geçici klasöründe 24 saatten eski geçici kareler temizlenerek (`CleanupOldCaptures`) gereksiz disk birikimi önlendi.
- **Kapsamlı Otomatik Regresyon Testleri (`OlcuAtolyesi.PC.Tests`):**
  - 32. test olarak `CheckCapturedFrameTransferAndMeasurementWorkflow` eklendi:
    - Boş kare yakalama engellemesi ve hata bildirimi,
    - Bozuk JPEG baytlarının reddi ve mevcut durumun korunması,
    - Demo alanından gerçek kareye geçiş ve referans sıfırlaması,
    - Önceden açılmış fotoğrafın yeni kareyle sorunsuz değiştirilmesi,
    - Başarısız aktarımda mevcut fotoğrafın korunması,
    - Mevcut açık projenin diskte korunması (`ActiveProjectId` sıfırlaması),
    - `MainWindowViewModel` üzerinden sayfa navigasyon sırası ve başarısızlıkta sayfada kalma,
    - Canlı kamera önizlemesinden dondurma ve tam ölçüm aktarımı iş akışı.
  - Toplam kontrol sayısı `32/32` oldu.

---

## [v0.5.0.1] — 2026-11-08 — Wi-Fi Kamera Görüntüsü ve Bağlantı Durumu Hataları Düzeltmesi

### Düzeltilenler ve İyileştirmeler
- **RFC Uyumlu MJPEG Akış Ayrıştırıcı (`CameraStreamClient.cs`):**
  - Önceki sürümde JPEG karelerinin `0xFF, 0xD9` ile kaba tespiti nedeniyle EXIF/küçük resim (thumbnail) içeren fotoğrafların eksik kesilmesi ve WPF tarafında sessiz çözümleme hatası vermesi (`JPEG datastream contains no image`) sorunu çözüldü.
  - HTTP `Content-Length` başlığı ve `--frame` multipart boundary ayrıştırması eklenerek tam ve eksiksiz JPEG karelerinin alınması sağlandı.
  - Parçalanmış (chunked) TCP ağ paketlerinin birleştirilmesi ve 10 MB bellek şişme koruması eklendi.
- **Gerçek Çözümlenen Kareler Üzerinden FPS ve Çözünürlük Metrikleri (`CameraCalibrationViewModel.cs`):**
  - Kareler WPF `BitmapImage` tarafından gerçekten çözümlenmeden ve ekrana aktarılmadan FPS sayacının artırılması engellendi.
  - Anlık çözünürlük bilgisi (`WifiResolutionText`) yalnızca başarıyla çözümlenen karelerin gerçek piksel boyutlarından (`PixelWidth × PixelHeight`) üretildi.
  - Çözümlenemeyen veya bozuk kareler durumunda ekranda Türkçe hata mesajı gösterilerek sessiz siyah ekran kalma durumu ortadan kaldırıldı.
- **Kusursuz Bağlantı Durumu ve Buton Senkronizasyonu (`MainWindow.xaml` & `CameraCalibrationViewModel.cs`):**
  - İstemciye `ConnectionStateChanged` olayı eklendi; bağlantı koptuğunda, kullanıcı kapattığında veya PIN hatalı olduğunda ViewModel durumunu anında günceller.
  - Bağlantı kesildiğinde:
    - "▶ Canlı Yayına Bağlan" butonu yeniden anında etkinleşir (`CanConnectWifi = true`).
    - "⏹ Bağlantıyı Kes" butonu devre dışı kalır (`CanDisconnectWifi = false`).
    - FPS değeri `0.0` olarak sıfırlanır.
    - Çözünürlük metni `"-"` olarak temizlenir.
    - Kamera önizleme görseli temizlenir ve rehberlik katmanı gösterilir.
  - Butonlara hem `Command` hem de `IsEnabled` iki yönlü güvence bağlandı; `CommandManager.InvalidateRequerySuggested()` ve `RaiseCanExecuteChanged` ile WPF buton pasif kalma hatası kökten giderildi.
- **Genişletilmiş Otomatik Test Paketi (`OlcuAtolyesi.PC.Tests`):**
  - `CheckWifiMjpegRobustStreamingAndErrorScenarios` testi eklendi: Parçalı TCP paketleri, bozuk kare izolasyonu, yaşam döngüsü kopması, buton durumları ve yeniden bağlanma senaryoları doğrulandı (`31/31` kontrol başarılı).

---

## [v0.5.0] — 2026-11-08 — Bağımsız Android Kamera Servisi ve WPF Canlı Önizleme

### Eklenenler
- **Bağımsız Wi-Fi Kamera İstemcisi (`CameraStreamClient`):**
  - scrcpy ve ADB gerektirmeyen, doğrudan yerel ağ üzerinden Ölçü Atölyesi Android Kamera Servisi'ne bağlanan HTTP MJPEG istemcisi.
  - 6 haneli geçici PIN eşleştirmesi, 401 Unauthorized yakalama ve Türkçe hata rehberliği.
  - Arka planda `multipart/x-mixed-replace` akış ayrıştırma, canlı FPS ve çözünürlük takibi.
  - Düşük gecikmeli tamponlama (`DropOldest`) ve ağ kopmalarında kaynakları güvenle temizleme.
- **WPF Pencere İçi Canlı Önizleme ve Mod Seçimi (`MainWindow.xaml` & `CameraCalibrationViewModel`):**
  - İki bağlantı modu: `🔗 USB — scrcpy Motoru` vs. `📡 Wi-Fi — Ölçü Atölyesi Android Kamera Servisi`.
  - Wi-Fi modunda IP:Port ve PIN giriş alanları, "Canlı Yayına Bağlan" ve "Bağlantıyı Kes" butonları.
  - Doğrudan uygulama penceresi içinde gösterilen akıcı canlı görüntü ekranı (`<Image Source="{Binding LiveFrameSource}" />`).
  - "📸 Bu Kareyi Yakala ve Ölç" butonu: Canlı yayından dondurulan kareyi atomik JPEG olarak kaydedip doğrudan Fotoğraftan Ölçüm modülüne aktarma.
- **Otomatik Test Paketi Genişletmesi (`OlcuAtolyesi.PC.Tests`):**
  - Sahte MJPEG sunucusu ile `CheckWifiCameraStreamClient` testi (PIN doğrulaması, 401 Unauthorized, akış ayrıştırma).
  - `CheckCameraModeSwitchingAndLivePreview` testi (mod değişimi, canlı önizleme, anlık kare yakalama).
  - Toplam test sayısı `30/30` oldu.

---

## [v0.4.2] — 2026-11-08 — Kamera Sensörlerini Algılama ve Seçme

### Eklenenler ve Düzeltilenler
- **Kamera Sensör Listesi Düzeltmesi (`ScrcpyLocatorAndParser` & `CameraCalibrationViewModel`):**
  - Sayfa açılışında ve USB üzerinden telefon bağlandığında kamera sensörlerinin taranması otomatik hale getirildi (`ScanCamerasAsync`).
  - scrcpy çıktılarındaki ANSI renk kaçış kodları (`StripAnsi`) temizlenerek regex eşleşme sorunları giderildi.
  - Tırnaklı (`--camera-id="0"`) ve tırnaksız kamera kimlikleri hatasız algılanacak şekilde regex (`CameraLineRegex`) genişletildi.
  - Kamera listesi boş döndüğünde asla işlem başarılı kabul edilmeyerek kullanıcıya açık Türkçe hata/rehberlik mesajı sunuldu.
- **Dinamik Çözünürlük ve Mod Filtreleme:**
  - Kullanıcı bir kamera sensörü seçtiğinde (`SelectedCamera`), cihazın o sensör için desteklediği gerçek çözünürlükler scrcpy `--list-camera-sizes` ile dinamik olarak sorgulanır.
  - Standart modlar ile yüksek hızlı (`high-speed / slow-motion`) modlar birbirinden ayrıştırıldı ve `[Yüksek Hız / Slow-Motion]` etiketiyle sunuldu.
  - Çözünürlük modu seçildiğinde kare hızı (`AvailableFpsOptions`) ve varsayılan FPS (`SelectedFps`) dinamik olarak güncellenir; desteklenmeyen FPS kombinasyonları engellendi.
  - Canlı kamera başlatılırken seçili kamera sensörü doğrudan `--camera-id=<id>` argümanıyla oturuma aktarılır.
- **Arayüz ve ComboBox Geliştirmeleri (`MainWindow.xaml`):**
  - Çözünürlük ComboBox'ı `AvailableCameraSizes` listesine bağlanarak en boy oranı, çözünürlük, desteklenen FPS ve High-Speed rozeti tek satırda görünür kılındı.
  - Sürüm etiketleri ve başlıklar `v0.4.2` olarak güncellendi.
- **Otomatik Test Paketi Genişletmesi (`OlcuAtolyesi.PC.Tests`):**
  - ANSI kaçış kodları, tırnaklı parametreler, high-speed mod ayrıştırma ve uçtan uca dinamik mod senkronizasyonunu doğrulayan yeni mock test paketi eklendi; toplam test sayısı `28/28` oldu.

---

## [v0.4.1] — 2026-11-08 — USB Üzerinden Canlı Telefon Kamerası

### Eklenenler
- **scrcpy 5.0.1 Güvenli Entegrasyonu ve Süreç Yöneticisi (`OlcuAtolyesi.Core.Phone`):**
  - **scrcpy Bulucu (`ScrcpyLocator`):** WinGet (`%LOCALAPPDATA%\Microsoft\WinGet\Packages\Genymobile.scrcpy*`), `PATH`, Scoop, Chocolatey ve standart Windows dizinlerinde `scrcpy.exe` tespiti; bulunamadığında Türkçe rehberlik mesajı.
  - **Güvenli Komut Yöneticisi (`ScrcpyProcessManager`):** Kullanıcı ve cihaz girdisini kabuğa eklemeden `ProcessStartInfo.ArgumentList` ile doğrudan çalıştırma, `-s <serial>` cihaz hedefleme, izole PID takibi ve güvenli oturum kapatma.
  - **scrcpy Çıktı Ayrıştırıcı (`ScrcpyOutputParser`):** `scrcpy --version`, `--video-source=camera --list-cameras` ve `--list-camera-sizes` çıktılarını regex ile hatasız ayrıştırma; virgüllü ve noktalı ondalık toleransı (`zoom-range=[0,55, 8]`).
  - **Dinamik Kamera ve Çözünürlük Sorgusu:** Sabit üretici değerleri yerine cihazdan canlı kamera listesi ve desteklenen çözünürlükleri dinamik olarak alma.
- **Kamera ve Kalibrasyon Ekranı (`CameraCalibrationViewModel` + `MainWindow.xaml`):**
  - **Kamera Sensörü Seçimi:** Ön/Arka kamera dinamik açılır listesi ve "Kameraları Tara" butonu.
  - **Çözünürlük ve Akış Parametreleri:** Çözünürlük ("1920x1080", "1280x720", "3840x2160" vb.), FPS (15-60) ve Bit Hızı (4M-20M) seçimi.
  - **Canlı Akış Denetimi:** Kullanıcı kontrollü "▶ Kamerayı Başlat" ve "⏹ Kamerayı Durdur" butonları; oturum durumu rozeti (`CANLI YAYINDA • AKTİF`, `BEKLEMEDE` vb.).
  - **Yönetilen Önizleme Penceresi Mimarisi (SDL3):** scrcpy 5.0.1 donanım hızlandırmalı SDL3 penceresi üzerinde teknik dürüst bilgilendirme ve gelecek sürüm (v0.5.0) in-app decoding yol haritası.
  - **Uygulama Kapanışında Otomatik Temizlik:** Pencere kapandığında `ViewModel.Dispose()` ile çalışan scrcpy sürecinin temiz sonlandırılması.
- **Genişletilmiş Otomatik Test Paketi (`OlcuAtolyesi.PC.Tests`):**
  - Sahte süreç yöneticisi (`FakeScrcpyProcessManager`) ile scrcpy çıktı ayrıştırıcı, süreç yaşam döngüsü / argüman güvenliği ve Kamera Kalibrasyonu ViewModel iş akışı testleri eklendi; toplam test sayısı `27/27` oldu.

---

## [v0.4.0] — 2026-11-08 — Android Telefon Bağlantısı ve USB Cihaz Yönetimi

### Eklenenler
- **ADB Entegrasyonu ve Güvenli USB Cihaz Yönetimi (`OlcuAtolyesi.Core.Phone`):**
  - **Güvenli ADB Bulucu (`AdbLocator`):** `ANDROID_HOME`, `ANDROID_SDK_ROOT`, `PATH` ve bilinen Windows Android SDK Platform-Tools dizinlerinde `adb.exe` tespiti; bulunamadığında anlaşılır Türkçe uyarı.
  - **Kabuksuz Komut Çalıştırıcı (`ProcessAdbCommandRunner` & `AdbInputValidator`):** Kullanıcı veya cihaz girdisini kabuğa eklemeden `ProcessStartInfo.ArgumentList` ile çalıştırma, `-s <serial>` seri numarası doğrulama, zaman aşımı (`TimeSpan`) ve iptal (`CancellationToken`) yönetimi.
  - **Çoklu Cihaz ve Durum Ayrımı (`AdbOutputParser` & `UsbPhoneConnectionService`):** `adb devices -l` çıktısından `device` (hazır), `unauthorized` (izin bekliyor) ve `offline` (çevrimdışı) durumlarını ayırt etme; yalnızca `device` durumundaki cihazlardan model ve Android sürümü sorgulama; `unauthorized` ve `offline` cihazlara işlem gönderilmesini engelleme.
- **Telefon Bağlantısı Ekranı (`PhoneConnectionViewModel` + `MainWindow.xaml`):**
  - **Cihazları Tara**, **Cihaz Seç**, **Bağlantıyı Kontrol Et**, **Bağlantıyı Yenile** ve **Bağlantıyı Ayır** işlevleri.
  - Bağlantıyı Ayır işlemi `adb kill-server` çalıştırmaz; yalnızca uygulamanın seçili cihaz oturumunu sonlandırır ve diğer bağlı cihazları korur.
- **Gelecek Bağlantı Seçenekleri ve Kamera Kontrol Sözleşmeleri (`RemoteCameraContracts`):**
  - `USB` (`Etkin • v0.4.0`), `Wi-Fi` (`Planlandı`) ve `Bluetooth` (`Planlandı`) iletişim kanalı tanımları.
  - Gelecekteki kamera kontrolü için `IRemotePhoneCameraService`, `RemoteCameraCapabilities`, `RemoteCameraSettings` ve 9 planlanan kamera kontrol sözleşmesi (`Kamera Seçimi`, `Canlı Görüntü`, `Zoom`, `Çözünürlük ve Kalite`, `FPS`, `Otomatik/Manuel Odaklama`, `Pozlama`, `Beyaz Dengesi`, `Flaş/Işık`).
- **Genişletilmiş Otomatik Test Paketi (`OlcuAtolyesi.PC.Tests`):**
  - Sahte ADB sağlayıcısı (`FakeAdbCommandRunner`) ile gerçek telefon gerektirmeden çalışan 6 yeni test paketi eklendi; toplam test sayısı `24/24` oldu.

---

## [v0.3.0] — 2026-11-08 — Kalıcı Proje Kayıt Sistemi ve Mobil Proje Uyumluluğu

### Eklenenler
- **Kalıcı Proje Yönetim Sistemi (`DesktopProjectService` + `DesktopProject`):**
  - Her proje için izole veri klasörü (`projects/<id:N>/project.json`, `project.json.bak` ve `photo.jpg`), atomik yazım, `.bak` kurtarma, mobil `v0.4.0` proje paketi içe/dışa aktarma, `Projeler` yönetim ekranı ve `Fotoğraftan Ölçüm` üzerinde `Projeyi Kaydet` / `Farklı Kaydet` işlevleri.

---

## [v0.2.0] — 2026-11-08 — Gerçek Fotoğraf Üzerinde Referanslı Ölçüm

### Eklenenler
- **Gerçek Fotoğraf Üzerinde Referanslı Ölçüm (`Fotoğraftan Ölçüm` Modülü):**
  - `PhotoLoader` (`JPG`/`JPEG`/`PNG`/`BMP` + EXIF yön), `MeasurementCanvas` (yakınlaştırma, kaydırma, `R1–R2` referans ve `M1–M2` mesafe ölçümü, nokta sürükleme, `Geri Al`, `Sıfırla`, `Görünümü Sığdır`).

---

## [v0.1.0] — 2026-11-08 — Bağımsız Windows Masaüstü Temeli

### Eklenenler
- **Çözüm ve Proje Yapısı (`OlcuAtolyesi.PC.sln`):**
  - `src/OlcuAtolyesi.Core` (`net10.0`), `src/OlcuAtolyesi.PC` (`net10.0-windows` WPF) ve `tests/OlcuAtolyesi.PC.Tests`.
