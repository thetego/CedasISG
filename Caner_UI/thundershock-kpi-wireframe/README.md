# Thundershock KPI Portalı — HTML Wireframe Prototipi

Thundershock iş güvenliği eğitim simülasyonundan toplanan telemetri verisini,
**çalışan** ve **yönetici** için ayrı ayrı anlaşılır KPI'lara dönüştüren gezilebilir
bir web prototipi.

> ⚠️ **Buradaki tüm çalışan ve olay kayıtları temsilidir.** Gerçek kişisel veri,
> gerçek çalışan adı veya gerçek PlayFab kaydı içermez. Prototipte **gerçek kimlik
> doğrulama yoktur.**

---

## Hızlı başlangıç

Build adımı, paket kurulumu ve internet bağlantısı **gerekmez.**

```bash
# Repo kökünden:
cd UI/thundershock-kpi-wireframe

# Herhangi bir statik sunucu yeterli — biri:
python -m http.server 8777
#   veya
npx serve -l 8777
```

Ardından tarayıcıda: **http://127.0.0.1:8777/index.html**

> `index.html` dosyasını doğrudan çift tıklayarak da açabilirsiniz. Bu durumda
> uygulama çalışır ancak `file://` protokolü altında **yerel fontlar yüklenmeyebilir**
> (tarayıcı CORS kısıtı); arayüz sistem fontuna düşer. Tam görünüm için yerel sunucu
> önerilir.

---

## Demo hesaplar

Bu hesaplar **gerçek kullanıcı değildir**; yalnızca prototip içindir. Şifre alanı
doldurulabilir ama **doğrulanmaz** (oyunun mevcut giriş akışında da şifre yoktur —
`UILoginPanel.cs` yalnızca çalışan ID ister).

### Çalışan girişi

| ID | Ne gösterir |
|---|---|
| `TEST001` | **Zengin veri** — 3 senaryo, 6 deneme, gelişim trendi, karşılaştırma |
| `EMP-1049` | Gelişen performans — 3 deneme, hata oranı düşüyor |
| `EMP-1050` | Kötüleşen performans — hata oranı artıyor |
| `EMP-1047` | **Tek deneme** — "karşılaştırma yapılamaz" durumu |
| `EMP-1048` | **Hiç verisi yok** — boş durum ekranı |
| `EMP-1051` | Eksik `timeSpent` içeren eventler |
| `EMP-1053` | Yarım kalan oturum (`LevelCompleted` gelmemiş) |

Tam liste için `assets/js/mock-data.js › EMPLOYEES`.

### Yönetici girişi

| ID | Ne gösterir |
|---|---|
| `ADMIN_DEMO` | Tüm kurum verisi, risk analizi, drill-down |

---

## Route listesi

| Route | Ekran |
|---|---|
| `#/login` | Giriş ve rol seçimi |
| **Çalışan** | |
| `#/employee/dashboard` | Genel Bakış — KPI'lar, son performans, gelişim trendi |
| `#/employee/scenarios` | Senaryolarım — senaryo kartları ve durumlar |
| `#/employee/scenario/:levelId` | **Senaryo detayı** — yol haritası, adım drawer'ı, karşılaştırma |
| `#/employee/performance` | Performansım — trendler, senaryo kırılımı, deneme tablosu |
| `#/employee/mistakes` | Hatalarım — filtrelenebilir hata kartları |
| `#/employee/progress` | Gelişimim — kilometre taşları (konsept) |
| `#/employee/profile` | Profil ve veri erişim kapsamı |
| **Yönetici** | |
| `#/manager/dashboard` | Yönetim Özeti — KPI'lar, trendler, riskli adımlar, veri kalitesi |
| `#/manager/employees` | Çalışanlar — arama, sıralama, sayfalama |
| `#/manager/employee/:id` | Çalışan detayı — kurum içi konum, tekrar eden hatalar, öneriler |
| `#/manager/scenarios` | Senaryolar — adım zorluk faktörleri |
| `#/manager/scenario/:levelId` | Senaryo detayı — sequence kırılımı, adım faktörleri |
| `#/manager/risks` | **Hata ve Risk Analizi** — ısı haritası, severity, tekrar kalıpları |
| `#/manager/trends` | Gelişim Trendleri — 6 ayrı grafik |
| `#/manager/reports` | Raporlar (placeholder — backend gerekli) |
| `#/manager/settings` | Ayarlar — karara bağlanması gereken iş kuralları |

Örnek derin bağlantılar:
`#/employee/scenario/level%201` · `#/manager/scenario/lvl1` · `#/manager/employee/EMP-1045`

---

## Dosya yapısı

```
UI/thundershock-kpi-wireframe/
├── index.html                     Tek giriş noktası
├── README.md                      Bu dosya
├── DESIGN_SOURCE_AUDIT.md         Her tasarım kararının kaynak dosyası
├── PRODUCT_CONTEXT.md             GDD/PDF/koddan çıkarılan ürün bağlamı
├── DATA_MAPPING.md                UI ↔ KPI ↔ event alanı eşlemesi
├── ASSET_GAPS.md                  Eksik ve export edilmesi gereken assetler
├── UNRESOLVED_REFERENCES.md       Dış web referansları
├── assets/
│   ├── css/styles.css             Tasarım sistemi (tokenlar kaynak dosyalarıyla yorumlu)
│   ├── js/
│   │   ├── mock-data.js           İçerik kataloğu + temsili event akışı (2017 event)
│   │   ├── kpi-calculations.js    Tüm metrik hesapları — "payda yoksa oran yok"
│   │   ├── ui.js                  Ortak bileşenler + SVG grafikler
│   │   ├── views-employee.js      Çalışan ekranları
│   │   ├── views-manager.js       Yönetici ekranları
│   │   └── app.js                 Router, kabuk, giriş, filtreler
│   ├── fonts/                     Open Sans (Apache-2.0, repodan kopyalandı)
│   └── images/                    20 ikon — gerçek oyun texture'larından üretildi
└── screenshots/                   24 doğrulama ekran görüntüsü
```

**Oyun dosyalarına dokunulmadı.** Prototip `Assets/` dizininin tamamen dışındadır;
Unity bu klasörü import etmez ve `.meta` dosyası üretmez.

---

## Mimari

- Semantic HTML + modern CSS + **vanilla JavaScript** (ES5 uyumlu sözdizimi)
- CSS custom properties ile tasarım tokenları
- Hash tabanlı router (`#/rol/sayfa/detay`)
- Build adımı yok, paket yok, **harici CDN yok**
- Grafikler saf SVG ile çizilir — chart kütüphanesi eklenmedi

Yükleme sırası: `mock-data` → `kpi-calculations` → `ui` → `views-*` → `app`.

---

## İsimlendirme

Arayüzde **THUNDERSHOCK** kullanıldı. Repoda üç farklı ad geçiyor:

| Ad | Nerede | Katman |
|---|---|---|
| **THUNDERSHOCK** | `GDD_Thundershock.pdf` s.1/s.7, `AssetLibrary_Report.pdf` s.1/s.11 (Nisan 2026) | **Ürün adı** |
| `Cedas-ISG` | `ProjectSettings/ProjectSettings.asset` → `productName` | Unity build kimliği |
| `SafetyTraining` | Tüm C# dosyalarının namespace'i | Kod katmanı |

Depo adı ayrıca `CedasISG` (git remote). **"Thundershot" yazımı repoda hiçbir yerde
geçmiyor.** En güncel ve ürünü doğrudan adlandıran iki resmi belge "Thundershock"
dediği için arayüzde bu ad kullanıldı. Ayrıntı: `DESIGN_SOURCE_AUDIT.md` §7.

---

## Kullanılan assetler

- **Fontlar:** `Assets/Modern UI Pack/Fonts/OpenSans-{Regular,Semibold,Bold}.ttf`
  (Apache-2.0) → `assets/fonts/`
- **İkonlar:** 20 adet gerçek oyun texture'ı, 128×128'e küçültülerek → `assets/images/`.
  Kaynak dosya yollarının tamamı `DESIGN_SOURCE_AUDIT.md` §6'da.
- **Renkler:** `Assets/Modern UI Pack/Resources/MUIP Manager.asset` +
  `UIQuizPanel.cs` / `SequenceData.cs` içindeki gerçek renk sabitleri.
- **Arayüz ikonları:** `assets/js/ui.js` içinde satır içi SVG — harici kütüphane yok.

**Eksik:** Thundershock logosu, ana menü arka planı, level kapak görselleri.
Bunların yerine açıkça işaretlenmiş placeholder'lar kullanıldı — `ASSET_GAPS.md`.

---

## Temsili veri hakkında

`assets/js/mock-data.js` **2017 event** üretir. Şema, `PlayFabDataManager.cs`
`SendEvent()` formatına birebir uyar:

```jsonc
{ "eventType": "QuizAnswered",
  "clientTimestamp": "2026-07-22T10:09:31.000Z",
  "employeeId": "TEST001",
  "payload": { "actionId": "Q4", "levelId": "lvl1", "sequenceId": "Box1",
               "questionId": "Q4", "selectedAnswer": "B) Ayırıcıya",
               "correctAnswer": "C) Fazlara", "isCorrect": false,
               "attempts": 1, "timeSpent": 24 } }
```

Üretim deterministiktir (seed'li PRNG) — her yenilemede aynı veri gelir.

**Bilerek kapsanan uç durumlar:** başarılı çalışan · birkaç yanlış cevap · çok tekrar
deneme · çok hata · birden fazla senaryo · tek deneme · hiç veri yok · gelişen ·
kötüleşen · eksik `timeSpent` · `QuizAnswered` ile eşleşmeyen `MistakeRecorded` ·
yarım kalan oturum.

Quiz soruları **repodaki gerçek metinlerdir** (12 adet dolu soru;
repoda toplam 34 Quiz action'ın 22'sinin metni boş bırakılmış).

---

## Bilinen sınırlamalar

### Hesaplanmayan KPI'lar ve nedenleri

| KPI | Neden yok | Portalın yaptığı |
|---|---|---|
| **Kritik hata oranı** | `MistakeRecorded` her yerde `severity: 1` gönderiyor (`SequenceManager.cs:671`, `UIDropZone.cs:179`); ölçek hiçbir belgede tanımlı değil | severity ham kategori olarak sayılıyor, kritik sınıflandırma yapılmıyor |
| **Katılım oranı** | Atanmış eğitim (roster) verisi yok; PlayFab whitelist bir erişim listesi | Yerine **"Aktif Çalışan"** gösteriliyor |
| **Ekip/departman/lokasyon/vardiya** | Ne event'te ne whitelist'te var | Filtreler **devre dışı**, sahte seçenek üretilmiyor |
| **Anket başarı oranı** | `SurveyResultTracker.cs` hiçbir event göndermiyor — veriler bellekte kalıyor | Survey adımları için yalnızca `ActionCompleted` görünüyor |
| **Rol bazlı filtre** | `role` whitelist'te var ama hiçbir event payload'ında yok | Filtre üretilmedi |
| **Zorluk skoru** | Ağırlıklar tanımsız iş kuralı | Faktörler yan yana ayrı sütunlarda |

### Veri kalitesi bulguları

Bunlar prototipin değil, **mevcut telemetri şemasının** sorunlarıdır:

1. **`sessionId` yok** — denemeler `LevelStarted`→`LevelCompleted` çiftinden
   türetilmek zorunda; çökme veya çok cihaz durumunda bozulur.
2. **`levelID` değerleri tutarsız** — `level_1.asset` = `"level 1"`,
   `level_2.asset` = `"lvl1"` (yanlış), `level_3.asset` = `"NewLevel"`
   (Unity varsayılanı, hiç ayarlanmamış).
3. **`MistakeRecorded` levelId/sequenceId taşımıyor** — hata ancak `actionId` →
   içerik kataloğu araması ile senaryoya bağlanabiliyor.
4. **`ActionCompleted.type` 10 adım tipini 5 dizeye indiriyor** —
   `CameraMove`/`ModalWindow`/`Fade` ayırt edilemiyor.
5. **`questionId` = `actionId`** (`UIQuizPanel.cs:173`) — ayrı soru kimliği yok.
6. **`completionRate` yanıltıcı ad** — `Clamp01(1 - mistakes*0.05)`, tamamlanma
   yüzdesi değil hata cezası. Portal bu alanı kullanmıyor.
7. **`actionId` benzersizliği garanti değil** — üretim level'larında 1 çakışma var
   (`Anahtari cevir_Copy` ×2).

Portal bu bulguları Yönetim Özeti'ndeki **"Veri Kalitesi"** kartında canlı olarak
yüzeye çıkarır.

### Prototip sınırları

- **Gerçek kimlik doğrulama yoktur.** Rol ayrımı yalnızca istemci tarafındadır ve
  bir güvenlik sınırı **değildir**.
- Rapor/export işlevleri çalışmaz — "Prototip" ve "Backend gerekli" olarak etiketli.
- Bildirimler sabittir.
- Filtre seçimleri kalıcı değildir (sayfa yenilenince sıfırlanır).
- Mobil telefon için tam üretim tasarımı yapılmadı; arayüz 768px'te kırılmadan çalışır.

---

## Gerçek ürüne geçiş

### 1. Öncelikli kod değişiklikleri (Unity tarafı)

```csharp
// PlayFabDataManager.cs — TEK EN DEĞERLİ DEĞİŞİKLİK
private string _sessionId;

public void LogLevelStarted(string levelId) {
    _sessionId = Guid.NewGuid().ToString("N");
    // ...
}

private void SendEvent(string eventName, Dictionary<string, object> payload) {
    payload["sessionId"] = _sessionId;   // TÜM eventlere eklenir
    // ...
}
```

| # | Değişiklik | Açtığı özellik |
|---|---|---|
| 1 | Tüm event'lere `sessionId` | Güvenilir deneme ayrımı, karşılaştırma, trend |
| 2 | `MistakeRecorded`'a `levelId` + `sequenceId` | Hatanın senaryoya doğrudan bağlanması |
| 3 | Gerçek `severity` değerleri + ölçek tanımı | Kritik hata oranı, risk sıralaması |
| 4 | `level_2.asset` ve `level_3.asset` `levelID` düzeltmesi | Doğru senaryo gruplaması |
| 5 | `SurveyCompleted` event'i | Anket ve fotoğraf analizi |
| 6 | Event zarfına `role` | Rol bazlı filtre |

Ayrıntılı sözleşme: `DATA_MAPPING.md` §6.

### 2. Backend gereksinimleri

- Event sorgu API'si (`/api/events`, `/api/employees`, `/api/kpi/summary`)
- **İçerik kataloğu export'u** — build sırasında ScriptableObject'lerden otomatik
  üretilmeli; şu an prototipte elle yazılı (`mock-data.js › CONTENT`) ve içerik
  değiştikçe sessizce eskiyecektir
- Organizasyon tablosu (ekip/departman/lokasyon/vardiya)
- Eğitim ataması (roster) tablosu
- Rapor üretimi ve zamanlama

### 3. Güvenlik ve gizlilik — üretim için zorunlu

| Gereksinim | Neden |
|---|---|
| **Role-based access control (RBAC)** | Prototipteki rol ayrımı istemci tarafındadır, atlanabilir |
| **Sunucu tarafı yetkilendirme** | Her sorgu çağıranın rolüne göre sunucuda filtrelenmeli |
| **Employee data isolation** | `role=employee` isteği yalnızca kendi `employeeId`'sini döndürmeli |
| **Audit logging** | Kimin hangi çalışanın verisini görüntülediği kayıt altına alınmalı |
| **Export yetkilendirmesi** | CSV/PDF dışa aktarımı ayrı bir yetki gerektirmeli |
| **Kişisel veri maskeleme** | Dışa aktarımlarda ad/ID maskeleme politikası |
| **Veri saklama politikası** | Event kayıtlarının saklama süresi ve anonimleştirme takvimi |

Çalışan başka çalışanların verisini görememelidir. Prototipte bu sınır
`app.js › render()` içindeki rol kontrolü ve `filters.employeeScope()`'un
`employeeId`'yi oturum sahibine sabitlemesi ile **temsil edilmiştir** — gerçek
koruma sunucuda olmalıdır.

### 4. Tasarım devri

- Renk tokenları `styles.css` başında, her biri kaynak dosya + satır yorumuyla
- Grafik paletleri erişilebilirlik doğrulamasından geçti (`DESIGN_SOURCE_AUDIT.md` §3)
- Bileşenler `ui.js` içinde bağımsız fonksiyonlar — bir framework'e taşınabilir
- Eksik assetler `ASSET_GAPS.md` içinde öncelik sırasıyla

---

## Doğrulama

Headless Chrome (Puppeteer) ile otomatik test edildi:

- **17 ekran** 1440px'de, ayrıca 1280 / 1024 / 768px'de kontrol edildi
- **Console hatası: 0** · başarısız istek: 0 · kırık görsel: 0 · yatay taşma: 0
- Test edilen etkileşimler: rol geçişi, demo giriş, menü navigasyonu, senaryo kartı,
  action node → drawer, ısı haritası hücresi → drill-down, drawer ESC ile kapanma,
  tarih filtresi (kapsam 1677→613 event güncellendi), tablo sıralama, tablo arama
  (10→1 satır), rol sınırı yönlendirmesi, çıkış → giriş ekranı
- Erişilebilirlik: `alt`'sız görsel 0, erişilebilir adı olmayan buton 0, etiketsiz
  form alanı 0, başlık sırası `H1→H2→H3` (atlama yok)

Ekran görüntüleri: `screenshots/` (24 dosya).
# Güncellik notu (1 Ağustos 2026)

Bu klasör statik tarihsel wireframe'i korur. Üretim portalı
`Caner_UI/thundershock-operations-portal`, güncel telemetri mimarisi ise repo
kökündeki `TELEMETRY_ARCHITECTURE.md` altındadır.
