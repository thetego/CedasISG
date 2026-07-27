# DATA_MAPPING.md

Her UI bileşeninin hangi event alanından beslendiği, nasıl hesaplandığı ve verinin
o KPI'yı üretmeye **yetip yetmediği**.

**Veri yeterliliği sınıfları:**

| Sınıf | Anlamı |
|---|---|
| 🟢 **Doğrudan üretilebilir** | Alan event'te var, ek kural gerekmiyor |
| 🔵 **Türetilerek üretilebilir** | Mevcut alanlardan hesaplanıyor, varsayım gerektirmiyor |
| 🟡 **Ek iş kuralı gerekir** | Alan var ama anlamı/eşiği tanımlanmamış |
| 🟠 **Ek veri kaynağı gerekir** | Alan hiç yok, dışarıdan veri gerekiyor |
| 🔴 **Belirsiz** | Alanın semantiği koddan da belgeden de netleşmiyor |

---

## 1. Gerçek event şeması

Kaynak: **`Assets/_Project/Scripts/PlayFabDataManager.cs`**

Her event `SendEvent()` (satır 496-521) üzerinden şu zarfla gönderilir:

```jsonc
{
  "eventType":       "QuizAnswered",         // event adı
  "clientTimestamp": "2026-07-22T10:09:31Z", // DateTime.UtcNow.ToString("o")
  "employeeId":      "TEST001",              // CurrentPlayerId
  "payload":         { /* event'e özel */ }
}
```

> **Not (satır 486-493):** `timestamp` ve `playerId` adları bilerek kullanılmıyor —
> PlayFab bunları rezerve etmiş. PlayFab sunucusu her event'e kendi zaman damgasını
> ve entity bilgisini **ayrıca** ekler. Prototipte bu, `_serverTimestamp` alanıyla
> temsil edilir ve **istemci şemasının parçası değildir**.

### 1.1 Event kataloğu — tam liste

| # | eventType | Gönderen | payload alanları |
|---|---|---|---|
| 1 | `LevelStarted` | `SequenceManager.cs:145` | `levelId`, `displayName` |
| 2 | `LevelCompleted` | `SequenceManager.cs:832` | `levelId`, `completed`, `score`, `timeSpent`, `mistakes`, `completionRate` |
| 3 | `SequenceStarted` | `SequenceManager.cs:235` | `sequenceId`, `levelId` |
| 4 | `SequenceCompleted` | `SequenceManager.cs:732` | `sequenceId`, `levelId`, `timeSpent`, `mistakes`, `completed` |
| 5 | `ActionCompleted` | `SequenceManager.cs:555` | `actionId`, `levelId`, `sequenceId`, `type`, `objectId`, `duration`, `result` |
| 6 | `QuizAnswered` | `UIQuizPanel.cs:171` | `actionId`, `levelId`, `sequenceId`, `questionId`, `selectedAnswer`, `correctAnswer`, `isCorrect`, `attempts`, `timeSpent` |
| 7 | `QuizSummary` | `PlayFabDataManager.cs:409` (otomatik) | `levelId`, `totalQuestions`, `correctAnswers`, `wrongAnswers`, `accuracy` |
| 8 | `DragDropAttempt` | `UIDropZone.cs:196` | `actionId`, `levelId`, `sequenceId`, `targetObject`, `attempts`, `placements[{item,droppedOn,correct}]` |
| 9 | `MistakeRecorded` | `SequenceManager.cs:671`, `UIDropZone.cs:179` | `mistakeType`, `actionId`, `severity` |
| 10 | `SessionEnded` | `PlayFabDataManager.cs:476` | `levelId` |

### 1.2 Şemanın kritik boşlukları

| Boşluk | Kanıt | Etkisi |
|---|---|---|
| **`sessionId` / `attemptId` yok** | Hiçbir payload'da yok | Denemeler `LevelStarted`→`LevelCompleted` çiftinden **türetilmek zorunda**; çökme veya çok cihaz durumunda bozulur |
| **`MistakeRecorded` levelId/sequenceId taşımıyor** | `PlayFabDataManager.cs:460-468` | Bir hata ancak `actionId` → içerik kataloğu araması ile senaryoya bağlanabilir |
| **`severity` sabit `1`** | Her iki çağrı yerinde de literal `1` | Kritik hata sınıflandırması **imkânsız** |
| **`mistakeType` yalnızca 2 değer** | `"wrong_answer"`, `"wrong_drop"` | Hata taksonomisi çok dar |
| **`questionId` = `actionId`** | `UIQuizPanel.cs:173` — `_actionID` iki kez geçiliyor | Bir action'da birden fazla soru olsaydı ayrıştırılamazdı |
| **`role` hiçbir event'te yok** | `SendEvent()` zarfı yalnızca `employeeId` taşır | Rol bazlı filtre üretilemez |
| **Survey sonuçları hiç gönderilmiyor** | `SurveyResultTracker.cs` — PlayFab çağrısı yok | Anket cevapları, fotoğraf hizalama skorları kayıp |
| **`ActionCompleted.type` 10 tipi 5'e indirir** | `SequenceManager.cs:1362-1375` | `CameraMove`/`ModalWindow`/`Fade` ayırt edilemez |
| **`actionId` benzersizliği garanti değil** | Üretim level'larında 1 çakışma tespit edildi: `Anahtari cevir_Copy` (×2) | `actionId` → senaryo eşlemesi kırılgan |

---

## 2. Ana eşleştirme tablosu

### 2.1 Çalışan portalı

| UI Bileşeni | KPI | Event Türü | Kullanılan Alanlar | Hesaplama | Veri Yeterliliği | Not |
|---|---|---|---|---|---|---|
| Genel Bakış → KPI 1 | Son Başarı Oranı | `QuizAnswered` | `isCorrect` | `count(isCorrect=true) / count(*)` | 🟢 | Payda 0 ise **yüzde gösterilmez**, "Hesaplanamıyor" basılır |
| Genel Bakış → KPI 2 | Ortalama Soru Süresi | `QuizAnswered` | `timeSpent` | Geçerli değerlerin ortalaması | 🔵 | Geçersiz/null değerler **sıfır sayılmaz**, dışlanır ve sayısı raporlanır |
| Genel Bakış → KPI 3 | Toplam Hata | `MistakeRecorded` | — (adet) | `count(*)` | 🟢 | **Oran değil, sayı.** "Hata oranı" olarak sunulmaz |
| Genel Bakış → KPI 4 | Kritik Hata | `MistakeRecorded` | `severity` | — | 🟡 | **Hesaplanmıyor.** severity ölçeği tanımsız → "Sınıflandırma yok" |
| Genel Bakış → KPI 5 | Ortalama Deneme | `QuizAnswered` | `attempts`, `questionId` | Her soru için **max(attempts)**, sonra ortalama | 🔵 | `attempts` kümülatiftir (`UIQuizPanel.cs:157`), toplanmaz |
| Genel Bakış → değişim rozetleri | Önceki denemeye göre | türetilmiş deneme | tümü | Aynı senaryonun ardışık iki denemesi farkı | 🔵 | Tek deneme varsa rozet "veri yok" |
| Son Performans Özeti | Deneme detayı | `LevelCompleted`, `ActionCompleted`, `QuizAnswered`, `MistakeRecorded` | `timeSpent`, `score`, `mistakes`, `attempts` | Doğrudan okuma + sayım | 🟢 | Tamamlanma yalnızca `LevelCompleted` varsa |
| Son Performans → Severity dağılımı | Severity kırılımı | `MistakeRecorded` | `severity` | Ham kategori sayımı | 🟡 | Değerler **yorumlanmadan** listelenir |
| Gelişim Trendi (4 sekme) | Başarı / Süre / Hata / Deneme | tümü | — | Deneme bazlı seri | 🔵 | Tek deneme varsa **sahte trend çizilmez**, açıklama gösterilir |
| Senaryolarım → durum | Senaryo durumu | `LevelStarted`, `LevelCompleted` | `completed` | Açık completion event'i | 🟢 | Son `QuizAnswered` **asla** tamamlanma sayılmaz |
| Senaryolarım → "Tekrar Öneriliyor" | Tekrar önerisi | `QuizAnswered` | `isCorrect` | Son denemede doğruluk < %70 | 🟡 | Eşik **prototip önerisi**, ekranda belirtildi |
| Senaryo Detay → yol haritası düğümü | Adım durumu | `QuizAnswered`, `MistakeRecorded`, `ActionCompleted`, `DragDropAttempt` | `isCorrect`, `attempts` | Son quiz sonucu + tekrar var mı | 🔵 | Durumlar: Doğru / Yanlış / Tekrar denendi / Hata kaydı / Atlandı / Veri yok |
| Adım Drawer → cevap bloğu | Verilen ve doğru cevap | `QuizAnswered` | `selectedAnswer`, `correctAnswer`, `isCorrect`, `attempts`, `timeSpent` | Doğrudan okuma | 🟢 | Cevaplar `"C) Fazlara"` biçiminde önekli gelir |
| Adım Drawer → sürükle-bırak | Yerleştirme denemeleri | `DragDropAttempt` | `placements[]`, `attempts` | Doğrudan okuma | 🟢 | Her deneme item→zone eşleşmesiyle listelenir |
| Adım Drawer → Teknik Detay | Ham event | tümü | tümü | `JSON.stringify` | 🟢 | **Varsayılan olarak kapalı** `<details>` içinde |
| Karşılaştırma tablosu | Son ↔ Önceki | türetilmiş deneme | tümü | Metrik başına ayrı fark | 🔵 | **Tek skorda birleştirilmez**; karışık sinyalde uyarı basılır |
| Karşılaştırma → tekrar/çözülen/yeni | Hata kümesi farkı | `MistakeRecorded` | `actionId`, `mistakeType` | Küme farkı (`actionId::mistakeType`) | 🔵 | — |
| Hatalarım → hata kartı | Ne / nerede / kaç kez | `MistakeRecorded` | `actionId`, `mistakeType`, `clientTimestamp` | `actionId`+`mistakeType` grupla | 🔵 | Hatanın **nedeni** üretilmez — veride yok |
| Hatalarım → severity filtresi | Önem filtresi | `MistakeRecorded` | `severity` | — | 🟡 | **Devre dışı** kontrol; "Ölçek tanımsız" |
| Gelişimim → kilometre taşları | Başarımlar | çeşitli | çeşitli | Her kartta hesap temeli yazılı | 🟡 | Rozet sistemi projede yok → hepsi "Konsept" rozetli |
| Profil → rol | Kullanıcı rolü | — | — | Whitelist `PlayerEntry.role` | 🟠 | Telemetride yok; yalnızca whitelist'ten |

### 2.2 Yönetici portalı

| UI Bileşeni | KPI | Event Türü | Kullanılan Alanlar | Hesaplama | Veri Yeterliliği | Not |
|---|---|---|---|---|---|---|
| Yönetim Özeti → KPI 1 | Aktif Çalışan | tümü | `employeeId` | Benzersiz `employeeId` sayısı | 🟢 | **"Katılım oranı" değil** — roster yok |
| Yönetim Özeti → KPI 2 | Senaryo Denemesi | `LevelStarted` | — | `count(*)` | 🟢 | Tamamlanan sayısı `LevelCompleted`'dan |
| Yönetim Özeti → KPI 3 | Genel Doğruluk | `QuizAnswered` | `isCorrect` | `count(true)/count(*)` | 🟢 | — |
| Yönetim Özeti → KPI 4 | Ortalama Soru Süresi | `QuizAnswered` | `timeSpent` | Geçerli değer ortalaması | 🔵 | Eksik kayıt sayısı altta gösterilir |
| Yönetim Özeti → KPI 5 | Toplam Hata + oran | `MistakeRecorded`, `ActionCompleted` | — | Sayı; oran = `mistakes/actionCompleted` | 🔵 | Payda yoksa yalnızca sayı |
| Yönetim Özeti → KPI 6 | Kritik Hata Oranı | `MistakeRecorded` | `severity` | — | 🟡 | **Hesaplanmıyor** — "Sınıflandırılamıyor" |
| Yönetim Özeti → önceki dönem farkı | Dönem karşılaştırması | tümü | `clientTimestamp` | Eşit uzunlukta önceki pencere | 🔵 | "Tüm zamanlar" seçiliyse fark gösterilmez |
| Performans Trendleri (5 sekme) | Zaman serileri | tümü | `clientTimestamp` | 7 günlük kovalar | 🔵 | **Tek eksen** — farklı ölçekler ayrı sekmede |
| En Riskli Adımlar | Normalize risk | `MistakeRecorded`, `ActionCompleted` | `actionId` | `mistakes(action)/actionCompleted(action)` | 🔵 | Ham sayı **kullanılmaz** — hacim yanlılığını önler |
| İncelenmesi Gerekenler | Çalışan işaretleme | çeşitli | çeşitli | Doğruluk<%60 & ≥4 soru; yarım oturum; 3+ tekrar hata | 🟡 | Eşikler **prototip önerisi**, ekranda belirtildi |
| Veri Kalitesi kartı | Şema sağlığı | tümü | `levelId`, `actionId`, `timeSpent`, `severity` | Katalog eşleşmesi + eksik alan sayımı | 🟢 | Audit bulgularından doğan gerçek ürün özelliği |
| Çalışanlar tablosu | Çalışan listesi | tümü | `employeeId`, `isCorrect`, `timeSpent` | Çalışan başına toplama | 🟢 | Arama / sıralama / sayfalama çalışır |
| Çalışanlar → Ekip/Departman sütunu | — | — | — | — | 🟠 | **Sütun yok** — uydurulmadı |
| Çalışanlar → CSV | Dışa aktarma | — | — | — | 🟠 | Placeholder; backend + yetkilendirme gerekli |
| Çalışan Detay → Kurum Ortalaması | Göreli konum | `QuizAnswered` | `isCorrect` | Diğer çalışanların doğruluğu | 🔵 | **En az 3 diğer çalışan + 20 soru** yoksa gösterilmez |
| Çalışan Detay → Level bazlı KPI | Senaryo kırılımı | tümü | `levelId` | Level'a göre gruplama | 🟢 | — |
| Çalışan Detay → Tekrar Eden Hatalar | Tekrar kalıbı | `MistakeRecorded` | `actionId`, `mistakeType` | 2+ tekrar eden kombinasyonlar | 🔵 | — |
| Çalışan Detay → Öneriler | İnceleme alanları | çeşitli | çeşitli | Yalnızca veriden çıkarılabilen gözlemler | 🔵 | **Neden üretilmez** |
| Senaryolar → Zorluk faktörleri | 4 ayrı faktör | `QuizAnswered`, `DragDropAttempt`, `ActionCompleted`, `MistakeRecorded` | `isCorrect`, `attempts`, `timeSpent`/`duration` | Her faktör bağımsız sütun | 🔵 | **Tek "Zorluk Skoru" üretilmez** (bkz. §4) |
| Risk → Isı Haritası | Adım × Hata Türü | `MistakeRecorded`, `ActionCompleted` | `actionId`, `mistakeType` | `mistakes(action,type)/actionCompleted(action)` | 🔵 | Normalize oran; hücre tıklanınca drill-down |
| Risk → Severity Dağılımı | Ham kategori | `MistakeRecorded` | `severity` | Kategori sayımı | 🟡 | Yorumlanmaz |
| Risk → Kritik Hata Trendi | Trend | `MistakeRecorded` | `clientTimestamp` | Haftalık sayım | 🟡 | "Kritik" ayrıştırılamadığı için **toplam** hata trendi |
| Risk → Aynı Hatayı Tekrar Edenler | Kurum geneli kalıp | `MistakeRecorded` | `employeeId`, `actionId`, `mistakeType` | 2+ tekrar eden çalışan sayısı | 🔵 | — |
| Gelişim Trendleri (6 grafik) | Zaman serileri | tümü | `clientTimestamp` | 7 günlük kovalar | 🔵 | Her metrik **ayrı grafikte**, çift eksen yok |
| Raporlar | Rapor çıktıları | — | — | — | 🟠 | Tamamı placeholder |
| Ayarlar | İş kuralı tanımları | — | — | — | 🟡 | Karara bağlanması gereken 6 konu listeleniyor |

---

## 3. Hesaplanmayan KPI'lar ve gerekçeleri

Bu KPI'lar **bilerek üretilmemiştir**. Kod `kpi-calculations.js` içinde açık gerekçeyle
`ok: false` döner.

### 3.1 Katılım Oranı — 🟠 Ek veri kaynağı gerekir

```
katılım = katılan çalışan / atanmış çalışan
```

`atanmış çalışan` verisi **yok**. PlayFab whitelist bir *erişim listesidir*, bir
*eğitim ataması* değildir — listede olan bir çalışanın o senaryoyu tamamlaması
gerekip gerekmediği bilinmiyor.

**Portalın yaptığı:** "Katılım Oranı" yerine **"Aktif Çalışan"** (benzersiz
`employeeId` sayısı) gösteriliyor.

**Açılması için gereken:** `employeeId × levelId` atama tablosu + son tarih alanı.

### 3.2 Kritik Hata Oranı — 🟡 Ek iş kuralı gerekir

```csharp
// SequenceManager.cs:671
PlayFabDataManager.Instance?.LogMistakeRecorded(actionID, "wrong_answer", 1);
// UIDropZone.cs:179
PlayFabDataManager.Instance?.LogMistakeRecorded(_actionID, "wrong_drop", 1);
```

`severity` **her iki çağrı yerinde de literal `1`**. Yani veri kümesinde tek bir
severity değeri var ve ne 1'in "kritik" ne de "düşük" anlamına geldiği hiçbir belgede
yazılı değil.

**Portalın yaptığı:** severity değerlerini **ham kategori** olarak sayıyor
("severity = 1: 99 adet"), hiçbir değeri kritik saymıyor, kritik oranı hesaplamıyor.

**Açılması için gereken:**
1. Ölçek tanımı (ör. `1=düşük, 2=orta, 3=kritik`)
2. Kod değişikliği: her `LogMistakeRecorded` çağrısına doğru severity'nin geçilmesi.
   Özellikle GDD'deki **"Eğitim Sonu"** (`PrerequisiteFailAction.GameOver`) durumları
   — kritik güvenlik ihlalleri — şu an hiç `MistakeRecorded` üretmiyor bile.

### 3.3 Ekip / Departman / Lokasyon / Vardiya karşılaştırması — 🟠

Bu boyutlar ne event şemasında ne whitelist kaydında var. Portal bu filtreleri
**devre dışı** kontroller olarak gösteriyor ("Veri kaynağı yok / Gelecek veri
entegrasyonu") — sahte seçenek üretmiyor ve tabloya boş sütun eklemiyor.

### 3.4 Anket başarı oranı — 🟠

`SurveyResultTracker.cs` anket cevaplarını, doğruluk skorlarını ve fotoğraf hizalama
puanlarını hesaplıyor ama **hiçbirini event olarak göndermiyor** — veriler yalnızca
bellekte. Telemetride Survey adımları için sadece
`ActionCompleted { type: "survey" }` görünüyor.

**Açılması için gereken:** `SurveyCompleted` event'i:
```jsonc
{ "actionId", "levelId", "sequenceId",
  "questionResults": [{ "questionText", "selectedOptionIndex", "isCorrect" }],
  "photoResults":    [{ "slotLabel", "wasCaptured", "isAligned", "alignmentScore" }],
  "completionTime" }
```

### 3.5 `completionRate` alanı — 🔴 Belirsiz, kullanılmıyor

```csharp
// PlayFabDataManager.cs:311
{ "completionRate", Mathf.Clamp01(1f - mistakes * 0.05f) }
```

Adı "tamamlanma oranı" ama aslında **hata sayısından türetilmiş bir ceza katsayısı**.
Tamamlanma yüzdesiyle ilgisi yok. Portal bu alanı **hiçbir yerde kullanmıyor**;
tamamlanma için `LevelCompleted.completed` bayrağına bakıyor.

---

## 4. "Zorluk Skoru" neden üretilmedi

Tek bir zorluk skoru şu bileşenlerin ağırlıklandırılmasını gerektirir:

```
zorluk = w₁·yanlışOranı + w₂·ortDeneme + w₃·ortSüre + w₄·hataOranı + w₅·kritikOranı
```

**Sorunlar:**
1. `w₁..w₅` ağırlıkları bir **iş kuralıdır** ve hiçbir belgede tanımlı değil.
2. `w₅` (kritik oranı) zaten hesaplanamıyor (§3.2).
3. Tek skor, **düşük hacimli ama güvenlik açısından kritik** bir adımı, yüksek hacimli
   zararsız bir adımın içinde gizleyebilir. GDD s.5-6'daki "Kritik Güvenlik Noktaları"
   tam da bu tür adımlardır.

**Portalın yaptığı:** Faktörler `Senaryolar` sayfasında **yan yana ayrı sütunlar**
olarak gösteriliyor; yönetici hangi sütuna göre sıralayacağına kendisi karar veriyor.

**Yine de tek skor isteniyorsa** karar verilmesi gerekenler: normalizasyon yöntemi
(min-max mı z-score mu), ağırlıklar, ve skorun kritik güvenlik adımlarını
maskelemesini önleyecek bir taban kural.

---

## 5. Deneme (run) türetme kuralı

Şemada `sessionId` **yok**. Portal (`kpi-calculations.js › deriveRuns`) şu kuralı uygular:

```
Bir "deneme" = aynı employeeId + levelId için
               LevelStarted event'i ile başlayan,
               LevelCompleted veya SessionEnded ile kapanan event bloğu
```

**Bu türetmenin bilinen kırılganlıkları:**

| Durum | Sonuç |
|---|---|
| Uygulama çöker, `LevelCompleted` gelmez | Deneme "tamamlanmadı" kalır — terk mi çökme mi ayırt edilemez |
| Aynı çalışan iki cihazdan aynı anda oynar | Bloklar iç içe geçer, event'ler yanlış denemeye düşer |
| `LevelStarted` kaybolur (offline kuyruk) | Sonraki event'ler **hiçbir denemeye bağlanamaz** |
| `MistakeRecorded` açık deneme yokken gelir | "Yetim hata" olarak işaretlenir |

**Çözüm (önerilen kod değişikliği):**

```csharp
// PlayFabDataManager.cs
private string _sessionId;

public void LogLevelStarted(string levelId) {
    _sessionId = Guid.NewGuid().ToString("N");
    // ...
}

private void SendEvent(string eventName, Dictionary<string, object> payload) {
    payload["sessionId"] = _sessionId;   // TÜM event'lere eklenir
    // ...
}
```

Bu tek değişiklik, portaldaki deneme ayrımını 🔵 *türetilmiş* seviyeden 🟢 *doğrudan*
seviyeye taşır ve karşılaştırma/trend ekranlarını güvenilir hale getirir.

---

## 6. Önerilen minimum veri sözleşmesi

Portalın tüm ekranlarının çalışabilmesi için backend'in sağlaması gerekenler:

### 6.1 Event akışı (mevcut şemaya eklenecekler)

| Alan | Nereye | Öncelik | Açar |
|---|---|---|---|
| `sessionId` | **Tüm** event payload'ları | ⭐ Yüksek | Güvenilir deneme ayrımı, karşılaştırma, trend |
| `levelId`, `sequenceId` | `MistakeRecorded` | ⭐ Yüksek | Hatanın senaryoya doğrudan bağlanması |
| Gerçek `severity` | `MistakeRecorded` | ⭐ Yüksek | Kritik hata oranı, risk sıralaması |
| `role` | Event zarfı | Orta | Rol bazlı filtre ve karşılaştırma |
| `SurveyCompleted` event'i | Yeni | Orta | Anket ve fotoğraf analizi |
| `attemptIndex` | `QuizAnswered` | Düşük | `attempts` kümülatifliğinin netleşmesi |

### 6.2 Yardımcı tablolar (event dışı)

| Tablo | Alanlar | Açar |
|---|---|---|
| **Organizasyon** | `employeeId`, `team`, `department`, `location`, `shift`, `managerId` | Ekip/lokasyon/vardiya filtreleri ve karşılaştırmaları |
| **Eğitim Ataması** | `employeeId`, `levelId`, `assignedAt`, `dueAt` | Katılım oranı, gecikmiş eğitim uyarısı |
| **İçerik Kataloğu** | `levelId`, `sequenceId`, `actionId`, adlar, tipler | `actionId` → senaryo eşlemesi (şu an istemcide sabit) |

> **İçerik kataloğu neden gerekli:** `MistakeRecorded` levelId taşımadığı için portal,
> `actionId`'yi senaryoya bağlamak üzere oyunun ScriptableObject içeriğinden türetilmiş
> bir tabloya bağımlı. Bu tablo şu an prototipte elle yazılmış durumda
> (`mock-data.js › CONTENT`). Üretimde build sırasında otomatik export edilmelidir;
> aksi halde içerik değiştikçe portal sessizce yanlış eşleme yapar.

### 6.3 Önerilen sorgu uçları

```
GET /api/events?from&to&employeeId&levelId&sequenceId&mistakeType   → event listesi
GET /api/employees?from&to                                          → çalışan özetleri
GET /api/catalog                                                    → içerik kataloğu
GET /api/kpi/summary?from&to&...                                    → önceden hesaplanmış KPI'lar
```

Her uç noktanın çağıran kullanıcının rolüne göre **sunucu tarafında** filtrelenmesi
zorunludur: `role=employee` isteği yalnızca kendi `employeeId`'sini döndürmelidir.

---

## 7. Mock veri ile gerçek şema uyumu

`assets/js/mock-data.js` şu kurallara uyar:

- Zarf birebir `SendEvent()` formatında (`eventType`, `clientTimestamp`, `employeeId`, `payload`)
- Alan adları ve tipleri gerçek `SendEvent` çağrılarıyla aynı
- `selectedAnswer` / `correctAnswer` `"C) Fazlara"` biçiminde önekli
- `severity` **her zaman `1`** — gerçek koddaki gibi
- `mistakeType` yalnızca `"wrong_answer"` ve `"wrong_drop"`
- `questionId` her zaman `actionId` ile aynı
- `levelId` gerçek asset değerleriyle: `"level 1"`, `"lvl1"`, `"NewLevel"`
- `completionRate` gerçek formülle: `Clamp01(1 - mistakes * 0.05)`
- Survey adımları **yalnızca** `ActionCompleted` üretir — anket sonucu event'i yok
- `_serverTimestamp` `_` önekiyle işaretli, istemci şemasının parçası olmadığı belli

**Kapsanan uç durumlar:** başarılı çalışan · birkaç yanlış · çok tekrar deneme ·
çok hata · çok senaryo · tek deneme · hiç veri yok · gelişen · kötüleşen ·
eksik `timeSpent` · yetim `MistakeRecorded` · yarım kalan oturum.
