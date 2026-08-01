# PlayFab Data Connection entegrasyonu

## Olay sözleşmesi

Unity, `custom.thundershock` namespace'inde `WriteEvents` ile en fazla 200 olaylık
batch gönderebilir. Bu projede varsayılan batch 50'dir. Her olayın `PayloadJSON`
alanında şu zarf bulunur:

```json
{
  "eventId": "32-char-guid",
  "schemaVersion": 2,
  "eventType": "ActionCompleted",
  "clientTimestamp": "2026-08-01T07:00:00.000Z",
  "employeeId": "EMP-1001",
  "payload": {
    "sessionId": "session-guid",
    "playerId": "EMP-1001",
    "role": "trainee",
    "levelId": "level-1",
    "sequenceId": "Box1",
    "actionId": "Switch",
    "actionKey": "level-1/box1/001/switch",
    "type": "click",
    "result": "success"
  }
}
```

Desteklenen olaylar:

1. `LevelStarted`
2. `LevelCompleted`
3. `SequenceStarted`
4. `SequenceCompleted`
5. `ActionCompleted`
6. `QuizAnswered`
7. `QuizSummary`
8. `DragDropAttempt`
9. `MistakeRecorded`
10. `SurveyCompleted`
11. `SessionEnded`

Gateway zorunlu alanları ve tipleri doğrular. Geçersiz satırlar
`rejected_events` tablosuna kaynak adı ve hata listesiyle yazılır; dashboard
event tablosuna karışmaz. `eventId` tekrarları `INSERT OR IGNORE` ile tekilleşir.

## PlayFab -> Azure Blob

PlayFab Game Manager'da title event Data Connection oluşturun ve yalnız gateway
sunucusunun okuyabildiği özel Azure Blob container'ı hedefleyin. Container URL'si
SAS içeriyorsa onu secret manager'da `AZURE_BLOB_CONTAINER_URL` olarak tutun;
repository'ye veya portal ayar ekranına yazmayın.

Gateway `.parquet` bloblarını listeler, ETag ile yalnız yeni/değişen dosyaları
işler, sıkıştırma codec'lerini açar, doğrular ve SQLite'a transaction ile yazar.
Başarılı bloblar `processed_blobs` tablosunda işaretlenir. Senkronizasyon
varsayılan 60 saniyedir.

## Gateway çalıştırma

Örnek environment:

```dotenv
PLAYFAB_TITLE_ID=797DC
GATEWAY_HOST=127.0.0.1
GATEWAY_PORT=4180
GATEWAY_SERVICE_TOKEN=<en-az-32-karakter-rastgele-secret>
AZURE_BLOB_CONTAINER_URL=<private-container-sas-url>
GATEWAY_DATABASE_FILE=.data/gateway.sqlite
GATEWAY_RETENTION_DAYS=365
GATEWAY_USERS_JSON=[{"id":"ADMIN-1","name":"CEDAŞ Yöneticisi","password":"<strong-password>","role":"admin","tenantId":"tenant-cedas"}]
PLAYFAB_SECRET_KEY=<yalnız-gdpr-operasyonları-için>
```

```bash
npm run gateway
```

Gateway sözleşmesi:

- `GET /health?titleId=797DC`
- `GET /bootstrap?titleId=797DC&cursor=...`
- `POST /authenticate?titleId=797DC`
- `POST /privacy/export?titleId=797DC`
- `POST /privacy/delete?titleId=797DC`
- `POST /sync?titleId=797DC`

Tüm istekler `Authorization: Bearer <GATEWAY_SERVICE_TOKEN>` ister. Gateway'i
internet üzerinden doğrudan yayınlamayın; portal ile private ağ veya loopback
üzerinden konuşturun.

## Portal canlı modu

```dotenv
NODE_ENV=production
DATA_PROVIDER=playfab
HOST=127.0.0.1
PORT=3040
APP_ORIGIN=https://cedas.collbrai.com
SESSION_SECRET=<en-az-32-karakter-rastgele-secret>
PLAYFAB_TITLE_ID=797DC
PLAYFAB_DATA_URL=http://127.0.0.1:4180/
PLAYFAB_SERVICE_TOKEN=<gateway-ile-ayni-token>
```

Portal gateway sayfalarını cursor ile toplar, tekrar şema doğrulamasından geçirir
ve 15 saniyelik kısa cache uygular. Kimlik doğrulama da gateway'deki gerçek
scrypt parolalarına gider. Tenant ve kullanıcı kapsamı portal sunucusunda yeniden
uygulanır.

## KVKK/GDPR operasyonu

Çalışan `Veri Hakları` ekranından dışa aktarma veya silme talebi oluşturur.
Yönetici talebi işlediğinde gateway çalışan Custom ID'sini mevcut PlayFab
hesabına eşler (`CreateAccount=false`) ve Admin API işini başlatır. Job receipt
kimliği şifreli talep kaydına ve audit loguna yazılır. Silme kabul edilirse aynı
çalışanın gateway'deki olayları da kaldırılır.

## Yayın kontrolü

```bash
npm ci
npm run check
curl -fsS -H "Authorization: Bearer $GATEWAY_SERVICE_TOKEN" \
  "http://127.0.0.1:4180/health?titleId=$PLAYFAB_TITLE_ID"
curl -fsS "http://127.0.0.1:3040/health/ready"
```

Gateway sağlık cevabı `lastSyncAt`, `eventCount`, `rejectedEventCount`,
`ingestionLagSeconds` ve `privacyReady` alanlarını içerir. Sağlık başarısızken
portal sağlayıcısını demo'dan canlıya çevirmeyin.
