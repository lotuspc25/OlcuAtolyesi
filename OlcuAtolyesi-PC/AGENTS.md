# Ölçü Atölyesi PC — Proje ve Ajan Kuralları (AGENTS.md)

Bu dosya, **Ölçü Atölyesi PC** (`D:\proje\OlcuAtolyesi-PC`) bağımsız Windows masaüstü çalışma alanında çalışan tüm yapay zeka ajanlarının uyması gereken kalıcı mimari, geliştirme, doğrulama, açık kaynak araştırma ve raporlama kurallarını tanımlar.

---

## 1. Dil ve İletişim Kuralları

- Bütün açıklamalar, kullanıcı arayüzü metinleri, dokümantasyon ve sonuç raporları **tamamen Türkçe** olmalıdır.
- Doğrulanmamış tahminler ile kaynak kod/test kanıtına dayalı bulgular birbirinden açıkça ayrılmalıdır.
- Gerçekleştirilmemiş derleme veya testler kesinlikle başarılı gösterilmemelidir.

---

## 2. Mimari ve Kod Koruma Kuralları

- **Mobil Projeden İzolasyon:** `D:\proje\OlcuAtolyesi` klasöründeki mobil projeye hiçbir dosya yazılmamalı, kaynak kodları değiştirilmemeli, taşınmamalı veya silinmemelidir.
- **MVVM ve Ortak Çekirdek Ayrımı:**
  - Görünümden bağımsız geometri, birim dönüşümü (`mm`, `cm`, `m`, `inç`), ölçüm matematiği ve veri modelleri `src/OlcuAtolyesi.Core` (`net10.0`) projesinde tutulmalıdır.
  - WPF kullanıcı arayüzü ve görünüm modelleri `src/OlcuAtolyesi.PC` (`net10.0-windows`) içinde MVVM mimarisiyle yapılandırılmalıdır.
- **Açık Kaynak Araştırma ve Entegrasyon Kuralı:** Yeni özellik geliştirirken önce ilgili güvenilir açık kaynak projeleri araştır (`OpenCV`, `OpenScan`, `FreeCAD`, `Open CASCADE / OCCT`, `Meshroom` / `AliceVision`, `ezdxf`, `OrcaSlicer`, `PrusaSlicer`). Mevcut ve uygun çözümleri değerlendir. Lisans uyumluluğu, güvenlik, bakım ve performans kontrollerini yap. Uygun olduğunda yeniden kullanılabilir teknolojilerden yararlan; gereksiz yere her şeyi sıfırdan yazma.
  - **Lisans Sınırı:** `GPL-3.0` (`OpenScan`) ve `AGPL-3.0` (`OrcaSlicer`, `PrusaSlicer`) lisanslı projelerden doğrudan kaynak kod kopyalanmamalı ve süreç içi (in-process) kütüphane bağlanmamalıdır; yalnızca ayrı süreç (`CLI`) veya ağ protokolü (`REST`/`WebSocket`) üzerinden haberleşilmelidir.
- **Git Geri Dönüş Noktası:** Kapsamlı kod değişikliklerine başlamadan önce Git durumu kontrol edilmeli ve güvenli geri dönüş noktası korunmalıdır.

---

## 3. Derleme ve Otomatik Test Kuralları

Her kod değişikliğinden sonra aşağıdaki kontroller çalıştırılmalıdır:

1. **Windows Debug Derlemesi:**
   ```powershell
   dotnet build D:\proje\OlcuAtolyesi-PC\OlcuAtolyesi.PC.sln -c Debug
   ```
2. **Windows Release Derlemesi:**
   ```powershell
   dotnet build D:\proje\OlcuAtolyesi-PC\OlcuAtolyesi.PC.sln -c Release
   ```
3. **Otomatik Başlangıç, Navigasyon ve Geometri Testleri:**
   ```powershell
   dotnet run --project D:\proje\OlcuAtolyesi-PC\tests\OlcuAtolyesi.PC.Tests\OlcuAtolyesi.PC.Tests.csproj -c Debug
   ```
4. **Gerçek Android Cihaz Sınırı (Kalıcı):**
   - Gerçek Android cihazda uygulama yükleme, başlatma, ADB üzerinden etkileşim veya otomatik cihaz testleri yapılmayacaktır.
   - Ajanların sorumluluğu kod geliştirme, Debug/Release derleme ve cihaz gerektirmeyen otomatik testlerle sınırlıdır.

---

## 4. Her Geliştirme Görevinden Sonra Uygulanacak Kalıcı Çalışma Akışı (PC Sürümleri)

Gelecekteki bütün PC sürümlerinde her geliştirme görevinden sonra aşağıdaki adımlar sırasıyla uygulanır:

1. Kod değişikliklerini tamamla.
2. Windows Debug ve Release derlemelerini gerçekleştir.
3. Birim, mantık ve diğer arayüz gerektirmeyen otomatik testleri çalıştır.
4. Hata varsa düzelt ve testleri yeniden çalıştır.
5. Ayrıntılı Türkçe sonuç raporunu `Docs/Reports` klasörüne kaydet.
6. İşlemler tamamlandığında Windows uygulamasını yalnızca başlat ve açık bırak.
7. Programın arayüzünde otomatik tıklama, menüler arasında gezinme, fare/klavye simülasyonu veya etkileşimli GUI testleri **yapma**.
8. Tüm manuel ve görsel kullanım testlerini kullanıcı ChatGPT ile birlikte yapacaktır.
9. Sohbet ekranına yalnızca kısa sonuç özetini yaz.

---

## 5. Zorunlu Codex Benzeri Raporlama Düzeni

1. **Dosyaya Kayıt:** Geliştirme tamamlandığında ayrıntılı sonuç raporu `D:\proje\OlcuAtolyesi-PC\Docs\Reports\` dizinine sürüm numarasıyla kaydedilir (ör. `Docs/Reports/v0.1.0_Sonuc_Raporu.md`).
2. **Rapor İçeriği:** Değiştirilen/eklenen dosyalar, yapılan geliştirmeler, Debug/Release derleme sonuçları, otomatik test sonuçları, bilinen eksikler ve sonraki sürüm önerilerini içerir.
3. **Kısa Sohbet Özeti:** Sohbet ekranına uzun rapor tekrar yazılmaz; yalnızca tamamlanan sürüm, derleme/test durumu, önemli eksikler ve rapor dosyasının yolu kısaca gösterilir.
