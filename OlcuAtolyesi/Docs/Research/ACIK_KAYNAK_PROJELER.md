# Ölçü Atölyesi — Açık Kaynak Projeler Teknik ve Lisans Araştırması

Bu belge, **Ölçü Atölyesi** projesinin uzun vadeli vizyonu (telefon kamerasının PC üzerinden yönetilmesi, kalibrasyonlu düzlem üzerinde hassas ölçüm, perspektif/lens düzeltmesi, otomatik kontur/çizgi/yay/daire/delik algılama, DXF/STEP/STL üretimi ve 3B baskı dilimleme/yazıcı kontrolü) doğrultusunda incelenen 9 temel açık kaynak projenin teknik mimarisini, API olanaklarını, `.NET 10 / MAUI` entegrasyon yöntemlerini, lisans koşullarını ve risk analizini içerir.

> **Önemli Kural:** Hiçbir açık kaynak projeden lisans uyumluluğu doğrulanmadan doğrudan kaynak kod kopyalanmamalıdır. Copyleft (`GPLv3`, `AGPLv3`) lisanslı projelerle süreç içi (in-process) statik/dinamik bağlantı yerine süreç dışı CLI veya REST/gRPC servis sınırları tercih edilmelidir.

---

## Özet Karşılaştırma Matrisi

| Proje | GitHub Deposu | Ana Dil / Mimari | Güncel Lisans | Ölçü Atölyesi İçin En Uygun Kullanım Şekli | Doğrudan Kod Kopyalama / Kütüphane Bağlama |
|---|---|---|---|---|---|
| **1. OpenCV** | `opencv/opencv` | C++ (C/Python/Java/C# sarmalayıcılar) | **Apache-2.0** | Cihaz içi kütüphane (`OpenCvSharp4` / yerel C++ P/Invoke) ve PC görüntü işleme motoru | **Uygun** (Apache-2.0 bildirim ve lisans metni korunarak) |
| **2. OpenScan** | `OpenScan-org/OpenScan3` | Python (FastAPI, Pydantic, Vue.js SPA) | **GPL-3.0** | Mimari referans ve harici tarama/kamera kontrol REST protokolü | **Uygun Değil** (GPLv3; süreç içi bağlanırsa tüm projeyi GPLv3 yapar) |
| **3. Meshroom** | `alicevision/Meshroom` | Python (Qt/QML düğüm tabanlı boru hattı) | **MPL-2.0** | Masaüstü (PC) üzerinde harici fotogrametri iş akışı / CLI orkestrasyonu | **Kısmen** (Dosya bazlı zayıf copyleft; harici süreç olarak çalıştırmak daha güvenli) |
| **4. AliceVision** | `alicevision/AliceVision` | C++ / CUDA (3B bilgisayarlı görü ve SfM/MVS) | **MPL-2.0** (bazı alt bileşenler MIT/BSD) | Masaüstü (PC) arka uç servisi veya CLI (`aliceVision_*` ikilileri) | **Harici Servis / CLI Önerilir** (NVIDIA CUDA/yüksek GPU gereksinimi nedeniyle mobil cihaza uygun değil) |
| **5. FreeCAD** | `FreeCAD/FreeCAD` | C++ / Python (Parametrik 3B CAD) | **LGPL-2.1+** | PC üzerinde başsız (`freecadcmd`) betik motoru veya harici CAD doğrulama aracı | **Harici CLI / Python Betik Motoru Önerilir** |
| **6. Open CASCADE (OCCT)** | `Open-Cascade-SAS/OCCT` | C++ (B-Rep katı modelleme çekirdeği) | **LGPL-2.1** (+ OCCT Exception) | PC veya sunucu tarafında STEP/IGES/STL/DXF üretim kütüphanesi (C++/CLI veya P/Invoke C API) | **Dinamik Kütüphane / Ayrı Servis Uygun** |
| **7. ezdxf** | `mozman/ezdxf` | Saf Python (DXF R12–R2018 okuma/yazma) | **MIT** | PC servisinde Python modülü VEYA algoritma/veri yapısı referansı olarak yerel C# DXF yazıcı | **Uygun** (MIT lisansı hem ticari hem açık kaynak dağıtıma tam uyumlu) |
| **8. OrcaSlicer** | `OrcaSlicer/OrcaSlicer` | C++ (wxWidgets, libnest2d, Bambu/Klipper/OctoPrint ağ protokolleri) | **AGPL-3.0** | PC üzerinde harici CLI dilimleyici (`orca-slicer --slice`) ve yazıcı API protokol referansı | **Doğrudan Kütüphane Bağlama Sakıncalı** (AGPLv3 ağ servisi copyleft yükümlülüğü taşır; harici CLI olarak çağrılmalı) |
| **9. PrusaSlicer** | `prusa3d/PrusaSlicer` | C++ (`libslic3r` çekirdeği, CGAL, OpenVDB) | **AGPL-3.0** | PC üzerinde harici CLI dilimleyici (`prusa-slicer-console.exe --export-gcode`) | **Doğrudan Kütüphane Bağlama Sakıncalı** (AGPLv3; harici CLI olarak çağrılmalı) |

---

## 1. OpenCV (`https://github.com/opencv/opencv`)

### 1.1. Ne İşe Yarıyor?
OpenCV (Open Source Computer Vision Library), gerçek zamanlı bilgisayarlı görü ve görüntü işleme için endüstri standardı C++ kütüphanesidir. Kamera kalibrasyonu (`calib3d`), perspektif dönüşümü (`getPerspectiveTransform`, `warpPerspective`), ArUco / ChArUco işaretçi algılama (`objdetect`), kenar/kontur bulma (`Canny`, `findContours`) ve geometrik ilkel algılama (`HoughLinesP`, `HoughCircles`, `fitEllipse`, `approxPolyDP`) modüllerini içerir.

### 1.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Kamera Açısı, Lens ve Perspektif Düzeltme:**
  - `calibrateCamera`, `undistort`, `initUndistortRectifyMap` ile telefon lensinin radyal ve teğetsel distorsiyon katsayılarının ($k_1, k_2, p_1, p_2, k_3$) giderilmesi.
  - Cetvelli veya 4 köşesi bilinen kalibrasyon zemini (`ArUco` / `ChArUco` / satranç tahtası) üzerinden homografi matrisi ($3 \times 3$ `findHomography` / `getPerspectiveTransform`) hesaplanarak eğik çekilmiş fotoğrafların kuşbakışı (ortoparalel) düzleme dönüştürülmesi (`warpPerspective`).
- **Otomatik Kontur, Çizgi, Yay, Daire ve Delik Algılama:**
  - `adaptiveThreshold` + `Canny` + `findContours` (`RETR_CCOMP` veya `RETR_TREE`) ile parçanın dış konturu ve iç deliklerinin (hiyerarşik iç konturlar) otomatik ayrıştırılması.
  - `minAreaRect`, `fitEllipse` ve `HoughCircles` ile vida delikleri, contalar ve dairesel çapların alt-piksel (`cornerSubPix`) hassasiyetinde bulunması.
- **Hassas Nokta Yakalama (Snap-to-Edge / Snap-to-Corner):**
  - Kullanıcının `R1/R2` veya `M1/M2` tutamacını sürüklediği bölgede `goodFeaturesToTrack` veya `cornerSubPix` ile en yakın gerçek kenara/köşeye otomatik yapışma.

### 1.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- **Seçenek A — Hazır .NET Sarmalayıcı (`OpenCvSharp4` veya `Emgu CV`):**
  - `OpenCvSharp4` (Apache-2.0) Windows (`win-x64`) üzerinde çok olgundur; Android (`android-arm64`) için özel derlenmiş `OpenCvSharpExtern.so` gerekir.
  - `Emgu CV` Android ve Windows MAUI paketleri sunar ancak çift lisanslıdır (GPLv3 veya ticari lisans).
- **Seçenek B — İnce Yerel C API Köprüsü (`LibraryImport` / P/Invoke) — Önerilen:**
  - Ölçü Atölyesi'nin yalnızca ihtiyaç duyduğu 10–15 OpenCV fonksiyonunu (perspektif düzeltme, ArUco tespiti, kontur/delik çıkarma) içeren küçük bir C++ paylaşımlı kütüphane (`olcu_vision_native.dll` / `libolcu_vision_native.so`) derlenip C# `.NET 10` `[LibraryImport]` ile çağrılır. Böylece tüm OpenCV modüllerini taşımadan yalnızca `core`, `imgproc`, `calib3d`, `objdetect` modülleri (~8–12 MB) kullanılır.

### 1.4. Kullanım Biçimi (Kütüphane / Servis / Harici Uygulama)
- **Hem Mobil hem PC'de Gömülü Kütüphane (In-Process Library):** 2B perspektif düzeltme ve kontur/delik algılama milisaniyeler içinde çalıştığından doğrudan uygulama içinde yerel kütüphane olarak çalıştırılması en uygundur.

### 1.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **Apache License 2.0** (OpenCV 4.5.0 ve sonrası; eski sürümler 3-Clause BSD).
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** Hem açık kaynak hem de kapalı/ticari dağıtımda serbestçe kullanılabilir, değiştirilebilir ve statik/dinamik bağlanabilir. Yalnızca `THIRD_PARTY_NOTICES.md` içinde Apache-2.0 lisans metni ve telif bildirimi korunmalıdır. Patentli algoritmalar içeren `opencv_contrib` (`xfeatures2d` içindeki SIFT/SURF gibi) modülleri derlemeye dahil edilmemelidir.

### 1.6. Performans ve Bakım Riskleri
- **APK Boyutu:** Tam OpenCV Android paketi tüm mimarilerle 50+ MB yer kaplar. Yalnızca `arm64-v8a` ve gerekli 4 modül seçilerek boyut sınırı korunmalıdır.
- **Bellek Yönetimi:** `cv::Mat` nesneleri yönetilmeyen (unmanaged) bellekte tutulur; C# tarafında `IDisposable` / `SafeHandle` ile sarmalanmazsa mobil cihazda bellek sızıntısı (`OutOfMemoryException`) oluşturur.

---

## 2. OpenScan (`https://github.com/OpenScan-org`)

### 2.1. Ne İşe Yarıyor?
OpenScan (`OpenScan3`, `OpenScan3-Client`, `OpenScanCloud`), küçük ve orta ölçekli nesnelerin otomatik fotogrametri ile 3B taranması için geliştirilmiş açık kaynaklı donanım ve yazılım ekosistemidir. Güncel `OpenScan3` mimarisi Python **FastAPI** ve **Pydantic** tabanlı bir REST API arka ucu ile **Vue.js** tabanlı bir istemci arayüzünden oluşur.

### 2.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Telefon Kamerasının PC Üzerinden Yönetilmesi İçin Protokol Tasarımı:**
  - OpenScan3'ün kamera soyutlama katmanı (`controllers/hardware/cameras`), önizleme akışı (MJPEG / WebSocket), odaklama/pozlama kilidi ve adım adım çekim dizisi (routine) durumu yönetimi, Ölçü Atölyesi'nin **"PC'den Telefon Kamerasını Yönetme"** modülü için doğrudan mimari model oluşturur.
- **Kullanıcıyı Adım Adım Yönlendiren Çekim Akışı:**
  - Bulanıklık kontrolü, ışık/parlama uyarısı ve açı kapsama kontrolü gibi kalite kapıları.

### 2.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- OpenScan3 Raspberry Pi donanımına (`libcamera`, GPIO step motor sürücüleri) bağlı bir Python uygulamasıdır. `.NET 10 / MAUI` içine kütüphane olarak gömülmez.
- Bunun yerine:
  1. Mevcut bir OpenScan cihazı varsa `.NET` `HttpClient` ile OpenScan3 REST API uç noktalarına (`/api/v1/camera/...`) bağlanılabilir.
  2. Ölçü Atölyesi'nin kendi **Telefon <-> PC Eşleştirme Servisi** tasarlanırken OpenScan3'ün durum makinesi ve REST şeması örnek alınarak C# (`ASP.NET Core Minimal API` / `SignalR` veya `WebSocket`) ile temiz bir şekilde yazılır.

### 2.4. Kullanım Biçimi
- **Mimari Referans / İsteğe Bağlı Harici REST Cihazı:** Doğrudan kod bağımlılığı kurulmamalıdır.

### 2.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **GNU General Public License v3.0 (GPL-3.0)** (`OpenScan3` deposu).
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** Güçlü copyleft lisanstır. OpenScan kaynak kodu doğrudan Ölçü Atölyesi'ne kopyalanırsa veya aynı süreçte bağlanırsa tüm Ölçü Atölyesi kodunun GPL-3.0 ile lisanslanması zorunlu olur. Bu nedenle **OpenScan'den kaynak kod kopyalanmamalı**, yalnızca HTTP/REST üzerinden harici cihaz olarak haberleşilmeli veya iş akışı fikri temiz oda (clean-room) yaklaşımıyla C# dilinde özgün olarak uygulanmalıdır.

### 2.6. Performans ve Bakım Riskleri
- Yerel ağ (Wi-Fi) üzerinden yüksek çözünürlüklü fotoğraf aktarımında gecikme ve bağlantı kopması riski vardır; önizleme için düşük gecikmeli MJPEG/WebRTC, ölçüm için tam çözünürlüklü kayıpsız/yüksek kaliteli JPEG aktarımı ayrılmalıdır.

---

## 3. Meshroom (`https://github.com/alicevision/meshroom`) ve 4. AliceVision (`https://github.com/alicevision/AliceVision`)

### 3.1. Ne İşe Yarıyor?
- **AliceVision:** Çoklu fotoğraflardan 3B yüzey ve kamera konumu çıkaran (Structure-from-Motion / `SfM` ve Multi-View Stereo / `MVS`) C++ / CUDA fotogrametri motorudur.
- **Meshroom:** AliceVision komut satırı araçlarını düğüm tabanlı (node-graph) bir iş akışıyla yöneten Python / Qt masaüstü uygulamasıdır.

### 3.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Çok Açılı Fotoğraflardan Ölçekli 3B Model (`OBJ` / `STL`) Üretimi:**
  - Telefon kamerasıyla kalibrasyonlu bir zemin (CCTag veya ArUco işaretçili tabla) üzerinde nesnenin etrafından çekilen 20–50 fotoğrafın PC'ye aktarılıp **gerçek milimetrik ölçekte** 3B ağ (mesh) modeline dönüştürülmesi.
- **Kamera Kalibrasyonu ve Lens Veritabanı:**
  - AliceVision'ın kamera sensör genişliği veritabanı (`cameraSensors.db`) ve EXIF tabanlı iç parametre (`intrinsics`) tahminleme yapısı.

### 3.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- AliceVision ve Meshroom yüksek hesaplama gücü ve çoğu yoğun derinlik haritası (`DepthMap`) adımında **NVIDIA CUDA GPU** gerektirir; Android telefonda doğrudan çalıştırılamaz.
- **Entegrasyon Yöntemi (PC Arka Uç Köprüsü):**
  - Telefon uygulaması (`MAUI Android`) çekilen fotoğraf setini ve kalibrasyon referans bilgisini yerel ağ üzerinden **Ölçü Atölyesi PC Masaüstü / Yardımcı Servisine (`MAUI Windows`)** gönderir.
  - PC uygulaması `System.Diagnostics.Process` ile `meshroom_batch` veya doğrudan `aliceVision_cameraInit`, `aliceVision_featureExtraction`, `aliceVision_structureFromMotion`, `aliceVision_meshing` komut satırı ikililerini (CLI) çalıştırır, standart çıktıdan (`stdout`) ilerleme yüzdesini okur ve üretilen `.obj` / `.stl` dosyasını alır.

### 3.4. Kullanım Biçimi
- **PC Üzerinde Harici CLI / Arka Uç İşlem Hattı (Out-of-Process Pipeline):** Kütüphane olarak derlemek yerine resmi önceden derlenmiş ikilileri (binaries) harici süreç olarak çağırmak en sağlam yöntemdir.

### 3.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **Mozilla Public License 2.0 (MPL-2.0)** (hem Meshroom hem AliceVision ana çekirdeği).
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** MPL-2.0 dosya düzeyinde zayıf copyleft lisanstır. Harici CLI olarak çağrıldığında veya değiştirilmeden dinamik bağlandığında Ölçü Atölyesi'nin kendi lisansını etkilemez; yalnızca AliceVision/Meshroom dosyalarında değişiklik yapılırsa o dosyaların MPL-2.0 altında paylaşılması gerekir. (Not: AliceVision derlemesinde isteğe bağlı bazı üçüncü taraf bağımlılıklar GPL içerebildiğinden resmi MPL-2.0 uyumlu dağıtım profili kullanılmalıdır.)

### 3.6. Performans ve Bakım Riskleri
- **Donanım Bağımlılığı:** CUDA destekli GPU olmayan bilgisayarlarda yoğun 3B nokta bulutu üretimi çok yavaş çalışır veya başarısız olur. Bu nedenle **2B/2.5B düzlemsel parça ölçümü (OpenCV + OCCT)** her bilgisayarda ve telefonda çalışan birincil yol olmalı; **AliceVision/Meshroom ile tam 3B fotogrametri** ise PC'de GPU bulunduğunda açılan ileri seviye seçenek olmalıdır.

---

## 5. FreeCAD (`https://github.com/FreeCAD/FreeCAD`)

### 5.1. Ne İşe Yarıyor?
FreeCAD, Open CASCADE (OCCT) geometri çekirdeği üzerine kurulu, C++ ve Python ile geliştirilmiş tam kapsamlı açık kaynaklı parametrik 3B CAD uygulamasıdır. `Sketcher`, `Part`, `PartDesign`, `Draft` ve `TechDraw` çalışma tezgahları ile 2B kısıt tabanlı eskizlerden 3B katı modeller (`STEP`, `IGES`, `BREP`) ve kesim resimleri (`DXF`, `SVG`) üretir.

### 5.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Başsız (Headless) CAD Dönüştürme ve Parametrik Ekstrüzyon (`freecadcmd`):**
  - Fotoğraftan çıkarılan 2B plaka konturu ve delik koordinatlarını içeren bir Python betiğini `freecadcmd.exe script.py` ile arayüz açmadan çalıştırarak tam geçerli `STEP` (`AP203`/`AP214`), `STL` ve `DXF` dosyaları üretmek.
- **Eskiz ve Geometri Doğrulama:**
  - Açık kalan konturların kapatılması, eşmerkezli deliklerin ve teğet yayların parametrik kısıtlarla düzeltilmesi.

### 5.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- FreeCAD devasa bir masaüstü uygulamasıdır; `.NET MAUI` içine kütüphane olarak bağlanmaz.
- Windows PC tarafında kullanıcı sisteminde FreeCAD kuruluysa (veya taşınabilir paket olarak sağlandıysa), C# tarafından JSON ara verisi oluşturulup küçük bir Python şablon betiğiyle `freecadcmd` üzerinden çağrılır.

### 5.4. Kullanım Biçimi
- **İsteğe Bağlı Harici Masaüstü Otomasyon Aracı (Headless CLI):** Ancak temel `DXF`, `STL` ve `STEP` üretimi için kullanıcının bilgisayarına tüm FreeCAD'i kurmasını zorunlu kılmak yerine, doğrudan **Open CASCADE (OCCT)** ve **yerel C# DXF/STL üreticisi** kullanmak çok daha hafif ve bağımsızdır. FreeCAD, `.FCStd` (FreeCAD yerel proje dosyası) dışa aktarımı veya ileri düzey CAD düzenleme entegrasyonu("FreeCAD'de Aç" düğmesi) için kullanılmalıdır.

### 5.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **LGPL-2.1 veya sonrası (LGPL-2.1+)**.
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** Harici süreç (`freecadcmd`) olarak çağrılması veya `.FCStd` / Python betiği üretilmesi lisans açısından tamamen serbesttir ve hiçbir copyleft kısıtlaması doğurmaz.

### 5.6. Performans ve Bakım Riskleri
- Tam FreeCAD dağıtımı >1 GB boyutundadır ve yalnızca masaüstü işletim sistemlerinde çalışır.

---

## 6. Open CASCADE Technology — OCCT (`https://github.com/Open-Cascade-SAS/OCCT`)

### 6.1. Ne İşe Yarıyor?
Open CASCADE (OCCT), FreeCAD ve KiCad dahil pek çok endüstriyel yazılımın kalbinde yer alan C++ sınır temsili (B-Rep / Boundary Representation) 3B katı modelleme ve CAD veri değişim çekirdeğidir.

### 6.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Gerçek `STEP` (`STEPControl_Writer`) ve `IGES` Üretimi:**
  - Fotoğraftan ölçülen 2B dış kontur (çizgiler, yaylar) ve iç deliklerin bir yüzeye (`TopoDS_Face`) dönüştürülüp kullanıcı tarafından girilen et kalınlığı (`thickness`) kadar ekstrüde edilerek (`BRepPrimAPI_MakePrism`) gerçek analitik `STEP` katı modeline dönüştürülmesi.
- **Yüksek Kaliteli `STL` Üçgenleme (`BRepMesh_IncrementalMesh` + `StlAPI_Writer`):**
  - Dairesel deliklerin ve yayların belirlenen sapma toleransıyla (ör. `0.02 mm` kiriş hatası) kusursuz su geçirmez (watertight / manifold) 3B baskıya hazır `STL` ağına dönüştürülmesi.
- **Boolean İşlemleri (`BRepAlgoAPI_Cut`, `BRepAlgoAPI_Fuse`) ve Pah/Radius (`BRepFilletAPI_MakeFillet`):**
  - Plakadan deliklerin çıkarılması ve köşe yuvarlatmaları.

### 6.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- **Dar Kapsamlı C API Köprüsü (Önerilen):**
  - Tüm OCCT sınıflarını C#'a sarmalamak yerine, yalnızca 2B profil + kalınlık alıp `STEP` ve `STL` dosyası yazan küçük bir C++ DLL/SO sarmalayıcısı (`olcu_cad_kernel`) hazırlanır ve `.NET 10` `[LibraryImport]` ile çağrılır.
  - Alternatif olarak basit prizmatik parçaların `STL` ve `DXF` çıktısı doğrudan saf C# ile (sıfır harici bağımlılıkla) hem Android telefonda hem Windows'ta üretilebilir; analitik `STEP` çıktısı için OCCT kullanılır.

### 6.4. Kullanım Biçimi
- **Gömülü Yerel Kütüphane (Native Shared Library) veya PC Mikroservisi.**

### 6.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **LGPL-2.1** (OCCT Özel İstisnası ile birlikte).
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** Dinamik kütüphane (`.dll` / `.so`) olarak bağlandığında hem açık kaynak hem ticari yazılımlarda serbestçe kullanılabilir. OCCT kaynak kodunda değişiklik yapılırsa yalnızca o değişikliklerin LGPL-2.1 kapsamında yayınlanması ve LGPL bildirimlerinin `THIRD_PARTY_NOTICES.md` içinde yer alması gerekir.

### 6.6. Performans ve Bakım Riskleri
- C++ derleme zinciri (CMake) platform bazlı (`win-x64`, `android-arm64`) derleme gerektirir. Yalnızca temel modelleme ve STEP/STL modülleri (`TKernel`, `TKMath`, `TKBRep`, `TKTopAlgo`, `TKPrim`, `TKMesh`, `TKSTEP`, `TKSTL`) derlenerek kütüphane boyutu küçültülmelidir.

---

## 7. ezdxf (`https://github.com/mozman/ezdxf`)

### 7.1. Ne İşe Yarıyor?
`ezdxf`, AutoCAD kurulmasına gerek kalmadan farklı DXF sürümlerini (`R12`, `R2000`, `R2010`, `R2018`) oluşturmak, okumak ve düzenlemek için geliştirilmiş, tip korumalı ve kapsamlı bir Python kütüphanesidir.

### 7.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Standartlara Tam Uyumlu DXF Yapısı:**
  - `LINE`, `ARC`, `CIRCLE`, `LWPOLYLINE` (kapalı konturlar ve bulge/yay değerleri), `DIMENSION` (ölçülendirme okları) ve katman (`LAYER`: `DIS_KONTUR`, `DELIKLER`, `REFERANS`, `OLCULER`) organizasyonu.
- **C# DXF Üreticisi İçin Referans:**
  - `ezdxf` MIT lisanslı olduğu için, hem PC tarafında Python tabanlı doğrulama/dönüştürme aracı olarak kullanılabilir hem de minimal DXF (`R12` / `R2000` ASCII) varlık şeması referans alınarak `.NET 10` içinde doğrudan telefonda da çalışan hafif bir C# DXF yazıcısı (veya MIT lisanslı `netDxf` entegrasyonu) geliştirilebilir.

### 7.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- **Android + Windows Cihaz İçi Çözüm:** DXF metin tabanlı (tagged ASCII) bir format olduğundan, `ezdxf`'in katman ve `LWPOLYLINE` / `LINE` / `CIRCLE` / `ARC` yapılarını temel alan **saf C# DXF yazıcı servisi (`DxfExportService`)** yazılması, Python çalışma zamanı taşıma zorunluluğunu ortadan kaldırır ve Android telefonda çevrimdışı anında DXF üretimi sağlar.
- **PC Gelişmiş Çözüm:** Karmaşık DXF dosyalarını okumak/onarmak için PC yardımcı servisinde `ezdxf` komutu çalıştırılabilir.

### 7.4. Kullanım Biçimi
- **Format/Mimari Referansı (C# Yerel Uygulama İçin) ve PC Yardımcı Betik Kütüphanesi.**

### 7.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **MIT License**.
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** Son derece esnek ve izin verici (permissive) lisanstır. Kod veya algoritmalar telif satırı korunarak hem açık kaynak hem ticari sürümlerde hiçbir kısıtlama olmadan kullanılabilir.

### 7.6. Performans ve Bakım Riskleri
- Saf C# ile kendi DXF `R2000` / `R12` yazıcımızı oluşturduğumuzda performans ve bakım riski sıfıra yakındır; harici CAD yazılımlarıyla (AutoCAD, LibreCAD, FreeCAD, lazer kesim yazılımları) uyumluluk için birim üstbilgileri (`$INSUNITS`: `4` = mm) ve ondalık nokta (`CultureInfo.InvariantCulture`) zorunlu tutulmalıdır.

---

## 8. OrcaSlicer (`https://github.com/OrcaSlicer/OrcaSlicer`) ve 9. PrusaSlicer (`https://github.com/prusa3d/PrusaSlicer`)

### 8.1. Ne İşe Yarıyor?
- **PrusaSlicer:** Slic3r tabanlı, C++ (`libslic3r`) ile yazılmış, 3B modelleri (`STL`, `OBJ`, `STEP`, `3MF`) katmanlara ayırarak 3B yazıcıların anlayacağı `G-code` komutlarına dönüştüren endüstri standardı dilimleme (slicing) yazılımıdır.
- **OrcaSlicer:** Bambu Studio ve PrusaSlicer üzerine inşa edilmiş; gelişmiş kalibrasyon testleri, çoklu tabla yönetimi ve modern yazıcı ağ protokolleri (**OctoPrint**, **Moonraker/Klipper**, **PrusaLink**, **Bambu Lab MQTT/LAN**) desteğine sahip popüler dilimleyicidir.

### 8.2. Hangi Özelliklerinden Yararlanabiliriz?
- **Komut Satırından (Headless CLI) 3B Baskıya Hazırlama ve Dilimleme:**
  - Ölçü Atölyesi tarafından üretilen `STL` veya `STEP` dosyasının kullanıcı arayüzü açmadan doğrudan komut satırından dilimlenmesi:
    - PrusaSlicer: `prusa-slicer-console.exe --load config.ini --export-gcode model.stl --output model.gcode`
    - OrcaSlicer: `orca-slicer --slice 0 --load-settings "..." model.3mf`
  - Dilimleme sonucunda tahmini baskı süresi, harcanacak filament miktarı (gram/metre) ve katman önizleme verisinin (`G-code` yorum satırlarından) okunup Ölçü Atölyesi arayüzünde gösterilmesi.
- **"Dilimleyicide Aç" (Open in Slicer)Masaüstü Entegrasyonu:**
  - PC üzerinde üretilen `STL` / `3MF` modelinin tek tıklamayla yüklü OrcaSlicer veya PrusaSlicer uygulamasında açılması.
- **3B Yazıcı Kontrol Protokolleri:**
  - İleride desteklenen yazıcıları doğrudan kontrol etmek için Klipper (`Moonraker` REST/WebSocket API) ve OctoPrint REST API standartlarının `.NET HttpClient` ile uygulanması.

### 8.3. .NET 10 / MAUI İle Nasıl Entegre Edilebilir?
- `libslic3r` C++ kütüphanesini doğrudan `.NET MAUI` içine bağlamak hem devasa bağımlılık ağacı (`Boost`, `CGAL`, `OpenVDB`, `TBB`, `Eigen`) hem de **AGPL-3.0** lisansı nedeniyle **kesinlikle önerilmez**.
- **Önerilen Entegrasyon (`SlicerIntegrationService`):**
  - Windows PC üzerinde `ProcessStartInfo` ile kurulu `prusa-slicer-console.exe` veya `orca-slicer.exe` ikililerini ayrı süreç (out-of-process CLI) olarak çağırmak.
  - Yazıcıya doğrudan gönderim için C# tarafında temiz bir `MoonrakerClient` / `OctoPrintClient` REST istemcisi yazmak.

### 8.4. Kullanım Biçimi
- **Harici Uygulama / Komut Satırı Aracı (External CLI Process).**

### 8.5. Lisans Koşulları ve Dağıtım Etkileri
- **Lisans:** **GNU Affero General Public License v3.0 (AGPL-3.0)** (hem OrcaSlicer hem PrusaSlicer).
- **Ticari ve Açık Kaynak Dağıtım Etkisi:** AGPL-3.0 en sıkı copyleft lisanstır; kütüphane olarak bağlanırsa veya ağ üzerinden servis olarak sunulursa temas ettiği tüm yazılımın AGPL-3.0 olmasını şart koşar.
- **Güvenli Sınır:** OrcaSlicer ve PrusaSlicer'dan **hiçbir kaynak kod kopyalanmamalı** ve statik/dinamik bağlantı kurulmamalıdır. Yalnızca kullanıcının bilgisayarında kurulu olan yürütülebilir dosyayı (`exe`) standart komut satırı argümanlarıyla (`STL`/`3MF` dosyası verip `G-code` çıktısı alarak) çağırmak işletim sistemi düzeyinde ayrı süreç iletişimi olduğundan lisans bulaşmasına (license contamination) yol açmaz.

### 8.6. Performans ve Bakım Riskleri
- CLI parametreleri sürümler arasında küçük farklılıklar gösterebilir; bu nedenle `SlicerIntegrationService` içinde sürüm algılama (`--version`) ve zaman aşımı (`CancellationToken`) koruması bulunmalıdır.
