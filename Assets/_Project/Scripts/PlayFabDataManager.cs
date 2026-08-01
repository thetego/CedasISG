using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.EventsModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SafetyTraining
{
    public class PlayFabDataManager : MonoBehaviour
    {
        private const int TelemetrySchemaVersion = 2;
        private const int OutboxSchemaVersion = 1;
        private const string ConsentKey = "cedas.telemetry.consent";
        private const string ConsentVersionKey = "cedas.telemetry.consent.version";
        private const string ConsentTimestampKey = "cedas.telemetry.consent.timestamp";

        public static PlayFabDataManager Instance { get; private set; }

        [Header("━━━ PLAYFAB ━━━")]
        [Tooltip("PlayFab Title ID — zorunlu")]
        public string titleId;

        [Tooltip("PlayFab Title Data'daki whitelist anahtarı")]
        public string whitelistTitleDataKey = "PlayerWhitelist";

        [Header("━━━ TELEMETRİ TESLİMATI ━━━")]
        [Tooltip("PlayFab WriteEvents isteği başına en fazla olay sayısı (PlayFab üst sınırı: 200)")]
        [Range(1, 200)] public int eventBatchSize = 50;

        [Tooltip("Diskte tutulacak en fazla bekleyen olay")]
        [Min(100)] public int maxStoredEvents = 5000;

        [Tooltip("Gönderilemeyen olayların diskte tutulacağı azami gün")]
        [Min(1)] public int maxEventAgeDays = 7;

        [Tooltip("İlk yeniden deneme bekleme süresi")]
        [Min(1f)] public float retryBaseSeconds = 2f;

        [Tooltip("Yeniden denemeler arasındaki azami bekleme süresi")]
        [Min(10f)] public float retryMaxSeconds = 300f;

        [Tooltip("PlayFab custom event namespace")]
        public string eventNamespace = "custom.thundershock";

        [Header("━━━ GİZLİLİK ━━━")]
        [Tooltip("Açık kullanıcı tercihi olmadan telemetri toplamayı engeller")]
        public bool requireTelemetryConsent = true;

        [Tooltip("Metin değiştiğinde kullanıcının tercihi yeniden sorulur")]
        public string privacyNoticeVersion = "2026-08-01";

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        // ─── Oyuncu verisi (doküman §4.1) ───
        public string CurrentPlayerId    { get; private set; }
        public string CurrentDisplayName { get; private set; }
        public string CurrentPlayerRole  { get; private set; }
        public int    CurrentPlayerLevel { get; private set; }
        public long   CurrentPlayerXp    { get; private set; }
        public string CurrentPlayerCreatedAt { get; private set; }
        public string CurrentPlayerLastLogin { get; private set; }

        private bool   _isLoggedIn;
        private string _playFabId;
        private string _currentLevelId;
        private string _sessionId;

        [Serializable]
        private sealed class PendingTelemetryEvent
        {
            public string eventId;
            public string eventName;
            public string employeeId;
            public string clientTimestamp;
            public string payloadJson;
            public long createdAtUnixMs;
            public long nextAttemptUnixMs;
            public int attempts;
        }

        [Serializable]
        private sealed class TelemetryOutboxFile
        {
            public int schemaVersion = OutboxSchemaVersion;
            public List<PendingTelemetryEvent> events = new List<PendingTelemetryEvent>();
        }

        // Her eklemede diske yazılan, uygulama kapanınca kaybolmayan outbox.
        private readonly List<PendingTelemetryEvent> _pendingEvents =
            new List<PendingTelemetryEvent>();
        private string _outboxPath;
        private bool _flushInProgress;
        private float _nextFlushCheck;
        private bool _currentLevelActive;

        // Callback'ler
        private Action          _onLoginSuccess;
        private Action<string>  _onLoginFailed;

        // Zaman takibi
        private float _levelStartTime;
        private float _sequenceStartTime;
        private float _actionStartTime;
        private string _sequenceStartUtc;

        // Doküman §4.6 Quiz Summary — aktif level boyunca biriktirilir, LogLevelCompleted'de gönderilir
        private int _quizTotalQuestions;
        private int _quizCorrectAnswers;
        private readonly Dictionary<string, bool> _quizResults =
            new Dictionary<string, bool>();

        public int PendingEventCount => _pendingEvents.Count;
        public bool HasExplicitTelemetryDecision =>
            PlayerPrefs.GetString(ConsentVersionKey, string.Empty) == privacyNoticeVersion;
        public bool HasTelemetryConsent => !requireTelemetryConsent ||
            (HasExplicitTelemetryDecision && PlayerPrefs.GetInt(ConsentKey, 0) == 1);

        // ═══════════════════════════════════════════════════════
        // PLAYER ENTRY MODEL
        // ═══════════════════════════════════════════════════════

        [Serializable]
        public class PlayerEntry
        {
            public string playerId;
            public string displayName;
            public string role;

            // ── Doküman §4.1 Player modeli — whitelist JSON'da bu alanlar varsa parse edilir ──
            public int    level;
            public long   xp;
            public string createdAt;
            public string lastLogin;
        }

        [Serializable]
        private class WhitelistJson
        {
            public List<PlayerEntry> players;
        }

        // ═══════════════════════════════════════════════════════
        // DRAG & DROP PLACEMENT MODEL (doküman §4.4.1)
        // ═══════════════════════════════════════════════════════

        public struct DragDropPlacement
        {
            public string item;
            public string droppedOn;
            public bool   correct;

            public DragDropPlacement(string item, string droppedOn, bool correct)
            {
                this.item = item;
                this.droppedOn = droppedOn;
                this.correct = correct;
            }
        }

        // ═══════════════════════════════════════════════════════
        // UNITY
        // ═══════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<PlayFabDataManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var host = new GameObject("PlayFabDataManager (Runtime)");
            host.AddComponent<PlayFabDataManager>();
        }

        private void Awake()
        {
            if (Instance == null || Instance == this)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (string.IsNullOrEmpty(_outboxPath))
                {
                    _outboxPath = Path.Combine(Application.persistentDataPath, "cedas-telemetry-outbox.json");
                    LoadOutbox();
                }
            }
            else
            {
                if (Instance != this)
                    Instance.ApplySerializedConfiguration(this);
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(titleId))
                PlayFabSettings.TitleId = titleId;

            if (string.IsNullOrEmpty(PlayFabSettings.TitleId))
                Debug.LogError("[PlayFabDataManager] PlayFab Title ID yapılandırılmamış.");
        }

        private void Update()
        {
            if (!_isLoggedIn || _flushInProgress || Time.unscaledTime < _nextFlushCheck)
                return;

            _nextFlushCheck = Time.unscaledTime + 2f;
            FlushPendingEvents();
        }

        private void ApplySerializedConfiguration(PlayFabDataManager source)
        {
            if (source == null) return;
            if (!string.IsNullOrWhiteSpace(source.titleId)) titleId = source.titleId;
            if (!string.IsNullOrWhiteSpace(source.whitelistTitleDataKey))
                whitelistTitleDataKey = source.whitelistTitleDataKey;
            if (!string.IsNullOrWhiteSpace(source.eventNamespace)) eventNamespace = source.eventNamespace;
            if (!string.IsNullOrWhiteSpace(source.privacyNoticeVersion))
                privacyNoticeVersion = source.privacyNoticeVersion;

            eventBatchSize = Mathf.Clamp(source.eventBatchSize, 1, 200);
            maxStoredEvents = Mathf.Max(100, source.maxStoredEvents);
            maxEventAgeDays = Mathf.Max(1, source.maxEventAgeDays);
            retryBaseSeconds = Mathf.Max(1f, source.retryBaseSeconds);
            retryMaxSeconds = Mathf.Max(10f, source.retryMaxSeconds);
            requireTelemetryConsent = source.requireTelemetryConsent;
            debugMode = source.debugMode;

            if (!string.IsNullOrEmpty(titleId))
                PlayFabSettings.TitleId = titleId;
        }

        // ═══════════════════════════════════════════════════════
        // WHİTELİST ÇEKME
        // ═══════════════════════════════════════════════════════

        // Legacy Title Data erişimi için önceden oluşturulmuş, salt-okuyucu hesap.
        // CreateAccount=false kullanılır; istemci yeni hesap üretemez.
        private const string WhitelistReaderCustomId = "whitelist_reader";

        /// <summary>
        /// PlayFab Title Data'dan whitelist'i çeker.
        /// Önce paylaşılan (cihaza özel olmayan) bir login yapar, ardından GetTitleData çağırır.
        /// </summary>
        public void FetchWhitelist(
            Action<List<PlayerEntry>> onSuccess,
            Action<string>           onFailed)
        {
            if (debugMode)
                Debug.Log("[PlayFabDataManager] Whitelist çekiliyor...");

            // Title Data okuyabilmek için paylaşılan hesapla giriş
            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest
                {
                    CustomId      = WhitelistReaderCustomId,
                    CreateAccount = false
                },
                _ => FetchTitleData(onSuccess, onFailed),
                err => onFailed?.Invoke(err.GenerateErrorReport())
            );
        }

        private void FetchTitleData(
            Action<List<PlayerEntry>> onSuccess,
            Action<string>           onFailed)
        {
            PlayFabClientAPI.GetTitleData(
                new GetTitleDataRequest { Keys = new List<string> { whitelistTitleDataKey } },
                result =>
                {
                    if (!result.Data.TryGetValue(whitelistTitleDataKey, out string json))
                    {
                        onFailed?.Invoke($"Title Data'da '{whitelistTitleDataKey}' anahtarı bulunamadı.");
                        return;
                    }

                    try
                    {
                        var parsed = JsonUtility.FromJson<WhitelistJson>(json);
                        if (parsed?.players == null || parsed.players.Count == 0)
                        {
                            onFailed?.Invoke("Whitelist boş veya hatalı JSON.");
                            return;
                        }

                        var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        parsed.players.RemoveAll(player =>
                            player == null || string.IsNullOrWhiteSpace(player.playerId) ||
                            string.IsNullOrWhiteSpace(player.displayName) ||
                            !uniqueIds.Add(player.playerId.Trim()));
                        foreach (var player in parsed.players)
                        {
                            player.playerId = player.playerId.Trim();
                            player.displayName = player.displayName.Trim();
                            if (player.role != "manager" && player.role != "inspector" &&
                                player.role != "admin" && player.role != "trainee")
                                player.role = "trainee";
                        }

                        if (parsed.players.Count == 0)
                        {
                            onFailed?.Invoke("Whitelist geçerli çalışan kaydı içermiyor.");
                            return;
                        }

                        if (debugMode)
                            Debug.Log($"<color=lime>[PlayFabDataManager] {parsed.players.Count} oyuncu yüklendi.</color>");

                        onSuccess?.Invoke(parsed.players);
                    }
                    catch (Exception e)
                    {
                        onFailed?.Invoke($"JSON parse hatası: {e.Message}");
                    }
                },
                err => onFailed?.Invoke(err.GenerateErrorReport())
            );
        }

        // ═══════════════════════════════════════════════════════
        // OYUNCU GİRİŞİ
        // ═══════════════════════════════════════════════════════

        public void LoginWithPlayer(
            PlayerEntry    entry,
            Action         onSuccess = null,
            Action<string> onFailed  = null)
        {
            if (entry == null)
            {
                onFailed?.Invoke("Geçersiz oyuncu.");
                return;
            }

            CurrentPlayerId       = entry.playerId;
            CurrentDisplayName    = entry.displayName;
            CurrentPlayerRole     = entry.role;
            CurrentPlayerLevel    = entry.level;
            CurrentPlayerXp       = entry.xp;
            CurrentPlayerCreatedAt = entry.createdAt;
            CurrentPlayerLastLogin = DateTime.UtcNow.ToString("o");
            _onLoginSuccess    = onSuccess;
            _onLoginFailed     = onFailed;

            if (debugMode)
                Debug.Log($"[PlayFabDataManager] Giriş: {entry.displayName} ({entry.playerId})");

            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest
                {
                    CustomId      = entry.playerId,
                    CreateAccount = false
                },
                OnLoginSuccess,
                OnLoginError
            );
        }

        private void OnLoginSuccess(LoginResult result)
        {
            _isLoggedIn = true;
            _playFabId  = result.PlayFabId;

            if (result.NewlyCreated || string.IsNullOrEmpty(CurrentPlayerCreatedAt))
                CurrentPlayerCreatedAt = CurrentPlayerLastLogin;

            if (debugMode)
                Debug.Log($"<color=lime>[PlayFabDataManager] Giriş başarılı: {CurrentDisplayName} → {_playFabId}</color>");

            FlushPendingEvents();
            _onLoginSuccess?.Invoke();
            _onLoginSuccess = null;
            _onLoginFailed  = null;
        }

        private void OnLoginError(PlayFabError error)
        {
            string msg = error.GenerateErrorReport();
            Debug.LogError($"[PlayFabDataManager] Giriş hatası: {msg}");
            _onLoginFailed?.Invoke(msg);
            _onLoginSuccess = null;
            _onLoginFailed  = null;
        }

        public void SetTelemetryConsent(bool allowed)
        {
            PlayerPrefs.SetInt(ConsentKey, allowed ? 1 : 0);
            PlayerPrefs.SetString(ConsentVersionKey, privacyNoticeVersion);
            PlayerPrefs.SetString(ConsentTimestampKey, DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();

            if (!allowed)
            {
                ClearPendingEventsForPlayer(CurrentPlayerId);
                if (debugMode)
                    Debug.Log("[PlayFabDataManager] Telemetri reddedildi; kullanıcıya ait bekleyen olaylar silindi.");
            }
            else if (_isLoggedIn)
            {
                FlushPendingEvents();
            }
        }

        public void RevokeTelemetryConsent()
        {
            SetTelemetryConsent(false);
        }

        private void FlushPendingEvents()
        {
            if (_flushInProgress || !_isLoggedIn || !HasTelemetryConsent ||
                string.IsNullOrWhiteSpace(CurrentPlayerId))
                return;

            PurgeExpiredEvents();
            long now = UtcNowUnixMs();
            int batchLimit = Mathf.Clamp(eventBatchSize, 1, 200);
            var records = new List<PendingTelemetryEvent>(batchLimit);

            foreach (var pending in _pendingEvents)
            {
                if (records.Count >= batchLimit) break;
                if (!string.Equals(pending.employeeId, CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (pending.nextAttemptUnixMs > now) continue;
                records.Add(pending);
            }

            if (records.Count == 0) return;

            var events = new List<EventContents>(records.Count);
            foreach (var record in records)
            {
                DateTime parsedTimestamp;
                DateTime? originalTimestamp = DateTime.TryParse(
                    record.clientTimestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsedTimestamp)
                    ? parsedTimestamp.ToUniversalTime()
                    : (DateTime?)null;

                events.Add(new EventContents
                {
                    EventNamespace = string.IsNullOrWhiteSpace(eventNamespace)
                        ? "custom.thundershock"
                        : eventNamespace,
                    Name = record.eventName,
                    OriginalId = record.eventId,
                    OriginalTimestamp = originalTimestamp,
                    PayloadJSON = record.payloadJson,
                    CustomTags = new Dictionary<string, string>
                    {
                        { "schemaVersion", TelemetrySchemaVersion.ToString(CultureInfo.InvariantCulture) },
                        { "appVersion", Application.version }
                    }
                });
            }

            _flushInProgress = true;
            try
            {
                PlayFabEventsAPI.WriteEvents(
                    new WriteEventsRequest { Events = events },
                    _ => OnBatchSent(records),
                    error => OnBatchFailed(records, error));
            }
            catch (Exception error)
            {
                OnBatchFailed(records, error.Message);
            }
        }

        private void OnBatchSent(List<PendingTelemetryEvent> records)
        {
            var sentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records) sentIds.Add(record.eventId);
            _pendingEvents.RemoveAll(record => sentIds.Contains(record.eventId));
            _flushInProgress = false;
            PersistOutbox();

            if (debugMode)
                Debug.Log($"<color=cyan>[PlayFabDataManager] ✓ {records.Count} olay gönderildi, " +
                          $"bekleyen: {_pendingEvents.Count}</color>");

            _nextFlushCheck = Time.unscaledTime;
        }

        private void OnBatchFailed(List<PendingTelemetryEvent> records, PlayFabError error)
        {
            OnBatchFailed(records, error != null ? error.GenerateErrorReport() : "Bilinmeyen PlayFab hatası");
        }

        private void OnBatchFailed(List<PendingTelemetryEvent> records, string error)
        {
            long now = UtcNowUnixMs();
            foreach (var record in records)
            {
                record.attempts++;
                double exponent = Math.Min(record.attempts - 1, 12);
                double delaySeconds = Math.Min(
                    retryMaxSeconds,
                    retryBaseSeconds * Math.Pow(2d, exponent));
                double jitterSeconds = UnityEngine.Random.Range(0f, (float)(delaySeconds * 0.2d));
                record.nextAttemptUnixMs = now + (long)((delaySeconds + jitterSeconds) * 1000d);
            }

            _flushInProgress = false;
            PersistOutbox();
            Debug.LogWarning($"[PlayFabDataManager] Batch gönderimi başarısız; " +
                             $"{records.Count} olay yeniden denenecek: {error}");
        }

        // ═══════════════════════════════════════════════════════
        // LEVEL EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogLevelStarted(string levelId)
        {
            _currentLevelId = CanonicalLevelId(levelId);
            _sessionId = Guid.NewGuid().ToString("N");
            _levelStartTime = Time.time;
            _currentLevelActive = true;
            _quizTotalQuestions = 0;
            _quizCorrectAnswers = 0;
            _quizResults.Clear();

            SendEvent("LevelStarted", new Dictionary<string, object>
            {
                { "levelId",     _currentLevelId },
                { "displayName", CurrentDisplayName }
            });
        }

        /// <summary>
        /// Doküman §4.2 Level Progress modeli. Aynı level içinde quiz cevaplanmışsa
        /// §4.6 Quiz Summary'i de otomatik olarak ayrı bir event ile gönderir.
        /// </summary>
        public void LogLevelCompleted(string levelId, int score, int mistakes)
        {
            int timeSpent = Mathf.RoundToInt(Time.time - _levelStartTime);
            float performanceRate = Mathf.Clamp01(1f - mistakes * 0.05f);

            SendEvent("LevelCompleted", new Dictionary<string, object>
            {
                { "levelId",        CanonicalLevelId(levelId) },
                { "completed",      true },
                { "score",          score },
                { "timeSpent",      timeSpent },
                { "mistakes",       mistakes },
                { "completionRate", 1f },
                { "performanceRate", performanceRate }
            });

            if (_quizTotalQuestions > 0)
                LogQuizSummary(levelId);

            _currentLevelActive = false;
        }

        // ═══════════════════════════════════════════════════════
        // SEKANS EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogSequenceStarted(string sequenceId, string levelId)
        {
            _sequenceStartTime = Time.time;
            _sequenceStartUtc = DateTime.UtcNow.ToString("o");

            SendEvent("SequenceStarted", new Dictionary<string, object>
            {
                { "sequenceId", sequenceId },
                { "levelId",    levelId },
                { "startTime",  _sequenceStartUtc }
            });
        }

        public void LogSequenceCompleted(string sequenceId, string levelId, int mistakes)
        {
            SendEvent("SequenceCompleted", new Dictionary<string, object>
            {
                { "sequenceId", sequenceId },
                { "levelId",    levelId },
                { "timeSpent",  Mathf.RoundToInt(Time.time - _sequenceStartTime) },
                { "mistakes",   mistakes },
                { "completed",  true },
                { "startTime",  _sequenceStartUtc ?? string.Empty },
                { "endTime",    DateTime.UtcNow.ToString("o") }
            });
        }

        // ═══════════════════════════════════════════════════════
        // ACTION EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogActionStarted(string actionId)
        {
            _actionStartTime = Time.time;
        }

        /// <summary>
        /// Doküman §4.4 Action modeli. actionType "click" ise objectId, §4.4.3
        /// Click/Inspect alt modelinin karşılığıdır (bu tip için ayrı bir event yok,
        /// doküman §5'teki event kataloğunda da Click/Inspect için ayrı event tanımlı değil).
        /// </summary>
        public void LogActionCompleted(string actionId, string levelId, string sequenceId,
            string actionType, string objectId = null, string result = "success")
        {
            SendEvent("ActionCompleted", new Dictionary<string, object>
            {
                { "actionId",   actionId },
                { "levelId",    levelId },
                { "sequenceId", sequenceId },
                { "type",       actionType },
                { "objectId",   objectId },
                { "startTime",  Mathf.Max(0, Mathf.RoundToInt(_actionStartTime - _levelStartTime)) },
                { "endTime",    Mathf.Max(0, Mathf.RoundToInt(Time.time - _levelStartTime)) },
                { "duration",   Mathf.RoundToInt(Time.time - _actionStartTime) },
                { "result",     result }
            });
        }

        // ═══════════════════════════════════════════════════════
        // QUIZ EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogQuizAnswered(string actionId, string levelId, string sequenceId,
            string questionId, string selectedAnswer, string correctAnswer,
            bool isCorrect, int attempts, int timeSpent)
        {
            // attempts are logged individually, but QuizSummary must count each
            // question only once and reflect its latest answer.
            _quizResults[questionId] = isCorrect;
            _quizTotalQuestions = _quizResults.Count;
            _quizCorrectAnswers = 0;
            foreach (bool result in _quizResults.Values)
                if (result) _quizCorrectAnswers++;

            SendEvent("QuizAnswered", new Dictionary<string, object>
            {
                { "actionId",       actionId },
                { "levelId",        levelId },
                { "sequenceId",     sequenceId },
                { "questionId",     questionId },
                { "selectedAnswer", selectedAnswer },
                { "correctAnswer",  correctAnswer },
                { "isCorrect",      isCorrect },
                { "attempts",       attempts },
                { "attemptIndex",   attempts },
                { "timeSpent",      timeSpent }
            });
        }

        /// <summary>
        /// Doküman §4.6 Quiz Summary — LogLevelCompleted tarafından otomatik tetiklenir.
        /// </summary>
        private void LogQuizSummary(string levelId)
        {
            int wrong = _quizTotalQuestions - _quizCorrectAnswers;
            float accuracy = _quizTotalQuestions > 0
                ? (float)_quizCorrectAnswers / _quizTotalQuestions
                : 0f;

            SendEvent("QuizSummary", new Dictionary<string, object>
            {
                { "levelId",        levelId },
                { "totalQuestions", _quizTotalQuestions },
                { "correctAnswers", _quizCorrectAnswers },
                { "wrongAnswers",   wrong },
                { "accuracy",       accuracy }
            });
        }

        // ═══════════════════════════════════════════════════════
        // DRAG & DROP EVENTLERİ
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Doküman §4.4.1 Drag & Drop modeli — bir action'a ait tüm denemeler
        /// (yanlış + doğru) tek bir "placements" listesi içinde, action tamamlandığında
        /// (son doğru drop'ta) tek event olarak gönderilir.
        /// </summary>
        public void LogDragDropAttempt(string actionId, string levelId, string sequenceId,
            string targetObject, int attempts, List<DragDropPlacement> placements)
        {
            var placementList = new List<object>();
            if (placements != null)
            {
                foreach (var p in placements)
                {
                    placementList.Add(new Dictionary<string, object>
                    {
                        { "item",      p.item },
                        { "droppedOn", p.droppedOn },
                        { "correct",   p.correct }
                    });
                }
            }

            SendEvent("DragDropAttempt", new Dictionary<string, object>
            {
                { "actionId",     actionId },
                { "levelId",      levelId },
                { "sequenceId",   sequenceId },
                { "targetObject", targetObject },
                { "attempts",     attempts },
                { "placements",   placementList }
            });
        }

        // ═══════════════════════════════════════════════════════
        // HATA EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogMistakeRecorded(string actionId, string mistakeType, int severity)
        {
            string levelId = SequenceManager.Instance?.CurrentLevelID ?? _currentLevelId ?? string.Empty;
            string sequenceId = SequenceManager.Instance?.CurrentSequenceID ?? string.Empty;
            LogMistakeRecorded(actionId, levelId, sequenceId, mistakeType, severity);
        }

        public void LogMistakeRecorded(string actionId, string levelId, string sequenceId,
            string mistakeType, int severity)
        {
            SendEvent("MistakeRecorded", new Dictionary<string, object>
            {
                { "mistakeType", mistakeType },
                { "actionId",    actionId },
                { "levelId",     levelId },
                { "sequenceId",  sequenceId },
                { "severity",    severity },
                { "timestamp",   DateTime.UtcNow.ToString("o") }
            });
        }

        public void LogSurveyCompleted(SurveySessionResult result)
        {
            if (result == null) return;

            var questionResults = new List<object>();
            for (int questionIndex = 0; questionIndex < result.questionResults.Count; questionIndex++)
            {
                var question = result.questionResults[questionIndex];
                questionResults.Add(new Dictionary<string, object>
                {
                    { "questionId",           $"{result.actionID}:survey:{questionIndex + 1}" },
                    { "questionText",         question.questionText },
                    { "selectedOptionIndex",  question.selectedOptionIndex },
                    { "isCorrect",            question.isCorrect }
                });
            }

            var photoResults = new List<object>();
            foreach (var photo in result.photoResults)
            {
                photoResults.Add(new Dictionary<string, object>
                {
                    { "slotLabel",      photo.slotLabel },
                    { "wasCaptured",    photo.wasCaptured },
                    { "isAligned",      photo.isAligned },
                    { "alignmentScore", photo.alignmentScore }
                });
            }

            SendEvent("SurveyCompleted", new Dictionary<string, object>
            {
                { "actionId",        result.actionID },
                { "levelId",         result.levelID },
                { "sequenceId",      result.sequenceID },
                { "questionResults", questionResults },
                { "photoResults",    photoResults },
                { "completionTime",  result.completionTime }
            });
        }

        // ═══════════════════════════════════════════════════════
        // OTURUM SONU
        // ═══════════════════════════════════════════════════════

        public void LogSessionEnded(string levelId, string reason = "application_quit")
        {
            SendEvent("SessionEnded", new Dictionary<string, object>
            {
                { "levelId", CanonicalLevelId(levelId) },
                { "reason", reason },
                { "timeSpent", Mathf.Max(0, Mathf.RoundToInt(Time.time - _levelStartTime)) }
            });
            _currentLevelActive = false;
        }

        // ═══════════════════════════════════════════════════════
        // CORE — Doküman §6 Event Payload Formatı:
        // { eventType, clientTimestamp, employeeId, payload: {...} }
        //
        // Olaylar önce kalıcı outbox'a yazılır, ardından PlayFab Events API ile
        // toplu gönderilir. OriginalId, yeniden denemelerde deduplication anahtarıdır.
        // ═══════════════════════════════════════════════════════

        private void SendEvent(string eventName, Dictionary<string, object> payload)
        {
            if (!HasTelemetryConsent)
            {
                if (debugMode)
                    Debug.Log($"[PlayFabDataManager] Telemetri tercihi yok; olay toplanmadı: {eventName}");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentPlayerId))
            {
                Debug.LogWarning($"[PlayFabDataManager] Oyuncu kimliği yok; olay toplanmadı: {eventName}");
                return;
            }

            string eventId = Guid.NewGuid().ToString("N");
            string clientTimestamp = DateTime.UtcNow.ToString("o");
            var documentPayload = new Dictionary<string, object>(payload);
            documentPayload["sessionId"] = _sessionId ?? string.Empty;
            documentPayload["playerId"] = CurrentPlayerId;
            documentPayload["role"] = CurrentPlayerRole ?? string.Empty;
            documentPayload["schemaVersion"] = TelemetrySchemaVersion;
            documentPayload["eventId"] = eventId;
            documentPayload["appVersion"] = Application.version;
            documentPayload["unityVersion"] = Application.unityVersion;
            if (!documentPayload.ContainsKey("timestamp"))
                documentPayload["timestamp"] = clientTimestamp;

            AddCanonicalIdentityKeys(documentPayload);

            var envelope = new Dictionary<string, object>
            {
                { "eventType",       eventName },
                { "eventId",         eventId },
                { "schemaVersion",   TelemetrySchemaVersion },
                { "clientTimestamp", clientTimestamp },
                { "employeeId",      CurrentPlayerId },
                { "payload",         documentPayload }
            };

            var serializer = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            var pending = new PendingTelemetryEvent
            {
                eventId = eventId,
                eventName = eventName,
                employeeId = CurrentPlayerId,
                clientTimestamp = clientTimestamp,
                payloadJson = serializer.SerializeObject(envelope),
                createdAtUnixMs = UtcNowUnixMs(),
                nextAttemptUnixMs = 0,
                attempts = 0
            };

            PurgeExpiredEvents();
            while (_pendingEvents.Count >= Mathf.Max(100, maxStoredEvents))
            {
                Debug.LogError("[PlayFabDataManager] Telemetri outbox kapasitesi doldu; en eski olay kaldırıldı.");
                _pendingEvents.RemoveAt(0);
            }

            _pendingEvents.Add(pending);
            PersistOutbox();

            if (debugMode)
                Debug.Log($"[PlayFabDataManager] Olay outbox'a yazıldı: {eventName} ({eventId})");

            if (_isLoggedIn && !_flushInProgress)
                FlushPendingEvents();
        }

        private void AddCanonicalIdentityKeys(Dictionary<string, object> payload)
        {
            string rawLevel = GetString(payload, "levelId");
            string levelId = CanonicalLevelId(rawLevel);
            if (!string.IsNullOrEmpty(rawLevel) && rawLevel != levelId)
                payload["sourceLevelId"] = rawLevel;
            if (!string.IsNullOrEmpty(levelId)) payload["levelId"] = levelId;

            string sequenceId = GetString(payload, "sequenceId");
            if (!string.IsNullOrEmpty(sequenceId))
                payload["sequenceKey"] = JoinIdentity(levelId, sequenceId);

            string actionId = GetString(payload, "actionId");
            if (!string.IsNullOrEmpty(actionId))
            {
                int actionIndex = GetInt(payload, "actionIndex", SequenceManager.Instance?.CurrentActionIndex ?? -1);
                if (actionIndex >= 0) payload["actionIndex"] = actionIndex;
                payload["actionKey"] = actionIndex >= 0
                    ? JoinIdentity(levelId, sequenceId, (actionIndex + 1).ToString("D3"), actionId)
                    : JoinIdentity(levelId, sequenceId, actionId);
            }

            string questionId = GetString(payload, "questionId");
            if (!string.IsNullOrEmpty(questionId))
                payload["questionKey"] = JoinIdentity(GetString(payload, "actionKey"), questionId);
        }

        private static string GetString(Dictionary<string, object> payload, string key)
        {
            object value;
            return payload.TryGetValue(key, out value) && value != null
                ? value.ToString().Trim()
                : string.Empty;
        }

        private static int GetInt(Dictionary<string, object> payload, string key, int fallback)
        {
            object value;
            int parsed;
            return payload.TryGetValue(key, out value) && value != null &&
                   int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static string JoinIdentity(params string[] parts)
        {
            var normalized = new List<string>();
            foreach (string part in parts)
            {
                string value = NormalizeIdentitySegment(part);
                if (!string.IsNullOrEmpty(value)) normalized.Add(value);
            }
            return string.Join("/", normalized);
        }

        private static string NormalizeIdentitySegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = new List<char>(value.Length);
            bool previousDash = false;
            foreach (char raw in value.Trim().ToLowerInvariant())
            {
                bool allowed = char.IsLetterOrDigit(raw);
                if (allowed)
                {
                    chars.Add(raw);
                    previousDash = false;
                }
                else if (!previousDash)
                {
                    chars.Add('-');
                    previousDash = true;
                }
            }

            return new string(chars.ToArray()).Trim('-');
        }

        public static string CanonicalLevelId(string levelId)
        {
            string normalized = NormalizeIdentitySegment(levelId);
            switch (normalized)
            {
                case "level-1":
                case "level1":
                case "lvl-1":
                case "lvl1":
                    return "level-1";
                case "level-2":
                case "level2":
                case "lvl-2":
                case "lvl2":
                    return "level-2";
                case "level-3":
                case "level3":
                case "lvl-3":
                case "lvl3":
                case "newlevel":
                    return "level-3";
                default:
                    return normalized;
            }
        }

        private static long UtcNowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private void LoadOutbox()
        {
            _pendingEvents.Clear();
            if (string.IsNullOrWhiteSpace(_outboxPath) || !File.Exists(_outboxPath)) return;

            try
            {
                var stored = JsonUtility.FromJson<TelemetryOutboxFile>(File.ReadAllText(_outboxPath));
                if (stored == null || stored.events == null) return;
                _pendingEvents.AddRange(stored.events);
                PurgeExpiredEvents();
                if (debugMode)
                    Debug.Log($"[PlayFabDataManager] Disk outbox yüklendi: {_pendingEvents.Count} olay.");
            }
            catch (Exception error)
            {
                string corruptPath = _outboxPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { File.Move(_outboxPath, corruptPath); }
                catch { /* Asıl parse hatasını koru. */ }
                Debug.LogError($"[PlayFabDataManager] Outbox okunamadı ve karantinaya alındı: {error.Message}");
            }
        }

        private void PersistOutbox()
        {
            if (string.IsNullOrWhiteSpace(_outboxPath)) return;

            try
            {
                string directory = Path.GetDirectoryName(_outboxPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string tempPath = _outboxPath + ".tmp";
                string json = JsonUtility.ToJson(new TelemetryOutboxFile
                {
                    schemaVersion = OutboxSchemaVersion,
                    events = new List<PendingTelemetryEvent>(_pendingEvents)
                });
                File.WriteAllText(tempPath, json);

                if (File.Exists(_outboxPath))
                {
                    try { File.Replace(tempPath, _outboxPath, null); }
                    catch
                    {
                        File.Delete(_outboxPath);
                        File.Move(tempPath, _outboxPath);
                    }
                }
                else
                {
                    File.Move(tempPath, _outboxPath);
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"[PlayFabDataManager] Outbox diske yazılamadı: {error.Message}");
            }
        }

        private void PurgeExpiredEvents()
        {
            long cutoff = UtcNowUnixMs() - (long)Mathf.Max(1, maxEventAgeDays) * 24L * 60L * 60L * 1000L;
            int removed = _pendingEvents.RemoveAll(record =>
                record == null || string.IsNullOrWhiteSpace(record.eventId) ||
                string.IsNullOrWhiteSpace(record.payloadJson) || record.createdAtUnixMs < cutoff);
            if (removed > 0) PersistOutbox();
        }

        private void ClearPendingEventsForPlayer(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                _pendingEvents.Clear();
            else
                _pendingEvents.RemoveAll(record =>
                    string.Equals(record.employeeId, employeeId, StringComparison.OrdinalIgnoreCase));
            PersistOutbox();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PersistOutbox();
        }

        private void OnApplicationQuit()
        {
            if (_isLoggedIn && _currentLevelActive && !string.IsNullOrEmpty(_currentLevelId))
                LogSessionEnded(_currentLevelId, "application_quit");
            PersistOutbox();
        }
    }
}
