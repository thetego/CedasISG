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

        // ─── Oyuncu verisi ───
        public string CurrentPlayerId    { get; private set; }
        public string CurrentDisplayName { get; private set; }

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

        [Serializable]
        private class WhitelistJson
        {
            public List<PlayerEntry> players;
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

        /// <summary>
        /// PlayFab Title Data'dan whitelist'i çeker.
        /// Önce anonim login yapar, ardından GetTitleData çağırır.
        /// </summary>
        public void FetchWhitelist(
            Action<List<PlayerEntry>> onSuccess,
            Action<string>           onFailed)
        {
            if (debugMode)
                Debug.Log("[PlayFabDataManager] Whitelist çekiliyor...");

            // Title Data okuyabilmek için anonim giriş
            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest
                {
                    CustomId      = "anon_" + SystemInfo.deviceUniqueIdentifier,
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

            CurrentPlayerId    = entry.playerId;
            CurrentDisplayName = entry.displayName;
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

            SendEvent("LevelStarted", new Dictionary<string, object>
            {
                { "levelId",     levelId },
                { "playerId",    CurrentPlayerId },
                { "displayName", CurrentDisplayName },
                { "timestamp",   DateTime.UtcNow.ToString("o") }
            });
        }

        public void LogLevelCompleted(string levelId, int score, int mistakes)
        {
            int timeSpent = Mathf.RoundToInt(Time.time - _levelStartTime);

            SendEvent("LevelCompleted", new Dictionary<string, object>
            {
                { "levelId",        levelId },
                { "playerId",       CurrentPlayerId },
                { "score",          score },
                { "timeSpent",      timeSpent },
                { "mistakes",       mistakes },
                { "completionRate", Mathf.Clamp01(1f - mistakes * 0.05f) }
            });
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
                { "levelId",    levelId },
                { "playerId",   CurrentPlayerId },
                { "timestamp",  DateTime.UtcNow.ToString("o") }
            });
        }

        public void LogSequenceCompleted(string sequenceId, string levelId, int mistakes)
        {
            SendEvent("SequenceCompleted", new Dictionary<string, object>
            {
                { "sequenceId", sequenceId },
                { "levelId",    levelId },
                { "playerId",   CurrentPlayerId },
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

        public void LogActionCompleted(string actionId, string levelId, string sequenceId, string actionType)
        {
            SendEvent("ActionCompleted", new Dictionary<string, object>
            {
                { "actionId",   actionId },
                { "levelId",    levelId },
                { "sequenceId", sequenceId },
                { "playerId",   CurrentPlayerId },
                { "type",       actionType },
                { "duration",   Mathf.RoundToInt(Time.time - _actionStartTime) },
                { "result",     "success" }
            });
        }

        // ═══════════════════════════════════════════════════════
        // QUIZ EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogQuizAnswered(string actionId, string levelId, string sequenceId,
            string questionId, string selectedAnswer, bool isCorrect, int attempts, int timeSpent)
        {
            SendEvent("QuizAnswered", new Dictionary<string, object>
            {
                { "actionId",       actionId },
                { "levelId",        levelId },
                { "sequenceId",     sequenceId },
                { "playerId",       CurrentPlayerId },
                { "questionId",     questionId },
                { "selectedAnswer", selectedAnswer },
                { "isCorrect",      isCorrect },
                { "attempts",       attempts },
                { "timeSpent",      timeSpent }
            });
        }

        // ═══════════════════════════════════════════════════════
        // DRAG & DROP EVENTLERİ
        // ═══════════════════════════════════════════════════════

        public void LogDragDropAttempt(string actionId, string levelId, string sequenceId,
            string item, string droppedOn, bool correct, int attempts)
        {
            SendEvent("DragDropAttempt", new Dictionary<string, object>
            {
                { "actionId",   actionId },
                { "levelId",    levelId },
                { "sequenceId", sequenceId },
                { "playerId",   CurrentPlayerId },
                { "item",       item },
                { "droppedOn",  droppedOn },
                { "correct",    correct },
                { "attempts",   attempts }
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
                { "playerId",    CurrentPlayerId },
                { "severity",    severity },
                { "timestamp",   DateTime.UtcNow.ToString("o") }
            });
        }

        // ═══════════════════════════════════════════════════════
        // OTURUM SONU
        // ═══════════════════════════════════════════════════════

        public void LogSessionEnded(string levelId)
        {
            SendEvent("SessionEnded", new Dictionary<string, object>
            {
                { "levelId",   levelId },
                { "playerId",  CurrentPlayerId },
                { "timestamp", DateTime.UtcNow.ToString("o") }
            });
        }

        // ═══════════════════════════════════════════════════════
        // CORE
        // ═══════════════════════════════════════════════════════

        private void SendEvent(string eventName, Dictionary<string, object> body)
        {
            var request = new WriteClientPlayerEventRequest
            {
                EventName = eventName,
                Body      = body
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
