# ASSET_GAPS.md

Prototipte kullanılamayan, eksik olan veya web'e export edilmesi gereken assetler.

---

## 1. Marka / logo — **eksik**

**Durum:** Repoda web'de kullanılabilir bir **Thundershock logosu yok.**

Bulunanlar ve neden kullanılmadıkları:

| Dosya | Format | Neden kullanılmadı |
|---|---|---|
| `bedas_logo.glb` | GLB (3D) | Tarayıcıda 2D logo olarak kullanılamaz; render/export gerekir |
| `camlibel_logo.fbx` | FBX (3D) | Aynı |
| `Assets/_Project/_Level 1/Textures/BOGAZICI_ELEKTRIK_DAGITIM_A.S..png` | PNG | **Dağıtım şirketinin kurumsal logosu.** Ürün markası değil — portal markası yerine koymak yanıltıcı olurdu |
| `Assets/_Project/_Level 1/Textures/camlibeledas4-removebg-preview.png` | PNG | Aynı gerekçe |

**Prototipte ne yapıldı:** `assets/js/app.js › brandMark()` içinde satır içi SVG bir
yıldırım işareti üretildi (yuvarlatılmış kare + accent renkli yıldırım). Bu bir
**placeholder'dır**, onaylanmış bir marka kimliği değildir.

**Gereken export:**

| Varlık | Format | Boyut | Not |
|---|---|---|---|
| Thundershock logo (yatay) | SVG | vektör | Koyu zemin varyantı zorunlu |
| Thundershock işareti (kare) | SVG + PNG | 512×512 | Favicon ve avatar için |
| Favicon | ICO / PNG | 32, 180, 512 | Prototipte satır içi SVG data-URI kullanılıyor |

---

## 2. Ana menü arka planı — **eksik**

**Durum:** Oyunun ana menü ekranının arka plan görseli repoda **bulunamadı.** Ana menü
sahnesi `Assets/Scenes/` altında bir Unity sahnesi olarak var; arka plan runtime'da
render ediliyor, statik bir görsel olarak export edilmemiş.

**Prototipte ne yapıldı:** Giriş ekranının sol paneli (`.login-art`) CSS ile üretildi:
radyal accent parlamaları + ince ızgara deseni + koyu gradyan. Oyunun endüstriyel
atmosferini temsil eder ama **oyunun gerçek ana menüsü değildir.**

**Gereken export:**

| Varlık | Format | Boyut | Not |
|---|---|---|---|
| Ana menü arka planı | WebP + JPG (yedek) | 1920×1080, ≤300 KB | KPI okunabilirliği için üzerine koyu katman uygulanacak |
| Level 1/2/3 sahne kapak görseli | WebP | 800×450, ≤120 KB | Senaryo kartlarının kapağı için |

> **Nasıl alınır:** Proje `Unity Recorder 5.1.5` içeriyor
> (`AssetLibrary_Report.pdf` s.5). Her level sahnesinden temsili bir kare
> Recorder ile PNG olarak alınıp WebP'ye dönüştürülebilir.

---

## 3. Senaryo kapak görselleri — **placeholder kullanıldı**

Senaryo kartlarının kapak alanı (`.scenario__art`) şu an CSS gradyan + ilgili KKD/alet
ikonu ile dolduruluyor. Gerçek sahne görselleri gelince yalnızca CSS'in
`background` özelliği değiştirilerek kapak eklenebilir; düzen değişikliği gerekmez.

---

## 4. Kullanılan oyun assetleri — dönüşüm kaydı

20 adet oyun texture'ı web ikonlarına dönüştürüldü. Tam kaynak listesi
**`DESIGN_SOURCE_AUDIT.md` §6**'da.

**Dönüşüm parametreleri:**

```
1. Kaynak PNG RGBA olarak açılır
2. Alfa kanalının bounding box'ı ile saydam kenarlar kırpılır
3. LANCZOS ile 128×128 kutusuna orantı korunarak küçültülür
4. 128×128 saydam tuvale ortalanır
5. optimize=True ile PNG olarak yazılır
```

Sonuç: 1080×1080 (~0.5–1.2 MB) → 128×128 (8–31 KB). Toplam ikon ağırlığı ~290 KB.

> Bu dönüşüm tek seferliktir ve betiği repoya eklenmemiştir. Yeniden üretilmesi
> gerekirse yukarıdaki 5 adım Pillow ile birebir tekrarlanabilir.

**Yüksek çözünürlük gerekirse:** Retina ekranlar için 256×256 (`@2x`) varyantları
üretilip `srcset` ile sunulabilir. Şu anki 128px, kullanıldıkları en büyük boyut olan
62px için yeterlidir.

---

## 5. Web'de kullanılamayan formatlar

Repoda bulunan ama tarayıcıda doğrudan kullanılamayan varlıklar:

| Format | Örnek | Neden kullanılamaz | Prototipteki karşılığı |
|---|---|---|---|
| `.glb` | `Bedas_character_v_10.glb`, `Trafo_1.glb`, `bedas_logo.glb` | 3D model; `<model-viewer>` veya three.js gerekir — wireframe için gereksiz ağır bağımlılık | Kullanılmadı |
| `.fbx` | `Kosk_v.1.fbx`, `kesici.fbx`, `cell_v.3.fbx` | Tarayıcı desteği yok | Kullanılmadı |
| `.asset` | `LevelData`, `SequenceData`, `ActionData` | Unity YAML | **İçeriği okunup** `mock-data.js › CONTENT` kataloğuna elle aktarıldı |
| `.prefab` | `Q-A Panel.prefab`, `Survey Panel.prefab` | Unity sahne verisi | Bileşen adlandırmasında referans alındı |
| `.mat` | `RedCable.mat`, `panel.mat` | Unity materyali | Kullanılmadı |
| `.renderTexture` | `Cam.renderTexture` | Runtime render hedefi | Kullanılmadı |
| `.anim` / `.controller` | `KapakOpen.anim`, `AGPanel.controller` | Unity animasyon | Kullanılmadı |

> **Not:** `.uasset` (Unreal formatı) bu projede **yoktur** — proje Unity tabanlıdır.

---

## 6. Font durumu — **çözüldü**

| Font | Kaynak | Lisans | Durum |
|---|---|---|---|
| Open Sans (Regular/Semibold/Bold) | `Assets/Modern UI Pack/Fonts/` | Apache-2.0 | ✅ `assets/fonts/` altına kopyalandı, `@font-face` ile yerelden yükleniyor |
| Roboto Regular | Aynı klasör | Apache-2.0 | Mevcut, kullanılmadı |

Toplam font ağırlığı: ~650 KB (3 dosya). Üretimde WOFF2'ye dönüştürülerek
**~180 KB'a** düşürülebilir — TTF web için verimsizdir.

**Önerilen:** `fonttools` ile WOFF2 dönüşümü + Türkçe karakterleri kapsayan
`latin-ext` alt kümesine indirgeme.

---

## 7. İkon seti — **satır içi SVG kullanıldı**

Portalın arayüz ikonları (menü, uyarı, ok, arama, filtre vb.) repodan gelmiyor —
oyunun ikonları oyun-içi KKD/alet görselleri olduğu için dashboard'a uygun değil.

**Prototipte ne yapıldı:** `assets/js/ui.js › PATHS` içinde 30 adet satır içi SVG
yol tanımı. Harici ikon kütüphanesi veya CDN **yok**.

Modern UI Pack'in `Assets/Modern UI Pack/Resources/Icon Library.asset` dosyası bir
ikon kütüphanesi içeriyor ancak bunlar Unity sprite'ları; web için ayrıca export
edilmeleri gerekir. Mevcut satır içi set yeterli olduğu için export önerilmiyor.

---

## 8. Eksik asset özeti

| # | Eksik | Öncelik | Engellediği |
|---|---|---|---|
| 1 | Thundershock logosu (SVG) | ⭐ Yüksek | Marka kimliği — şu an placeholder |
| 2 | Ana menü arka planı | Orta | Giriş ekranının oyunla görsel bağı |
| 3 | Level kapak görselleri (3 adet) | Orta | Senaryo kartlarının görsel ayrımı |
| 4 | Favicon seti (ICO/PNG) | Düşük | Şu an satır içi SVG yeterli |
| 5 | WOFF2 fontlar | Düşük | Sayfa ağırlığı (~470 KB tasarruf) |
