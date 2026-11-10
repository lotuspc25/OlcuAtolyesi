# Ölçü Atölyesi — Proje ve Ajan Kuralları (AGENTS.md)

Bu dosya, **Ölçü Atölyesi** (`D:\proje\OlcuAtolyesi`) çalışma alanında çalışan tüm yapay zeka ajanlarının her görevde otomatik olarak uyması gereken kalıcı mimari, geliştirme, doğrulama ve raporlama kurallarını tanımlar.

---

## 1. Dil ve İletişim Kuralları

- Bütün açıklamalar, kod içi kullanıcı metinleri (`Resources/Strings/`), dokümantasyon ve sonuç raporları **tamamen Türkçe** olmalıdır.
- Doğrulanmamış tahminler ile kaynak kod/test kanıtına dayalı bulgular birbirinden açıkça ayrılmalıdır.
- Gerçekleştirilmemiş derleme, test veya cihaz doğrulamaları kesinlikle başarılı gösterilmemelidir.

---

## 2. Mimari ve Kod Koruma Kuralları

- **Mevcut Sistemi Koru:** Çalışan kamera (`PhotoCameraPage`), galeri (`PhotoSourcePage`), fotoğraf görüntüleme (`PhotoViewerPage`), fotoğraf ölçümü (`PhotoMeasurePage`) ve demo araçları (`RulerPage`, `CameraMeasurePage`) bozulmamalıdır.
- **Gereksiz Yeniden Yazımdan Kaçın:** Kullanıcı açıkça istemedikçe arayüz yeniden tasarlanmamalı ve mevcut sayfalar sıfırdan yazılmamalıdır.
- **Koordinat ve Matematik Ayrımı:** Ölçüm noktaları (`R1`, `R2`, `M1`, `M2`) her zaman ekran/görünüm koordinatlarında değil, fotoğraf koordinatlarında (`ImagePoint` / `ImageSegment`) saklanmalıdır. Yakınlaştırma ve kaydırma (`ImageViewportTransform`) ölçüm matematiğini değiştirmemelidir.
- **Git Geri Dönüş Noktası:** Kapsamlı kod değişikliklerine başlamadan önce Git durumu kontrol edilmeli ve mevcut değişiklikler silinmeden/ezilmeden güvenli geri dönüş noktası korunmalıdır.
- **Açık Kaynak Araştırma ve Entegrasyon Kuralı:** Yeni özellik geliştirirken önce ilgili güvenilir açık kaynak projeleri araştır. Mevcut ve uygun çözümleri değerlendir. Lisans uyumluluğu, güvenlik, bakım ve performans kontrollerini yap. Uygun olduğunda yeniden kullanılabilir teknolojilerden yararlan; gereksiz yere her şeyi sıfırdan yazma. (Referans belgeler: `Docs/Research/ACIK_KAYNAK_PROJELER.md` ve `Docs/Research/ENTEGRASYON_YOL_HARITASI.md`).

---

## 3. Derleme ve Otomatik Test Kuralları

Her kod değişikliğinden sonra aşağıdaki kontroller çalıştırılmalıdır:

1. **Otomatik Mantık ve Matematik Testleri:**
   ```powershell
   dotnet run --project .\Tests\LogicChecks\LogicChecks.csproj -c Debug
   ```
2. **Windows Derlemesi:**
   ```powershell
   dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -f net10.0-windows10.0.19041.0 -c Debug
   ```
3. **Android Derlemesi ve APK Oluşturma:**
   ```powershell
   dotnet build .\OlcuAtolyesi\OlcuAtolyesi.csproj -f net10.0-android -p:TargetFrameworks=net10.0-android -r android-arm64 -c Debug
   ```
4. **Gerçek Android Cihaz Sınırı ve Sorumluluk Kuralı (Kalıcı):**
   - Gerçek Android cihazda uygulama yükleme, başlatma, ADB üzerinden etkileşim veya otomatik cihaz testleri **yapılmayacaktır**.
   - Ajanların sorumluluğu **kod geliştirme, Windows/Android derleme, APK oluşturma ve cihaz gerektirmeyen otomatik testlerle (`Tests/LogicChecks`)** sınırlıdır.
   - Gerçek cihazdaki tüm kullanım, arayüz ve işlev testleri **kullanıcı ile ChatGPT** tarafından gerçekleştirilecektir.
   - Yalnızca kullanıcının açıkça istediği tek seferlik cihaz işlemleri bu kuralın istisnasıdır.

---

## 4. Zorunlu Codex Benzeri Raporlama Düzeni

Bundan sonraki **bütün geliştirme görevlerinde** aşağıdaki raporlama düzeni otomatik olarak uygulanır:

1. **Dosyaya Kayıt:** Geliştirme tamamlandığında ayrıntılı sonuç raporu proje klasöründeki `Docs/Reports/` dizinine Markdown dosyası olarak kaydedilir.
2. **Adlandırma Standardı:** Rapor dosyası ilgili sürüm numarasıyla adlandırılır (ör. `Docs/Reports/v0.3.0_Sonuc_Raporu.md`, `Docs/Reports/v0.4.0_Sonuc_Raporu.md`).
3. **Rapor İçeriği:** Kaydedilen rapor dosyası şu başlıkları eksiksiz içermelidir:
   - Değiştirilen ve eklenen dosyalar
   - Yapılan geliştirmeler ve düzeltilen hatalar
   - Derleme sonuçları
   - Otomatik test sonuçları
   - Çalıştırma ve cihaz testleri
   - Bilinen eksikler ve riskler
   - Sonraki sürüm önerileri
4. **Kısa Sohbet Özeti (Uzun Raporu Tekrar Yazmama Kuralı):**
   - Sohbet ekranına uzun rapor tekrar yazılmaz.
   - Sohbet yanıtında yalnızca:
     1. Tamamlanan sürüm
     2. Derleme ve test durumu
     3. Önemli eksikler / bilinen sınırlar
     4. Kaydedilen rapor dosyasının yolu (`Docs/Reports/<surum>_Sonuc_Raporu.md`)
     kısaca gösterilir.
