using OlcuAtolyesi.Core.Models;

namespace OlcuAtolyesi.Core.Services;

public sealed class ModuleCatalogService
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules;
    private readonly IReadOnlyList<OpenSourceCandidate> _openSourceCandidates;

    public ModuleCatalogService()
    {
        _modules =
        [
            new ModuleDescriptor(
                ModuleId.Dashboard,
                "Ana Panel",
                "⊞",
                "Masaüstü çalışma alanının genel durumu, modül hazırlık seviyeleri ve hızlı geçiş kartları.",
                "Ölçü Atölyesi PC v0.1.0 sürümünde tüm masaüstü modüllerinin mevcut durumlarını, ortak geometri motorunu ve açık kaynak entegrasyon yol haritasını tek ekranda özetler.",
                ModuleImplementationState.Active,
                "v0.1.0 (Aktif)",
                [
                    "9 modüllü sol navigasyon menüsü ve hızlı modül geçişi",
                    "Canlı modül durum rozetleri (Etkin, Temel Hazır, Planlandı)",
                    "Açık / Koyu / Sistem teması ve varsayılan ölçü birimi özeti"
                ],
                [
                    "Bağlı Android telefon ve 3B yazıcı canlı durum göstergeleri (v0.2.0+)"
                ],
                ["OlcuAtolyesi.Core (.NET 10)"]),

            new ModuleDescriptor(
                ModuleId.Projects,
                "Projeler",
                "📁",
                "Kalıcı proje kayıt sistemi, yeniden adlandırma/silme ve mobil v0.4.0 proje paketi içe/dışa aktarma.",
                "PC üzerinde oluşturulan veya Android telefondan aktarılan ölçüm projelerinin yönetildiği modüldür. Her projeyi izole klasöründe (project.json + photo.jpg + .bak yedeği) atomik olarak saklar ve mobil v0.4.0 proje paketleriyle çift yönlü uyumluluk sunar.",
                ModuleImplementationState.Active,
                "v0.3.0 (Aktif)",
                [
                    "Yeni proje oluşturma, kaydetme, yeniden adlandırma ve silme",
                    "Her proje için izole veri klasörü, atomik JSON yazımı ve .bak yedeğinden kurtarma",
                    "Mobil v0.4.0 proje paketlerini (.zip / klasör / project.json) güvenli dönüştürme ile içe aktarma",
                    "PC projelerini taşınabilir .zip ve klasör paketi olarak dışa aktarma"
                ],
                [
                    "Çoklu ölçüm katmanı ve CAD geçmişi sürümleme (Planlandı)"
                ],
                ["System.Text.Json (MIT)", "System.IO.Compression (MIT)"]),

            new ModuleDescriptor(
                ModuleId.PhoneConnection,
                "Telefon Bağlantısı",
                "📱",
                "USB (ADB Köprüsü — Etkin), Wi-Fi (Planlandı) ve Bluetooth (Planlandı) üzerinden Android telefon yönetimi.",
                "Windows PC uygulamasının USB üzerinden bağlı Android telefonları tanımasını, yetki/çevrimiçi durumlarını (device, unauthorized, offline) ayırt etmesini ve kullanıcı kontrolünde güvenli cihaz oturumu seçmesini sağlar. Gelecekteki Wi-Fi, Bluetooth ve uzaktan kamera kontrol sözleşmeleri tanımlanmıştır.",
                ModuleImplementationState.Active,
                "v0.4.0 USB Etkin / Wi-Fi & Bluetooth Planlandı",
                [
                    "PATH ve bilinen Android SDK dizinlerinde güvenli ADB (adb.exe) bulucu",
                    "adb devices -l ile çoklu cihaz tarama; device, unauthorized ve offline ayrımı",
                    "Cihazları Tara, Cihaz Seç, Bağlantıyı Kontrol Et, Bağlantıyı Yenile ve Bağlantıyı Ayır",
                    "Kabuksuz (ArgumentList) güvenli komut çalıştırma, zaman aşımı ve iptal yönetimi",
                    "Gelecekteki kamera kontrolü (IRemotePhoneCameraService) servis ve yetenek sözleşmeleri"
                ],
                [
                    "Wi-Fi yerel ağ (REST / WebSocket) üzerinden yüksek çözünürlüklü kare ve proje aktarımı (Planlandı)",
                    "Bluetooth (BLE) ile yakın cihaz keşfi, eşleştirme ve düşük bant kontrol iletişimi (Planlandı)",
                    "Canlı kamera önizleme ve uzaktan çekim başlatma (v0.5.0 Planlandı)"
                ],
                ["Android SDK Platform-Tools (ADB)", "OpenScan3 (REST/WebSocket Mimari Referansı)"]),

            new ModuleDescriptor(
                ModuleId.CameraCalibration,
                "Kamera ve Kalibrasyon",
                "🎯",
                "USB (scrcpy) ve Yerel Ağ (Wi-Fi) üzerinden canlı telefon kamerası akışı, doğrudan pencere içi önizleme ve kalibrasyon temeli.",
                "Bağlı Android telefon kamerasını USB (scrcpy 5.0.1) veya yerel ağ Wi-Fi (Ölçü Atölyesi Bağımsız Kamera Servisi) üzerinden canlı başlatan, güvenli PIN eşleştirmesi ile doğrudan WPF penceresi içinde veya SDL3 önizlemesinde izleyen, anlık kare yakalayıp ölçüme aktaran modüldür.",
                ModuleImplementationState.Active,
                "v0.5.0 Wi-Fi Bağımsız Kamera ve Pencere İçi Önizleme Aktif / Kalibrasyon Planlandı",
                [
                    "Bağımsız Wi-Fi Kamera Servisi: Yerel ağ üzerinden sıfır scrcpy/ADB bağımlılığıyla canlı MJPEG akışı",
                    "Güvenli PIN Eşleştirmesi: 6 haneli oturum kodu ile yetkisiz erişim koruması",
                    "Doğrudan WPF Pencere İçi Canlı Önizleme ve anlık FPS/çözünürlük metrikleri",
                    "Canlı Akıştan Anlık Fotoğraf Yakalama ve Fotoğraftan Ölçüm modülüne doğrudan aktarım",
                    "Alternatif USB scrcpy 5.0.1 motoru (donanım hızlandırmalı SDL3 penceresi) korundu",
                    "scrcpy dinamik kamera sensörü tespiti (--list-cameras) ve çözünürlük/FPS mod filtreleme"
                ],
                [
                    "4 köşe kalibrasyon işaretçisi (ArUco / ChArUco) ile otomatik perspektif düzeltme / kuşbakışı dönüşüm (Planlandı)",
                    "Kamera lens distorsiyon (k1, k2, p1, p2) kalibrasyon profili (Planlandı)"
                ],
                ["scrcpy 5.0.1 (Apache-2.0)", "OpenCV (Apache-2.0)", "OpenScan (Mimari İlham)"]),

            new ModuleDescriptor(
                ModuleId.PhotoMeasurement,
                "Fotoğraftan Ölçüm",
                "📐",
                "Bilgisayardan fotoğraf açıp R1–R2 referanslı M1–M2 mesafe ölçümü; gelecekte otomatik kontur, daire, yay ve delik tespiti.",
                "JPG, JPEG, PNG ve BMP fotoğrafları açar; fare tekerleği ile yakınlaştırma, fareyle kaydırma, R1–R2 referans ve M1–M2 ölçüm noktalarını sürükleyerek düzeltme sunar. Noktalar fotoğrafın gerçek piksel koordinatlarında tutulur ve ortak OlcuAtolyesi.Core geometri motoruyla hesaplanır. Otomatik algılama sonraki sürümlerde OpenCV ile eklenecektir.",
                ModuleImplementationState.Active,
                "v0.2.0 Fotoğraf Üzerinde Ölçüm Aktif / v0.5.0 Otomatik Algılama Planlandı",
                [
                    "Fotoğraf Aç: JPG, JPEG, PNG ve BMP desteği (EXIF yönü uygulanır)",
                    "Fare tekerleği ile yakınlaştırma, fareyle kaydırma ve Görünümü Sığdır",
                    "Referans Belirle (R1–R2) ve Mesafe Ölç (M1–M2) araçları; noktalar sürüklenerek düzeltilir",
                    "mm, cm, m ve inç birimlerinde gerçek zamanlı sonuç; referans veya birim değişince otomatik yeniden hesaplama",
                    "Geri Al ve Sıfırla; noktalar fotoğraf piksel koordinatlarında saklanır"
                ],
                [
                    "Piksel büyüteci ile hassas nokta seçimi (Planlandı)",
                    "Otomatik dış kontur, çizgi, yay, daire ve vida deliği algılama (Planlandı)",
                    "Kenara ve köşeye alt-piksel hassasiyetinde otomatik yapışma / Snap (Planlandı)"
                ],
                ["OpenCV (Apache-2.0)"]),

            new ModuleDescriptor(
                ModuleId.CadExport,
                "CAD / DXF / STEP / STL",
                "🧊",
                "Ölçülen 2B geometriden DXF kesim dosyası ve ekstrüde edilmiş 3B STEP / STL katı model üretimi.",
                "Fotoğraftan çıkarılan dış kontur, delikler ve ölçülendirmelerden lazer/CNC kesim için katmanlı DXF, 3B CAD yazılımları için analitik STEP (AP214) ve 3B baskı için su geçirmez STL üretir.",
                ModuleImplementationState.Planned,
                "v0.6.0 DXF & STL / v0.8.0 STEP (Planlandı)",
                [
                    "DXF katman standardı (DIS_KONTUR, DELIKLER, REFERANS, OLCULER) tanımlandı",
                    "Açık kaynak CAD çekirdeği lisans ve entegrasyon mimarisi tamamlandı"
                ],
                [
                    "2B vektör profillerinden standart R2000 ASCII DXF dışa aktarma (Planlandı)",
                    "2B profil + et kalınlığı parametresiyle 3B prizmatik STL ağ üretimi (Planlandı)",
                    "Open CASCADE (OCCT) ve FreeCAD köprüsü ile analitik STEP katı model üretimi (Planlandı)",
                    "Çok açılı fotoğraflardan AliceVision / Meshroom ile 3B fotogrametri ağı üretimi (Planlandı)"
                ],
                ["ezdxf (MIT — Şema Referansı)", "Open CASCADE / OCCT (LGPL-2.1)", "FreeCAD (LGPL-2.1+)", "AliceVision & Meshroom (MPL-2.0)"]),

            new ModuleDescriptor(
                ModuleId.Print3D,
                "3B Baskı",
                "🖨",
                "STL/STEP modellerini 3B baskıya hazırlama (CLI dilimleme) ve ileride desteklenen yazıcıları kontrol etme.",
                "Oluşturulan 3B modelleri OrcaSlicer veya PrusaSlicer komut satırı (CLI) üzerinden dilimleyerek G-code üretir; ilerleyen sürümlerde Klipper (Moonraker) ve OctoPrint API üzerinden yazıcı takibi sağlar.",
                ModuleImplementationState.Planned,
                "v0.9.0 (Planlandı)",
                [
                    "AGPL-3.0 lisans sınırına uygun süreç dışı (out-of-process CLI) dilimleyici mimarisi belirlendi"
                ],
                [
                    "Tek tıkla modeli OrcaSlicer veya PrusaSlicer'da açma (Planlandı)",
                    "Arka planda başsız (headless CLI) G-code üretimi ve süre/filament tahmini okuma (Planlandı)",
                    "Moonraker (Klipper) ve OctoPrint REST API ile yerel ağdaki yazıcıya iş gönderme (Planlandı)"
                ],
                ["OrcaSlicer (AGPL-3.0 — Harici CLI)", "PrusaSlicer (AGPL-3.0 — Harici CLI)"]),

            new ModuleDescriptor(
                ModuleId.Settings,
                "Ayarlar",
                "⚙",
                "Açık / Koyu / Sistem teması, varsayılan ölçü birimi ve çalışma alanı yapılandırması.",
                "Kullanıcının masaüstü görünüm temasını ve varsayılan uzunluk birimini (mm, cm, m, inç) kalıcı olarak kaydeder ve anında arayüze uygular.",
                ModuleImplementationState.Active,
                "v0.1.0 (Aktif)",
                [
                    "Anlık Açık / Koyu / Sistem teması geçişi",
                    "Varsayılan uzunluk birimi seçimi (mm, cm, m, inç) ve kalıcı JSON kaydı",
                    "Çalışma alanı dizini bilgisi"
                ],
                [
                    "Harici araç yolları yapılandırması: FreeCAD, OrcaSlicer, PrusaSlicer, Meshroom (Planlandı)"
                ],
                ["OlcuAtolyesi.Core (.NET 10)"]),

            new ModuleDescriptor(
                ModuleId.Help,
                "Yardım",
                "?",
                "Masaüstü mimarisi, mobil-PC ortak kütüphane önerisi ve açık kaynak entegrasyon rehberi.",
                "Ölçü Atölyesi PC'nin kullanım kılavuzunu, mobil proje ile paylaşılan geometri mimarisini ve gelecekteki açık kaynak entegrasyon adaylarını açıklar.",
                ModuleImplementationState.Active,
                "v0.1.0 (Aktif)",
                [
                    "Modül kullanım açıklamaları ve sürüm yol haritası",
                    "Açık kaynak projeler (OpenCV, OpenScan, FreeCAD, OCCT, Meshroom, ezdxf, OrcaSlicer, PrusaSlicer) lisans tablosu",
                    "Mobil ve PC için ortak kütüphane (OlcuAtolyesi.Core) mimari kılavuzu"
                ],
                [
                    "Adım adım görsel kalibrasyon sihirbazı rehberi (Planlandı)"
                ],
                ["Tüm Açık Kaynak Adayları"])
        ];

        _openSourceCandidates =
        [
            new OpenSourceCandidate(
                "OpenCV",
                "https://github.com/opencv/opencv",
                "Apache-2.0",
                "Yerel Kütüphane (C API P/Invoke veya OpenCvSharp4)",
                "Kamera kalibrasyonu, lens/perspektif düzeltme (homografi), ArUco algılama ve otomatik kontur/daire/yay/delik tespiti."),
            new OpenSourceCandidate(
                "OpenScan",
                "https://github.com/OpenScan-org",
                "GPL-3.0",
                "Yalnızca Mimari/Protokol Referansı (Kod kopyalanmaz)",
                "Telefon kamerasının PC üzerinden uzaktan yönetilmesi ve adım adım yönlendirmeli tarama/çekim iş akışı."),
            new OpenSourceCandidate(
                "Meshroom",
                "https://github.com/alicevision/meshroom",
                "MPL-2.0",
                "Harici CLI / Masaüstü İş Akışı (meshroom_batch)",
                "Çok açılı fotoğraflardan düğüm tabanlı 3B fotogrametri boru hattı yönetimi."),
            new OpenSourceCandidate(
                "AliceVision",
                "https://github.com/alicevision/AliceVision",
                "MPL-2.0",
                "Harici CLI Süreci (aliceVision_* ikilileri)",
                "Structure-from-Motion (SfM) ve Multi-View Stereo (MVS) ile ölçekli 3B yüzey ağı çıkarma."),
            new OpenSourceCandidate(
                "FreeCAD",
                "https://github.com/FreeCAD/FreeCAD",
                "LGPL-2.1+",
                "Harici Başsız CLI (freecadcmd) ve Masaüstü CAD Köprüsü",
                "Parametrik 2B eskiz ve 3B katı model (.FCStd, STEP) doğrulama/düzenleme otomasyonu."),
            new OpenSourceCandidate(
                "Open CASCADE (OCCT)",
                "https://github.com/Open-Cascade-SAS/OCCT",
                "LGPL-2.1 (+ OCCT İstisnası)",
                "Dinamik Kütüphane (Native C++ DLL Köprüsü)",
                "B-Rep katı modelleme çekirdeği, 2B kontur + delik ekstrüzyonu ve endüstriyel STEP / STL üretimi."),
            new OpenSourceCandidate(
                "ezdxf",
                "https://github.com/mozman/ezdxf",
                "MIT",
                "DXF Şema Referansı (C# Yerel Yazıcı) veya Python Aracı",
                "Katmanlı, standartlara uyumlu 2B DXF (LINE, ARC, CIRCLE, LWPOLYLINE, DIMENSION) oluşturma."),
            new OpenSourceCandidate(
                "OrcaSlicer",
                "https://github.com/OrcaSlicer/OrcaSlicer",
                "AGPL-3.0",
                "Yalnızca Harici CLI Süreci (Kod kopyalanmaz / bağlanmaz)",
                "3B modelleri komut satırından dilimleme (G-code) ve modern yazıcı ağ protokolleri (Moonraker/Bambu/OctoPrint)."),
            new OpenSourceCandidate(
                "PrusaSlicer",
                "https://github.com/prusa3d/PrusaSlicer",
                "AGPL-3.0",
                "Yalnızca Harici CLI Süreci (prusa-slicer-console.exe)",
                "Başsız (headless) STL/STEP/3MF dilimleme, baskı süresi ve filament tüketimi analizi.")
        ];
    }

    public IReadOnlyList<ModuleDescriptor> GetAllModules() => _modules;

    public ModuleDescriptor GetModule(ModuleId id) =>
        _modules.First(m => m.Id == id);

    public IReadOnlyList<OpenSourceCandidate> GetOpenSourceCandidates() => _openSourceCandidates;
}
