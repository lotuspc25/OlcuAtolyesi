---
description: Ölçü Atölyesi geliştirme, doğrulama ve Codex benzeri raporlama kuralları
alwaysApply: true
---

# Ölçü Atölyesi — Proje Hafızası ve Raporlama Kuralları

1. **Dil:** Tüm raporlar, açıklamalar ve kullanıcı metinleri Türkçe olmalıdır.
2. **Sistem Bütünlüğü:** Mevcut çalışan kamera, galeri, fotoğraf görüntüleme, gerçek fotoğraf ölçümü ve demo araçları korunmalı; kullanıcı istemedikçe arayüz yeniden tasarlanmamalıdır.
3. **Doğruluk:** Gerçekleştirilmemiş derleme, otomatik test veya cihaz testleri kesinlikle başarılı gösterilmemelidir.
4. **Açık Kaynak Araştırma ve Entegrasyon Kuralı:** Yeni özellik geliştirirken önce ilgili güvenilir açık kaynak projeleri araştır. Mevcut ve uygun çözümleri değerlendir. Lisans uyumluluğu, güvenlik, bakım ve performans kontrollerini yap. Uygun olduğunda yeniden kullanılabilir teknolojilerden yararlan; gereksiz yere her şeyi sıfırdan yazma. (`Docs/Research/ACIK_KAYNAK_PROJELER.md` ve `Docs/Research/ENTEGRASYON_YOL_HARITASI.md` belgelerini dikkate al).
5. **Gerçek Android Cihaz Sınırı:** Gerçek Android cihazda uygulama yükleme, başlatma, ADB üzerinden etkileşim veya otomatik cihaz testleri yapılmayacaktır. Ajanların sorumluluğu kod geliştirme, Windows/Android derleme, APK oluşturma ve cihaz gerektirmeyen otomatik testlerle sınırlıdır. Gerçek cihazdaki tüm kullanım, arayüz ve işlev testleri kullanıcı ile ChatGPT tarafından gerçekleştirilecektir. Yalnızca kullanıcının açıkça istediği tek seferlik cihaz işlemleri istisnadır.
6. **Codex Benzeri Raporlama Düzeni (Zorunlu):**
   - Her geliştirme görevi tamamlandığında ayrıntılı sonuç raporu `Docs/Reports/` dizinine sürüm numarasıyla kaydedilir (ör. `Docs/Reports/v0.3.0_Sonuc_Raporu.md`).
   - Rapor dosyası; değiştirilen dosyaları, yapılan geliştirmeleri, derleme sonuçlarını, testleri, hataları, eksikleri ve sonraki önerileri içerir.
   - **Sohbet ekranına uzun rapor tekrar yazılmaz.** Sohbet yanıtında yalnızca tamamlanan sürüm, derleme/test durumu, önemli eksikler ve kaydedilen rapor dosyasının yolu kısaca gösterilir.
