using UnityEngine;
using UnityEngine.Networking;
using PlayFab.Json;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace SafetyTraining
{
    public class PlayFabDataManager : MonoBehaviour
    {
        public static PlayFabDataManager Instance { get; private set; }

        [Header("━━━ AUTH API ━━━")]
        [Tooltip("CEDAŞ backend login endpoint'i. id+password alır, kullanıcı bilgisini döner.")]
        public string authEndpoint = "https://cedas.collbrai.com/api/v1/auth/login";

        [Header("━━━ TELEMETRİ API ━━━")]
        [Tooltip("Event telemetrisinin gönderileceği ingest endpoint'i (PlayFab değil)")]
        public string telemetryEndpoint = "https://cedas.collbrai.com/api/v1/telemetry/events";

        [Tooltip("GÜVENLİK: Bearer ingest token. Kaynak koduna gömmek yerine bu değeri sahnedeki bileşen üzerinde Inspector'dan girin.")]
        public string ingestToken = "";

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        // ─── Oyuncu verisi ───
        public string CurrentPlayerId    { get; private set; }
        public string CurrentDisplayName { get; private set; }
        public string CurrentPlayerRole  { get; private set; }

        // Menüye her dönüşte login panelinin tekrar sorulmaması için (bkz. UILoginPanel.Start)
        public bool IsLoggedIn => _isLoggedIn;

        private bool   _isLoggedIn;
        private string _currentLevelId;
        // Telemetri API'sinin kabul ettiği sabit levelId (level-1/level-2/level-3) —
        // LevelData.telemetryLevelId'den gelir, yukarıdaki serbest metin levelID'den bağımsız (bkz. LogLevelStarted).
        private string _currentApiLevelId = "level-1";
        private string _sessionId;

        // Telemetri API'sinin payload.role enum'u — CEDAŞ backend'inden dönen rol
        // bunlarla birebir örtüşmeyebilir, eşleşmeyen her şey "trainee"ye düşer.
        private static readonly HashSet<string> ValidApiRoles =
            new HashSet<string> { "admin", "manager", "inspector", "trainee" };

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
        // OYUNCU GİRİŞİ — CEDAŞ backend (POST /api/v1/auth/login)
        // Swagger'da belgelenmemiş ama üretimde çalışan bir endpoint;
        // {id, password} alır, doğruysa kullanıcı bilgisini (id/name/role) döner.
        // ═══════════════════════════════════════════════════════

        public void LoginWithCredentials(
            string               employeeId,
            string               password,
            Action<PlayerEntry>  onSuccess = null,
            Action<string>       onFailed  = null)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                onFailed?.Invoke("ID boş olamaz.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                onFailed?.Invoke("Şifre boş olamaz.");
                return;
            }

            if (debugMode)
                Debug.Log($"[PlayFabDataManager] Giriş deneniyor: {employeeId}");

            StartCoroutine(SendLoginRequest(employeeId.Trim(), password, onSuccess, onFailed));
        }

        private IEnumerator SendLoginRequest(
            string               employeeId,
            string               password,
            Action<PlayerEntry>  onSuccess,
            Action<string>       onFailed)
        {
            var body = new Dictionary<string, object>
            {
                { "id",       employeeId },
                { "password", password }
            };
            byte[] bodyRaw = Encoding.UTF8.GetBytes(PlayFabSimpleJson.SerializeObject(body));

            using (var request = new UnityWebRequest(authEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler.text;

                if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
                {
                    PlayerEntry entry = ParseLoginResponse(responseText);
                    if (entry == null)
                    {
                        onFailed?.Invoke("Sunucu yanıtı okunamadı.");
                        yield break;
                    }

                    ApplyLoggedInPlayer(entry);
                    onSuccess?.Invoke(entry);
                }
                else
                {
                    Debug.LogError($"[PlayFabDataManager] Giriş hatası ({request.responseCode}): {responseText}");
                    onFailed?.Invoke(MapLoginError(request.responseCode, responseText));
                }
            }
        }

        private static PlayerEntry ParseLoginResponse(string json)
        {
            try
            {
                if (PlayFabSimpleJson.DeserializeObject(json) is IDictionary<string, object> root &&
                    root.TryGetValue("user", out object userObj) &&
                    userObj is IDictionary<string, object> user)
                {
                    return new PlayerEntry
                    {
                        playerId    = user.TryGetValue("id", out object id) ? id as string : string.Empty,
                        displayName = user.TryGetValue("name", out object name) ? name as string : string.Empty,
                        role        = user.TryGetValue("role", out object role) ? role as string : "trainee"
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFabDataManager] Giriş yanıtı parse edilemedi: {e.Message}");
            }
            return null;
        }

        private void ApplyLoggedInPlayer(PlayerEntry entry)
        {
            CurrentPlayerId    = entry.playerId;
            CurrentDisplayName = entry.displayName;
            CurrentPlayerRole  = entry.role;
            _isLoggedIn        = true;

            if (debugMode)
                Debug.Log($"<color=lime>[PlayFabDataManager] Giriş başarılı: {CurrentDisplayName} ({CurrentPlayerId})</color>");

            TryFlushQueue();
        }

        private static string MapLoginError(long statusCode, string responseText)
        {
            string errorCode = null;
            try
            {
                if (PlayFabSimpleJson.DeserializeObject(responseText) is IDictionary<string, object> root &&
                    root.TryGetValue("error", out object err))
                    errorCode = err as string;
            }
            catch (Exception)
            {
                // yanıt JSON değilse (ör. ağ hatası) durum koduna düşülür
            }

            switch (errorCode)
            {
                case "invalid_credentials":
                    return "ID veya şifre hatalı.";
                default:
                    return statusCode == 0
                        ? "Sunucuya ulaşılamadı, internet bağlantınızı kontrol edin."
                        : "Giriş başarısız, lütfen tekrar deneyin.";
            }
        }

        // ═══════════════════════════════════════════════════════
        // LEVEL EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogLevelStarted(LevelData level)
        {
            string levelId = level?.levelID;
            _currentLevelId = levelId;
            _currentApiLevelId = !string.IsNullOrEmpty(level?.telemetryLevelId)
                ? level.telemetryLevelId
                : "level-1";
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

        /// <summary>
        /// Telemetri API'sinin eventType enum'unda "SequenceEntryDenied" yok — bu yüzden
        /// MistakeRecorded olarak, mistakeType="sequence_entry_denied" ile gönderilir.
        /// </summary>
        public void LogSequenceEntryDenied(string sequenceId, string levelId,
            string[] missingPrerequisiteIds, string failAction)
        {
            const string mistakeType = "sequence_entry_denied";
            int severity = ComputeMistakeSeverity(sequenceId, mistakeType, out int occurrence);

            SendEvent("MistakeRecorded", new Dictionary<string, object>
            {
                { "mistakeType",           mistakeType },
                { "actionId",              sequenceId },
                { "levelId",               levelId },
                { "sequenceId",            sequenceId },
                { "severity",              severity },
                { "occurrence",            occurrence },
                { "timestamp",             DateTime.UtcNow.ToString("o") },
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
        // CORE — Telemetri Ingest API'sinin TelemetryEvent şeması:
        // { eventId, schemaVersion, eventType, clientTimestamp, employeeId, payload }
        // payload zorunlu alanları: sessionId, playerId, levelId (level-1/2/3).
        // levelId ve role burada merkezi olarak set edilir/override edilir ki
        // hangi çağıran ne geçerse geçsin API'nin kabul ettiği sabit değerler gitsin
        // (bkz. LogLevelStarted → _currentApiLevelId, MapApiRole).
        // ═══════════════════════════════════════════════════════

        private static string MapApiRole(string role)
        {
            if (string.IsNullOrEmpty(role))
                return "trainee";

            string normalized = role.Trim().ToLowerInvariant();
            return ValidApiRoles.Contains(normalized) ? normalized : "trainee";
        }

        private void SendEvent(string eventName, Dictionary<string, object> payload)
        {
            var documentPayload = new Dictionary<string, object>(payload);
            documentPayload["sessionId"] = _sessionId ?? string.Empty;
            documentPayload["playerId"] = CurrentPlayerId ?? string.Empty;
            documentPayload["levelId"] = _currentApiLevelId;
            documentPayload["role"] = MapApiRole(CurrentPlayerRole);
            if (!documentPayload.ContainsKey("timestamp"))
                documentPayload["timestamp"] = DateTime.UtcNow.ToString("o");

            var envelope = new Dictionary<string, object>
            {
                { "eventId",         Guid.NewGuid().ToString() },
                { "schemaVersion",   2 },
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

            StartCoroutine(SendTelemetryEvent(_offlineQueue[0]));
        }

        // Telemetri API'si tek event de kabul ediyor, batch de (max 200/256 KiB) —
        // mevcut "kuyruktan tek tek gönder" akışıyla uyumlu kalması için tek elemanlı
        // bir batch olarak gönderiyoruz.
        private IEnumerator SendTelemetryEvent(QueuedEvent next)
        {
            var batch = new Dictionary<string, object>
            {
                { "events", new List<object> { next.Body } }
            };
            byte[] bodyRaw = Encoding.UTF8.GetBytes(PlayFabSimpleJson.SerializeObject(batch));

            using (var request = new UnityWebRequest(telemetryEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + ingestToken);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (debugMode)
                        Debug.Log($"<color=cyan>[PlayFabDataManager] ✓ {next.EventName}</color>");

                    _offlineQueue.RemoveAt(0);
                    PersistQueueToDisk();
                    SendNextQueuedEvent();
                }
                else
                {
                    Debug.LogError(
                        $"[PlayFabDataManager] Telemetri hatası ({next.EventName}: {request.responseCode} {request.error}), " +
                        $"{_currentRetryDelay:F0}s sonra tekrar denenecek.");
                    _isFlushingQueue = false;
                    ScheduleRetry();
                }
            }
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
