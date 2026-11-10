namespace OlcuAtolyesi.Resources.Strings;

/// <summary>
/// v0.2.0 boyunca kullanıcıya gösterilen Türkçe metinlerin merkezi kaynağı.
/// İleride .resx yerelleştirmesine taşınabilecek şekilde görünüm kodundan ayrıdır.
/// </summary>
public static class AppText
{
    public const string AppName = "Ölçü Atölyesi";
    public const string Version = "0.6.2 — Akıcı Canlı Önizleme ve Düşük Gecikmeli Kare Aktarımı";
    public const string Subtitle = "Fotoğraftan Ölçüm • Kamera Kalibrasyonu • Hassas Geometrik Ölçüm";
    public const string DemoWarning = "DEMO — Gerçek ölçüm değildir";
    public const string SavedBadge = "Kalıcı kayıt";
    public const string PrototypeNote = "Android'de gerçek fotoğraf üzerinde R1–R2 referanslı M1–M2 mesafe ölçümü, kalıcı proje kaydı ve Wi-Fi üzerinden PC kamera yayını yapılabilir.";
    public const string Help = "Yardım";
    public const string HelpButton = "?";
    public const string Back = "Geri";
    public const string Close = "Kapat";
    public const string Cancel = "Vazgeç";
    public const string Confirm = "Onayla";
    public const string Reset = "Sıfırla";
    public const string Undo = "Geri al";
    public const string Demo = "Demo";
    public const string Planned = "Planlanıyor";

    public static class Tabs
    {
        public const string Home = "Ana Sayfa";
        public const string Tools = "Araçlar";
        public const string Projects = "Projeler";
        public const string Settings = "Ayarlar";
    }

    public static class Home
    {
        public const string Intro = "Günlük ölçüm işlerin için anlaşılır, tek yerde toplanmış araçlar.";
        public const string PhotoAction = "Fotoğraftan ölç";
        public const string RulerAction = "Ekran cetveli";
        public const string CameraAction = "Kamerayla ölç";
        public const string QuickAccess = "Hızlı erişim";
        public const string RecentProjects = "Son projeler";
        public const string NoRecentProjects = "Henüz kayıtlı proje yok.";
        public const string OnboardingTitle = "İlk adım";
        public const string OnboardingBody = "Bir araç seç, fotoğraf üzerinde referanslı ölçüm yap veya demo araçlarını dene. Kaydettiğin projeler cihazında kalıcı olarak saklanır.";
        public const string DismissOnboarding = "Anladım, kapat";
        public const string PrototypeLabel = "Uygulama bilgisi";
    }

    public static class Tools
    {
        public const string ScreenGroup = "Ekran araçları";
        public const string PhotoGroup = "Fotoğraf araçları";
        public const string LiveGroup = "Canlı araçlar";
        public const string Ruler = "Cetvel";
        public const string RulerDescription = "İki kılavuz çizgisi arasındaki demo uzunluğu karşılaştırır.";
        public const string Protractor = "Açıölçer";
        public const string ProtractorDescription = "Ekran üzerinde iki yön arasındaki açıyı inceleyecek.";
        public const string PhotoLength = "Uzunluk";
        public const string PhotoLengthDescription = "Referans verilen demo görselinde iki nokta arasını hesaplar.";
        public const string PhotoAngle = "Açı";
        public const string PhotoAngleDescription = "Fotoğraftaki üç nokta arasındaki açıyı inceleyecek.";
        public const string Diameter = "Çap / Yarıçap";
        public const string DiameterDescription = "Dairesel parçaların çap ve yarıçapını belirleyecek.";
        public const string PerimeterArea = "Çevre / Alan";
        public const string PerimeterAreaDescription = "Kapalı bir şeklin çevre ve alanını hesaplayacak.";
        public const string CameraDistance = "Kamerayla mesafe";
        public const string CameraDistanceDescription = "Canlı ölçüm yaklaşımını çizilmiş bir sahnede tanıtır.";
        public const string Height = "Yükseklik";
        public const string HeightDescription = "Bir nesnenin taban ve üst noktasıyla yüksekliğini ölçecek.";
        public const string Level = "Su terazisi";
        public const string LevelDescription = "Cihaz eğimini yatay ve dikey eksenlerde gösterecek.";
        public const string PlannedHint = "Bu araç henüz etkileşimli değildir.";
    }

    public static class Ruler
    {
        public const string Title = "Ekran cetveli";
        public const string Intro = "Mavi kılavuzları sürükleyerek örnek aralığı değiştir.";
        public const string Measurement = "Çizgiler arası demo mesafe";
        public const string Unit = "Birim";
        public const string Lock = "Kılavuzları kilitle";
        public const string Unlock = "Kilidi aç";
        public const string Locked = "Kılavuzlar kilitli. Değiştirmek için kilidi aç.";
        public const string Unlocked = "Kılavuzlar sürüklenebilir.";
        public const string Calibration = "Kalibrasyon";
        public const string CalibrationTitle = "Kalibrasyon eşleştirmesi — Demo";
        public const string CalibrationIntro = "Bilinen bir uzunluk girip görsel kılavuzu eşleştirebilirsin.";
        public const string KnownLength = "Bilinen uzunluk";
        public const string VisualMatch = "Görsel eşleştirme";
        public const string Validate = "Referansı doğrula";
        public const string CalibrationCaution = "Bu panel yalnız etkileşim demosudur; ekranı fiziksel olarak kalibre etmez ve doğruluk sağlamaz.";
        public const string CalibrationAccepted = "Demo eşleştirme tamamlandı. Fiziksel ölçek değişmedi.";
        public const string InvalidReference = "Sıfırdan büyük bir uzunluk gir. Ondalık için virgül kullanabilirsin.";
        public const string TickNote = "Küçük çizgiler yalnız görsel kılavuzdur; fiziksel hassasiyet göstergesi değildir.";
    }

    public static class Photo
    {
        public const string Title = "Fotoğraftan ölçüm";
        public const string Intro = "Uygulamaya ait çizilmiş bağlantı plakası üzerinde ölçüm akışını dene.";
        public const string RealPhotoHeader = "GERÇEK FOTOĞRAF — Referanslı Ölçüm";
        public const string RealPhotoIntro = "Aynı düzlemdeki bilinen bir nesnenin (cetvel, pul veya madeni para çapı) iki ucuna R1–R2 noktalarını yerleştirip uzunluğunu gir, ardından M1–M2 ile mesafeyi ölç.";
        public const string DemoImage = "Demo görseli";
        public const string NoCamera = "Gerçek kamera veya galeri kullanılmaz.";
        public const string StepReference = "1  Referans belirle";
        public const string StepMeasure = "2  Ölç";
        public const string StepReview = "3  Sonucu incele";
        public const string ReferenceLength = "Referans uzunluğu";
        public const string ReferenceHint = "R1 ve R2 noktalarını referans nesnenin iki ucuna sürükle veya Referans belirle düğmesini kullan.";
        public const string ApplyReference = "Referansı uygula";
        public const string ReferenceApplied = "Referans uygulandı. Şimdi M1–M2 ölçüm noktalarını seç veya sürükle.";
        public const string MeasurementResult = "Hesaplanan demo uzunluk";
        public const string RealMeasurementResult = "Hesaplanan uzunluk";
        public const string InteractionHint = "R1–R2 (referans) ve M1–M2 (ölçüm) noktalarını sürükle. Boş alanda kaydır; iki parmakla yakınlaştır.";
        public const string SelectReference = "Referans belirle";
        public const string SelectMeasure = "Mesafe ölç";
        public const string SelectR1 = "R1 seç";
        public const string SelectR2 = "R2 seç";
        public const string SelectM1 = "M1 seç";
        public const string SelectM2 = "M2 seç";
        public const string AddToProject = "Projeyi kaydet";
        public const string SaveChanges = "Değişiklikleri kaydet";
        public const string AddedToSession = "Proje ve ölçüm noktaları cihazda kalıcı olarak kaydedildi.";
        public const string UpdatedProject = "Proje ölçüm ve referans verileri güncellendi.";
        public const string SaveFailed = "Proje kaydedilirken bir hata oluştu. Önceki sağlam veriler korundu.";
        public const string SavedPhotoMissing = "Kayıtlı fotoğraf dosyasına ulaşılamadı; ölçüm koordinatları referans düzleminde açıldı.";
        public const string NeedReference = "Önce sıfırdan büyük bir referans uzunluğu gir.";
        public const string PrecisionSelection = "Hassas seçim";
        public const string Zoom = "Yakınlaştırma";
        public const string ResetView = "Görünümü sığdır";
    }

    public static class Camera
    {
        public const string Title = "Kamerayla ölçüm";
        public const string Intro = "Çizilmiş sahnede gelecekteki canlı ölçüm akışını incele.";
        public const string Notice = "Bu tanıtım ekranında kamera açılmaz. Gerçek fotoğraf çekmek için Fotoğraftan ölç girişini kullan. AR ölçümü henüz uygulanmadı.";
        public const string StepOne = "1. Başlangıç noktasını hedefle.";
        public const string StepTwo = "2. Bitiş noktasına ilerle.";
        public const string StepThree = "3. Sonucu ve yüzeyi birlikte kontrol et.";
        public const string ExampleResult = "Örnek demo sonuç";
        public const string SceneDescription = "Canlı görüntü olmayan, çizilmiş oda sahnesi";
    }

    public static class Projects
    {
        public const string StorageTitle = "Kalıcı proje depolama";
        public const string SessionNotice = "Projeler, fotoğraflar ve R1–R2 / M1–M2 ölçüm noktaları cihazda kalıcı olarak saklanır.";
        public const string NewProject = "Yeni proje";
        public const string EmptyTitle = "Kayıtlı proje bulunamadı";
        public const string EmptyBody = "Yeni proje oluşturabilir veya Fotoğraftan ölç ekranında yaptığın ölçümleri kaydedebilirsin.";
        public const string ProjectNamePrompt = "Proje adı";
        public const string ProjectNamePlaceholder = "Örn. Raf ölçüsü";
        public const string OpenAndEdit = "Ölçümü aç / düzenle";
        public const string Rename = "Yeniden adlandır";
        public const string Delete = "Sil";
        public const string DeleteTitle = "Proje silinsin mi?";
        public const string DeleteMessage = "Bu işlem yalnızca bu projeye ait kayıt ve fotoğraf verilerini cihazdan kalıcı olarak kaldırır.";
        public const string Details = "Ayrıntıları aç";
        public const string Measurements = "Kayıtlı ölçüler";
        public const string ReferenceInfo = "Referans ölçeği (R1–R2)";
        public const string ExportTitle = "Dışa aktarma";
        public const string Dxf = "DXF oluştur";
        public const string Stl = "STL oluştur";
        public const string OpenOnComputer = "Bilgisayarda aç";
        public const string ExportPlanned = "DXF, STL ve bilgisayar aktarımı planlanıyor; bu sürümde dosya veya bağlantı oluşturulmaz.";
        public const string CreatedForSession = "Yeni proje oluşturuldu ve kaydedildi.";
    }

    public static class Settings
    {
        public const string Appearance = "Görünüm";
        public const string Theme = "Tema";
        public const string Light = "Açık";
        public const string Dark = "Koyu";
        public const string System = "Sistem";
        public const string Measurement = "Ölçüm";
        public const string DefaultUnit = "Varsayılan uzunluk birimi";
        public const string Calibration = "Kalibrasyon durumu";
        public const string CalibrationStatus = "Gerçek kalibrasyon henüz uygulanmadı.";
        public const string Information = "Bilgi";
        public const string About = "Hakkında";
        public const string VersionLabel = "Sürüm";
        public const string PreferenceSaved = "Tercih bu cihazda saklandı.";
    }

    public static class About
    {
        public const string Title = "Hakkında";
        public const string Body = "Ölçü Atölyesi, farklı ölçüm akışlarını sade bir mobil araç kutusunda birleştirmeyi amaçlar.";
        public const string Scope = "v0.6.2 Android kamera ve galeri fotoğrafları üzerinde R1–R2 referanslı M1–M2 mesafe ölçümü, cihaz içi kalıcı proje kayıt sistemi, Wi-Fi üzerinden PC Kamera Bağlantısı servisi, yüksek çözünürlüklü fotoğraf çekimi ve kayıpsız dosya aktarımı sunar. PIN denemeleri sınırlandırılır ve art arda hatalı denemelerde oturum kilitlenir. Sensör/AR ölçümü, ekran kalibrasyonu ve bulut aktarımı etkin değildir.";
        public const string Privacy = "Bu prototip üyelik, reklam, analiz veya telemetri kullanmaz.";
    }

    public static class HelpContent
    {
        public const string What = "Ne işe yarar?";
        public const string How = "Nasıl kullanılır?";
        public const string Works = "Bu prototipte neler çalışır?";
        public const string Conditions = "Ölçümü etkileyebilecek koşullar nelerdir?";
        public const string GeneralWhat = "Araçların amacını, demo sınırlarını ve temel kullanım adımlarını açıklar.";
        public const string GeneralHow = "Araçlar sekmesinden bir araç seç; ekrandaki yönergeleri sırayla izle.";
        public const string GeneralWorks = "Android fotoğraf edinme/görüntüleme, referanslı fotoğraf ölçümü, kalıcı proje kaydı, gezinme, tema, birim seçimi ve demo etkileşimleri çalışır.";
        public const string GeneralConditions = "Demo araçlar gerçek ölçüm yapmadığı için fiziksel kararlar için kullanılmamalıdır.";
        public const string RulerWhat = "Ekrandaki iki kılavuz arasındaki örnek uzunluğu farklı birimlerde karşılaştırır.";
        public const string RulerHow = "Kılavuzları sürükle, birim seç, gerekirse kilitle veya sıfırla.";
        public const string RulerWorks = "Sürükleme, kilit, sıfırlama, birim dönüşümü ve kalibrasyon paneli çalışır.";
        public const string RulerConditions = "Ekran yoğunluğu, koruyucu cam ve gerçek kalibrasyon fiziksel sonucu etkiler; bu sürüm bunları ölçmez.";
        public const string PhotoWhat = "Gerçek fotoğraflar ve demo plaka üzerinde R1–R2 referanslı M1–M2 mesafe ölçümü ve kalıcı proje kaydı sunar.";
        public const string PhotoHow = PhotoText.Help;
        public const string PhotoWorks = "Android canlı arka kamera önizlemesi, çekim/onay/tekrar çekim, galeriden fotoğraf seçimi, R1–R2 manuel referans belirleme, gerçek referans uzunluğu/birimi tanımlama, M1–M2 mesafe ölçümü ve projeyi kalıcı olarak kaydedip yeniden düzenleme çalışır.";
        public const string PhotoConditions = "Referans ile ölçülen yüzey aynı düzlemde olmalıdır. Perspektif, lens bozulması ve görünmeyen 3B geometri sonucu etkiler. Referanssız tek fotoğraftan kesin gerçek uzunluk çıkarılmaz.";
        public const string CameraWhat = "Gelecekteki kamera destekli mesafe ölçümünün adımlarını tanıtır.";
        public const string CameraHow = "Çizilmiş sahnedeki başlangıç ve bitiş noktalarını, ardından demo sonucu incele.";
        public const string CameraWorks = "Tanıtım sahnesi ve adım anlatımı çalışır; kamera veya sensör açılmaz.";
        public const string CameraConditions = "Gerçek sürümde yüzey, ışık, hareket ve cihaz desteği etkili olacaktır; bu prototip uyumluluk iddiasında bulunmaz.";
    }

    public static class PcCamera
    {
        public const string Title = "PC Kamera Bağlantısı";
        public const string Action = "PC'ye Canlı Kamera";
        public const string Description = "Telefon kamerasını Wi-Fi üzerinden Ölçü Atölyesi PC uygulamasına canlı aktarır.";
        public const string ServerRunning = "Yayın etkin";
        public const string ServerStopped = "Yayın durduruldu";
        public const string StartServer = "Yayını Başlat";
        public const string StopServer = "Yayını Durdur";
        public const string LocalIpLabel = "Telefon Yerel IP Adresi:";
        public const string PortLabel = "Port:";
        public const string PinLabel = "Eşleştirme Kodu (PIN):";
        public const string NewPin = "PIN Yenile";
        public const string ActiveClients = "Bağlı PC:";
        public const string NoClients = "Henüz istemci bağlanmadı";
        public const string CameraPosition = "Kamera Sensörü:";
        public const string Front = "Ön Kamera";
        public const string Back = "Arka Kamera";
        public const string Instruction = "PC uygulamasında 'Kamera ve Kalibrasyon' sayfasına gidin, 'Wi-Fi' modunu seçin ve yukarıdaki adresi ile PIN kodunu girerek bağlanın.";
        public const string SecurityNotice = "Yayın yalnızca yerel ağınızda (Wi-Fi) çalışır. PIN kodu olmadan hiçbir bilgisayar kameraya erişemez.";
    }
}


