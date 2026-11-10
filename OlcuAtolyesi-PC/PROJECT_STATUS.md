# Ölçü Atölyesi PC — Proje Durumu (PROJECT_STATUS.md)

- **Güncel Sürüm:** `v0.6.7 — Sade Ana Pencere, Cetveller ve Ekran Kalibrasyonu`
- **Tarih:** 2026-11-11
- **Çalışma Klasörü:** `D:\proje\OlcuAtolyesi-PC`
- **Teknoloji Yığını:** C# / .NET 10 / WPF (`net10.0-windows`) + `OlcuAtolyesi.Core` (`net10.0`) / MVVM
- **Git Etiketleri:** `v0.1.0-baseline`, `v0.2.0-baseline`, `v0.3.0-baseline`, `v0.4.0-baseline`, `v0.4.1-baseline`, `v0.4.2-baseline`, `v0.5.0-baseline`, `v0.5.0.1-baseline`, `v0.5.0.2-baseline`, `v0.5.0.3-baseline`, `v0.5.0.4-baseline`, `v0.5.0.5-baseline`, `v0.5.0.6-baseline`, `v0.6.0-baseline`, `v0.6.1-baseline`, `v0.6.2-baseline`, `v0.6.3-baseline`, `v0.6.4-baseline`, `v0.6.5-baseline`, `v0.6.6-baseline`, `v0.6.7-baseline`
- **Yol Haritası:** `Docs/ROADMAP.md` — v1.0 kapsamı: zemin kalibrasyonu + perspektif düzeltme + 2B DXF ihracı

---

## 1. Sol Navigasyon Modülleri ve Durumları

| Modül | Durum Rozeti | v0.6.7 Kapsamı | Hedef Sürüm |
|---|---|---|---|
| **Uzaktan Yüksek Çözünürlüklü Çekim** | `Etkin • v0.5.0.6` | `RemotePhotoClient` ile Bearer token yetkilendirmesi, HTTP 401 yetkisiz erişimde önbellekteki PIN (`_currentPin`) ile otomatik tek seferlik yeniden doğrulama ve tekrar deneme, Türkçe yönlendirici hata mesajları (401/403/404/429), `/api/v1/capture` uzaktan donanımsal çekim, `/api/v1/photos/{id}` üzerinden SHA-256 bütünlük doğrulamalı indirme, önbellek tekilleştirmesi ve bekleyen fotoğrafları listeleme. | `v0.5.0.6` (Aktif) |
| **Ölçüm Modülü & Kalibrasyon Güvenliği** | `Etkin • v0.6.7` | `LoadCapturedPhoto` ve `LoadHighResPhoto` ile yüksek çözünürlük rozeti (`>1920x1080`), eski referansı geçersiz kılma (`HasValidReference = false`), bilinen uzunluğu sıfırlama, kullanıcıyı yeni referans belirlemeye yönlendirme, CAD tipi ok başlı çizim, adım rehberi, opaklığı düzeltilmiş şeffaf büyüteç, "Ayrı Pencerede Aç" butonu ile bağımsız tam ekran `MeasurementWindow`, nesne ve ekran cetvelleri, ekran kalibrasyon penceresi ve 2B DXF dışa aktarma düğmesi entegrasyonu. | `v0.6.7` (Aktif) |
| **Merkezi Tanılama Konsolu** | `Etkin • v0.5.0.4` | Canlı PC ve Android olay akışı, kaynak/seviye filtreleri, anlık metin arama, kopyalama, 1366×768 responsive layout, GridSplitter detay inceleme ve telefondan `/logs` güvenli aktarımı. | `v0.5.0.4` (Aktif) |
| **Kalıcı Döngüsel Günlük** | `Etkin • v0.5.0.4` | `%LOCALAPPDATA%\OlcuAtolyesi\Logs` klasöründe 2MB dosya sınırı, 5 döngüsel arşiv (10MB maks), PIN/gizli veri maskeleme, `session_active.marker` çökme algılama ve rapor dışa aktarma (`.txt`). | `v0.5.0.4` (Aktif) |
| **Kurumsal Kimlik & İkon** | `Etkin • v0.5.0.3` | Hassas dijital kumpas ve optik reticle temalı Windows PE `.ico` (16, 24, 32, 48, 64, 128, 256 px) ve yerel WPF `splash.png` (700x380 px). | `v0.5.0.3` (Aktif) |
| **Responsive Ekran Uyumu** | `Etkin • v0.5.0.3` | Minimum 920x600'den 1366x768 (HD) ve 1920x1080 (FHD) çözünürlüklerine kadar tüm kontroller ve metin blokları orantılı, taşma yapmayan `WordWrap` / `ScrollViewer` ile korundu. | `v0.5.0.3` (Aktif) |
| **1. Ana Panel** | `Etkin • v0.5.0.5` | Tüm modüllerin hazırlık durumu özeti, metrikler, hızlı modül geçiş kartları ve kurumsal rozet. | `v0.5.0.5` (Aktif) |
| **2. Projeler** | `Etkin • v0.5.0.4` | Kalıcı proje oluşturma, kaydetme, yeniden adlandırma, silme, atomik `.bak` kurtarma; mobil `v0.4.0` proje paketlerini (`.zip` / klasör / `project.json`) içe ve dışa aktarma. | `v0.5.0.6` (Aktif) |
| **3. Telefon Bağlantısı** | `Etkin • v0.5.0.4` | Güvenli ADB bulucu, `adb devices -l` ile çoklu USB cihaz tarama, `device` / `unauthorized` / `offline` ayrımı, `Cihazları Tara`, `Cihaz Seç`, `Bağlantıyı Kontrol Et`, `Bağlantıyı Yenile`, `Bağlantıyı Ayır`; Wi-Fi ve Bluetooth sözleşmeleri hazır. | `v0.5.0.6` (USB Aktif, Wi-Fi/BT planlı) |
| **4. Kamera ve Kalibrasyon** | `Etkin • v0.5.0.5` | **İki Bağımsız Kamera Modu:**<br>1. `Ölçü Atölyesi Wi-Fi Kamera Servisi`: Tam RFC uyumlu multipart MJPEG ayrıştırma, gerçek FPS/çözünürlük, anlık bağlantı yönetimi, JPEG bütünlük kontrolü (SOI 0xFFD8 / EOI 0xFFD9), "⭐ Yüksek Çözünürlüklü Fotoğraf Çek ve Ölç" butonu ile kayıpsız dosya aktarımı.<br>2. `USB scrcpy Motoru`: Donanım hızlandırmalı SDL3 penceresi, dinamik kamera sensörü tespiti (`--list-cameras`) ve çözünürlük/FPS seçimi (`--list-camera-sizes`). | `v0.5.0.5` (Wi-Fi Pencere İçi ve USB Aktif) / `v0.6.0` (Kalibrasyon Planlandı) |
| **5. Fotoğraftan Ölçüm** | `Etkin • v0.6.7` | Sadeleştirilmiş ana pencere düzeni (büyük "ÖLÇÜM PENCERESİ (F2)" butonu, hızlı hazır şablon referans seçimi ComboBox'ı, referans uzunluğu kutusu, birim seçimi, "Cetvelleri Göster" kutusu, uyarı bandı, kompakt alt bilgi çubuğu), tam ekran bağımsız `MeasurementWindow` içinde sekmeli araç paneli (`Görüntü`, `Ölçüm`, `Proje`, `DXF`), `Segoe Fluent Icons` glif ikonları (`IconGlyph`), görüntüleme filtreleri, geometrik düzenleme, iki yönlü tersinir koordinat dönüşümü (`ImageDisplayTransform`), A Cetveli (Nesne Cetveli: mm/px taksimatı), B Cetveli (Ekran Cetveli: fiziksel mm), etkileşimli Ekran Kalibrasyon Penceresi (`ScreenCalibrationWindow`), tuval sağ tık menüsü (ContextMenu), CAD tipi ok başlı ölçü çizgileri, `ToolStateAdvisor` Türkçe adım rehberi, imleci kapatmayan 2×/4×/8× büyüteç, klavye kısayolları (F2, F, L, 2, 4, 8, Ctrl+Sol/Sağ, Ctrl+H/V) ve 2B DXF dışa aktarma. | `v0.6.7` (Aktif) |
| **6. CAD / DXF / STEP / STL** | `Kısmen Etkin • v0.6.0 (2B DXF)` | 2B DXF (R12 ASCII, AC1009) ihracı: R1–R2 referansı ve M1–M2 ölçümü mm cinsinden ($INSUNITS=4), Y ekseni ters çevrilmiş, CIRCLE ve TEXT elemanlarıyla dışa aktarılır. STEP ve STL mimarisi planlandı. | `v0.6.0` (2B DXF Aktif) – `v0.8.0` (STEP/STL) |
| **7. 3B Baskı** | `Planlandı` | `OrcaSlicer` ve `PrusaSlicer` harici CLI dilimleme ve `Moonraker`/`OctoPrint` yazıcı kontrol mimarisi. | `v1.0 sonrası` |
| **8. Ayarlar** | `Etkin • v0.5.0.5` | Anlık Açık / Koyu / Sistem teması geçişi, monitör fiziksel kalibrasyonu ve varsayılan ölçü birimi (`mm`, `cm`, `m`, `inç`) kalıcı JSON kaydı. | `v0.1.0` (Aktif) |
| **9. Yardım** | `Etkin • v0.5.0.5` | Ortak kütüphane (`OlcuAtolyesi.Core`) ve açık kaynak projelerin lisans/entegrasyon matrisi. | `v0.1.0` (Aktif) |

---

## 2. Doğrulama Özeti (v0.6.7)

- **Debug Derlemesi (`dotnet build -c Debug`):** `0 Uyarı, 0 Hata`
- **Release Derlemesi (`dotnet build -c Release`):** `0 Uyarı, 0 Hata`
- **Otomatik Testler (`OlcuAtolyesi.PC.Tests`):** `54/54` kontrol başarılı (%100).
- **Responsive Düzen:** 920x600, 1366x768 ve 1920x1080 ekran çözünürlüklerinde doğrulandı.
- **Kullanım Kuralı Doğrulaması:** Canlı cihaza müdahale edilmemiş; masaüstü uygulaması bağımsız olarak başlatılmış ve arka planda açık bırakılmıştır.
