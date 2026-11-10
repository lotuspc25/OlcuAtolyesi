# Ölçü Atölyesi — Proje Durumu (PROJECT_STATUS.md)

- **Güncel Sürüm:** `0.6.2 — Akıcı Canlı Önizleme ve Düşük Gecikmeli Kare Aktarımı`
- **Tarih:** 2026-11-10
- **Platformlar:** Android (.NET 10 MAUI), Windows Desktop (.NET 10 MAUI)
- **Git Etiketleri:** `v0.2.0-baseline`, `v0.3.0-baseline`, `v0.4.0-baseline`, `v0.5.0-baseline`, `v0.5.0.1-baseline`, `v0.5.0.3-baseline`, `v0.5.0.4-baseline`, `v0.5.0.5-baseline`, `v0.6.0-baseline`, `v0.6.1-baseline`

---

## 1. Modül Durumları

| Modül | Durum | Açıklama |
|---|---|---|
| **Kayıpsız Fotoğraf Depolama** | `Etkin • v0.6.1` | `PhotoStorageService` ile telefonun özel depolama alanına (`Captures/`) donanımsal orijinal JPEG kaydı, min 20MB boş alan kontrolü, SHA-256 hesaplama ve kullanıcı onaysız silmeme koruması. Boş alan Android'de veri bölümünden ölçülür (v0.6.1). |
| **Akıcı Canlı Önizleme Hattı** | `Etkin • v0.6.2` | CameraX `SurfaceView` yüzeyinden `PixelCopy` ile hızlı kare alma, UI donmasını önleyen arka plan JPEG sıkıştırması, `CaptureImage` yedek yolunun kaldırılması ve `PreviewFrameRateLimiter` ile 15 FPS hedef sınır. `ContinuousPreviewPipeline` tek oturumda kare atlama (`DropOldest`) ile çalışır. |
| **Güvenli Uzaktan Çekim API** | `Etkin • v0.5.0.5` | Bearer token kimlik doğrulaması (`/api/v1/auth`), uzaktan çekim (`/api/v1/capture`), fotoğraf listeleme ve depolama alanı sorgusu (`/api/v1/photos`), SHA-256 başlıklı dosya akışı (`/api/v1/photos/{id}`) ve arka plan çekim koruması (`IsForeground`). |
| **PIN Kaba Kuvvet Koruması** | `Etkin • v0.6.0` | İstemci IP'si başına 5 hatalı PIN denemesinde 60 saniye kilit (HTTP 429 + `Retry-After`), başarılı girişte sayaç sıfırlama; PIN'in sorgu dizesinden kaldırılıp `X-Session-Pin` başlığına taşınması; joker CORS başlıklarının kaldırılması. |
| **Kamera Değiştirme Güvencesi** | `Etkin • v0.5.0.5` | Yayın sırasında donanım çökmesini önlemek için kamera değiştirme arayüzde kilitlenir; varsayılan arka kamera olarak başlar. |
| **Kalıcı Tanılama & Günlük** | `Etkin • v0.5.0.4` | `CircularDiagnosticLogger` ile 2MB dosya sınırı, 5 arşiv (10MB maks), PIN/gizli veri maskeleme (`DiagnosticDataMasker`), `session_active.marker` çökme algılama ve `/logs?pin={pin}` güvenli aktarım endpoint'i. |
| **Kurumsal Kimlik & İkon** | `Etkin • v0.5.0.3` | Dijital kumpas temalı modern adaptive icon (`appicon.svg`, `appiconfg.svg`) ve koyu lacivert açılış ekranı (`splash.svg`). |
| **Responsive Ekran Uyumu** | `Etkin • v0.5.0.3` | Tüm metinler `WordWrap` ile sarılır; dar telefonlar (360-390 dp) için esnek `FlexLayout` ve `ScrollView` entegrasyonu sağlandı. |
| **Ana Sayfa** | `Etkin • v0.5.0.3` | Marka alt başlığı ("Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm"), hızlı araç erişimi ve son projeler. |
| **Araçlar Menüsü** | `Etkin • v0.5.0.1` | Canlı araçlar grubunda `PC Kamera Bağlantısı` kartı. `Kamerayla mesafe (Demo)` ve diğer tüm araçlar korundu. |
| **PC Kamera Bağlantısı** | `Etkin • v0.5.0.5` | Wi-Fi üzerinden yerel ağda PC'ye güvenli PIN eşleştirmeli JPEG/MJPEG kamera yayını, uzaktan yüksek çözünürlüklü çekim ve kayıtlı fotoğrafları inceleme kartı. |
| **Fotoğraf Ölçümü** | `Etkin • v0.3.0` | Canlı kamera veya galeriden fotoğraf edinme, R1–R2 referansı ve M1–M2 mesafe ölçümü. |
| **Projeler** | `Etkin • v0.4.0` | Cihazda kalıcı proje kaydı, JSON ve fotoğraf saklama, düzenleme ve silme. |
| **Demo Araçlar** | `Demo` | Cetvel ve Kamera Ölçüm kılavuz sahneleri. |
| **Ayarlar** | `Etkin` | Açık / Koyu tema ve varsayılan ölçü birimi kalıcılığı. |

---

## 2. Derleme ve Test Özeti

- **Otomatik Testler (`Tests/LogicChecks`):** `25/25` kontrol başarılı (%100).
- **Android APK Çıktısı:** `OlcuAtolyesi\bin\Debug\net10.0-android\android-arm64\com.companyname.olcuatolyesi-Signed.apk` hazır (VersionCode: 13, VersionName: 0.6.2, Tam Bağımsız Gömülü APK, 49,977,917 bayt).
- **Windows MAUI Derlemesi:** Başarılı (`0 Hata`).
