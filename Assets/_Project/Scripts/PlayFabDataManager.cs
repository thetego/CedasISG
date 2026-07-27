using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
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

        // Giriş tamamlanmadan gelen eventler için kuyruk
        private readonly Queue<WriteClientPlayerEventRequest> _pendingEvents =
            new Queue<WriteClientPlayerEventRequest>();

        // Callback'ler
        private Action          _onLoginSuccess;
        private Action<string>  _onLoginFailed;

        // Zaman takibi
        private float _levelStartTime;
        private float _sequenceStartTime;
        private float _actionStartTime;

        // Doküman §4.6 Quiz Summary — aktif level boyunca biriktirilir, LogLevelCompleted'de gönderilir
        private int _quizTotalQuestions;
        private int _quizCorrectAnswers;

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

        private void FlushPendingEvents()
        {
            while (_pendingEvents.Count > 0)
                SendEventRequest(_pendingEvents.Dequeue());
        }

        // ═══════════════════════════════════════════════════════
        // LEVEL EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogLevelStarted(string levelId)
        {
            _currentLevelId = levelId;
            _levelStartTime = Time.time;
            _quizTotalQuestions = 0;
            _quizCorrectAnswers = 0;

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

            SendEvent("SequenceStarted", new Dictionary<string, object>
            {
                { "sequenceId", sequenceId },
                { "levelId",    levelId }
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
                { "completed",  true }
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
            _quizTotalQuestions++;
            if (isCorrect) _quizCorrectAnswers++;

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
            SendEvent("MistakeRecorded", new Dictionary<string, object>
            {
                { "mistakeType", mistakeType },
                { "actionId",    actionId },
                { "severity",    severity }
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
            var envelope = new Dictionary<string, object>
            {
                { "eventType",       eventName },
                { "clientTimestamp", DateTime.UtcNow.ToString("o") },
                { "employeeId",      CurrentPlayerId },
                { "payload",         payload }
            };

            var request = new WriteClientPlayerEventRequest
            {
                EventName = eventName,
                Body      = envelope
            };

            if (!_isLoggedIn)
            {
                _pendingEvents.Enqueue(request);
                if (debugMode)
                    Debug.Log($"[PlayFabDataManager] Event kuyruğa alındı: {eventName}");
                return;
            }

            SendEventRequest(request);
        }

        private void SendEventRequest(WriteClientPlayerEventRequest request)
        {
            PlayFabClientAPI.WritePlayerEvent(
                request,
                _ =>
                {
                    if (debugMode)
                        Debug.Log($"<color=cyan>[PlayFabDataManager] ✓ {request.EventName}</color>");
                },
                error => Debug.LogError(
                    $"[PlayFabDataManager] Event hatası ({request.EventName}): {error.GenerateErrorReport()}")
            );
        }

        private void OnApplicationQuit()
        {
            if (_isLoggedIn && !string.IsNullOrEmpty(_currentLevelId))
                LogSessionEnded(_currentLevelId);
        }
    }
}
