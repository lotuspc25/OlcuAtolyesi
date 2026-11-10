# Ölçü Atölyesi — Açık Kaynak Entegrasyon ve Geliştirme Yol Haritası

Bu belge, `Docs/Research/ACIK_KAYNAK_PROJELER.md` kapsamında incelenen açık kaynak teknolojilerin **Ölçü Atölyesi** (`.NET 10 / MAUI` Android ve Windows) mimarisine hangi aşamalarda, hangi lisans sınırlarıyla ve hangi teknik katmanlarla entegre edileceğini tanımlar.

---

## 1. Hedef Mimari: Hibrit Mobil + PC Çalışma Modeli

Ölçü Atölyesi'nin uzun vadeli hedefleri hem **sahada tek başına telefonla hızlı ölçüm/CAD çıktısı** almayı hem de **PC ile eşleşerek ileri seviye görüntü işleme, CAD modelleme ve 3B yazıcı yönetimini** kapsar:

1. **Cihaz İçi (Android & Windows Ortak Çekirdek — .NET 10):**
   - Çoklu ölçüm (uzunluk, açı, çap/yarıçap, çevre/alan, poligon).
   - **OpenCV (Apache-2.0)** tabanlı perspektif düzeltme (homografi), kalibrasyonlu zemin algılama (`ArUco` / `ChArUco` / cetvelli tabla) ve otomatik kenar/kontur/delik bulma.
   - **ezdxf (MIT)** şema referanslı yerel C# `DXF` (`R12`/`R2000`) ve su geçirmez `STL` (2.5B ekstrüzyon) üreticisi.
2. **Masaüstü / PC Köprüsü (Windows MAUI & Yerel Ağ Servisi):**
   - **OpenScan** mimarisinden esinlenen ancak temiz oda (clean-room) C# REST/WebSocket protokolüyle yazılmış **PC'den Telefon Kamerası Yönetimi** (canlı önizleme, uzaktan netleme/çekim, otomatik fotoğraf aktarma).
   - **Open CASCADE / OCCT (LGPL-2.1)** dinamik kütüphanesi veya **FreeCAD (`freecadcmd`)** ile analitik `STEP` katı model üretimi.
   - **AliceVision / Meshroom (MPL-2.0)** CLI entegrasyonu ile çok açılı fotoğraflardan ölçekli 3B fotogrametri ağı üretimi.
   - **OrcaSlicer / PrusaSlicer (AGPL-3.0)** harici CLI entegrasyonu ile 3B baskıya hazırlama (`G-code`) ve **OctoPrint / Moonraker (Klipper)** REST API üzerinden desteklenen 3B yazıcıları kontrol etme.

---

## 2. Aşamalı Entegrasyon Planı

### Faz 1 (v0.5.0 – v0.6.0): Çoklu Geometrik Ölçüm ve Yerel DXF / STL Dışa Aktarma
- **Hedef:** Kullanıcı tek fotoğraf üzerinde birden fazla uzunluk, açı (`MeasureAngleDegrees`), daire çapı ve kapalı poligon (çevre/alan) ölçebilmeli; ölçülen 2B profili doğrudan `DXF` ve belirli bir kalınlıkla ekstrüde edilmiş `STL` olarak dışa aktarabilmelidir.
- **Kullanılacak Teknolojiler:**
  - `ezdxf` (MIT) katman ve `LWPOLYLINE` / `CIRCLE` / `ARC` / `LINE` spesifikasyonu referans alınarak sıfır harici bağımlılıklı `OlcuAtolyesi.Export.DxfWriter` ve `StlExtruder` servisleri.
- **Lisans ve Güvenlik:** %100 MIT/özgün C# kodu; hem Android hem Windows'ta çevrimdışı çalışır.

### Faz 2 (v0.7.0 – v0.8.0): Kalibrasyonlu Zemin, Perspektif Düzeltme ve Otomatik Kontur/Delik Algılama
- **Hedef:**
  - Cetvelli veya 4 köşeli kalibrasyon işaretli (`ArUco` / `ChArUco`) zemin üzerinde çekilen fotoğraflarda kamera açısı, lens distorsiyonu ve perspektif eğikliğinin tek dokunuşla düzeltilmesi (`warpPerspective`).
  - Kullanıcıyı adım adım yönlendiren çekim asistanı (eğim açısı kontrolü, ışık/kontrast kontrolü, referans görünürlüğü).
  - Parçanın dış sınırının, düz kenarlarının, yaylarının ve iç deliklerinin otomatik algılanarak düzenlenebilir vektör düğümlerine dönüştürülmesi.
- **Kullanılacak Teknolojiler:**
  - **OpenCV 4.x (Apache-2.0):** `calib3d` (`findHomography`, `undistort`), `objdetect` (`ArucoDetector`), `imgproc` (`Canny`, `findContours`, `approxPolyDP`, `fitEllipse`, `cornerSubPix`).
  - Entegrasyon: `OpenCvSharp4` veya yalnızca gerekli 4 modülü içeren hafif C API P/Invoke köprüsü.

### Faz 3 (v0.9.0): Telefon Kamerasının PC Üzerinden Yönetilmesi (Masaüstü Stüdyo Modu)
- **Hedef:** Telefon bir tarama/ölçüm standına sabitlendiğinde, PC üzerindeki Ölçü Atölyesi uygulamasından telefon kamerasının canlı önizlemesinin izlenmesi, odak/pozlama ayarlarının yapılması, çekim tetiklenmesi ve fotoğrafın anında PC ekranına düşmesi.
- **Kullanılacak Teknolojiler:**
  - **OpenScan3** REST/WebSocket durum makinesi mimarisi referans alınarak (GPLv3 kodu kopyalanmadan) `.NET 10` üzerinde geliştirilen yerel ağ eşleştirme servisi (`HttpListener` / `Kestrel` + `WebSocket`, QR kod ile hızlı eşleşme).

### Faz 4 (v1.0.0): Open CASCADE (OCCT) ile STEP Üretimi ve FreeCAD Köprüsü
- **Hedef:** Otomatik veya manuel çıkarılan 2B kontur ve delik profillerinden endüstriyel CAD/CAM yazılımlarında (SolidWorks, Fusion, FreeCAD) doğrudan katı model olarak açılabilen `STEP` (`AP214`) dosyalarının üretilmesi.
- **Kullanılacak Teknolojiler:**
  - **Open CASCADE / OCCT (LGPL-2.1):** Dinamik bağlantı (`.dll` / `.so`) üzerinden `BRepPrimAPI_MakePrism`, `BRepAlgoAPI_Cut` ve `STEPControl_Writer`.
  - **FreeCAD (LGPL-2.1+):** İsteğe bağlı `freecadcmd` betik köprüsü ile parametrik `.FCStd` proje dosyası oluşturma ve "FreeCAD'de Aç" masaüstü eylemi.

### Faz 5 (v1.1.0+): 3B Fotogrametri (AliceVision) ve 3B Baskı / Dilimleyici / Yazıcı Kontrolü (OrcaSlicer & PrusaSlicer)
- **Hedef:**
  - Çoklu fotoğraftan 3B nesne tarama (PC üzerinde AliceVision/Meshroom CLI boru hattı).
  - Üretilen `STL` / `STEP` modellerinin tek tıkla **OrcaSlicer** veya **PrusaSlicer** CLI üzerinden dilimlenmesi (`G-code`) ve yerel ağdaki desteklenen 3B yazıcılara (**Moonraker/Klipper**, **OctoPrint**) gönderilmesi.
- **Kullanılacak Teknolojiler:**
  - `aliceVision_*` / `meshroom_batch` (MPL-2.0 — harici süreç).
  - `orca-slicer` / `prusa-slicer-console` (AGPL-3.0 — **yalnızca harici komut satırı süreci olarak**, kod kopyalamadan).
  - C# `MoonrakerApiClient` ve `OctoPrintApiClient` (`HttpClient` / `WebSocket`).

---

## 3. Lisans Uyumluluğu ve Açık Kaynak Dağıtım Kuralları

Ölçü Atölyesi açık kaynak olarak yayımlandığında (ör. MIT, Apache-2.0 veya MPL-2.0 ana lisansıyla) lisans temizliğini korumak için aşağıdaki kurallar zorunludur:

1. **İzin Verici Lisanslar (`MIT`, `BSD`, `Apache-2.0` — OpenCV, ezdxf):**
   - Doğrudan NuGet paketi, dinamik kütüphane veya kaynak/algoritma referansı olarak kullanılabilir.
   - `THIRD_PARTY_NOTICES.md` dosyasına telif ve lisans metinleri eklenmelidir.
2. **Zayıf Copyleft Lisanslar (`LGPL-2.1`, `MPL-2.0` — OCCT, FreeCAD, AliceVision, Meshroom):**
   - Dinamik kütüphane (`.dll` / `.so`) veya harici komut satırı süreci (CLI) olarak kullanılmalıdır.
   - Bu kütüphanelerin kendi kaynak dosyalarında değişiklik yapılırsa yalnızca o değişiklikler aynı lisansla paylaşılmalıdır.
3. **Güçlü Copyleft Lisanslar (`GPL-3.0`, `AGPL-3.0` — OpenScan, OrcaSlicer, PrusaSlicer):**
   - **Kesinlikle doğrudan kaynak kod kopyalanmamalı ve süreç içi (in-process) kütüphane olarak bağlanmamalıdır.**
   - Yalnızca işletim sistemi düzeyinde ayrı süreç (`Process.Start` ile CLI çağrısı) veya ağ protokolü (`HTTP/REST`) üzerinden haberleşilmelidir.
