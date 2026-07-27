# DESIGN_SOURCE_AUDIT.md

Bu dosya, prototipteki her tasarım kararının repodaki hangi dosyadan geldiğini kaydeder.
**Burada listelenmeyen hiçbir dosya yolu prototipte kullanılmamıştır.** Uydurulmuş asset
yolu yoktur.

---

## 1. İncelenen klasörler

| Klasör | Ne bulundu | Prototipe etkisi |
|---|---|---|
| `UI/` | Yalnızca `UI/UILink.md` (tek satır: Modern UI Pack Asset Store linki) | Referans link olarak kaydedildi (bkz. §5). Prototip bu klasörün altına kuruldu. |
| `Assets/Modern UI Pack/` | Oyunun tüm arayüz altyapısı: tema dosyası, fontlar, buton/panel/modal prefabları | **Ana görsel dil kaynağı** — renk rampası ve tipografi buradan alındı |
| `Assets/_Project/Scripts/` | 37 C# dosyası; UI davranışı ve telemetri | Renk sabitleri, giriş akışı, event şeması |
| `Assets/_Project/_Level 1/`, `Level 2/`, `Level3/` | Level içerikleri, ScriptableObject verileri, texture'lar | İçerik kataloğu + ikon assetleri |
| `Assets/Data/Training/Levels/` | Level 4–11 arası taslak/kopya level assetleri | **Kullanılmadı** — üretim dışı taslaklar (bkz. §6) |
| `Assets/_Project/UI/Prefabs/` | 18 sahne-içi UI prefabı (Q-A Panel, Survey Panel, Scada, ToolUI…) | Panel isimlendirmesi ve bileşen envanteri |
| `Assets/PlayFabSDK/`, `PlayFabEditorExtensions/` | PlayFab SDK | Telemetri API'si (`WritePlayerEvent`) |
| `Assets/TextMesh Pro/` | Metin render altyapısı | Tipografi bağlamı |
| `Assets/Real-Time Procedural Cable Simple/` | Kablo simülasyonu | Görsel dile etkisi yok |
| `ProjectSettings/ProjectSettings.asset` | `productName: Cedas-ISG` | **İsimlendirme çelişkisi** (bkz. §7) |
| `GDD_Thundershock.pdf` (7 sayfa) | Oyun tasarımı, level yapısı, adım tipleri, skorlama | Terminoloji, senaryo açıklamaları, kritik güvenlik notları |
| `AssetLibrary_Report.pdf` (11 sayfa) | Paket ve asset envanteri | Font/paket doğrulaması, KKD ikon listesi |

Taranmayan klasörler (gereksiz): `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, `.git/`.

---

## 2. Renk tokenları — kaynak dosya ve satır

Tüm renkler Unity'nin 0–1 float formatından hex'e çevrilmiştir.

### 2.1 Yüzey rampası — Modern UI Pack tema dosyasından

Kaynak: **`Assets/Modern UI Pack/Resources/MUIP Manager.asset`**

| Token | Hex | Unity değeri | Kaynak satır | Alan |
|---|---|---|---|---|
| `--panel-2` | `#23374B` | `(0.137, 0.216, 0.294)` | satır 29 | `dropdownItemBackgroundColor` |
| `--panel-3` | `#2D4155` | `(0.176, 0.255, 0.333)` | satır 41 | `modalWindowBackgroundColor` |
| `--panel-4` | `#374B5F` | `(0.216, 0.294, 0.373)` | satır 21 | `buttonNormalColor` |

Aynı üç değer satır 19 (`contextBackgroundColor`), 26–27 (`dropdownBackgroundColor`,
`dropdownContentBackgroundColor`), 48 (`notificationBackgroundColor`) ve 33
(`selectorHighlightedColor`) içinde de tekrarlanıyor — yani bu, oyunun **tutarlı**
üç kademeli panel rampası.

**Türetilen değerler** (dashboard okunabilirliği için rampanın altına eklendi, kaynağı yok):

| Token | Hex | Gerekçe |
|---|---|---|
| `--bg-0` | `#0E1821` | Sayfa zemini — panellerin ayrışması için rampanın iki kademe altı |
| `--bg-1` | `#16232F` | İçerik zemini ve grafik yüzeyi |
| `--panel` | `#1B2B3A` | Kart yüzeyi — `--bg-1` ile `--panel-2` arası |

### 2.2 Durum renkleri — oyun kodundaki gerçek sabitler

| Token | Hex | Unity değeri | Kaynak |
|---|---|---|---|
| `--ok` | `#1ACC33` | `(0.1, 0.8, 0.2)` | `Assets/_Project/Scripts/UIQuizPanel.cs:45` `correctColor` |
| `--bad` | `#D92626` | `(0.85, 0.15, 0.15)` | `Assets/_Project/Scripts/UIQuizPanel.cs:46` `wrongColor` |
| `--neutral` | `#999999` | `(0.6, 0.6, 0.6)` | `Assets/_Project/Scripts/UIQuizPanel.cs:47` `disabledColor` |
| `--accent` | `#3399FF` | `(0.2, 0.6, 1.0)` | `Assets/_Project/Scripts/SequenceData.cs:94` `completedColor` |
| `--seq-live` | `#33CC33` | `(0.2, 0.8, 0.2)` | `Assets/_Project/Scripts/SequenceData.cs:91` `buttonColor` |

`--accent` ve `--seq-live` değerleri gerçek sekans assetlerinde de doğrulandı
(ör. `Assets/_Project/_Level 1/DATA/direk1__d_ir_ek_1.asset` içinde
`buttonColor: {r: 0.2, g: 0.8, b: 0.2, a: 0.9}`).

İlgili diğer sabitler (referans için, doğrudan token yapılmadı):
`UIQuizPanel.cs:35` `correctFeedbackColor` = `(0.1,0.8,0.2,0.95)`,
`UIQuizPanel.cs:38` `wrongFeedbackColor` = `(0.85,0.15,0.15,0.95)`,
`SurveyActionData.cs:71-74` `indicatorAlignedColor` = `#1AE633`,
`indicatorOffColor` = `#E6331A`.

### 2.3 Türetilen renkler — kaynağı OLMAYAN, açıkça işaretli

| Token | Hex | Neden türetildi |
|---|---|---|
| `--warn` | `#E8A33D` | **Oyunda uyarı için renk sabiti yok.** `SequenceManager.cs:984` uyarıyı yalnızca `Debug.Log("<color=orange>")` ile basıyor; `warningPanel` rengini MUIP temasından alıyor. Panel zeminine karşı 6.70:1 kontrastla seçildi. |
| `--bad-ink` | `#FF6B6B` | Oyunun `#D92626` kırmızısı koyu panelde metin için 2.93:1 — WCAG 4.5:1 eşiğinin altında. Metin için açılmış varyant (5.21:1); **dolgu/mark olarak hâlâ `#D92626` kullanılıyor**. |

---

## 3. Grafik paletleri — doğrulanmış

Kategorik ve sıralı paletler `dataviz` doğrulayıcısı ile test edildi
(OKLCH açıklık bandı, kroma tabanı, protanopi/döteranopi ΔE ayrımı, normal görüş
tabanı, yüzey kontrastı). Yüzey: `#16232F` (`--bg-1`).

**Kategorik** — `--cat-1..5`, sabit sıra, döngüsüz:

```
#2B85E8, #C6811F, #A96AE0, #DE6647, #219FB2
→ TÜM KONTROLLER GEÇTİ
   en kötü komşu çift ΔE 15.7 (protan) · normal görüş 23.5 · kontrast ≥3:1
```

**Sıralı (ordinal) rampa** — `--seq-1..5`, ısı haritası için, tek hue:

```
#31556F, #2C6C97, #2887C4, #3AA0EC, #86C2F9
→ TÜM KONTROLLER GEÇTİ (--ordinal)
   monoton açıklık · komşu ΔL ≥0.06 · açık uç 2.02:1 · hue yayılımı 6°
```

Durum renkleri (`--ok`/`--warn`/`--bad`) **rezervedir**: hiçbir grafikte "seri 4"
olarak kullanılmaz ve her zaman ikon + metin etiketiyle birlikte gösterilir.

---

## 4. Tipografi

| Özellik | Değer | Kaynak |
|---|---|---|
| Arayüz fontu | **Open Sans** | `Assets/Modern UI Pack/Fonts/OpenSans-{Regular,Semibold,Bold}.ttf` |
| Ağırlıklar | 400 / 600 / 700 | Aynı klasörde 9 ağırlık mevcut; 3'ü kopyalandı |
| Lisans | Apache-2.0 | Yeniden dağıtılabilir — prototipe kopyalandı |
| Alternatif | Roboto-Regular.ttf | Aynı klasörde mevcut, kullanılmadı |

Fontlar `assets/fonts/` altına kopyalandı ve `@font-face` ile yerelden yüklenir.
**Harici font CDN'i yoktur.**

`AssetLibrary_Report.pdf` s.2 bunu doğruluyor: "Yazı Tipleri — `Fonts/` klasörü —
Tüm arayüz metinleri".

Başlık hiyerarşisi: `h1` 1.5rem → kart başlıkları `h2` (1rem, görsel olarak küçük
ama semantik olarak doğru sırada) → alt başlıklar `h3`.

---

## 5. Şekil, boşluk ve bileşen dili

| Özellik | Prototip değeri | Gerekçe |
|---|---|---|
| Köşe yuvarlaklığı | 6 / 10 / 14 px | MUIP panel ve buton prefabları yumuşak köşeli; ölçek yaklaştırıldı |
| Boşluk sistemi | 4 / 8 / 12 / 16 / 24 / 32 / 48 px | 4px tabanlı ölçek |
| Gölge | 3 kademe | MUIP modal pencereleri gölgeli |
| Buton stilleri | Dolu accent + dış çizgili (ghost) | MUIP `ButtonManager` prefablarının iki ana varyantı |
| Modal | Ortalanmış, koyu zemin, gölge | MUIP `ModalWindowManager` (`AssetLibrary_Report.pdf` s.2) |

Oyundaki panel envanteri (`GDD_Thundershock.pdf` s.6 §7) prototipteki bileşen
adlandırmasına yansıtıldı:

| Oyun paneli | Prototip karşılığı |
|---|---|
| Soru Paneli (4 şıklı + geri bildirim) | Action drawer'daki "Soru ve cevaplar" bloğu |
| Bildirim Penceresi (uyarı / eğitim sonu) | `notice` bileşeni ve modal |
| Ekipman Paneli (sürükle-bırak) | Action drawer'daki "Sürükle-bırak denemeleri" |
| Anket Paneli | Survey adımları — veri gelmediği için yalnızca durum gösterimi |

---

## 6. Kullanılan asset dosyaları — tam liste

Aşağıdaki 20 PNG, repodaki **gerçek oyun texture'larından** üretilmiştir:
saydam kenarlar kırpıldı, 128×128 kareye orantı korunarak yerleştirildi,
PNG olarak optimize edildi (1080×1080 → ~8–31 KB).

| Prototip dosyası | Kaynak (repo yolu) | Orijinal boyut |
|---|---|---|
| `assets/images/kask.png` | `Assets/_Project/_Level 1/Textures/kask.png` | 1080×1080 |
| `assets/images/eldiven.png` | `Assets/_Project/_Level 1/Textures/eldiven.png` | 1080×1080 |
| `assets/images/gozluk.png` | `Assets/_Project/_Level 1/Textures/gozluk.png` | 1080×1080 |
| `assets/images/maske.png` | `Assets/_Project/_Level 1/Textures/maske.png` | 1080×1080 |
| `assets/images/klemens.png` | `Assets/_Project/_Level 1/Textures/klemens.png` | 1080×1080 |
| `assets/images/sigorta.png` | `Assets/_Project/_Level 1/Textures/sigorta.png` | 1080×1080 |
| `assets/images/matkap.png` | `Assets/_Project/_Level 1/Textures/matkap.png` | 1080×1080 |
| `assets/images/pense.png` | `Assets/_Project/_Level 1/Textures/pense.png` | 1080×1080 |
| `assets/images/falcata.png` | `Assets/_Project/_Level 1/Textures/falcata.png` | 1080×1080 |
| `assets/images/tabela.png` | `Assets/_Project/_Level 1/Textures/tabela.png` | 1080×1080 |
| `assets/images/kontrol_kalemi.png` | `Assets/_Project/_Level 1/Textures/kontrol kalemi.png` | 1080×1080 |
| `assets/images/isg_boot.png` | `Assets/_Project/Level 2/Textures/isg_boot.png` | 1080×1080 |
| `assets/images/long_gloves.png` | `Assets/_Project/Level 2/Textures/long_gloves_yellow.png` | 1080×1080 |
| `assets/images/megger.png` | `Assets/_Project/Level 2/Textures/megger.png` | 1080×1080 |
| `assets/images/cell.png` | `Assets/_Project/Level 2/Textures/cell.png` | 1024×1024 |
| `assets/images/tornavida.png` | `Assets/_Project/Level 2/Textures/metal_tornavida.png` | 1080×1080 |
| `assets/images/catal_cubugu.png` | `Assets/_Project/Level 2/Textures/catal_cubugu.png` | 1080×1080 |
| `assets/images/termal_kamera.png` | `Assets/_Project/Level3/Textures/termal_kamera.png` | 1080×1080 |
| `assets/images/kismi_desarj.png` | `Assets/_Project/Level3/Textures/kismi_desarj.png` | 1080×1080 |
| `assets/images/ag_eldiven.png` | `Assets/_Project/Level3/Textures/ag_eldiven.png` | 1024×1024 |

Üretim betiği kalıcı değildir; dönüşüm parametreleri `ASSET_GAPS.md` §4'te kayıtlıdır.

**Kullanılmayan ama repoda bulunan görseller** (bilerek atlandı):
`BOGAZICI_ELEKTRIK_DAGITIM_A.S..png` ve `camlibeledas4-removebg-preview.png`
kurumsal logolardır. Portal markası GDD'deki **THUNDERSHOCK** adıdır; bir dağıtım
şirketinin logosunu ürün markası yerine koymak yanıltıcı olurdu. Bunun yerine
`app.js › brandMark()` içinde satır içi SVG bir yıldırım işareti üretildi
(bkz. `ASSET_GAPS.md` §1).

---

## 7. İsimlendirme tespiti — hangi ad nerede geçiyor

| Kaynak | Kullanılan ad | Güvenilirlik |
|---|---|---|
| `GDD_Thundershock.pdf` s.1 başlık ve s.7 altbilgi | **THUNDERSHOCK** | Yüksek — v1.0, Nisan 2026, ürün belgesi |
| `AssetLibrary_Report.pdf` s.1 başlık ve s.11 altbilgi | **THUNDERSHOCK** | Yüksek — Nisan 2026 |
| Repo klasör adı | `thundershock` | Orta |
| `ProjectSettings/ProjectSettings.asset:productName` | `Cedas-ISG` | Yüksek ama farklı katman — **build/ürün kimliği** |
| Git remote (`git log`) | `github.com/thetego/CedasISG` | Orta — depo adı |
| C# namespace (tüm scriptler) | `SafetyTraining` | Yüksek — kod katmanı |

**Karar:** Arayüzde **THUNDERSHOCK** kullanıldı. Gerekçe: en güncel (Nisan 2026) ve
ürünün kendisini adlandıran iki resmi belge bu adı taşıyor; `Cedas-ISG` ise Unity
build kimliği, `SafetyTraining` ise kod namespace'i — ikisi de ürün adı değil.
**"Thundershot" yazımı repoda hiçbir yerde geçmiyor.** Belirsizlik `README.md`
§İsimlendirme içinde de belirtildi.

---

## 8. Belge ile mevcut ürün arasındaki farklar

Kaynak önceliği kuralı gereği çelişkide **mevcut üründeki kod** kazandı:

| Konu | Belgede yazan | Kodda olan | Prototipin izlediği |
|---|---|---|---|
| Adım tipi sayısı | GDD s.3: "7 farklı adım tipi" | `ActionData.cs:7-19`: **10** enum değeri (`ModalWindow`, `Fade` ve `OpenClose` ayrıca) | Kod — 10 tip, telemetride 5 dizeye indirgeniyor |
| Level sayısı | GDD s.7: "Level Sayısı 3" | 3 üretim level'ı + `Assets/Data/Training/Levels/` altında 11 taslak | Kod — yalnızca 3 üretim level'ı |
| Anket verisi | GDD s.3: "Veriler analitik sistemine aktarılır" | `SurveyResultTracker.cs` **hiçbir event göndermiyor** (satır 9 yorumu: "PlayFab veya başka analytics SDK'ya bağlanmak için") | Kod — anket sonuçları portalda yok, eksik olarak işaretlendi |
| Skorlama | GDD s.4: `Puan = Maks − (Hata × Ceza) + Hız Bonusu` | `SequenceManager.cs:832` skoru gönderiyor; `LevelData.cs:35-37` varsayılanlar 100/5/20 | Kod — skor `LevelCompleted.score` alanından okunuyor |
| Giriş akışı | — | `UILoginPanel.cs`: **yalnızca çalışan ID**, şifre alanı yok | Kod — şifre alanı prototipe eklendi ama "doğrulanmaz" olarak işaretlendi |

---

## 9. Erişilebilirlik doğrulaması

Headless Chrome ile tüm ekranlarda otomatik tarama yapıldı:

- `alt` özniteliği olmayan görsel: **0**
- Erişilebilir adı olmayan buton: **0**
- Etiketi olmayan form alanı: **0**
- Başlık sırası: `H1 → H2 → H3` (atlama yok)
- Odak göstergesi: tüm etkileşimli öğelerde `:focus-visible` ile görünür
- Durum bilgisi hiçbir yerde yalnızca renkle taşınmıyor — her rozet ikon + metin içerir
- Drawer/modal: `role="dialog"`, `aria-modal`, ESC ile kapanma, odak tuzağı, kapanışta odak iadesi
