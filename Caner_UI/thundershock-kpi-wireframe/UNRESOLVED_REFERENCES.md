# UNRESOLVED_REFERENCES.md

Repoda bulunan dış web referansları ve durumları.

---

## 1. Bulunan dış linkler

Tüm repo tarandı (`.md`, `.cs`, `.asset`, `.json`, PDF metinleri).

| # | Link | Nerede | Durum |
|---|---|---|---|
| 1 | `https://assetstore.unity.com/packages/tools/gui/modern-ui-pack-201717` | `UI/UILink.md` (tek satır, dosyanın tamamı) | **Çözülmedi** — dış erişim yapılmadı |

`UI/` klasörü bu tek dosyadan ibarettir. Prototip, bu klasörün altına
`UI/thundershock-kpi-wireframe/` olarak kuruldu ve `UILink.md` dosyasına dokunulmadı.

---

## 2. Neden erişilmedi

Bu görev kapsamında dış web sitelerine erişim yapılmadı. Link bir **Unity Asset Store
ürün sayfasıdır** — Modern UI Pack v5.5.25'in satın alma sayfası.

Bu sayfaya erişmek prototip için **gerekli değildi**, çünkü:

1. Paketin kendisi zaten repoda kurulu: `Assets/Modern UI Pack/`
2. Görsel dil, ürün sayfasının pazarlama görsellerinden değil, **projede fiilen
   kullanılan tema dosyasından** çıkarıldı:
   `Assets/Modern UI Pack/Resources/MUIP Manager.asset`
3. `AssetLibrary_Report.pdf` s.2 paketin projede hangi bileşenlerle kullanıldığını
   zaten belgeliyor (ModalWindowManager, buton prefabları, Animations/, Fonts/,
   Textures/)

Yerel kaynak, ürün sayfasından **daha güvenilir**dir: tema dosyası projenin gerçekten
kullandığı renkleri içerirken, ürün sayfası paketin varsayılan demo temasını gösterir.

---

## 3. Asset Store sayfası incelenirse ne yapılmalı

Eğer ileride bu sayfa incelenirse:

- **Yalnızca tasarım prensipleri** çıkarılmalı: düzen, hiyerarşi, kart yapısı,
  navigasyon yaklaşımı, görsel yoğunluk
- Sayfa içeriği veya tasarımı **birebir kopyalanmamalı**
- Sayfadaki görseller **hotlink edilmemeli**; Asset Store görselleri Unity
  Technologies ve paket geliştiricisine aittir
- Paketin lisansı **Unity Asset Store EULA**'dır (`AssetLibrary_Report.pdf` s.2).
  Paket içeriğinin (prefab, texture, font) projeden çıkarılıp üçüncü taraf bir web
  ürününde dağıtılması EULA'ya tabidir.

> **Prototipteki font kullanımı bu kısıtın dışındadır:** Open Sans, Modern UI Pack'in
> kendi varlığı değil, Apache-2.0 lisanslı açık kaynak bir fonttur ve serbestçe
> yeniden dağıtılabilir (bkz. `ASSET_GAPS.md` §6).

---

## 4. Belgelerde geçen diğer teknik referanslar

`AssetLibrary_Report.pdf` içinde paket kimlikleri geçiyor ancak bunlar URL değil,
paket tanımlayıcılarıdır. Erişim gerektirmedi:

| Tanımlayıcı | Paket |
|---|---|
| `com.michsky.muip` | Modern UI Pack v5.5.25 |
| `DrinkingWindGames.Cable` | Real-Time Procedural Cable Simple v1.1 |
| `com.unity.render-pipelines.universal` | URP 17.2.0 |
| `com.unity.cinemachine` | Cinemachine 3.1.5 |
| `com.unity.inputsystem` | Input System 1.14.2 |
| `com.unity.splines` | Splines 2.8.2 |
| `org.khronos.unitygltf` | UnityGLTF (GitHub açık kaynak) |
| `com.unity.ai.navigation` | AI Navigation 2.0.9 |
| `com.unity.timeline` | Timeline 1.8.9 |
| `com.unity.recorder` | Unity Recorder 5.1.5 |

Bunların tümü `Packages/manifest.json` ile doğrulanabilir.

---

## 5. Git remote

`git log` çıktısında görülen remote: `https://github.com/thetego/CedasISG`

Bu bir tasarım referansı değil, projenin kendi deposudur. İsimlendirme tespitinde
kanıt olarak kullanıldı (bkz. `DESIGN_SOURCE_AUDIT.md` §7) — depo adı `CedasISG`,
ürün adı ise GDD'ye göre `THUNDERSHOCK`.
