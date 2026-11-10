# Ölçü Atölyesi — Bütüncül Derin Bağlam ve Mimari Başvuru Raporu (Deep Context Report)

**Hedef Kitle:** Baş Yapay Zeka Mimarı / Kıdemli Yazılım Mimarı  
**Tarih:** 2026-11-11  
**Kapsam:** `D:\proje\OlcuAtolyesi` (MAUI Mobil) & `D:\proje\OlcuAtolyesi-PC` (WPF Masaüstü) & `OlcuAtolyesi.Core` (.NET 10 Çekirdek)  
**Mevcut Sürümler:** Mobil `v0.6.2 (Uncommitted Working Tree)` / PC `v0.6.7 (Tagged: v0.6.7-baseline)`  

---

## BÖLÜM 1: TÜM DOKÜMANTASYONLAR, VİZYON VE STRATEJİK HEDEFLER

### 1.1. Ürün Vizyonu ve Hibrit Mimari Felsefesi
Ölçü Atölyesi projesinin temel amacı; endüstriyel, atölye ve saha ortamlarında fiziksel cetvel, kumpas ve karmaşık tarayıcılar gerektirmeden, **akıllı telefon kamerası veya fotoğraf üzerinden milimetrik hassasiyette ölçüm yapmak, CAD/CAM verisi üretmek ve 3B baskıya hazırlamaktır**.

Sistem iki temel çalışma modeline dayanır:
1. **Bağımsız Mobil Saha Modu (.NET 10 MAUI):** Android telefonun kamerasını veya galerisini kullanarak tek bir bilinen referans nesne (kredi kartı, madeni para vb.) yardımıyla R1–R2 referansı üzerinden M1–M2 mesafesini anında hesaplamak ve proje paketi (`project.json` + `photo.jpg`) olarak saklamak.
2. **Masaüstü Mühendislik ve Stüdyo Modu (.NET 10 WPF):** Masaüstü bilgisayarın yüksek işlem gücünden yararlanarak, Wi-Fi veya USB üzerinden telefon kamerasını canlı izlemek, yüksek çözünürlüklü fotoğraflar çekmek, gelişmiş filtreler ve çift cetvelli çalışma alanında analiz yapmak ve AutoCAD R12 uyumlu **2B DXF** vektör çıktısı üretmek. Gelecek fazlarda ise zemin homografisi, OpenCV kontur algılama, OCCT ile STEP katı modelleme ve dilimleyici (Slicer) entegrasyonu hedeflenmektedir.

### 1.2. Mimari Kurallar ve Sınırlar (AGENTS.md Direktifleri)
Her iki depoda yer alan `AGENTS.md` kuralları, projede çalışan ajanlar için mutlak sınırları çizer:
- **Mobil Proje İzolasyonu:** `D:\proje\OlcuAtolyesi-PC` üzerinde çalışırken `D:\proje\OlcuAtolyesi` mobil deposuna **kesinlikle hiçbir dosya yazılamaz, silinemez veya değiştirilemez**.
- **Koordinat ve Matematik Koruma İlkesi:** Ölçüm noktaları (`R1, R2, M1, M2`) asla ekran veya tuval koordinatlarında saklanamaz. Her zaman orijinal fotoğraf piksel koordinatlarında (`ImagePoint` / `ImageSegment`) tutulur. Yakınlaştırma (zoom), kaydırma (pan) veya görüntüleme rotasyonu (`ImageDisplayTransform`) ölçümün matematiksel sonucunu değiştiremez.
- **Açık Kaynak Lisans Sınırları:**
  - `Apache-2.0` (OpenCV) ve `MIT` (ezdxf): Süreç içi kütüphane veya referans olarak serbestçe kullanılabilir.
  - `LGPL-2.1` (Open CASCADE / OCCT, FreeCAD): Dinamik kütüphane (.dll/.so) veya harici CLI olarak kullanılabilir.
  - `GPL-3.0` (OpenScan) ve `AGPL-3.0` (OrcaSlicer, PrusaSlicer): **Asla süreç içi bağlanamaz ve kod kopyalanamaz.** Sadece ayrı işletim sistemi süreci (`Process.Start`) veya ağ protokolü (`HTTP/REST/WebSocket`) üzerinden haberleşilebilir.
- **Gerçek Cihaz Test Yasağı:** Ajanlar gerçek Android cihaza ADB üzerinden otomatik test yapamaz, uygulama yükleyemez veya dokunma simülasyonu çalıştıramaz. Doğrulama `dotnet build` (Debug/Release) ve kafa gerektirmeyen mantık testleri (`Tests/LogicChecks`, `OlcuAtolyesi.PC.Tests`) ile sınırlıdır. Canlı testler kullanıcıya aittir.

### 1.3. v1.0.0 "Tamamlandı" Tanımı ve Kabul Kriterleri (`ROADMAP.md`)
Proje aşağıdaki üç yetenek uçtan uca sağlandığında **v1.0.0** sürümüne ulaşır:
1. **Zemin Kalibrasyonu ve Perspektif Düzeltme:** 4 köşe homografi tanımlanarak eğik açılı fotoğrafların dik açılı ortoparalel düzleme dönüştürülmesi.
2. **2B DXF İhracı:** Ölçülen noktaların AutoCAD R12 ASCII DXF ($INSUNITS=4 mm, REFERENCE/MEASURE/LABELS katmanları) olarak dışa aktarılması (v0.6.0'da tamamlandı).
3. **Uçtan Uca Akış ve Regresyon Koruması:** Fotoğraf edinimi → R1–R2 → M1–M2 → Kalibrasyon → DXF akışının korunması.

| Kriter No | Kriter Açıklaması | Ölçüm Yöntemi | Hedef / Durum |
|---|---|---|---|
| **K1** | Kalibrasyon sonrası ölçüm hatası | Sentetik görüntüde bilinen uzunluk testi | Sapma **<%2** |
| **K2** | DXF dosyası geçerliliği | ASCII DXF R12 uyumu, `$INSUNITS=4` (mm) | **Tamamlandı (v0.6.0)** |
| **K3** | DXF katman yapısı | `REFERENCE`, `MEASURE`, `LABELS` ayrımı | **Tamamlandı (v0.6.0)** |
| **K4** | Birim tutarlılığı | mm / cm / m / inç dönüşümlerinin DXF'e doğru yansıması | **Tamamlandı (v0.6.0)** |
| **K5** | Regresyon ve Test Sayısı | Sıfır hata ile tüm testlerin geçmesi | **54/54 Başarılı (v0.6.7)** |
| **K6** | Derleme Kalitesi | Debug ve Release derlemeleri | **0 Uyarı, 0 Hata** |
| **K7** | Dokümantasyon | README, PROJECT_STATUS, CHANGELOG ve Sürüm Raporları | **Eksiksiz Güncel** |

### 1.4. Uzun Vadeli Ürün Haritası (`ROADMAP.md §7`)
SolidWorks benzeri iki katmanlı çalışma alanı modeli:
- **Üst Sekmeler (İş İstasyonları):** "Ne yapıyorum?" sorusunun yanıtı (`Görüntü`, `Ölçüm`, `Proje`, `DXF`, `Çizim` [v0.8.0], `3B` [v0.9.0], `CAM` [v1.0+]).
- **Sağ Dikey Şerit (Ortak Yardımcılar):** "Nasıl görüyorum / Nasıl yardımcı olur?" sorusunun yanıtı (Yakınlaştır, sığdır, kaydır, büyüteç, ızgara, snap, cetvel, geri/ileri al).
- **Alt Durum Çubuğu:** Sonuç, piksel ölçeği, adım rehberi ve koordinatlar.

---

## BÖLÜM 2: MİMARİ VE VERİ AKIŞI

### 2.1. Çözüm ve Katman Yapısı

```
                +-------------------------------------------------+
                |            OlcuAtolyesi.Core (net10.0)          |
                |  - Geometry (ImageCoordinates, Transforms, etc) |
                |  - Export (DxfBuilder, DxfExportService)        |
                |  - Measurement (Ruler, Calibration, Library)    |
                |  - Phone (Clients, Contracts, ADB, Scrcpy)      |
                |  - Services (ProjectService, SettingsService)   |
                |  - Diagnostics (Logger, Masker, LogHub)         |
                +-------------------------------------------------+
                         ^                               ^
                         |                               |
        +--------------------------------+   +--------------------------------+
        |  OlcuAtolyesi.PC (net10.0-win) |   |  OlcuAtolyesi (MAUI net10.0)   |
        |  - WPF Desktop Application     |   |  - Android (CameraX, PixelCopy)|
        |  - MVVM Architecture           |   |  - Windows Desktop (Companion) |
        |  - Views: MainWindow, Views/*  |   |  - PcCameraStreamServer (HTTP) |
        |  - Controls: MeasurementCanvas |   |  - Mobile UI: ContentPages     |
        +--------------------------------+   +--------------------------------+
```

1. **`OlcuAtolyesi.Core`:** Hiçbir UI çerçevesine (WPF veya MAUI) bağımlı olmayan saf C# .NET 10 sınıf kütüphanesidir. Ölçüm geometrisi, DXF motoru, telefon istemcileri ve dosya yönetimi burada yaşar.
2. **`OlcuAtolyesi.PC`:** Kurumsal Windows masaüstü uygulamasıdır. MVVM desenini izler. Çoklu pencere (Modeless) yaşam döngüsüyle ana pencere, bağımsız tam ekran ölçüm penceresi (`MeasurementWindow`), tanılama konsolu (`DiagnosticConsoleWindow`) ve ekran kalibrasyon sihirbazını (`ScreenCalibrationWindow`) yönetir.
3. **`OlcuAtolyesi` (Mobil):** Android öncelikli MAUI uygulamasıdır. İçinde hem bağımsız mobil ölçüm araçları hem de PC'ye kamera akışı sağlayan dahili HTTP/MJPEG sunucusu barındırır.

### 2.2. Telefon <-> PC Ağ İletişim Protokolü (Wi-Fi & HTTP API)

Telefon tarafında `PcCameraStreamServer` (`HttpListener` tabanlı) çalışır. PC tarafında ise `CameraStreamClient` (önizleme için) ve `RemotePhotoClient` (fotoğraf transferi için) istemcileri görev yapar.

```
[Android Telefon: PcCameraStreamServer :8088]
      ^
      |-- 1. POST /api/v1/auth { "pin": "123456" }
      |   <-- 200 OK { "token": "64_hex_chars", "expiresInSeconds": 3600 }
      |   (5 hatalı denemede 60 sn IP kilidi -> HTTP 429 Too Many Requests)
      |
      |-- 2. GET /stream (Header: X-Session-Pin: 123456)
      |   <-- 200 OK (Content-Type: multipart/x-mixed-replace; boundary=--frame)
      |       [15 FPS JPEG Kareleri - BoundedChannel DropOldest]
      |
      |-- 3. POST /api/v1/capture (Header: Authorization: Bearer <token>)
      |   <-- 200 OK { "photoId": "uuid", "fileName": "...", "sha256": "...", "width": 4080, "height": 3060 }
      |
      |-- 4. GET /api/v1/photos/{id} (Header: Authorization: Bearer <token>)
      |   <-- 200 OK (Binary JPEG Data, Header: X-SHA256: ...)
      |
      |-- 5. GET /logs (Header: X-Session-Pin: 123456)
          <-- 200 OK (JSON Array of DiagnosticLogEntry)
```

#### Protokolün Kritik Güvenlik ve Dayanıklılık Nitelikleri:
- **PIN Kaba Kuvvet Kilidi (Brute-Force Guard):** `_pinAttempts` eşlemi ile istemci IP'si izlenir. 5 ardışık hatalı PIN denemesinde IP 60 saniye kilitlenir (`HTTP 429`).
- **Oturum Belirteci (Bearer Token):** 32 bayt kriptografik rastgele bayttan üretilen 64 karakterli hex dizesidir. 1 saat geçerlidir.
- **Otomatik 401 Kurtarma (Token Refresh):** `RemotePhotoClient`, telefon sunucusu yeniden başlatıldığında veya token süresi dolduğunda HTTP 401 alırsa, hafızadaki güvenli PIN ile arka planda sessizce `/api/v1/auth` çağırıp yeni token alır ve çekim/indirme isteğini tek seferlik yineler.
- **SHA-256 Bütünlük Doğrulaması:** Dosya indirilirken telekomünikasyon/Wi-Fi paket bozulmalarını önlemek için indirilen baytların SHA-256 özeti hesaplanır; sunucunun bildirdiği özet ile eşleşmezse dosya diske yazılmaz, `InvalidDataException` fırlatılır.
- **Düşük Gecikmeli MJPEG Kanalı:** `System.Threading.Channels` ile `BoundedChannelOptions(Capacity = 2, FullMode = DropOldest)` kullanılır. Ağ veya PC render hızı yetişemezse eski kareler atılır; video akışında gecikme birikmesi (lag accumulation) imkansız kılınır.

### 2.3. USB scrcpy İletişim Hattı
Alternatif olarak USB üzerinden telefon bağlıyken:
- `AdbCommandRunner`: `adb.exe` konumunu otomatik bulur (`LOCALAPPDATA`, `Program Files`, Android SDK yolları). `adb devices -l` çıktısını ayrıştırır (`device`, `unauthorized`, `offline` durumlarını ayırt eder).
- `ScrcpyLocatorAndParser`: `scrcpy.exe` (5.0.1) sürümünü denetler; `--list-cameras` ve `--list-camera-sizes` komutlarının konsol çıktılarını ayrıştırarak sensörleri (ön, arka) ve çözünürlükleri dinamik ComboBox'a doldurur.
- `ScrcpyProcessManager`: Donanım hızlandırmalı SDL3 penceresini ayrı bir süreç olarak başlatır, PID takibi yapar ve pencere kapandığında süreci temizce sonlandırır.

---

## BÖLÜM 3: KRİTİK ALGORİTMALAR VE MATEMATİKSEL ALTYAPI

### 3.1. Koordinat Sistemleri ve Ayrım İlkesi

Sistemde üç ayrı koordinat düzlemi bulunur ve bunlar asla birbirine karıştırılmaz:

```
[1. Orijinal Fotoğraf Koordinatı]  --->  (x, y) piksel, [0..ImageWidth, 0..ImageHeight]
       |
       | ImageDisplayTransform (Kırp -> Aynala -> Döndür)
       v
[2. Gösterim / Tuval Koordinatı]   --->  (x', y') ekranda yönlendirilmiş piksel
       |
       | ImageViewportTransform (ZoomLevel, PanOffset)
       v
[3. Ekran / Viewport Koordinatı]   --->  (X_dip, Y_dip) WPF FrameworkElement üzeri nokta
```

- **`MeasurementSession`:** Tüm ölçüm noktalarını (`R1, R2, M1, M2`) **1. Orijinal Fotoğraf Koordinatında** saklar.
- **`ImageViewportTransform`:** Tuvalin yakınlaştırma (`ZoomLevel`, %10–%5000) ve kaydırma (`PanOffset`) parametrelerini yönetir. Görüntü pikselini tuval koordinatına (`ImageToViewport`) ve fare tıklamasını görüntü pikseline (`ViewportToImage`) dönüştürür.
- **`ImageDisplayTransform`:** Görüntü 90°/180° döndürüldüğünde, yatay/dikey aynalandığında veya kırpıldığında, noktaları ekranda doğru yere çizmek ve fare etkileşimini doğru almak için iki yönlü tersinir dönüşüm sağlar:
  $$\text{ToDisplay}(P) \longleftrightarrow \text{ToOriginal}(P')$$

### 3.2. Ölçüm Matematiği (`ReferenceScale` ve `MeasurementGeometry`)

1. **Piksel Mesafesi:**
   $$\Delta X_R = R2.X - R1.X, \quad \Delta Y_R = R2.Y - R1.Y$$
   $$L_{\text{px, ref}} = \sqrt{\Delta X_R^2 + \Delta Y_R^2}$$
2. **Ölçek Katsayısı (Scale Factor):**
   Kullanıcının girdiği bilinen gerçek uzunluk $L_{\text{known}}$ (örn: 85.60 mm) ise:
   $$K_{\text{mm/px}} = \frac{L_{\text{known, mm}}}{L_{\text{px, ref}}}$$
3. **Mesafe Ölçümü (M1–M2):**
   $$L_{\text{px, meas}} = \sqrt{(M2.X - M1.X)^2 + (M2.Y - M1.Y)^2}$$
   $$L_{\text{real, mm}} = L_{\text{px, meas}} \times K_{\text{mm/px}}$$
   $$L_{\text{target}} = \frac{L_{\text{real, mm}}}{\text{UnitScale(TargetUnit)}}$$
4. **Açı Hesabı (`MeasureAngleDegrees`):**
   Merkez nokta $B$, kollar $A$ ve $C$ olmak üzere:
   $$\vec{u} = A - B, \quad \vec{v} = C - B$$
   $$\theta = \arccos\left(\frac{\vec{u} \cdot \vec{v}}{\|\vec{u}\| \|\vec{v}\|}\right) \times \frac{180}{\pi}$$

### 3.3. DXF Üretim Algoritması (`DxfBuilder`)

AutoCAD Release 12 ASCII (`AC1009`) spesifikasyonuna göre sıfır harici kütüphane ile saf C# üzerinde inşa edilmiştir:

1. **Y Ekseni Ters Çevirme (Coordinate Flip):**
   - Fotoğraf koordinatında orijin sol-üsttedir ($Y$ aşağı artar).
   - CAD/DXF standart Kartezyen koordinatında orijin sol-alttadır ($Y$ yukarı artar).
   - Formül:
     $$X_{\text{DXF}} = X_{\text{img}} \times K_{\text{mm/px}}$$
     $$Y_{\text{DXF}} = (H_{\text{img}} - Y_{\text{img}}) \times K_{\text{mm/px}}$$
2. **Birim Tanımı:**
   Header içine `$INSUNITS = 4` (Milimetre) yazılarak AutoCAD/SolidWorks/FreeCAD'de dosya açıldığında ölçeğin birebir 1:1 milimetre gelmesi garanti edilir.
3. **Katman Yapısı (Layer Table):**
   - `REFERENCE` (Renk No: 1 - Kırmızı): Bilinen referans çizgisi ve R1/R2 uç noktalarına yerleştirilen yarıçapı $1.5 \times K_{\text{mm/px}}$ olan `CIRCLE` varlıkları.
   - `MEASURE` (Renk No: 3 - Yeşil): Ölçülen mesafe çizgisi ve M1/M2 uç noktalarına yerleştirilen `CIRCLE` varlıkları.
   - `LABELS` (Renk No: 7 - Beyaz): Nokta adları ve hesaplanan uzunluk metinleri (`TEXT` varlıkları).
4. **ASCII Karakter Normalizasyonu:**
   AutoCAD R12 ASCII parser'ının Unicode UTF-8 karakterlerde (Türkçe `ı, İ, ş, Ş, ğ, Ğ, ü, Ü, ö, Ö, ç, Ç`) çökmesini önlemek amacıyla `ToAsciiText` metodu tüm harfleri 7-bit ASCII karşılıklarına (`i, I, s, S, g, G, u, U, o, O, c, C`) çevirir.

### 3.4. Büyüteç (Magnifier / Loupe) ve Crosshair Matematiği

- **Optik Büyütme:** 2×, 4× ve 8× kademeleri desteklenir.
- **İmleç / Parmak Kapatmama Algoritması:** Büyüteç penceresi fare imlecinin doğrudan üzerine konmaz. Tuval 4 kadrana bölünür; imleç sol-üstteyse büyüteç sağ-alta, sağ-alttaysa sol-üste dinamik olarak ötelenir.
- **Görüntüleme Düzeltmesi (v0.6.3):** Büyüteç içindeki daire kırpma (`EllipseGeometry`) işleminde WPF arka plan opak fırçasının görüntüyü örtmemesi için önce arka plan çizilir, ardından kırpılmış bitmap basılır, kenarlık dolgusuz (`null`) olarak üzerine çizilir.

### 3.5. Dinamik Cetvel Taksimat Algoritması (`RulerTickCalculator` & `ScreenCalibration`)

- **A Cetveli (Nesne Cetveli):** Fotoğrafın üst ve sol kenarında nesnenin gerçek boyutunu gösterir. Ekranda hedef aralık (~90 DIP) baz alınarak milimetre adayları `[0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000]` arasından en uygun adım seçilir. Referans yoksa piksel adayları `[5, 10, 20, 50, 100, 200, ...]` üzerinden taksimat yapılır. Her ana taksimat 5 alt bölmeye (`Subdivisions = 5`) ayrılır.
- **B Cetveli (Ekran Cetveli):** Monitörün fiziksel camı üzerine konulan cetveller için ekranın sağ alt köşesinde çizilir. Yalnızca `ScreenCalibration` geçerliyse çizilir; aksi halde ekranda hiçbir şey gösterilmez.
- **Ekran Kalibrasyonu:**
  $$\text{MillimetresPerDip} = \frac{\text{KnownMillimetres}}{\text{MeasuredDips}}$$
  Örnek: Ekranda 378 DIP tutan bir mesafe fiziksel 100 mm cetvelle ölçüldüyse $\text{MillimetresPerDip} = 100 / 378 \approx 0.26455 \text{ mm/DIP}$.

---

## BÖLÜM 4: KOD KALİTESİ, TASARIM DESENLERİ VE STANDARTLAR

### 4.1. İsimlendirme ve Dil Standartları
- **C# Kodları ve Tipleri:** Standart PascalCase (sınıflar, metotlar, özellikler) ve `_camelCase` (özel alanlar). Kod içi semboller İngilizce terimlerle yapılandırılmıştır.
- **Kullanıcı Arayüzü ve Dokümantasyon:** %100 saf Türkçe kuralı geçerlidir. Hata mesajları, düğme metinleri, log açıklamaları ve raporlar Türkçedir (`AppText.cs`, `PcText.cs`).
- **Birimler:** Metrik sistem önceliklidir (`mm`, `cm`, `m`); inç (`inç` / `inch`) desteklenir.

### 4.2. Tasarım Desenleri (Design Patterns)
1. **MVVM (Model-View-ViewModel):**
   - Core katmanında `ObservableObject` (`INotifyPropertyChanged` tabanlı, `SetProperty` ile optimize edilmiş) ve `RelayCommand` (`ICommand`).
   - XAML tarafında zengin `DataTemplate` seçicileri; Code-behind dosyaları yalnızca saf UI etkileşimi (tuval fare hareketleri, klavye kancaları) için kullanılır.
2. **Modeless Window & Tekil Örnek Yaşam Döngüsü (Singleton Window Pattern):**
   - `DiagnosticConsoleWindow`, `MeasurementWindow` ve `ScreenCalibrationWindow` pencereleri modeless (arka planı kilitlemeyen) olarak açılır.
   - ViewModel seviyesinde tekil referans tutulur (`_measurementWindow`, `_calibrationWindow`); pencere zaten açıksa `Activate()` edilir; kapatıldığında `Closed` olayıyla referans `null` yapılır.
3. **Hub / Observer Deseni (`DiagnosticLogHub`):**
   - Uygulama genelindeki tüm loglar statik bir hub'a akar. Konsol pencereleri ve dosya kaydediciler bu hub'ın `LogEmitted` olayına abone olur.
4. **Boru Hattı Deseni (`PhotoFilterPipeline`):**
   - Görüntü filtreleri (parlaklık, kontrast, keskinlik, gri ton, negatif, Sobel) sırayla çalışan, birbirinden bağımsız pikselleri işleyen zincirleme bir boru hattıdır.

### 4.3. Hata Yönetimi ve Dayanıklılık Stratejileri
- **Atomik Dosya Kaydı ve .bak Kurtarma:** `DesktopProjectService`, projeleri kaydederken önce geçici dosyaya (`.tmp`) yazar, ardından asıl dosyanın yedeğini (`.bak`) alır ve hedef dosyayı atomik olarak değiştirir. Güç kesintisi durumunda `.bak` dosyasından otomatik onarım yapılır.
- **Zip Slip Güvenliği:** Mobil veya dış kaynaklı `.zip` proje paketleri açılırken, arşivdeki dosya yollarının hedef klasörün dışına çıkmasını (`..\..\evil.exe`) engelleyen kanonik yol denetimi uygulanır.
- **Döngüsel Günlükleme (Circular File Logging):** `%LOCALAPPDATA%\OlcuAtolyesi\Logs` altında en fazla 2 MB'lık dosyalar oluşturulur; 5 dosyaya ulaşıldığında en eski dosya silinerek diskin dolması engellenir (maks 10 MB).
- **Hassas Veri Maskeleme (`DiagnosticDataMasker`):** Loglarda geçen PIN kodları, şifreler ve kimlik bilgileri regex ile `***` şeklinde maskelenir.

---

## BÖLÜM 5: ÇALIŞMA AĞACI (WORKING TREE), TUTARSIZLIKLAR VE TESPİT EDİLEN SORUNLAR

Bu bölüm, projeyi devralacak yapay zeka mimarının kod yazmaya başlamadan önce düzeltmesi veya dikkat etmesi gereken pürüzleri listeler.

### 5.1. Sürüm Numarası Tutarsızlıkları (Version Inconsistencies)
Proje şu anda PC tarafında **v0.6.7**, Mobil tarafında ise **v0.6.2** seviyesindedir. Ancak kod tabanında eski sürümlerden kalma statik metinler mevcuttur:

1. **`MainWindow.xaml` Başlık ve Kartları:**
   - Satır 6: `Title="Ölçü Atölyesi PC — v0.5.0.5"` (v0.6.7 olmalıdır).
   - Satır 42: `Text="Ölçü Atölyesi PC — Masaüstü Çalışma Alanı (v0.5.0.5)"` (v0.6.7 olmalıdır).
   - Satır 1264: `Text="v0.5.0.5 • Windows Masaüstü"` (v0.6.7 olmalıdır).
2. **`MainWindowViewModel.cs`:**
   - Satır 30: `public string StateBadge => Descriptor.State == ModuleImplementationState.Planned ? "Planlandı" : "v0.5.0.5";` ifadesi hardcoded olarak `v0.5.0.5` döndürmektedir.
   - Satır 299: `new PlannedActionItem("2B DXF Dosyası Oluştur • Planlandı", ...)` v0.6.0'da tamamlanmış olmasına rağmen hâlâ "Planlandı" olarak listelenmektedir.
3. **`PcText.cs`:**
   - Satır 6: `public const string Version = "v0.5.0.5 — Akıcı Kamera, Telefona Orijinal Fotoğraf Kaydı ve PC'ye Kayıpsız Dosya Aktarımı";` (v0.6.7 açıklamasıyla güncellenmelidir).
4. **`ModuleCatalogService.cs`:**
   - Modül rozetlerinde ve açıklamalarında `v0.4.0` ve `v0.5.0` metinleri kalmıştır; v0.6.7 kabiliyetlerini tam yansıtmamaktadır.

### 5.2. Yorum Satırları ve TODO Analizi
- Kod tabanında hiçbir aktif `TODO`, `FIXME` veya `HACK` yorum satırı **bulunmamaktadır**.
- Bunun yerine gelecekteki fazlar mimari modellerde `ModuleImplementationState.Planned` veya sözleşme sınıflarında (`RemoteCameraContracts.cs`) açıkça tanımlanmıştır.

### 5.3. Mobil Projedeki Commit Edilmemiş Değişiklikler (Working Tree Analizi)
`D:\proje\OlcuAtolyesi` deposunda `git status` yapıldığında commit edilmemiş **7 değiştirilmiş** ve **2 yeni** dosya bulunmaktadır:

```text
Changes not staged for commit:
  modified:   CHANGELOG.md
  modified:   OlcuAtolyesi/OlcuAtolyesi.csproj
  modified:   OlcuAtolyesi/Resources/Strings/AppText.cs
  modified:   OlcuAtolyesi/Views/PcCameraPage.cs
  modified:   PROJECT_STATUS.md
  modified:   README.md
  modified:   Tests/LogicChecks/Program.cs

Untracked files:
  Docs/Reports/v0.6.2_Onizleme_Hatti_Duzeltmesi_Raporu.md
  OlcuAtolyesi/Services/PreviewFrameRateLimiter.cs
```

#### Satır Satır Değişiklik Özeti:
1. **`OlcuAtolyesi/Services/PreviewFrameRateLimiter.cs` (YENİ DOSYA):**
   - 15 FPS hedefleyen (`TargetIntervalMs = 66.66 ms`) zamanlayıcı sınıfı. Önceki kare zamanı ile şimdiki zaman farkını ölçerek kare yakalamaya onay verir (`ShouldCapture`).
2. **`OlcuAtolyesi/Views/PcCameraPage.cs`:**
   - **Kaldırılan Tehlikeli Kod:** CameraX üzerinde `FindTextureView` başarısız olunca her karede tam çözünürlüklü (12 MP, 4080×3060) donanımsal çekim yapan ve UI iş parçacığında JPEG sıkıştıran `CaptureImage` yedek yolu tamamen silindi (1 FPS ve siyah ekranın ana sebebiydi).
   - **Eklenen PixelCopy Kodu:** `FindSurfaceView` metodu eklendi. Android N (API 24+) `Android.Views.PixelCopy.Request` kullanılarak `SurfaceView` donanım tamponundan doğrudan hızlı kare kopyalaması sağlandı.
   - **Asenkron JPEG Sıkıştırma:** Alınan bitmap `Task.Run` ile arka planda %70 kalitede JPEG'e çevrilip `PcCameraStreamServer.PublishFrame`'e gönderildi.
   - Döngü sonu gecikmesi 100 ms'den 10 ms'ye düşürüldü; arayüze `_surfaceFrameFailures` hata sayacı eklendi.
3. **`OlcuAtolyesi/OlcuAtolyesi.csproj`:**
   - `ApplicationDisplayVersion` 0.6.1'den `0.6.2`'ye yükseltildi.
   - `ApplicationVersion` 12'den `13`'e yükseltildi.
4. **`OlcuAtolyesi/Resources/Strings/AppText.cs`:**
   - Sürüm başlığı `0.6.2 — Akıcı Canlı Önizleme ve Düşük Gecikmeli Kare Aktarımı` yapıldı.
5. **`Tests/LogicChecks/Program.cs`:**
   - Test 25 eklendi: `CheckPreviewFrameSourceAndRateLimiter`. `PreviewFrameRateLimiter` matematiksel davranışı ve `PcCameraPage.cs` içinde `CaptureImage` kelimesinin en fazla 1 kez geçebileceği kuralı kilitlendi.
6. **`Docs/Reports/v0.6.2_Onizleme_Hatti_Duzeltmesi_Raporu.md` (YENİ DOSYA):**
   - v0.6.2 için ayrıntılı Türkçe sonuç raporu yazıldı (25/25 test başarılı, 47.66 MB APK üretildi).

> **KRİTİK UYARI:** AGENTS.md §2 gereğince mobil projedeki bu değişikliklere PC ajanları tarafından dokunulmamalı, commit/push yapılmamalıdır. Kullanıcı ve mobil ajan tarafından yönetilmelidir.

---

## BÖLÜM 6: YENİ BİR MİMAR İÇİN REHBER VE DİKKAT EDİLECEK HUSUSLAR

Bir sonraki sürümde (ör. `v0.7.0 — Zemin Kalibrasyonu ve Perspektif Düzeltme`) çalışacak yapay zeka mimarının bilmesi gereken temel direktifler:

1. **Asla Koordinat Dönüşümünü Kırma:** Yeni bir zemin homografisi eklerken, homografi matrisini ($3 \times 3$ `H`) `MeasurementSession` içine ayrı bir `IPlaneTransform` olarak ekle. `R1`, `R2`, `M1`, `M2` orijinal piksel değerleri olarak kalmalıdır.
2. **OpenCV Kararını Dikkatli Ver:** `Docs/ROADMAP.md §5` gereğince OpenCV entegrasyonu için hafif saf C# homografi mi yoksa `OpenCvSharp4` mü kullanılacağı karara bağlanmalıdır. Bağımlılık boyutu ve bakım maliyeti ön planda tutulmalıdır.
3. **Sürüm Tutarsızlıklarını Gider:** Kodlamaya başlamadan önce `MainWindow.xaml` ve `MainWindowViewModel` içindeki `v0.5.0.5` statik metinlerini temizleyip dinamik sürüm bilgisine (`Assembly.GetExecutingAssembly()`) bağlamak teknik borcu kapatacaktır.
4. **Testleri Daima Çalıştır:** PC projesinde 54 test bulunmaktadır. Her değişiklikten sonra:
   ```powershell
   dotnet build D:\proje\OlcuAtolyesi-PC\OlcuAtolyesi.PC.sln -c Debug
   dotnet run --project D:\proje\OlcuAtolyesi-PC\tests\OlcuAtolyesi.PC.Tests\OlcuAtolyesi.PC.Tests.csproj -c Debug
   ```
   çalıştırılmalı ve 54/54 başarısı teyit edilmelidir.
