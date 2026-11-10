# Ölçü Atölyesi PC

**v0.6.7 — Sade Ana Pencere, Cetveller ve Ekran Kalibrasyonu (.NET 10 / WPF / MVVM)**

Ölçü Atölyesi PC, Android telefon kamerasıyla veya masaüstündeki fotoğraflarla yapılan ölçümleri kalibre edilmiş düzlemde işlemek, yerel ağ (Wi-Fi) üzerinden Ölçü Atölyesi Android Kamera Servisi'ne bağlanarak doğrudan WPF penceresi içinde akıcı kamera önizlemesi sunmak, tek dokunuşla donanımın desteklediği yüksek çözünürlükte uzaktan fotoğraf çekmek ve SHA-256 bütünlük doğrulamasıyla kayıpsız dosya aktarımı sağlamak, alternatif USB/scrcpy motorunu çalıştırmak, USB/ADB üzerinden bağlı Android telefonları denetlemek, projeleri kalıcı olarak saklamak, mobil `v0.4.0` proje paketleriyle çift yönlü paylaşmak, otomatik kontur/delik çıkarmak, **DXF / STEP / STL** CAD çıktıları üretmek, modelleri **3B baskıya** hazırlamak ve **Merkezi Tanılama Konsolu** ile PC/telefon loglarını gerçek zamanlı izlemek için geliştirilen bağımsız Windows masaüstü uygulamasıdır.

> **Kapsam notu:** Aşağıdaki giriş metni ürün vizyonunu tanımlar. **Uygulanmış** özellikler için
> §2'deki durum rozetlerine ve `PROJECT_STATUS.md`'ye bakın. **2B DXF ihracı v0.6.0 ile etkinleştirilmiştir.**
> Otomatik kontur çıkarma, 3B STEP/STL üretimi ve 3B baskı entegrasyonu planlanan sırada
> (`Docs/ROADMAP.md`) geliştirilecektir.

---

## 1. Çözüm Mimarisi ve Klasör Yapısı

```text
D:\proje\OlcuAtolyesi-PC
├── OlcuAtolyesi.PC.sln
├── AGENTS.md
├── PROJECT_STATUS.md
├── CHANGELOG.md
├── README.md
├── Docs/
│   ├── ROADMAP.md
│   └── Reports/
│       ├── v0.1.0_Sonuc_Raporu.md
│       ├── v0.2.0_Sonuc_Raporu.md
│       ├── v0.3.0_Sonuc_Raporu.md
│       ├── v0.4.0_Sonuc_Raporu.md
│       ├── v0.4.1_Sonuc_Raporu.md
│       ├── v0.4.2_Sonuc_Raporu.md
│       ├── v0.5.0_Sonuc_Raporu.md
│       ├── v0.5.0.1_WiFi_Kamera_Duzeltme_Raporu.md
│       ├── v0.5.0.2_Kare_Yakalama_Duzeltme_Raporu.md
│       ├── v0.5.0.3_Ikon_Splash_Responsive_Raporu.md
│       ├── v0.5.0.4_Kamera_Guvencesi_Ve_Tanilama_Konsolu_Raporu.md
│       ├── v0.5.0.5_Kamera_Performansi_ve_Fotograf_Aktarimi_Raporu.md
│       ├── v0.5.0.6_Belirtec_Yenileme_Raporu.md
│       ├── v0.5.0.6_Dokuman_ve_Yol_Haritasi_Raporu.md
│       ├── v0.6.0_DXF_Ihrac_Raporu.md
│       ├── v0.6.1_Profesyonel_Cizim_Raporu.md
│       ├── v0.6.2_Buyutec_Iyilestirmesi_Raporu.md
│       ├── v0.6.3_Buyutec_Duzeltmesi_Raporu.md
│       ├── v0.6.4_Olcum_Penceresi_Raporu.md
│       ├── v0.6.5_Goruntu_Ayarlari_Raporu.md
│       ├── v0.6.6_Ikonlar_ve_Pencere_Tasima_Raporu.md
│       └── v0.6.7_Sade_Ana_Pencere_ve_Cetveller_Raporu.md
├── src/
│   ├── OlcuAtolyesi.Core/          # Platformdan bağımsız ortak çekirdek (.NET 10)
│   │   ├── Diagnostics/            # DiagnosticEnums, DiagnosticDataMasker, DiagnosticLogEntry, CircularDiagnosticLogger, DiagnosticLogHub
│   │   ├── Export/                 # DxfExportModels, DxfBuilder, DxfExportService (2B DXF R12 ASCII AC1009)
│   │   ├── Geometry/               # LengthUnit, LengthValue, ImagePoint, ImageSegment, ImageViewportTransform, ImageDisplayTransform
│   │   ├── Measurement/            # RulerTickCalculator, ReferenceObjectLibrary, ScreenCalibration
│   │   ├── Geometry/               # LengthUnit, LengthValue, ImagePoint, ReferenceScale, MeasurementGeometry, ImageViewportTransform, MeasurementSession, ImageDisplayTransform
│   │   ├── Models/                 # ModuleId, ModuleDescriptor, OpenSourceCandidate, DesktopProject, ProjectMeasurementItem
│   │   ├── Mvvm/                   # ObservableObject, RelayCommand
│   │   ├── Phone/                  # AdbLocator, ProcessAdbCommandRunner, AdbOutputParser, UsbPhoneConnectionService, RemoteCameraContracts, ScrcpyLocator, ScrcpyOutputParser, ScrcpyProcessManager, CameraStreamClient
│   │   └── Services/               # DesktopSettingsService, ModuleCatalogService, DesktopProjectService
│   └── OlcuAtolyesi.PC/            # WPF Masaüstü Uygulaması (net10.0-windows)
│       ├── Controls/               # MeasurementCanvas (CAD ölçü çizgisi, ok başı, büyüteç, ölçek çubuğu, ızgara, ekran dönüşümü), ToolStateAdvisor (adım rehberi)
│       ├── Media/                  # PhotoLoader, BrandedAssetGenerator, PhotoFilterPipeline
│       ├── Resources/Strings/      # Türkçe merkezi metinler (PcText)
│       ├── Resources/              # app.ico (7 katman), app_icon.png, appicon.svg, splash.png (WPF SplashScreen)
│       ├── Services/               # IPhotoFileDialogService, WpfPhotoFileDialogService
│       ├── Themes/                 # DarkTheme.xaml, LightTheme.xaml, SharedStyles.xaml, ThemeManager.cs
│       ├── ViewModels/             # MainWindow, Dashboard, Projects, PhoneConnection, CameraCalibration, PhotoMeasurement, DiagnosticConsole, Settings, Help, ImageAdjustment, ModulePage VM
│       ├── Views/                  # DiagnosticConsoleWindow.xaml (.cs), MeasurementWindow.xaml (.cs)
│       ├── App.xaml / App.xaml.cs
│       └── MainWindow.xaml / MainWindow.xaml.cs
└── tests/
    └── OlcuAtolyesi.PC.Tests/      # 54 otomatik test: başlangıç, navigasyon, geometri, fotoğraf, kalıcı proje, mobil uyumluluk, USB/ADB, scrcpy, Wi-Fi doğrudan önizleme, kare yakalama-aktarım, kurumsal kimlik/responsive düzen, döngüsel loglama, PIN maskeleme, uzaktan log alımı, uzaktan yüksek çözünürlüklü çekim, oturum belirteci 401 kurtarma, 2B DXF üretimi, adım rehberi, profesyonel çizim, gelişmiş büyüteç, kısayollar, ayrı pencere (MeasurementWindow), tersinir görüntü dönüşümü, filtre hattı, ikonlar, pencere ayrımı, cetvel taksimatı, referans kütüphanesi, ekran kalibrasyonu ve sade ana pencere kontrolleri
```

---

## 2. Sol Navigasyon Modülleri (v0.6.7)

1. **Ana Panel (`Etkin • v0.5.0.5`):** Modül hazırlık durumları, özet sayaçlar ve her modüle tek tıkla geçiş kartları.
2. **Projeler (`Etkin • v0.5.0.4`):** Kalıcı proje oluşturma, kaydetme, yeniden adlandırma, silme, atomik `project.json` + `.bak` yedeği ve mobil `v0.4.0` proje paketlerini (`.zip` / klasör / `project.json`) içe ve dışa aktarma.
3. **Telefon Bağlantısı (`Etkin • v0.5.0.4`):** USB (`ADB`) üzerinden bağlı Android telefonları tarama (`Cihazları Tara`), `device` / `unauthorized` / `offline` durum ayrımı, `Cihaz Seç`, `Bağlantıyı Kontrol Et`, `Bağlantıyı Yenile`, `Bağlantıyı Ayır`; `Wi-Fi` ve `Bluetooth` planlandı; kamera kontrol sözleşmeleri (`IRemotePhoneCameraService`) hazır.
4. **Kamera ve Kalibrasyon (`Etkin • v0.5.0.5`):** `scrcpy 5.0.1` motoru ile USB üzerinden canlı Android telefon kamerası akışı, dinamik kamera sensörü algılama (`--list-cameras`) ve ön/arka/harici sensör seçimi, seçili sensörün gerçek modlarını dinamik sorgulama (`--list-camera-sizes`), standart ve high-speed mod ayrımı, çözünürlüğe bağlı dinamik FPS seçenekleri, kullanıcı kontrollü `Kamerayı Başlat` ve `Kamerayı Durdur`, izole PID takibi ve yönetilen SDL3 önizleme penceresi. Zemin kalibrasyonu ve perspektif düzeltme `v0.7.0` için planlandı (bkz. `Docs/ROADMAP.md`).
5. **Fotoğraftan Ölçüm (`Etkin • v0.6.7`):** Bilgisayardan fotoğraf açma, sadeleştirilmiş ana pencere düzeni (büyük "ÖLÇÜM PENCERESİ (F2)" butonu, hızlı hazır şablon referans seçimi ComboBox'ı, referans uzunluğu kutusu, birim seçimi, "Cetvelleri Göster" kutusu, uyarı bandı, kompakt alt bilgi çubuğu), "Ayrı Pencerede Aç" butonu ile bağımsız tam ekran `MeasurementWindow` (F11 geçişi, paylaşılan senkron oturum) içinde sekmeli araç paneli (`Görüntü`, `Ölçüm`, `Proje`, `DXF`), `Segoe Fluent Icons` glif ikonları (`IconGlyph`), parlaklık/kontrast/keskinlik/gri ton/negatif/Sobel filtreleri, geometrik düzenleme (döndürme, aynalama, kırpma), iki yönlü tersinir koordinat dönüşümü (`ImageDisplayTransform`), A Cetveli (Nesne Cetveli: mm/px taksimatı), B Cetveli (Ekran Cetveli: fiziksel mm), etkileşimli Ekran Kalibrasyon Penceresi (`ScreenCalibrationWindow`), tuval sağ tık menüsü (ContextMenu), CAD tipi ok başlı ölçü çizgileri, `ToolStateAdvisor` Türkçe adım rehberi, imleci kapatmayan 2×/4×/8× büyüteç, klavye kısayolları (F2, F, L, 2, 4, 8, Ctrl+Sol/Sağ, Ctrl+H/V) ve 2B DXF dışa aktarma.
6. **CAD / DXF / STEP / STL (`Kısmen Etkin • v0.6.0 (2B DXF)`):** 2B DXF (R12 ASCII, AC1009, $INSUNITS=4 mm) ihracı etkinleştirildi; CIRCLE, LINE ve TEXT varlıkları REFERENCE, MEASURE ve LABELS katmanlarında oluşturulur. 3B STEP ve STL katı model oluşturma mimarisi planlandı (`v0.8.0`).
7. **3B Baskı (`Planlandı`):** `OrcaSlicer` / `PrusaSlicer` dilimleme ve `Moonraker` / `OctoPrint` yazıcı kontrolü (`v1.0 sonrası`).
8. **Ayarlar (`Etkin • v0.5.0.5`):** Açık / Koyu / Sistem teması, monitör kalibrasyonu ve varsayılan birim (`mm`, `cm`, `m`, `inç`) kalıcılığı.
9. **Yardım (`Etkin • v0.5.0.5`):** Mimari yol haritası ve açık kaynak projelerin lisans/entegrasyon matrisi.

---

## 3. Derleme ve Test

```powershell
# Bütün otomatik mantık, geometri, proje, ADB, scrcpy, uzaktan çekim, DXF, çizim, büyüteç, ayrı pencere, görüntü dönüşümü, filtre, ikon, cetvel ve kalibrasyon testleri (54/54)
dotnet run --project tests/OlcuAtolyesi.PC.Tests/OlcuAtolyesi.PC.Tests.csproj -c Debug

# Windows Debug Derlemesi
dotnet build OlcuAtolyesi.PC.sln -c Debug

# Windows Release Derlemesi
dotnet build OlcuAtolyesi.PC.sln -c Release
```
