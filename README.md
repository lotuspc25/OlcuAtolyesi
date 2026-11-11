# Ölçü Atölyesi (Measure Workshop)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Android-lightgrey.svg)]()

> **Fiziksel dünyadan milimetrik CAD verisine:** Telefon kamerasından yüksek çözünürlüklü görüntü yakalama, optik referanslama ve doğrudan 2B DXF vektörel teknik çizim üretimine uzanan uçtan uca hibrit ölçüm ve sayısallaştırma ekosistemi.

---

## 1. Proje Vizyonu

**Ölçü Atölyesi**, atölye, şantiye ve tasarım ofisi arasındaki veri köprüsünü kurmak amacıyla tasarlanmış açık kaynaklı bir optik ölçüm ve tersine mühendislik platformudur. Geleneksel kumpas ve şerit metre ölçümlerinin yetersiz kaldığı karmaşık profilli sac parçalar, contalar, flanşlar, bağlantı plakaları veya fiziksel numuneler; bilinen bir referans nesne (kredi kartı, madeni para, kalibre edilmiş cetvel vb.) ile birlikte fotoğraflanır.

Sistem, elde edilen görüntüyü optik bozulma ve perspektif kısıtlarından arındırarak alt piksel düzeyinde işler, milimetrik koordinat düzlemine oturtur ve doğrudan CNC tezgâhlarında, lazer kesim makinelerinde veya CAD yazılımlarında (AutoCAD, SolidWorks, FreeCAD vb.) açılabilecek **standart 2B DXF (R12 ASCII)** formatına dönüştürür.

Bu ekosistem üç tamamlayıcı ayaktan oluşur:
1. **Saha Modu (.NET MAUI - Android):** Taşınabilir bir optik tarama ve veri toplama istasyonu.
2. **Stüdyo Modu (WPF - Windows Masaüstü):** Görüntü işleme filtreleri, çift eksenli cetvel taksimatları, ekran kalibrasyonu ve CAD ihracı sunan masaüstü analiz stüdyosu.
3. **Çapraz Masaüstü Modu (Avalonia UI - Linux Debian & Windows):** Sanal makine ve emülatör gerektirmeden Debian, Ubuntu ve Windows üzerinde yerel pencere olarak çalışan çapraz platform analiz arayüzü (`OlcuAtolyesi.Desktop`).

---

## 2. Saha Modu (Mobil MAUI)

Mobil uygulama (`OlcuAtolyesi`), Android platformunda çalışan hafif, hızlı ve güvenilir bir saha ölçüm istasyonudur:

- **Hızlı Referanslama ve Ölçüm:** Kamera veya sistem galerisinden alınan fotoğraflar üzerinde iki noktalı $R_1 - R_2$ referans hattı belirlenir. Sistem, fiziksel uzunluğu bilinen bu referans üzerinden piksel-milimetre ölçek katsayısını ($s = L / d_{px}$) hesaplar. Ardından $M_1 - M_2$ ölçüm noktaları ile parça boyutları saniyeler içinde ölçülür.
- **Kalıcı Proje ve Veri Yönetimi:** Yapılan tüm ölçümler, fotoğraflar ve meta veriler cihazın korumalı depolama alanında atomik JSON ve otomatik `.bak` kurtarma altyapısıyla saklanır.
- **Kablosuz Ölçüm Kafası (Wi-Fi Sensör Modu):** Mobil cihaz, masaüstü uygulaması için kablosuz bir optik kafa gibi çalışır. Donanım seviyesinde çalışan `ContinuousPreviewPipeline` ve `PixelCopy` mimarisiyle, cihazın canlı kamera görüntüsünü yerel ağ üzerinden PC'ye düşük gecikmeli olarak aktarır.
- **Donanımsal Kayıpsız Çekim:** PC'den gelen uzaktan çekim tetiklemesiyle, önizleme akışından bağımsız olarak telefon kamerasının tam optik çözünürlüğünde (FHD / 4K) sıkıştırmasız fotoğraf yakalar.

![Mobil Uygulama](Docs/Images/mobile_preview.png)

---

## 3. Stüdyo Modu (Masaüstü WPF)

Masaüstü uygulaması (`OlcuAtolyesi-PC`), .NET 10 WPF ve saf C# matematik çekirdeği (`OlcuAtolyesi.Core`) üzerinde koşan profesyonel bir teknik ölçüm ve CAD stüdyosudur:

- **Özel Çalışma Alanı (F2 Ölçüm Penceresi):** Sadeleştirilmiş ana pencereden tek tuşla (F2) açılabilen bağımsız, sekmeli ve tam ekran ölçüm penceresi (`MeasurementWindow`).
- **Çift Cetvel Sistemi:**
  - **A Cetveli (Nesne Cetveli):** Fotoğrafın kenarında parça koordinatlarına göre gerçek milimetrik taksimat (1/5/10/50 mm adımlı dinamik adaptif skala).
  - **B Cetveli (Ekran Cetveli):** Monitörün fiziksel DPI değerine kalibre edilmiş, ekran üzerinde gerçek 1:1 fiziksel cetvel.
- **Ekran Kalibrasyon Sihirbazı:** Standart bir referans nesne (ör. kredi kartı) monitöre tutularak ekran piksel yoğunluğu kalibre edilir ve ayarlar kalıcı olarak saklanır.
- **Hazır Referans Kütüphanesi:** Kredi kartı (85.60 mm), A4 kağıt (210/297 mm), 1 TL (26.15 mm), 50 Kuruş (23.85 mm) ve standart cetveller için tek tıkla şablon seçimi.
- **Gelişmiş Görüntü İşleme Filtreleri:**
  - Kontrast, parlaklık ve keskinlik ayarları.
  - Gri tonlama (Grayscale), negatif ve kenar vurgulama (Laplacian / Edge Detection) filtreleri.
  - Milimetrik kılavuz ızgara (Grid) katmanı.
- **Tersinir Geometrik Dönüşüm (`ImageDisplayTransform`):** 90°/180° döndürme veya aynalama işlemlerinde ölçüm noktaları asla bozulmaz; tüm matematiksel hesaplamalar ve CAD vektörleri daima orijinal görüntü uzayında korunur.
- **2B DXF İhracı:** Çizilen referans ve ölçüm geometrileri, standart AutoCAD R12 ASCII DXF formatında ($INSUNITS=4$ mm, Y ekseni CAD dönüşümü, ok başları ve daire işaretleyicileri ile) doğrudan dışa aktarılır.

![PC Çalışma Alanı](Docs/Images/pc_workspace.png)

---

## 4. Ağ ve İletişim Mimarisi

Ölçü Atölyesi, mobil cihaz ile masaüstü bilgisayarı aynı yerel ağda (Wi-Fi) sıfır konfigürasyonla ve endüstriyel güvenlik standartlarında birleştirir:

```text
+----------------------------+                     +----------------------------+
|      Mobil Cihaz           |                     |       Masaüstü PC          |
|      (Saha Modu)           |                     |       (Stüdyo Modu)        |
|                            |                     |                            |
|  +----------------------+  |   HTTP GET /stream  |  +----------------------+  |
|  | MJPEG Stream Servisi |==|====================>|  | RemoteCameraClient   |  |
|  +----------------------+  |   (Multipart/x-mix) |  +----------------------+  |
|                            |                     |                            |
|  +----------------------+  |   POST /api/capture |  +----------------------+  |
|  | HTTP REST API        |<=|=====================|  | RemotePhotoClient    |  |
|  | - PIN Auth / Bearer  |  |   (Tetikleme & PIN) |  +----------------------+  |
|  +----------------------+  |                     |                            |
|                            |   GET /photos/{id}  |                            |
|  +----------------------+  |====================>|  +----------------------+  |
|  | PhotoStorageService  |  |   SHA-256 Checksum  |  | Ölçüm Tuvali (F2)    |  |
|  +----------------------+  |   (Kayıpsız Dosya)  |  +----------------------+  |
+----------------------------+                     +----------------------------+
```

1. **Canlı Görüntü Hattı (MJPEG):** RFC uyumlu `multipart/x-mixed-replace` veri akışı ile telefon kamerasından PC'ye minimum gecikmeyle canlı önizleme aktarılır. Her kare SOF (0xFFD8) ve EOI (0xFFD9) bütünlük kontrolünden geçirilir.
2. **Güvenli Eşleşme ve Yetkilendirme:** Mobil ekranda üretilen tek kullanımlık 6 haneli oturum PIN kodu ile masaüstü tarafında `Bearer Token` yetkilendirmesi sağlanır. Hatalı girişimler ve arka plana geçişlerde kamera akışı derhal sonlandırılır.
3. **Kayıpsız Dosya Aktarımı ve SHA-256 Doğrulama:** Uzaktan çekilen orijinal fotoğraf, `/api/v1/photos/{id}` uç noktası üzerinden parça parça indirilir ve SHA-256 hash sağlama toplamı doğrulanarak bit seviyesinde kayıpsız aktarım garanti edilir.
4. **Merkezi Tanı ve Günlük Senkronizasyonu:** Her iki cihazdaki çalışma kayıtları (`/logs`), PC üzerindeki merkezi konsolda anlık olarak izlenebilir ve döngüsel arşivlenir.
5. **Alternatif USB / scrcpy Modu:** Wi-Fi ağının olmadığı veya yüksek elektromanyetik parazit içeren atölye ortamlarında, donanım hızlandırmalı ADB / scrcpy USB köprüsü üzerinden sıfır gecikmeli akış desteği mevcuttur.

---

## 5. Depo Yapısı (Monorepo)

```text
OlcuAtolyesi/
├── OlcuAtolyesi/                 # Mobil (.NET 10 MAUI) Android saha uygulaması
│   ├── OlcuAtolyesi/             # Mobil kaynak kodları ve sayfalar
│   └── Tests/LogicChecks/        # Mobil bağımsız matematik ve mantık testleri
├── OlcuAtolyesi-PC/              # Masaüstü stüdyo ve çekirdek (.NET 10)
│   ├── src/OlcuAtolyesi.Core/    # Görünümden bağımsız saf C# geometri, ölçüm ve DXF motoru
│   ├── src/OlcuAtolyesi.PC/      # Windows masaüstü WPF kullanıcı arayüzü ve görünüm modelleri
│   ├── src/OlcuAtolyesi.Desktop/ # Linux (Debian/Ubuntu) ve Windows Çapraz Masaüstü (Avalonia UI)
│   ├── Calistir.bat              # Windows tek tıkla başlatıcı
│   ├── Calistir.sh               # Linux tek tıkla başlatıcı
│   ├── Paketle-Linux.bat         # Linux x64 bağımsız paket üretim scripti
│   └── tests/OlcuAtolyesi.PC.Tests/ # Otomatik doğrulama testleri (54/54 Başarılı)
├── Calistir-Linux.sh             # Kök dizin Linux başlatıcısı
├── Docs/
│   ├── Images/                   # Ekran görüntüleri (mobile_preview.png, pc_workspace.png)
│   ├── Reports/                  # Sürüm ve test doğrulama raporları
│   └── ROADMAP.md                # Ürün vizyonu ve uzun vadeli sürüm planları
├── LICENSE                       # MIT Lisansı
└── README.md                     # Ana proje tanıtım ve kullanım rehberi
```

---

## 6. Kurulum ve Çalıştırma

### A. Linux (Debian 10/11/12, Ubuntu, LinuxCNC) Üzerinde Çalıştırma

Linux üzerinde sanal makine veya yerel masaüstünüzde projeyi doğrudan GitHub'dan çekip çalıştırmak için:

```bash
# 1. Gerekli temel sistem kütüphanelerini kurun:
sudo apt update
sudo apt install -y git curl libgl1-mesa-glx libssl-dev libfontconfig1 libx11-6

# 2. Depoyu klonlayın ve klasöre girin:
git clone https://github.com/lotuspc25/OlcuAtolyesi.git
cd OlcuAtolyesi

# 3. Başlatıcıya yetki verip çalıştırın:
chmod +x Calistir-Linux.sh
./Calistir-Linux.sh
```
> *Not: Sisteminizde .NET 10 kurulu değilse, başlatıcı betik kullanıcı ev dizininize (`~/.dotnet`) otomatik olarak kurulumu tamamlayıp uygulamayı başlatacaktır.*

---

### B. Windows Üzerinde Çalıştırma

1. Depoyu klonlayın veya GitHub'dan ZIP olarak indirin:
   ```powershell
   git clone https://github.com/lotuspc25/OlcuAtolyesi.git
   cd OlcuAtolyesi\OlcuAtolyesi-PC
   ```
2. Klasördeki **`Calistir.bat`** dosyasına çift tıklayarak uygulamayı başlatın.
   *(VEYA `OlcuAtolyesi.PC.sln` çözüm dosyasını Visual Studio 2022/2026 ile açabilirsiniz).*

---

### C. Otomatik Testler ve Doğrulama

```powershell
# PC ve Çapraz Masaüstü Testleri (54/54 Başarılı)
dotnet run --project OlcuAtolyesi-PC/tests/OlcuAtolyesi.PC.Tests/OlcuAtolyesi.PC.Tests.csproj -c Debug

# Mobil Mantık ve Geometri Testleri
dotnet run --project OlcuAtolyesi/Tests/LogicChecks/LogicChecks.csproj -c Debug
```

---

## 7. Lisans

Bu proje [MIT Lisansı](LICENSE) altında lisanslanmıştır. Açık kaynak kodlu olup ticari ve kişisel kullanıma uygundur.
