# PRODUCT_CONTEXT.md

GDD, Asset Library raporu ve proje kaynak kodundan çıkarılan ürün bağlamı.
Belgelerde **bulunmayan** hiçbir bilgi burada gerçek kabul edilmemiştir.

Kaynaklar:
- `GDD_Thundershock.pdf` — 7 sayfa, v1.0, Nisan 2026
- `AssetLibrary_Report.pdf` — 11 sayfa, Nisan 2026
- `Assets/_Project/Scripts/*.cs` — 37 dosya
- `Assets/_Project/{_Level 1,Level 2,Level3}/DATA/*.asset` — ScriptableObject içeriği

---

## 1. Ürün özeti

**Thundershock**, saha çalışanlarına elektrik tesisi operasyonlarını öğreten bir
**3D iş güvenliği eğitim simülasyonudur** (GDD s.1). Çalışanlar gerçek saha ortamını
yansıtan sanal bir dünyada elektrik ekipmanlarını doğru sıra ve güvenlik kurallarına
uygun kullanmayı öğrenir.

- **Platform:** Bilgisayar ve mobil
- **Motor:** Unity 6000.2.7f2, URP 17.2.0, Cinemachine 3.1.5
- **Hedef kitle (GDD başlığı):** Yönetici & Proje Koordinatörü
- **Ton:** Endüstriyel, kurumsal, iş güvenliği odaklı. Oyunlaştırılmış ama çocukça değil.

**Temel döngü (GDD s.1):**

```
Ana Menü → Level (Görev Sahası) Seçimi
  → Sahnede Görev Grupları (Sekanslar) listelenir
  → Çalışan bir görev grubunu seçer
  → Adım adım iş akışı tamamlanır
  → Görev grubu tamamlandı → sıradaki grup açılır
  → Tüm gruplar tamamlandı → Skor ve değerlendirme ekranı
```

---

## 2. İçerik hiyerarşisi

Kod tarafında (`LevelData.cs`, `SequenceData.cs`, `ActionData.cs`):

```
LevelData        (levelID, levelName, sequences[], maxScore, penaltyPerMistake)
  └─ SequenceData   (sequenceID, sequenceName, actions[], prerequisiteSequences[])
       └─ ActionData   (actionID, actionName, actionType, prerequisiteActionIDs[])
            ├─ QuizActionData     (questionText, options[4], correctOptionIndex)
            ├─ SurveyActionData   (questions[], photoSlots[])
            ├─ EquipmentData[]    (giyilecek KKD'ler)
            └─ ToolData[]         (kullanılacak aletler)
```

Portalın drill-down zinciri bu hiyerarşiyi birebir izler:
**Kurum → Çalışan → Senaryo (Level) → Görev Grubu (Sequence) → Adım (Action) → Event.**

> Ekip/grup katmanı **yok** (bkz. §7), bu yüzden zincirde "Grup" adımı atlanmıştır.

---

## 3. Terminoloji sözlüğü

Portalda kullanılan Türkçe karşılıklar GDD'den alınmıştır:

| Kod / Telemetri | GDD terimi | Portal etiketi |
|---|---|---|
| `LevelData` / `levelId` | Level, Görev Sahası | **Senaryo** |
| `SequenceData` / `sequenceId` | Sekans, Görev Grubu | **Görev Grubu** |
| `ActionData` / `actionId` | Adım | **Adım** |
| `QuizActionData` | Bilgi Sorusu | **Bilgi sorusu** |
| `SurveyActionData` | Saha Anketi & Fotoğraf | **Anket** |
| `EquipmentData` | KKD (Kişisel Koruyucu Donanım) | **Ekipman** |
| `MistakeRecorded` | Hata | **Hata** |
| `PrerequisiteFailAction.GameOver` | Eğitim Sonu | **Eğitim sonu** |

---

## 4. Adım tipleri

`ActionData.cs:7-19` **10 adım tipi** tanımlıyor. GDD s.3 bunların 7'sini anlatıyor
(`OpenClose`'u `Click` ile birleştirmiş, `ModalWindow` ve `Fade`'i saymamış).

`SequenceManager.cs:1362-1375` bu 10 tipi telemetride **5 dizeye** indirger:

| ActionType (enum) | GDD açıklaması | `ActionCompleted.type` |
|---|---|---|
| `WearEquipment` (0) | Envanterden doğru KKD'yi vücut bölgesine sürükle-bırak | `drag_drop` |
| `DragToWorld` (2) | Doğru aleti 3D sahnedeki hedef noktaya bırak | `drag_drop` |
| `Click` (3) | Nesne üzerinde beliren butona tıkla | `click` |
| `OpenClose` (1) | Kapı/kapak aç-kapat | `click` |
| `PanelInteraction` (5) | Saha kontrol panelini simüle et | `click` |
| `Quiz` (6) | 4 şıklı çoktan seçmeli soru | `quiz` |
| `Survey` (7) | Açılır menülü sorular + fotoğraf çekimi | `survey` |
| `CameraMove` (4) | Otomatik kamera geçişi (kullanıcı müdahalesi yok) | `interaction` |
| `ModalWindow` (8) | Modal pencere aç → confirm | `interaction` |
| `Fade` (9) | Tam ekran fade efekti | `interaction` |

> **Analitik etkisi:** `type` alanı 10 tipi 5'e indirdiği için, telemetriden bakarak
> bir adımın `ModalWindow` mu `CameraMove` mu olduğu **ayırt edilemez** — ikisi de
> `interaction`. Portal bu yüzden adım tipini içerik kataloğundan (ScriptableObject)
> okur, event'ten değil.

---

## 5. Senaryolar (Level'lar)

### Level 1 — Direk & Trafo Köşkü
- **Asset:** `Assets/_Project/_Level 1/DATA/level_1.asset`
- **Telemetriye gönderilen `levelId`:** `"level 1"`
- **Sahne (GDD s.5):** Açık alan elektrik dağıtım direği ve trafo köşkü
- **Senaryo:** Hat bakımı için bağlantı kesme ve yeniden bağlantı verme; klemens montajı
- **Kritik güvenlik noktası (GDD s.5):** *"Elektrik kesilmeden klemens montajı yapılmaya çalışılırsa eğitim sonlandırılır."*

Sekanslar (GUID çözümlemesiyle assetlerden doğrulandı):

| sequenceID | Asset adı | Adım sayısı | Adımlar (actionID) |
|---|---|---|---|
| `equipment` | `E qu ip me nt` | 2 | `Equipment_Act_02`, `wear` |
| `direk1` | `d ir ek 1` | 5 | `direk1_Act_04`, `checkEnergy1`, `openPlate`, `direk1_Act_04_2`, `direk1_Act_03` |
| `direk2` | `d ir ek 2` | 10 | `direk2_Act_07`, `Kablolardakacakkontroluyap`, `cutDuct`, `klemens`, `sigorta`, `closePlate`, `direk2_Act_09`… |
| `direk3` | `d ir ek 3` | 4 | `tabela`, `tabelaMat`, `Uyaritabelasinisabitle`, `direk3_Act_04` |
| `trafo1` | `t ra fo 1` | 7 | `openDoor`, `SwitchIn`, `openin`, `SwitchPanel`, `off`, `trafo1_Act_06/07` |
| `trafo2` | `t ra fo 2` | 8 | `on`, `SwitchTrafo`, `openin_2`, `openDoor_2`, `trafo2_Act_07/08`… |
| `equipment2` | `E qu ip me nt 2` | 2 | `Equipment2_Act_02`, `wear2` |
| `equipment3` | `E qu ip me nt 3` | 1 | `wear_2` |

> `AssetLibrary_Report.pdf` s.10 Level 1 görev gruplarını "Direk1, Direk2, Direk3, OFF,
> ON, Equipment1-3, Trafo1-2" olarak listeliyor. Assetlerde **OFF ve ON ayrı sekans
> değil**: OFF adımları (`off`, `openDoor`, `openin`, `SwitchIn`, `SwitchPanel`)
> `trafo1` sekansına, ON adımları (`on`, `openDoor_2`, …) `trafo2` sekansına aittir.
> Bu, belge ile asset arasındaki bir farktır; prototip assetleri izler.

### Level 2 — Hücre / Pano Odası
- **Asset:** `Assets/_Project/Level 2/DATA/level_2.asset`
- **Telemetriye gönderilen `levelId`:** `"lvl1"` ⚠ **hatalı** (bkz. §8)
- **Sahne (GDD s.5):** Kapalı alan AG (Alçak Gerilim) hücre odası
- **Senaryo:** Kesici açma operasyonu; doğru alet seçimi ve SCADA onayı zorunlu
- **Kritik güvenlik noktası:** *"Yanlış alet seçimi ölçüm hatasına yol açar — uyarı verilir."*
- **sequenceID'ler:** `EquipmentSequence`, `Box1`, `Box2`, `BuildingSequence`,
  `BuildingSequence2/3/4`, `BackSequence`, `CarSequence`, `scada`, `Building`

### Level 3 — AG Trafo Dairesi & Otopark
- **Asset:** `Assets/_Project/Level3/Data/Training/Levels/level_3.asset`
- **Telemetriye gönderilen `levelId`:** `"NewLevel"` ⚠ **Unity varsayılanı, hiç ayarlanmamış**
- **Sahne (GDD s.6):** Otopark altında AG trafo dairesi
- **Senaryo:** Periyodik bakım; termal kamera ile kısmi deşarj tespiti, fotoğraflama
- **Kritik güvenlik noktası:** *"Termal tarama tamamlanmadan panel açılmaya çalışılırsa eğitim sonlandırılır."*
- **sequenceID'ler:** `Level3_Seq_01` (Equipment), `Level3_Seq_02` (Trafo Check),
  `Level3_Seq_03` (AG Check), `Level3_Seq_04` (Trafo Merkez), `Level3_Seq_05` (Pano 1),
  `Level3_Seq_09` (AG Eldivenleri)

---

## 6. Quiz içeriği

Repoda **34 adet `Quiz` tipi ActionData** var; bunlardan **12'sinin soru metni dolu**,
kalan 22'sinin `questionText` alanı boş bırakılmış.

Prototip mock verisi 12 gerçek soruyu kullanır. Örnekler:

| actionID | Soru | Doğru cevap |
|---|---|---|
| `Q4` | Test cihazı bağlantısı yapılırken kablolar nereye bağlanmalıdır? | C) Fazlara |
| `Q5` | Üç fazın tamamı test edildikten sonra ilk yapılması gereken işlem hangisidir? | C) Test cihazını kaldırıp bağlantıları sökmek |
| `Q6` | Çalışma tamamlandıktan sonra son adım hangisidir? | C) SCADA operatörüne çalışma sonucunu bildirmek |
| `TrafoCheck_Act_10` | Termal kamera ile yapılan ölçümün amacı nedir? | B) Aşırı ısınan ekipmanları tespit etmek |
| `BuildingSequence_Act_17_2` | Ayırıcı açıldıktan sonra hücre üzerinde çalışmadan önce ne yapılmalı? | B) Hücre topraklanmalıdır |

**Cevap biçimi:** `UIQuizPanel.cs:68` şık öneklerini `"A) "`, `"B) "`, `"C) "`, `"D) "`,
`"E) "` olarak tanımlar ve `selectedAnswer` / `correctAnswer` alanlarını **önekli metin**
olarak gönderir (satır 122, 101). Yani telemetride cevap `"C) Fazlara"` biçimindedir,
index değil. Mock veri bu biçimi birebir taklit eder.

---

## 7. Kullanıcı rolleri

`PlayFabDataManager.PlayerEntry` (satır 60-72) ve `PlayFabWhitelistData.cs` (satır 18-21)
şu alanları tanımlar:

```csharp
public string playerId;      // PlayFab'a gönderilen benzersiz ID
public string displayName;
public string role;          // "worker" / "inspector" / "trainee"
public int    level;
public long   xp;
public string createdAt;
public string lastLogin;
```

**Giriş akışı (`UILoginPanel.cs`):** Oyun açılışında PlayFab Title Data'dan whitelist
çekilir; çalışan **yalnızca kendi ID'sini** bir input'a yazar. Tüm çalışan listesi
ekrana hiç basılmaz (satır 9-11 yorumu). **Şifre alanı yoktur.**

> ⚠ **Kritik boşluk:** `role` alanı whitelist kaydında var ama **hiçbir event
> payload'ında gönderilmiyor**. `SendEvent()` zarfı yalnızca `employeeId` taşır.
> Bu yüzden telemetriden rol bazlı filtreleme/karşılaştırma **üretilemez**.

**Yönetici rolü kodda tanımlı değildir.** Oyun içinde yönetici arayüzü yoktur;
portalın yönetici rolü bu prototiple birlikte önerilen yeni bir roldür.

---

## 8. Belgelerde bulunmayan alanların sınıflandırması

### 8.1 Prototip için güvenle varsayılabilir

| Konu | Varsayım | Gerekçe |
|---|---|---|
| Portal dili | Türkçe (tek dil) | Tüm oyun UI metinleri, GDD ve kod yorumları Türkçe; çoklu dil altyapısı yok |
| Görsel tema | Koyu, endüstriyel | MUIP teması koyu; oyun sahneleri saha ortamı |
| Adım sıra numarası | Sequence içindeki `actions[]` dizisi sırası | `SequenceData.GetActionAt(index)` bu sırayı kullanıyor |
| Senaryo adları | GDD s.5-6 başlıkları | Belgede açıkça yazılı |
| Skor formülü bileşenleri | `maxScore=100`, `penalty=5` | `LevelData.cs:35-36` varsayılanları; gerçek assetlerde de aynı |

### 8.2 İş kuralı tanımlanmadan uygulanamaz

| Konu | Neden uygulanamaz | Portalın davranışı |
|---|---|---|
| **Severity ölçeği** | `MistakeRecorded` her yerde `severity: 1` gönderiyor (`SequenceManager.cs:671`, `UIDropZone.cs:179`). Ölçek hiçbir belgede tanımlı değil. | Kritik sınıflandırma **yapılmıyor**; severity ham kategori olarak sayılıyor |
| **"Tekrar öneriliyor" eşiği** | Hangi doğruluk altında tekrar gerektiği tanımlı değil | %70 eşiği kullanıldı, ekranda "prototip önerisi" olarak işaretlendi |
| **Terk edilmiş oturum tanımı** | `SessionEnded` gelip `LevelCompleted` gelmeyen oturum "terk" mi "çökme" mi? | "Tamamlanmadı" olarak gösteriliyor, neden iddia edilmiyor |
| **Rozet/başarım kuralları** | Repoda başarım sistemi **yok** | "Gelişimim" ekranındaki kartlar "Konsept" rozetiyle işaretli |
| **Zorluk skoru ağırlıkları** | Faktörlerin nasıl ağırlıklandırılacağı tanımsız | Tek skor üretilmiyor; faktörler yan yana gösteriliyor |
| **Kurum ortalaması eşiği** | Kaç çalışandan sonra ortalama anlamlı? | En az 3 diğer çalışan + 20 soru şartı kondu, ekranda belirtildi |

### 8.3 Backend veya ek veri kaynağı gerektirir

| Konu | Gereken kaynak | Kilitlenen özellik |
|---|---|---|
| Ekip / departman / lokasyon / vardiya | İK sisteminden `employeeId` eşleşmeli organizasyon tablosu | Ekip karşılaştırması, lokasyon riski, vardiya analizi |
| Atanmış eğitim (roster) | `employeeId × levelId` atama tablosu + son tarih | **Katılım oranı** — payda yok |
| Oturum / deneme kimliği | Kod değişikliği: `LogLevelStarted` içinde `sessionId` üretip tüm payload'lara eklemek | Güvenilir deneme ayrımı |
| Anket & fotoğraf sonuçları | Kod değişikliği: `SurveyResultTracker` → `SurveyCompleted` event'i | Saha anketi analizi, fotoğraf kalite raporu |
| Rapor üretimi / export | Sunucu tarafı PDF/CSV üretimi + yetkilendirme + audit log | Raporlar sayfasının tamamı |
| Kimlik doğrulama | RBAC, sunucu tarafı yetkilendirme, veri izolasyonu | Gerçek rol sınırı |

---

## 9. Tespit edilen çelişkiler

| # | Çelişki | Kanıt | Prototipin kararı |
|---|---|---|---|
| 1 | GDD "7 adım tipi" diyor, kod 10 tanımlıyor | GDD s.3 ↔ `ActionData.cs:7-19` | Kod izlendi |
| 2 | GDD "anket verileri analitik sistemine aktarılır" diyor, hiçbir event gönderilmiyor | GDD s.3 ↔ `SurveyResultTracker.cs` (PlayFab çağrısı yok) | Kod izlendi; eksik olarak raporlandı |
| 3 | `productName` = `Cedas-ISG`, GDD/PDF başlıkları = `THUNDERSHOCK` | `ProjectSettings.asset` ↔ iki PDF | GDD adı arayüzde; fark belgelendi |
| 4 | Belge Level 1'de OFF/ON'u ayrı görev grubu sayıyor, assetlerde yok | `AssetLibrary_Report.pdf` s.10 ↔ sequence assetleri | Assetler izlendi |
| 5 | Üretim level'larının `levelID` değerleri tutarsız | `level_1.asset` = `"level 1"`, `level_2.asset` = `"lvl1"`, `level_3.asset` = `"NewLevel"` | Gerçek değerler kullanıldı, portalda **veri kalitesi uyarısı** olarak gösterildi |
| 6 | Bazı `sequenceName` değerlerinde harf araları bozuk | `"d ir ek 1"`, `"E qu ip me nt"`, `"t ra fo 1"` | Portal okunabilir ad gösterir, ham asset adını da yanında belirtir |
| 7 | `LevelCompleted.completionRate` "tamamlanma yüzdesi" gibi görünüyor | `PlayFabDataManager.cs:311`: `Clamp01(1 - mistakes * 0.05)` — aslında bir **hata cezası** | Portal bu alanı tamamlanma oranı olarak **kullanmıyor** |
| 8 | `questionId` ayrı bir soru kimliği değil | `UIQuizPanel.cs:173` — `actionID` iki kez geçiliyor | Soru kimliği olarak `actionId` kullanılıyor |
# Güncellik notu (1 Ağustos 2026)

Bu dosya tarihsel ürün/wireframe bağlamıdır. Güncel uygulama mimarisi için repo
kökündeki `TELEMETRY_ARCHITECTURE.md` dosyasını esas alın.
