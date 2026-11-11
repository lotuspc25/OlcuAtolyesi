# Ölçü Atölyesi PC

**v0.6.8 — Sade Ana Pencere, Cetveller, Ekran Kalibrasyonu ve Kolay Dağıtım (.NET 10 / WPF / MVVM)**

Ölçü Atölyesi PC, Android telefon kamerasıyla veya masaüstündeki fotoğraflarla yapılan ölçümleri kalibre edilmiş düzlemde işlemek, yerel ağ (Wi-Fi) üzerinden Ölçü Atölyesi Android Kamera Servisi'ne bağlanarak doğrudan WPF penceresi içinde akıcı kamera önizlemesi sunmak, tek dokunuşla donanımın desteklediği yüksek çözünürlükte uzaktan fotoğraf çekmek ve SHA-256 bütünlük doğrulamasıyla kayıpsız dosya aktarımı sağlamak, alternatif USB/scrcpy motorunu çalıştırmak, USB/ADB üzerinden bağlı Android telefonları denetlemek, projeleri kalıcı olarak saklamak, mobil `v0.4.0` proje paketleriyle çift yönlü paylaşmak, otomatik kontur/delik çıkarmak, **DXF / STEP / STL** CAD çıktıları üretmek, modelleri **3B baskıya** hazırlamak ve **Merkezi Tanılama Konsolu** ile PC/telefon loglarını gerçek zamanlı izlemek için geliştirilen bağımsız Windows masaüstü uygulamasıdır.

---

## 🚀 Hızlı Başlangıç: İndirme ve Çalıştırma Rehberi

Uygulamayı bilgisayarınızda veya sanal makinenizde çalıştırmak için aşağıdaki yöntemlerden size en uygun olanını seçebilirsiniz:

### Yöntem 1: Kurulumsuz Taşınabilir Sürüm (Önerilen — En Kolay Yol)
> **Kimler İçin:** Bilgisayarına yazılım geliştirme araçları (.NET SDK, Python, Visual Studio vb.) kurmak istemeyen son kullanıcılar.

1. GitHub deposunun sağ tarafında bulunan **[Releases (Sürümler)](https://github.com/lotuspc25/OlcuAtolyesi/releases)** sayfasına gidin.
2. En güncel sürüm altındaki **`OlcuAtolyesi-PC-win-x64.zip`** arşivini indirin.
3. İndirdiğiniz `.zip` arşivini istediğiniz bir klasöre çıkartın.
4. Klasör içerisindeki **`OlcuAtolyesi.PC.exe`** dosyasına çift tıklayarak uygulamayı doğrudan başlatın!

---

### Yöntem 2: GitHub'dan İndirip Tek Tıkla Başlatma (`Calistir.bat`)
> **Kimler İçin:** GitHub üzerinden kaynak kodları ZIP olarak indiren veya klonlayan kullanıcılar.

1. GitHub sayfasındaki yeşil **`Code`** butonuna tıklayın ve **`Download ZIP`** seçeneği ile projeyi indirin (veya terminalden `git clone` yapın).
2. ZIP dosyasını klasöre çıkartın.
3. Klasör kök dizininde yer alan **`Calistir.bat`** dosyasına çift tıklayın.
   * *Not: Kaynak kodları derleyebilmek için sisteminizde [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) kurulu olmalıdır.*

---

### Yöntem 3: Konsol / PowerShell Üzerinden Çalıştırma
Kaynak kodları terminal üzerinden yönetmek isterseniz:

```powershell
# 1. Proje dizinine geçin
cd OlcuAtolyesi-PC

# 2. Bağımlılıkları geri yükleyin
dotnet restore OlcuAtolyesi.PC.sln

# 3. Uygulamayı başlatın
dotnet run --project src/OlcuAtolyesi.PC/OlcuAtolyesi.PC.csproj
```

---

### Yöntem 4: Kendi Taşınabilir Paketinizi Üretme (`Paketle.bat` & `Paketle-Linux.bat`)
* **Windows Paketi:** Kök dizindeki **`Paketle.bat`** dosyasına çift tıklayın (`Publish/win-x64/` üretilir).
* **Linux x64 Paketi:** Kök dizindeki **`Paketle-Linux.bat`** dosyasına çift tıklayın (`dist/linux-x64/` üretilir).

---

### Yöntem 5: Linux (Debian 10/11/12, Ubuntu) Üzerinde Tek Tıkla Çalıştırma (`Calistir.sh`)
> **Kimler İçin:** Linux sanal makinesinde veya doğrudan Linux masaüstünde çalışan kullanıcılar.

1. Terminalde proje dizinine geçin:
   ```bash
   cd OlcuAtolyesi-PC
   ```
2. Başlatıcıya çalıştırma yetkisi verip çalıştırın:
   ```bash
   chmod +x Calistir.sh
   ./Calistir.sh
   ```
   * *Not: Sisteminizde .NET SDK yoksa betik otomatik olarak kullanıcı ev dizinine kuracak ve uygulamayı başlatacaktır.*
   * *Gerekli grafik kütüphaneleri: `sudo apt install -y libgl1-mesa-glx libssl-dev libfontconfig1 libx11-6`*

---

## 1. Çözüm Mimarisi ve Klasör Yapısı

```text
OlcuAtolyesi-PC/
├── OlcuAtolyesi.PC.sln
├── Calistir.bat                    # Tek tıkla uygulamayı başlatan betik
├── Paketle.bat                     # Bağımsız (Self-Contained) paket üretici
├── AGENTS.md
├── PROJECT_STATUS.md
├── CHANGELOG.md
├── README.md
├── .github/
│   └── workflows/
│       └── build-and-release.yml   # Otomatik derleme, test ve GitHub Release CI/CD akışı
├── Docs/
│   ├── ROADMAP.md
│   └── Reports/
├── src/
│   ├── OlcuAtolyesi.Core/          # Platformdan bağımsız ortak çekirdek (.NET 10)
│   │   ├── Diagnostics/            # DiagnosticEnums, DiagnosticDataMasker, DiagnosticLogEntry, CircularDiagnosticLogger, DiagnosticLogHub
│   │   ├── Export/                 # DxfExportModels, DxfBuilder, DxfExportService (2B DXF R12 ASCII AC1009)
│   │   ├── Geometry/               # LengthUnit, LengthValue, ImagePoint, ImageSegment, ImageViewportTransform, ImageDisplayTransform
│   │   ├── Measurement/            # RulerTickCalculator, ReferenceObjectLibrary, ScreenCalibration
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
    └── OlcuAtolyesi.PC.Tests/      # Otomatik mantık, geometri, navigasyon, DXF, filtre ve kalibrasyon testleri
```

---

## 2. Sol Navigasyon Modülleri

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
