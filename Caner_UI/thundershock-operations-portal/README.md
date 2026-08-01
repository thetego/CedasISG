# Thundershock Eğitim Analitiği

Unity 6 tabanlı CedasISG/Thundershock uygulamasının 11 telemetri olayını doğrulayan,
rol bazlı olarak sunan ve PlayFab Data Connection çıktısını tüketen operasyon
portalıdır.

## Bileşenler

- `src/`: React 19 + TypeScript yönetim arayüzü.
- `server/`: güvenli oturum, RBAC, CSRF, tenant kapsamı, audit ve KVKK talep akışı.
- `gateway/`: Azure Blob Parquet senkronizasyonu, şema doğrulama, SQLite outbox,
  gerçek kullanıcı doğrulama ve PlayFab GDPR yönetim çağrıları.
- `tests/`: auth, konfigürasyon, 11 olay, gateway ve şifreli veri deposu testleri.

Tarayıcı PlayFab veya Azure anahtarlarına erişmez. Akış:

```text
Unity WriteEvents
  -> PlayFab Data Connection
  -> özel Azure Blob container
  -> gateway (validate + dedupe + retention)
  -> portal server (session + RBAC + tenant scope)
  -> React arayüzü
```

## Yerel doğrulama

Node.js 20 veya üzeri gerekir.

```bash
npm ci
npm run check
```

`npm run check`; TypeScript build, Vite üretim paketi, sunucu/gateway sözdizimi
kontrolü ve bütün Node testlerini birlikte çalıştırır.

Demo başlatma:

```bash
SESSION_SECRET="$(openssl rand -hex 32)" DEMO_PASSWORD="demo123" npm start
```

Demo hesapları `ADMIN_DEMO`, `SUPER_ADMIN` ve çalışan ID'leridir. Demo şifresi
`DEMO_PASSWORD` değeridir. Canlı sağlayıcıda demo kullanıcıları ve demo şifre
yardımı kullanılmaz.

## Canlı bağlantı

1. PlayFab Game Manager'da event Data Connection'ını özel bir Azure Blob
   container'a yönlendirin.
2. Gateway için güçlü bir service token, Azure container SAS URL'si ve gerçek
   portal kullanıcılarını secret manager üzerinden tanımlayın.
3. Gateway'i yalnız loopback/private ağda çalıştırın.
4. Portalda `DATA_PROVIDER=playfab`, `PLAYFAB_DATA_URL` ve aynı service token'ı
   tanımlayın.
5. Önce gateway `/health`, sonra portal `/health/ready` uçlarının 200 döndüğünü
   doğrulayın.

Gerekli değişkenler `.env.example`, uç sözleşmeleri ve operasyon sırası
`docs/INTEGRATION.md` içindedir. `PLAYFAB_SECRET_KEY` yalnız veri dışa aktarma ve
silme taleplerini yönetici onayıyla PlayFab'a iletmek için gateway'de gerekir.

## Güvenlik kararları

- Cookie `HttpOnly`, `SameSite=Lax` ve HTTPS'te `Secure`/`__Host-` kapsamındadır.
- Durum değiştiren portal istekleri Origin ve CSRF doğrulamasından geçer.
- Gateway tüm uçlarda sabit zamanlı Bearer token karşılaştırması yapar.
- Kullanıcı şifreleri SQLite içinde scrypt hash + ayrı salt ile tutulur.
- Entegrasyon ayarları ve gizlilik talepleri AES-256-GCM ile şifrelenir.
- Çalışan yalnız kendisini, denetçi ekibini, yönetici tenant'ını görür.
- Olaylar `eventId` ile tekilleştirilir; hatalı satırlar karantinaya alınır.
- Lokal olay saklama süresi varsayılan 365 gündür ve yapılandırılabilir.

## Canlıya geçiş kapısı

Canlı mod aşağıdakiler olmadan etkinleştirilmemelidir:

- `AZURE_BLOB_CONTAINER_URL`
- en az 32 karakter `GATEWAY_SERVICE_TOKEN`
- güçlü `SESSION_SECRET`
- en az 8 karakterli kullanıcı parolalarını içeren `GATEWAY_USERS_JSON`
- gizlilik silme/dışa aktarma işlemleri için `PLAYFAB_SECRET_KEY`
- gateway ve portal sağlık kontrollerinin başarılı olması

Portalın demo etiketi canlı veri gelmeden kaybolmaz; arayüz demo metriklerini
operasyonel veri gibi göstermez.
