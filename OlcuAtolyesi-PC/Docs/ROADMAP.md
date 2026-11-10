# Ölçü Atölyesi PC — Yol Haritası (ROADMAP.md)

- **Hazırlandığı sürüm:** v0.5.0.6
- **Tarih:** 2026-11-10
- **Durum:** Yürürlükte — v1.0 kapsamı kullanıcı tarafından onaylandı.

---

## 1. Amaç

Bu belge, Ölçü Atölyesi PC projesinin **"tamamlandı" tanımını** ve o noktaya giden sıralı
sürüm planını tanımlar. Belgenin amacı kapsam kaymasını önlemektir: bir iş bu belgede yazmıyorsa
v1.0 kapsamında değildir.

---

## 2. "Tamamlandı" tanımı — v1.0.0

Proje **v1.0.0** sürümüne, aşağıdaki üç yetenek uçtan uca birlikte çalıştığında ulaşır:

1. **Zemin kalibrasyonu ve perspektif düzeltme:** Kullanıcı fotoğrafta düzlemin dört köşesini
   işaretleyerek homografi tanımlar; ölçümler bu düzleme göre düzeltilir.
2. **2B DXF ihracı:** Ölçülen noktalar ve referans, CAD yazılımlarında açılabilen DXF dosyası
   olarak dışa aktarılır.
3. **Uçtan uca akış:** Fotoğraf/kamera → referans (R1–R2) → ölçüm (M1–M2) → kalibrasyon →
   DXF çıktısı. Bu akışın her adımı en az bir otomatik testle korunur.

### v1.0.0 kabul kriterleri

| # | Kriter | Ölçüm yöntemi |
|---|---|---|
| 1 | Kalibrasyon sonrası ölçüm hatası düşer | Sentetik görüntüde bilinen uzunluk ile otomatik test; sapma **<%2** |
| 2 | DXF dosyası geçerli ve açılabilir | ASCII DXF R12 uyumu; `HEADER`/`TABLES`/`ENTITIES` bölümleri; `$INSUNITS` = 4 (mm) |
| 3 | DXF katman yapısı | `REFERENCE` (R1–R2), `MEASURE` (M1–M2), `CALIBRATION` (dört köşe) katmanları ayrı |
| 4 | Birim tutarlılığı | mm / cm / m / inç seçimleri DXF'e doğru birimle yansır |
| 5 | Regresyon | Mevcut 38 otomatik kontrol bozulmadan geçer; toplam **≥ 45** kontrol |
| 6 | Derleme | Debug ve Release: **0 hata, 0 uyarı** |
| 7 | Dokümantasyon | README, PROJECT_STATUS, CHANGELOG ve sürüm raporu güncel |

---

## 3. Fazlar

| Sürüm | Kapsam | Bağımlılık | Durum |
|---|---|---|---|
| **v0.6.0** | **2B DXF ihracı** (bağımlılıksız, kendi ASCII yazıcısı) | Yok | **Tamamlandı** |
| **v0.7.0** | **Zemin kalibrasyonu + perspektif düzeltme** | Görüntü işleme kararı (bkz. §5) | Sırada |
| **v0.8.0** | Sağlamlaştırma: hata yönetimi, UX tutarlılığı, performans | — | Planlı |
| **v1.0.0** | Sürüm kilidi: kurulum, sürüm notları, kullanım kılavuzu | — | Planlı |

### Sıra gerekçesi

- **DXF önce:** Yeni bağımlılık gerektirmez, saf geometri ve yazıcı işidir; mevcut test altyapısına
  hemen oturur ve ürünün ilk somut çıktısını üretir (ölçüm → CAD).
- **Kalibrasyon ikinci:** DXF'in *doğru* olmasını sağlayan katmandır. Nokta koordinatlarını
  değiştirmez; ayrı bir dönüşüm katmanı olarak eklenir (`AGENTS.md` §2 koordinat kuralı).
- **Kontur çıkarma sonrası:** Kalibrasyon olmadan kontur çıkarmanın ölçüm doğruluğuna katkısı
  sınırlıdır; v1.0 kapsamı dışındadır.

---

## 4. Kapsam dışı (v1.0)

Aşağıdakiler **v1.0.0'a dahil değildir** ve ancak v1.0 sonrasında ele alınır:

- Otomatik kontur / delik çıkarma (OpenCV)
- 3B STEP ve STL üretimi (OCCT / FreeCAD)
- 3B baskı entegrasyonu (OrcaSlicer / PrusaSlicer / Moonraker / OctoPrint)
- Wi-Fi ve Bluetooth telefon bağlantısının tamamlanması (USB/ADB yeterli kabul edilir)
- Bulut, telemetri, üyelik, reklam
- Çoklu kullanıcı / paylaşım

---

## 5. Karar bekleyen teknik noktalar

| Karar | Seçenekler | Değerlendirme ölçütü |
|---|---|---|
| Görüntü işleme (v0.7.0) | OpenCvSharp (Apache-2.0) / saf C# homografi | Bağımlılık boyutu, bakım, lisans, dağıtım kolaylığı |
| DXF yazıcı (v0.6.0) | Kendi ASCII yazıcısı / netDxf (MIT) | v0.6.0 için **kendi yazıcı** tercih edilir; bağımlılık eklenmez |
| 3B üretim (v1.0 sonrası) | OCCT (LGPL) / FreeCAD CLI / kendi mesh | Lisans uyumu (`AGENTS.md` §2 lisans sınırı) |

Her karar, alındığı sürümün sonuç raporunda **gerekçesiyle** kayda geçirilir.

---

## 6. Korunacak ilkeler (her sürümde geçerli)

- Ölçüm noktaları (`R1`, `R2`, `M1`, `M2`) **görüntü koordinatlarında** saklanır; yakınlaştırma,
  kaydırma ve kalibrasyon bu koordinatları değiştirmez.
- Geometri ve ölçüm matematiği `OlcuAtolyesi.Core` içinde kalır; WPF'e sızmaz.
- Mobil projeye (`D:\proje\OlcuAtolyesi`) hiçbir dosya yazılmaz (`AGENTS.md` §2).
- Gerçek cihazda test yapılmaz; cihaz testleri kullanıcıya aittir (`AGENTS.md` §3.4).
- Her sürüm sonunda `Docs/Reports/` altına Türkçe sonuç raporu yazılır.

---

## 7. Uzun vadeli ürün haritası (v1.0 sonrası dahil)

Bu bölüm, ürünün uzun vadeli yönünü kayda geçirir. **v1.0 kapsamı değişmez** (bkz. §2); buradaki
maddeler v1.0 sonrası sürümlere aittir ve bağlayıcı bir taahhüt değil, yön belgesidir.

### 7.1. Yerleşim ilkesi

Uygulama, SolidWorks benzeri iki katmanlı bir yerleşim kullanır:

| Katman | Görev | Örnek |
|---|---|---|
| **Üst sekmeler** | İş istasyonları (iş akışına göre gruplanmış komutlar) | Görüntü, Ölçüm, Çizim, 3B, CAM |
| **Sağ dikey şerit** | Her sekmede ortak yardımcı araçlar | Yakınlaştır, ızgara, snap, katman, büyüteç |
| **Alt durum çubuğu** | Okuma bilgisi | Sonuç, ölçek, adım rehberi |

**Kural:** Bir komut "ne yapıyorum" sorusuna cevapsa üst sekmeye, "nasıl görüyorum / nasıl yardımcı
olur" sorusuna cevapsa sağ şeride konur. Aynı komut iki yerde bulunmaz.

### 7.2. Sekme haritası

| # | Sekme | Kapsam | Hedef sürüm | Durum |
|---|---|---|---|---|
| 1 | **Görüntü** | Parlaklık, kontrast, keskinlik, gri ton, negatif, kenar vurgusu, ızgara, nesne/ekran cetveli, döndürme, aynalama, kırpma | v0.6.5 → v0.7.0 | Sırada |
| 2 | **Ölçüm** | Referans (R1–R2), mesafe (M1–M2), açı, çap, alan; ölçüm listesi | v0.6.5 → v0.6.8 | Etkin |
| 3 | **Proje** | Aç, kaydet, farklı kaydet, yeni çalışma, içe/dışa aktarma | v0.5.0.6 | Etkin |
| 4 | **DXF** | 2B DXF ihracı, katman ve birim ayarları | v0.6.0 | Etkin |
| 5 | **Çizim** | Çizgi, daire, yay, dikdörtgen, ofset, kırpma (trim), ölçülendirme | v0.8.0 | Planlı |
| 6 | **3B** | Ekstrüzyon, döndürme, STEP/STL üretimi | v0.9.0 | Planlı |
| 7 | **CAM** | Takım yolu, G-Code üretimi, post-processor | v1.0 sonrası | Vizyon |
| 8 | **Ayarlar** | Uygulama tercihleri, kalibrasyon katsayıları | v1.0 sonrası | Planlı |

### 7.3. Sağ araç şeridi (yardımcı araçlar)

| Grup | Araçlar | Hedef sürüm |
|---|---|---|
| **Görünüm** | Yakınlaştır / uzaklaştır, sığdır, kaydırma, büyüteç ve oranı | v0.6.6 |
| **Yardımcı** | Izgara aç/kapat, açı gönyesi, noktaya yapışma (snap), cetvel (v0.6.7 etkin) | v0.6.6 → v0.7.0 |
| **Düzenleme** | Geri al, ileri al, seçili öğeyi sil, kopyala | v0.6.6 |
| **Katman** | Aktif katman, renk, çizgi tipi | v0.8.0 |

### 7.4. Yeni yeteneklerin gerektirdiği motorlar

| Yetenek | Gerekli teknoloji | Tahmini büyüklük | Lisans notu |
|---|---|---|---|
| Perspektif düzeltme | Homografi (OpenCV veya saf C#) | Orta | OpenCV: Apache-2.0 |
| 2B çizim araçları | Kendi geometri katmanı | Orta | Bağımlılık yok |
| DXF/DWG | DXF: kendi yazıcımız (var). DWG: lisanslı SDK gerekir | DWG yüksek | DWG kapalı format; açık kaynak güvenilir çözüm yok |
| 3B katı model | Open CASCADE (OCCT) veya FreeCAD CLI | **Çok yüksek** (3–6 ay) | OCCT: LGPL-2.1 (ayrı süreç önerilir) |
| G-Code / CAM | Takım yolu motoru + post-processor | Yüksek (1–2 ay) | Kendi geliştirme |

**Uyarı:** 3B katı model ve CAM, mevcut mimariden farklı bir geometri çekirdeği gerektirir.
Bu yetenekler v1.0 kapsamına **alınmamalıdır**; ayrı bir ürün hattı olarak planlanmalıdır.

### 7.5. Sıralama ilkesi

1. **Önce ölçüm doğruluğu**, sonra çizim, sonra 3B.
2. Her yeni sekme **kendi sürümünde** gelir; aynı sürümde iki sekme eklenmez.
3. Her sekme en az bir otomatik testle korunur.
4. Yeni bir dış bağımlılık eklenmeden önce lisans ve bakım kontrolü yapılır (`AGENTS.md` §2).
