# Ölçü Atölyesi

**v0.5.0.5 — Akıcı Önizleme, Telefona Orijinal Kayıt ve PC'ye Kayıpsız Dosya Aktarımı**

Ölçü Atölyesi, farklı ölçüm akışlarını telefonda sade ve anlaşılır bir araç kutusunda birleştirmeyi amaçlayan .NET MAUI uygulamasıdır. Bu sürüm, Android üzerinde kamera veya galeriden alınan gerçek fotoğraflar üzerinde **R1–R2 manuel referanslı M1–M2 mesafe ölçümü**, yapılan ölçümleri fotoğraflarıyla birlikte cihazda kalıcı olarak saklayan **Kalıcı Proje Kayıt Sistemi**, yerel ağ (Wi-Fi) üzerinden PC uygulamasına düşük gecikmeli, akıcı kamera önizlemesi aktaran **ContinuousPreviewPipeline**, donanımdan çekilen fotoğrafları telefonun özel depolama alanına sıkıştırmasız kaydeden **PhotoStorageService**, PC'ye güvenli Bearer token ile bağlanan ve SHA-256 bütünlük doğrulamalı kayıpsız dosya aktarımı sağlayan **PC Kamera Bağlantısı**, canlı yayında sensör geçişlerini güvenceye alan koordinatör ve döngüsel **Kalıcı Tanılama Günlük Altyapısı** sunar.

## Bu sürümdeki ekranlar

- **Ana Sayfa:** Fotoğraftan ölç birincil eylemi, PC Kamera Bağlantısı hızlı erişimi, ekran cetveli ve kamerayla ölç kartları, son projeler ve sürüm bilgisi.
- **Araçlar:** Canlı araçlar ("PC Kamera Bağlantısı", "Kamerayla mesafe (Demo)", "Yükseklik", "Su terazisi") ve Geometri / Alan araçları.
- **PC Kamera Bağlantısı:** Telefon kamerasını yerel ağ üzerinden Ölçü Atölyesi PC uygulamasına canlı yayınlayan bağımsız HTTP MJPEG servisi. 6 haneli oturum PIN kodu, ön/arka kamera seçimi, IP/port bilgisi ve uygulama arka plana geçtiğinde otomatik kapanma ile tam gizlilik koruması.
- **Fotoğraftan ölçüm (Gerçek Fotoğraf & Demo):** Kamera/galeriden alınan fotoğraflar üzerinde R1–R2 referansı ve M1–M2 mesafe ölçümü.
- **Projeler:** Cihaz içi kalıcı kayıtlar (`FileSystem.AppDataDirectory/projects/<id>/`), yeniden düzenleme ve silme.
- **Ayarlar:** Açık/koyu/sistem teması, varsayılan uzunluk birimi, kalibrasyon durumu, yardım, hakkında ve sürüm bilgisi.
- **Yardım:** Temel araçların amacı, kullanım sırası, çalışan kısımlar ve sonucu etkileyebilecek koşullar.

Ana gezinme telefona uygun dört sekmeden oluşur: **Ana Sayfa**, **Araçlar**, **Projeler** ve **Ayarlar**. Ayrıntı ekranlarında ilgili yardım ve tutarlı geri dönüş kullanılır.

## Demo sınırları

Bu sürümde:

- kamera ve Android sistem fotoğraf seçicisi yalnız kullanıcı ilgili özelliği seçtiğinde açılır; mikrofon, sensör ve ARCore kullanılmaz;
- kamera izni yalnız Fotoğraf çek ekranında istenir; tüm depolama, mikrofon ve galeri tarama izni istenmez;
- ekran cetveli fiziksel ölçüye kalibre edilmiş sayılmaz;
- referanssız tek fotoğraftan kesin uzunluk ya da görünmeyen 3B geometri çıkarılmaz;
- DXF/STL dosyası, ağ bağlantısı, bilgisayar eşleştirmesi veya bulut aktarımı oluşturulmaz;
- projeler ve fotoğraf ölçümleri cihazın yerel uygulama depolamasında (`AppDataDirectory`) atomik JSON + yedek (`.bak`) yapısıyla kalıcı olarak saklanır;
- üyelik, reklam, analiz, telemetri ve ücretli servis bulunmaz.

Fotoğraf demosunda referans ve ölçülen yüzeyin aynı düzlemde olduğu varsayılır. Perspektif, lens bozulması ve yüzey geometrisi gerçek bir ölçümün sonucunu etkiler; bu prototip bunları düzeltmez.

## Gerçek fotoğraf üzerinde ölçüm akışı (Android)

“Fotoğraftan ölç” girişinde üç seçenek bulunur:

- **Fotoğraf çek:** uygulama içindeki canlı arka kamera → Çek → Fotoğrafı kullan / Tekrar çek / İptal. “Fotoğrafı kullan” seçildiğinde fotoğraf doğrudan ölçüm ekranında (`PhotoMeasurePage`) açılır.
- **Galeriden seç:** Android sistem seçicisinde yalnız kullanıcının seçtiği tek fotoğraf okunur ve doğrudan ölçüm ekranında (`PhotoMeasurePage`) açılır.
- **Demo ile dene:** Çizilmiş bağlantı plakası üzerinde aynı R1–R2 referans ve M1–M2 ölçüm motorunu çalıştırır.

Gerçek fotoğraf ölçüm ekranında kullanım adımları:
1. **Referans belirle** düğmesiyle veya doğrudan turuncu **R1** ve **R2** tutamaçlarını sürükleyerek fotoğraftaki bilinen bir uzunluğun (ör. cetvel, pul veya madeni para çapının iki kenarı) başlangıç ve bitiş noktalarını işaretleyin.
2. Gerçek referans uzunluğunu ve birimini (**mm**, **cm**, **m**, **inç**) girip **Referansı uygula** düğmesine dokunun.
3. **Mesafe ölç** düğmesiyle veya mavi **M1** ve **M2** tutamaçlarını sürükleyerek ölçmek istediğiniz iki noktayı seçin.
4. Ölçüm sonucu seçilen birimde anında hesaplanır; referans uzunluğu, birim veya R1–R2 noktaları değiştirildiğinde sonuç otomatik olarak yeniden hesaplanır.
5. Noktalar fotoğraf koordinatlarında saklanır; iki parmakla yakınlaştırma (1–5x), kaydırma ve **Görünümü sığdır** işlemleri ölçüm doğruluğunu bozmaz.

Fotoğraf kaynağı değiştirilmez veya silinmez. Yalnız uygulamanın cache/photo-session dizininde rastgele adlı geçici kopya oluşur. En fazla 40 MiB kaynak, 40.000 piksel kenar ve 200 milyon piksel üst sınırı uygulanır; boyut bilgisi okunmadan tam görsel çözümlenmez. Önizleme kopyası en fazla 2048 piksel kenara örneklenir, EXIF yönü uygulanır ve meta verisiz JPEG'e dönüştürülür. Bu bir arşiv veya tam çözünürlükte düzenleme özelliği değildir. Görüntüleyiciden geri çıkışta kopya silinir; işletim sistemi dosyayı meşgul tutarsa bir günden eski uygulama kopyaları sonraki açılışta temizlenir. Kaynak galeri dosyalarına bu temizlik uygulanmaz.

Kamera ekranından ayrılınca veya uygulama arka plana gidince önizleme durdurulur, handler ve kamera kaynakları bırakılır. Geri dönüşte izin hâlâ varsa ve bir çekim onayı beklenmiyorsa yeniden açılır. Tek işlem kapısı hızlı tekrar dokunmaları sınırlar; başlatma/çekim/görüntü işleme süreleri sınırlıdır.

### İzin verilmezse

İzin yalnız kamera özelliğine girince sorulur. Reddetme durumu saklanır; her girişte yeniden istem otomatik açılmaz. Android tekrar izin istemine olanak veriyorsa “İzni tekrar iste” kullanılır. Kalıcı ret durumunda açıklama ve kullanıcının isteğiyle “Uygulama ayarlarını aç” sunulur. İzinleri veya cihaz ayarlarını uygulama kendiliğinden sıfırlamaz. Alternatif olarak galeriyi veya demoyu kullanın.

Windows hedefi korunur; gerçek fotoğraf edinme bu sürümde Android'e özgüdür. Windows'ta desteklenmediği açıkça belirtilir, gerçek edinme düğmeleri kapalıdır; demo kullanılabilir.

## Güvenlik ve ağ

- Yerel kamera yayını **yalnızca aynı yerel ağdadır**; internet, bulut veya harici sunucu kullanılmaz.
- Eşleştirme 6 haneli PIN ile yapılır. Doğru PIN girildiğinde 1 saat geçerli kısa ömürlü Bearer belirteci verilir.
- Aynı istemci IP'sinden **art arda 5 hatalı PIN denemesinden sonra 60 saniye kilit** uygulanır
  (HTTP `429` + `Retry-After`). Kilit süresince doğru PIN de reddedilir.
- PIN, `/stream` isteğinde sorgu dizesinde taşınmaz; yalnızca `X-Session-Pin` başlığıyla gönderilir.
  `GET /logs` geriye uyumluluk için PIN'i sorgu dizesinde kabul etmeye devam eder.
- Joker (`*`) CORS başlıkları kaldırılmıştır; istemciler yerel uygulamalardır.
- Aktarım **düz HTTP**'dir (TLS yok). Bu nedenle yayın **güvenilmeyen veya kurumsal Wi-Fi ağlarında
  kullanılmamalıdır**; güvenilen ev ağı varsayılır.
- Uygulama arka plana geçtiğinde kamera ve sunucu kapatılır; arka plandayken uzaktan çekim istekleri
  `403` ile reddedilir.
- **Tanılama erişimi:** `GET /logs` yalnızca **PC Kamera Bağlantısı sayfası açık ve yayın çalışırken**
  erişilebilir. Sayfadan çıkıldığında veya uygulama arka plana geçtiğinde sunucu kapanır ve
  tanılama konsolu bağlantı kuramaz ("hedef makine reddetti"). Bu davranış gizlilik için bilinçlidir;
  sunucunun sayfadan bağımsız çalışması sonraki sürümde değerlendirilecektir.
- **Canlı önizleme:** Kareler SurfaceView yüzeyinden `PixelCopy` ile alınır; her karede tam
  çözünürlüklü fotoğraf çekilmez. Hedef 15 FPS, JPEG kalitesi 70.

## Teknik yapı

- C# / .NET 10 / .NET MAUI Controls 10.0.60
- Android canlı önizleme: CommunityToolkit.Maui.Camera 6.1.0 (MIT), yalnız kamera paketi; gerekli MAUI alt sınırı 10.0.60
- Media/TemporaryPhoto.cs: boyut sınırlama, EXIF yönü ve geçici kopya sahipliği
- Services/PhotoStorageService.cs: telefon depolama alanında güvenli fotoğraf kaydı ve SHA-256 bütünlüğü. Boş alan kontrolü Android'de `Java.IO.File.UsableSpace` ile veri bölümünden ölçülür (`/` kök dizininden değil).
- Services/PreviewFrameRateLimiter.cs: canlı önizleme hattında hedef kare hızını (15 FPS) aşmayı engelleyen, cihazdan bağımsız zamanlayıcı
- Views/PhotoSourcePage.cs, PhotoCameraPage.cs, PhotoViewerPage.cs: gerçek edinme; PhotoMeasurePage.cs mevcut demo olarak ayrı kalır
- Android hedefi: `net10.0-android`
- Windows hedefi: `net10.0-windows10.0.19041.0`
- İnternetten çalışma anında içerik yüklemeyen yerel SVG ikonları
- Açık/koyu tema ve yaklaşık 48 DIP veya daha büyük etkileşim hedefleri
- Görünümlerden ayrılmış demo modelleri, servisler ve görünüm modelleri
- Kullanıcı metinleri `Resources/Strings/AppText.cs` içinde merkezileştirilmiştir

Gerçek proje dosyası: `OlcuAtolyesi/OlcuAtolyesi.csproj`.

## Gereksinimler

- .NET 10 SDK
- .NET MAUI iş yükü
- Android derlemesi için Android SDK
- Cihazda deneme için USB hata ayıklaması etkin, ADB tarafından yetkilendirilmiş Android telefon veya uygun emülatör
- Windows hedefi için Windows üzerinde MAUI geliştirme bileşenleri

## Derleme ve çalıştırma

Komutları depo kökünde çalıştırın:

```powershell
dotnet restore .\OlcuAtolyesi\OlcuAtolyesi.csproj
dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -f net10.0-android -p:TargetFrameworks=net10.0-android -r android-arm64
```

Windows ortamı uygunsa:

```powershell
dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0 -r win-x64
```

Otomatik mantık ve matematik kontrollerini çalıştırmak için:

```powershell
dotnet run --project .\Tests\LogicChecks\LogicChecks.csproj
```

Yetkilendirilmiş Android cihazı kontrol etmek ve uygulamayı çalıştırmayı denemek için:

```powershell
adb devices
dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -t:Run -f net10.0-android
```

`adb devices` altında cihaz `device` durumunda görünmüyorsa kurulum/çalıştırma yapılmaz. Uygulama kimliği mevcut yapılandırmadan korunur.

## v0.2.0 telefonda kabul kontrolü

1. Ana sayfa veya Araçlar'dan Fotoğraftan ölç'ü açın; üç kaynak seçeneğini kontrol edin.
2. Kameraya ilk girişte izin istemini kontrol edin. Kamera dışındaki sayfalarda istem açılmamalı.
3. Kişisel bilgi içermeyen bir test yüzeyini çekin; Tekrar çek, Fotoğrafı kullan ve İptal/Android geri akışlarını deneyin.
4. Uygulamayı kamera açıkken arka plana gönderip geri dönün. Hızlı tekrar dokunmalarda ikinci sayfa/ikinci çekim oluşmamalı.
5. Sistem seçicisinden yalnız kendi seçtiğiniz veya sentetik bir görseli açın; iptal ederek de dönün. Fotoğrafta demo işaretleri olmamalı.
6. Dikey/yatay fotoğrafları, EXIF yönünü ve büyük bir sentetik görseli deneyin. Sığdır, zoom ve pan en-boy oranını değiştirmemeli.
7. İzni reddetme ve kalıcı ret yalnız kullanıcı kararıyla denenir; tekrar girişte kendiliğinden istem tekrarlanmamalı.
8. Gerçek ölçüm yapılmadığını, kaynak dosyanın korunduğunu ve demo akışının ayrı kaldığını kontrol edin.

## v0.1.0 demo kontrolleri (korunmuştur)

1. Uygulamanın dört ana sekmeyle açıldığını; alt sekme metinleri ve ikonlarının okunabildiğini doğrulayın.
2. Ana sayfadaki ilk kullanım kartını kapatın; Fotoğraftan ölç, Ekran cetveli ve Kamerayla ölç girişlerini deneyin.
3. Cetvel kılavuzlarını sürükleyin. Aynı demo uzunluğunun mm/cm/inç değerlerinin birbiriyle tutarlı olduğunu kontrol edin; kilitleme ve sıfırlamayı deneyin.
4. Kalibrasyon panelinde boş, `0` ve negatif değerin reddedildiğini; Türkçe ondalık virgüllü pozitif değerin kabul edildiğini kontrol edin.
5. Fotoğraf demosunda referansı uygulayın, iki noktayı taşıyın, görseli yakınlaştırıp kaydırın; noktaların ve etiketlerin plakayla hizalı kaldığını kontrol edin. Geri al, sıfırla ve Projeye ekle eylemlerini deneyin.
6. Kamera tanıtımında gerçek kamera açılmadığını ve herhangi bir izin istemi çıkmadığını doğrulayın.
7. Demo proje oluşturun, yeniden adlandırın, ayrıntısını açın ve silme işleminin onay istediğini kontrol edin.
8. Açık, koyu ve sistem temasını değiştirin; uygulamayı yeniden açınca seçimin korunduğunu doğrulayın. Varsayılan birimi değiştirip ölçüm ekranlarında yansımasını kontrol edin.
9. Android geri düğmesini ayrıntı ve yardım ekranlarında deneyin. Dar ekran, yatay yön ve büyük sistem yazı boyutunda temel eylemlerin erişilebilir kaldığını inceleyin.
10. Ekran okuyucu kullanabiliyorsanız sekme, araç, yardım ve eylem düğmelerinin anlamlı etiketlerini ayrıca doğrulayın; bu kontrol otomatik test yerine geçmez.

## Üçüncü taraf bildirimleri

Font ve Microsoft paket bildirimleri [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) dosyasındadır. Proje için hazırlanan SVG ikonlar özgündür; görevde listelenen GitHub projelerinden kod veya varlık alınmamıştır.



### Android geliştirme dağıtımı notu

MAUI sürümü değiştiğinde yalnız APK'yı ADB ile kurmak telefondaki eski Fast Deployment assembly dosyalarını güncellemez. Geliştirme kurulumu için veri/izinleri silmeden SDK dağıtım hedefini kullanın:

    dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -t:Install -f net10.0-android -p:TargetFrameworks=net10.0-android -r android-arm64 "-p:AdbTarget=-s CIHAZ_SERI_NO"

Bu işlem yalnız belirtilen uygulamayı günceller; uygulama kimliği korunur. Test amacıyla izinleri sıfırlama, uygulama verisini temizleme veya başka uygulamaların ayarlarını değiştirme yapılmamalıdır.
