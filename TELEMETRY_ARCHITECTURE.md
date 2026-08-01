# Thundershock Telemetri Mimarisi — Şema v2

Bu belge 1 Ağustos 2026 itibarıyla repodaki çalışan uygulamayı ve operasyon
portalını tanımlar. `Caner_UI/thundershock-kpi-wireframe` altındaki eski analiz
dokümanları tarihsel tasarım girdisidir; güncel uygulama kaynağı değildir.

## Güncel akış

```text
Unity oyun akışı
  -> PlayFabDataManager kalıcı disk outbox
  -> PlayFab Events WriteEvents (50 varsayılan, 200 üst sınır)
  -> PlayFab Data Connection / Azure Blob Parquet
  -> thundershock-operations-portal gateway
       - şema doğrulama
       - eventId tekilleştirme
       - hatalı satır karantinası
       - retention
  -> portal server RBAC + tenant/team/self kapsamı
  -> React yönetim arayüzü
```

## Unity teslimat garantileri

- `PlayFabDataManager` her sahneden önce otomatik oluşturulur; MENU sahnesine
  doğrudan bağımlılık kaldırılmıştır. MENU'deki serialize ayarlar runtime
  instance'a aktarılır.
- Olay önce `Application.persistentDataPath/cedas-telemetry-outbox.json` dosyasına
  atomik olarak yazılır, sonra gönderilir.
- Başarısız batch'ler üstel geri çekilme ve jitter ile yeniden denenir.
- Varsayılan batch 50, PlayFab API üst sınırı 200, outbox sınırı 5.000 olay,
  cihaz saklama süresi 7 gündür.
- Outbox çalışan kimliğine göre ayrılır; bir çalışanın bekleyen olayı başka
  çalışanın oturumunda gönderilmez.
- Whitelist okuyucu ve çalışan loginleri `CreateAccount=false` kullanır. İstemci
  bilinmeyen hesap üretemez.
- Açık telemetri tercihi olmadan olay toplanmaz. Uygulamadaki kalıcı
  `Veri Tercihi` düğmesi kabulü geri çekmeyi ve yeniden vermeyi sağlar.
- Aydınlatma metni sürümü değiştiğinde tercih yeniden sorulur.

## Olay zarfı

```json
{
  "eventId": "guid",
  "schemaVersion": 2,
  "eventType": "ActionCompleted",
  "clientTimestamp": "UTC ISO-8601",
  "employeeId": "employee-custom-id",
  "payload": {
    "sessionId": "guid",
    "playerId": "employee-custom-id",
    "role": "trainee",
    "levelId": "level-1",
    "sequenceKey": "level-1/box1",
    "actionKey": "level-1/box1/001/switch",
    "schemaVersion": 2,
    "appVersion": "...",
    "unityVersion": "..."
  }
}
```

11 olay türü korunur: `LevelStarted`, `LevelCompleted`, `SequenceStarted`,
`SequenceCompleted`, `ActionCompleted`, `QuizAnswered`, `QuizSummary`,
`DragDropAttempt`, `MistakeRecorded`, `SurveyCompleted`, `SessionEnded`.

## Düzeltilen veri kalitesi sorunları

- Level kimlikleri `level-1`, `level-2`, `level-3` olarak kanonikleştirildi.
- Aynı raw `actionID` tekrar etse bile `actionKey`, level + sequence + sıra + action
  bileşiminden benzersiz üretilir.
- Quiz `questionId` artık ayrı alandır; boş içerikte `<actionId>:q1` fallback'i var.
- 10 `ActionType`, 10 ayrı telemetri değerine birebir çevrilir.
- `completionRate` gerçek tamamlanmayı (`1`) taşır; hata cezası
  `performanceRate` olarak ayrıldı.
- Severity ölçeği `Info=1`, `Warning=2`, `Critical=3` olarak tanımlandı.
- Prerequisite hata sonucu ve gerçek `GameOver` davranışı telemetriye bağlandı.
- Build öncesi `TelemetryDataValidator`; level/sequence/action referanslarını,
  kanonik ID'leri, quiz verisini ve event key benzersizliğini doğrular.
- Kullanılmayan Unity Analytics modülü manifestten çıkarıldı.

## Portal güvenlik ve işletim

Üretim portalı `Caner_UI/thundershock-operations-portal` altındadır. Tarayıcıya
PlayFab/Azure secret verilmez. Portal; imzalı HttpOnly session, CSRF, Origin
doğrulama, RBAC, tenant/team/self veri kapsamı, audit log ve şifreli gizlilik
talep deposu uygular.

Gateway, private Azure Blob Parquet akışını okur ve SQLite WAL deposuna transaction
ile yazar. Kullanıcı parolaları scrypt ile hashlenir. GDPR dışa aktarma/silme
işleri yönetici onayıyla PlayFab Admin API'ye iletilir ve JobReceiptId saklanır.

Kurulum ve gerekli secret'lar:
`Caner_UI/thundershock-operations-portal/docs/INTEGRATION.md`.

## Doğrulama

Portal:

```bash
cd Caner_UI/thundershock-operations-portal
npm ci
npm run check
```

Unity Editor:

1. `Tools > Safety Training > Validate Telemetry Data`
2. MENU dışındaki her level sahnesini doğrudan Play Mode ile açın.
3. Ağı kesip birkaç aksiyon tamamlayın; outbox sayısının artmasını doğrulayın.
4. Ağı açın; batch başarılı olduğunda outbox'ın boşaldığını doğrulayın.
5. `Veri Tercihi: Kapalı` iken hiçbir yeni olay oluşmadığını doğrulayın.

Unity Editor bu sunucuda kurulu olmadığı için derleme/Play Mode kontrolü CI veya
Unity bulunan iş istasyonunda ayrıca çalıştırılmalıdır. Repo içi build validator
geçersiz telemetri içeriğinde üretim build'ini durdurur.
