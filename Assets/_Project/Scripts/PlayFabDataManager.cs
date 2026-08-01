using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace SafetyTraining
{
    public class PlayFabDataManager : MonoBehaviour
    {
        public static PlayFabDataManager Instance { get; private set; }

        [Header("━━━ PLAYFAB ━━━")]
        [Tooltip("PlayFab Title ID — zorunlu")]
        public string titleId;

        [Tooltip("PlayFab Title Data'daki whitelist anahtarı")]
        public string whitelistTitleDataKey = "PlayerWhitelist";

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

        // ─── Kalıcı offline kuyruk (§9) — giriş öncesi VE gönderim hatası durumunda
        // eventler burada birikir; disk üzerinde saklandığı için crash/force-close
        // sonrası da kaybolmaz. ───
        private class QueuedEvent
        {
            public string EventName;
            public object Body;
        }

        private readonly List<QueuedEvent> _offlineQueue = new List<QueuedEvent>();
        private bool     _isFlushingQueue;
        private Coroutine _retryCoroutine;
        private float    _currentRetryDelay = InitialRetryDelaySeconds;
        private const float InitialRetryDelaySeconds = 3f;
        private const float MaxRetryDelaySeconds = 60f;
        private const string QueueFileName = "pf_event_queue.json";
        private string QueueFilePath => Path.Combine(Application.persistentDataPath, QueueFileName);

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

        // Aynı action+mistakeType kaç kez tekrarlandı — MistakeRecorded.severity/occurrence için (§7)
        private readonly Dictionary<string, int> _mistakeRepeatCounts =
            new Dictionary<string, int>();

        public enum MistakeSeverity { Minor = 1, Moderate = 2, Critical = 3 }

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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadQueueFromDisk();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(titleId))
                PlayFabSettings.TitleId = titleId;
        }

        // ═══════════════════════════════════════════════════════
        // KALICI KUYRUK — DİSK OKUMA/YAZMA
        // ═══════════════════════════════════════════════════════

        private void LoadQueueFromDisk()
        {
            try
            {
                if (!File.Exists(QueueFilePath))
                    return;

                string json = File.ReadAllText(QueueFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                if (PlayFabSimpleJson.DeserializeObject(json) is IList<object> items)
                {
                    foreach (var item in items)
                    {
                        if (item is IDictionary<string, object> dict &&
                            dict.TryGetValue("eventName", out object nameObj) &&
                            dict.TryGetValue("body", out object bodyObj))
                        {
                            _offlineQueue.Add(new QueuedEvent { EventName = nameObj as string, Body = bodyObj });
                        }
                    }
                }

                if (debugMode && _offlineQueue.Count > 0)
                    Debug.Log($"<color=yellow>[PlayFabDataManager] Önceki oturumdan {_offlineQueue.Count} bekleyen event diskten yüklendi.</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFabDataManager] Kuyruk diskten okunamadı: {e.Message}");
            }
        }

        private void PersistQueueToDisk()
        {
            try
            {
                var items = new List<object>();
                foreach (var q in _offlineQueue)
                {
                    items.Add(new Dictionary<string, object>
                    {
                        { "eventName", q.EventName },
                        { "body",      q.Body }
                    });
                }
                File.WriteAllText(QueueFilePath, PlayFabSimpleJson.SerializeObject(items));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFabDataManager] Kuyruk diske yazılamadı: {e.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════
        // WHİTELİST ÇEKME
        // ═══════════════════════════════════════════════════════

        // Title Data'yı okuyabilmek için PlayFab bir oturum ister; cihaza özel bir ID
        // yerine SABİT/paylaşılan bu ID kullanılıyor. Böylece kaç cihaz/kullanıcı olursa
        // olsun bu amaçla yalnızca TEK bir PlayFab hesabı oluşur (Development Mode'daki
        // 1.000 hesap kotasını cihaz başına ayrı ayrı tüketmemek için).
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
                    CreateAccount = true
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
                    CreateAccount = true
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

            TryFlushQueue();
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

        // ═══════════════════════════════════════════════════════
        // LEVEL EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogLevelStarted(string levelId)
        {
            _currentLevelId = levelId;
            _sessionId = Guid.NewGuid().ToString("N");
            _levelStartTime = Time.time;
            _quizTotalQuestions = 0;
            _quizCorrectAnswers = 0;
            _quizResults.Clear();
            _mistakeRepeatCounts.Clear();

            SendEvent("LevelStarted", new Dictionary<string, object>
            {
                { "levelId",     levelId },
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

            SendEvent("LevelCompleted", new Dictionary<string, object>
            {
                { "levelId",        levelId },
                { "completed",      true },
                { "score",          score },
                { "timeSpent",      timeSpent },
                { "mistakes",       mistakes },
                { "completionRate", Mathf.Clamp01(1f - mistakes * 0.05f) }
            });

            if (_quizTotalQuestions > 0)
                LogQuizSummary(levelId);
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
        /// Doküman §4.4 Action modeli. "type" artık ActionData.actionType enum değerinin
        /// tam adını taşır (ör. "CameraMove", "ModalWindow", "Fade") — eski 5-bucket
        /// sınıflandırma geriye dönük uyumluluk için "category" alanında ayrıca gönderilir.
        /// "attempts" o action üzerinde başarıya ulaşılana kadar kaydedilen yanlış deneme sayısıdır.
        /// </summary>
        public void LogActionCompleted(string actionId, string levelId, string sequenceId,
            string actionType, string actionCategory, string objectId = null,
            int attempts = 0, string result = "success")
        {
            SendEvent("ActionCompleted", new Dictionary<string, object>
            {
                { "actionId",   actionId },
                { "levelId",    levelId },
                { "sequenceId", sequenceId },
                { "type",       actionType },
                { "category",   actionCategory },
                { "objectId",   objectId },
                { "startTime",  Mathf.Max(0, Mathf.RoundToInt(_actionStartTime - _levelStartTime)) },
                { "endTime",    Mathf.Max(0, Mathf.RoundToInt(Time.time - _levelStartTime)) },
                { "duration",   Mathf.RoundToInt(Time.time - _actionStartTime) },
                { "attempts",   attempts },
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

        public void LogMistakeRecorded(string actionId, string mistakeType)
        {
            string levelId = SequenceManager.Instance?.CurrentLevelID ?? _currentLevelId ?? string.Empty;
            string sequenceId = SequenceManager.Instance?.CurrentSequenceID ?? string.Empty;
            LogMistakeRecorded(actionId, levelId, sequenceId, mistakeType);
        }

        /// <summary>
        /// Doküman §7 Mistake modeli. severity artık sabit değil — mistakeType ve aynı
        /// action üzerindeki tekrar sayısına göre otomatik hesaplanır (bkz. ComputeMistakeSeverity).
        /// </summary>
        public void LogMistakeRecorded(string actionId, string levelId, string sequenceId, string mistakeType)
        {
            int severity = ComputeMistakeSeverity(actionId, mistakeType, out int occurrence);

            SendEvent("MistakeRecorded", new Dictionary<string, object>
            {
                { "mistakeType", mistakeType },
                { "actionId",    actionId },
                { "levelId",     levelId },
                { "sequenceId",  sequenceId },
                { "severity",    severity },
                { "occurrence",  occurrence },
                { "timestamp",   DateTime.UtcNow.ToString("o") }
            });
        }

        // ═══════════════════════════════════════════════════════
        // SEKANS GİRİŞ REDDİ (kilitli/önkoşulu sağlanmamış sekansa girme denemesi)
        // ═══════════════════════════════════════════════════════

        public void LogSequenceEntryDenied(string sequenceId, string levelId,
            string[] missingPrerequisiteIds, string failAction)
        {
            SendEvent("SequenceEntryDenied", new Dictionary<string, object>
            {
                { "sequenceId",            sequenceId },
                { "levelId",               levelId },
                { "missingPrerequisites",  missingPrerequisiteIds != null
                                                ? new List<object>(missingPrerequisiteIds)
                                                : new List<object>() },
                { "failAction",            failAction }
            });
        }

        public void LogSurveyCompleted(SurveySessionResult result)
        {
            if (result == null) return;

            var questionResults = new List<object>();
            foreach (var question in result.questionResults)
            {
                questionResults.Add(new Dictionary<string, object>
                {
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

        public void LogSessionEnded(string levelId)
        {
            SendEvent("SessionEnded", new Dictionary<string, object>
            {
                { "levelId", levelId }
            });
        }

        // ═══════════════════════════════════════════════════════
        // CORE — Doküman §6 Event Payload Formatı:
        // { eventType, clientTimestamp, employeeId, payload: {...} }
        //
        // Not: "timestamp" ve "playerId" adlarını bilerek kullanmıyoruz —
        // PlayFab, WritePlayerEvent body'sinde bu iki adı kendi rezerve ettiği
        // event şemasıyla çakıştığı için reddediyor ("Field X is a reserved
        // PlayFab field and may not be overridden"). PlayFab zaten her event'e
        // kimin (oturum sahibi) ve ne zaman (sunucu saatiyle) yazdığını otomatik
        // ekliyor; clientTimestamp/employeeId alanlarımız ise istemci tarafındaki
        // gerçek değerleri (login olmadan kuyruğa alınan eventlerde bunlar sunucu
        // değerlerinden farklı olabilir) doküman formatına uygun şekilde taşır.
        // ═══════════════════════════════════════════════════════

        private void SendEvent(string eventName, Dictionary<string, object> payload)
        {
            // PlayFab reserves top-level playerId/timestamp. Keep its transport
            // envelope intact and add the document contract context to payload.
            var documentPayload = new Dictionary<string, object>(payload);
            documentPayload["sessionId"] = _sessionId ?? string.Empty;
            documentPayload["playerId"] = CurrentPlayerId ?? string.Empty;
            documentPayload["role"] = CurrentPlayerRole ?? string.Empty;
            if (!documentPayload.ContainsKey("timestamp"))
                documentPayload["timestamp"] = DateTime.UtcNow.ToString("o");

            var envelope = new Dictionary<string, object>
            {
                { "eventType",       eventName },
                { "clientTimestamp", DateTime.UtcNow.ToString("o") },
                { "employeeId",      CurrentPlayerId ?? string.Empty },
                { "payload",         documentPayload }
            };

            // Her event önce kalıcı kuyruğa yazılır (disk), sonra gönderim denenir.
            // Böylece giriş yapılmamışsa, ağ koparsa veya uygulama aniden kapanırsa
            // hiçbir event kaybolmaz — bir sonraki açılışta kaldığı yerden devam eder.
            _offlineQueue.Add(new QueuedEvent { EventName = eventName, Body = envelope });
            PersistQueueToDisk();

            if (debugMode)
                Debug.Log($"[PlayFabDataManager] Event kuyruğa alındı ({_offlineQueue.Count} bekliyor): {eventName}");

            TryFlushQueue();
        }

        private void TryFlushQueue()
        {
            if (!_isLoggedIn || _isFlushingQueue || _offlineQueue.Count == 0)
                return;

            _isFlushingQueue = true;
            SendNextQueuedEvent();
        }

        private void SendNextQueuedEvent()
        {
            if (_offlineQueue.Count == 0)
            {
                _isFlushingQueue = false;
                _currentRetryDelay = InitialRetryDelaySeconds;
                return;
            }

            QueuedEvent next = _offlineQueue[0];
            var request = new WriteClientPlayerEventRequest
            {
                EventName = next.EventName,
                Body      = next.Body as Dictionary<string, object>
                            ?? new Dictionary<string, object>((IDictionary<string, object>)next.Body)
            };

            PlayFabClientAPI.WritePlayerEvent(
                request,
                _ =>
                {
                    if (debugMode)
                        Debug.Log($"<color=cyan>[PlayFabDataManager] ✓ {next.EventName}</color>");

                    _offlineQueue.RemoveAt(0);
                    PersistQueueToDisk();
                    SendNextQueuedEvent();
                },
                error =>
                {
                    Debug.LogError(
                        $"[PlayFabDataManager] Event hatası ({next.EventName}), {_currentRetryDelay:F0}s sonra tekrar denenecek: {error.GenerateErrorReport()}");
                    _isFlushingQueue = false;
                    ScheduleRetry();
                }
            );
        }

        private void ScheduleRetry()
        {
            if (_retryCoroutine != null)
                return;

            _retryCoroutine = StartCoroutine(RetryAfterDelay());
        }

        private IEnumerator RetryAfterDelay()
        {
            yield return new WaitForSeconds(_currentRetryDelay);
            _currentRetryDelay = Mathf.Min(_currentRetryDelay * 2f, MaxRetryDelaySeconds);
            _retryCoroutine = null;
            TryFlushQueue();
        }

        // ═══════════════════════════════════════════════════════
        // HATA CİDDİYETİ (SEVERITY) HESAPLAMA
        // Sabit "1" yerine mistakeType'a göre bir taban ciddiyet atanır;
        // aynı action üzerinde aynı hata 3+ kez tekrarlanırsa Critical'a yükselir.
        // ═══════════════════════════════════════════════════════

        public int ComputeMistakeSeverity(string actionId, string mistakeType, out int occurrence)
        {
            string key = (actionId ?? string.Empty) + "|" + mistakeType;
            _mistakeRepeatCounts.TryGetValue(key, out int count);
            count++;
            _mistakeRepeatCounts[key] = count;
            occurrence = count;

            int baseSeverity = mistakeType switch
            {
                "wrong_answer"    => (int)MistakeSeverity.Moderate,
                "wrong_equipment" => (int)MistakeSeverity.Moderate,
                "wrong_drop"      => (int)MistakeSeverity.Minor,
                _                 => (int)MistakeSeverity.Minor
            };

            return count >= 3 ? (int)MistakeSeverity.Critical : baseSeverity;
        }

        private void OnApplicationQuit()
        {
            if (_isLoggedIn && !string.IsNullOrEmpty(_currentLevelId))
                LogSessionEnded(_currentLevelId);
        }
    }
}
