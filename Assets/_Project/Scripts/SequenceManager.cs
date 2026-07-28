using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Michsky.MUIP;
using UnityEngine.UI;

namespace SafetyTraining
{
	/// <summary>
	/// Sekans bazlı yönetim sistemi
	/// - Level başladığında her sekans için buton spawn eder
	/// - Kullanıcı bir sekansa girdiğinde o sekansın action'larını yönetir
	/// - Sekanslar arası geçişi ve "kaldığı yer" sistemini yönetir
	/// </summary>
	public class SequenceManager : MonoBehaviour
	{
		public static SequenceManager Instance { get; private set; }

		[Header("━━━ LEVEL ━━━")]
		public LevelData currentLevel;

		[Header("━━━ UI ━━━")]
		public TMPro.TextMeshProUGUI instructionText;
		public TMPro.TextMeshProUGUI levelText;
		public TMPro.TextMeshProUGUI tmptext;
		[Min(1)] public int totalLevelCount = 15;
		public GameObject gameOverPanel;
		public TMPro.TextMeshProUGUI gameOverMessageText;
		public GameObject warningPanel;
		public TMPro.TextMeshProUGUI warningMessageText;
		public UnityEngine.UI.Button backButton; // Sekanslardan geri dönme butonu

		[Header("━━━ SEQUENCE FADE ━━━")]
		public Image sequenceFadeOverlay;
		[Min(0f)] public float sequenceFadeStartDelay = 0f;
		[Min(0.01f)] public float sequenceFadeDuration = 0.35f;
		[Min(0f)] public float sequenceFadeHoldDuration = 0.1f;

		[Header("━━━ ACTION TRANSITION ━━━")]
		[Min(0f)] public float nextActionTransitionDelay = 1f;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ═══════════════════════════════════════════════════════
		// RUNTIME STATE
		// ═══════════════════════════════════════════════════════

		// Analytics
		public string CurrentLevelID    => currentLevel?.levelID    ?? string.Empty;
		public string CurrentSequenceID => _currentSequence?.sequenceID ?? string.Empty;
		private int _sequenceMistakes;

		// Tüm sekanslar ve durumları
		private Dictionary<string, SequenceData> _allSequences = new Dictionary<string, SequenceData>();
		private Dictionary<string, SequenceState> _sequenceStates = new Dictionary<string, SequenceState>();
		private Dictionary<string, UISequenceButton> _sequenceButtons = new Dictionary<string, UISequenceButton>();

		// Mevcut sekans
		private SequenceData _currentSequence;
		private SequenceState _currentSequenceState;
		private int  _currentActionIndex = 0;
		[HideInInspector] public bool _waitingForTablet = false;
		private ActionData _currentAction;
		private bool _skipNextCompletionDelay = false;

		// Level state
		private bool _levelStarted;
		private float _levelStartTime;
		private int _score;
		private int _mistakes;
		private bool _inSequence; // Şu an bir sekans içinde miyiz?
		private bool _isCompletingSequence;

		// ═══════════════════════════════════════════════════════
		// INITIALIZATION
		// ═══════════════════════════════════════════════════════

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			else
			{
				Destroy(gameObject);
				return;
			}

			EnsureFadeOverlay();
			ResolveProgressTextsFromCanvas();
		}

		private void Start()
		{
			if (currentLevel != null)
				StartLevel();
			else
				Debug.LogError("[SequenceManager] currentLevel atanmamış!");

			// Back button setup
			if (backButton != null)
			{
				backButton.onClick.AddListener(ExitCurrentSequence);
				backButton.gameObject.SetActive(false);
			}
		}

		// ═══════════════════════════════════════════════════════
		// LEVEL LIFECYCLE
		// ═══════════════════════════════════════════════════════

		public void StartLevel()
		{
			_allSequences.Clear();
			_sequenceStates.Clear();
			_sequenceButtons.Clear();
			_levelStarted = true;
			_levelStartTime = Time.time;
			_score = currentLevel.maxScore;
			_mistakes = 0;
			_sequenceMistakes = 0;
			_inSequence = false;

			// Sekansları kaydet ve state oluştur
			foreach (var sequence in currentLevel.sequences)
			{
				if (sequence == null) continue;

				_allSequences[sequence.sequenceID] = sequence;
				_sequenceStates[sequence.sequenceID] = new SequenceState
				{
					sequenceID = sequence.sequenceID,
					isCompleted = false,
					currentActionIndex = 0,
					completedActionIDs = new HashSet<string>()
				};
			}

			if (debugMode)
				Debug.Log($"<color=lime>[SequenceManager] Level başladı: {currentLevel.levelName} ({_allSequences.Count} sequences)</color>");

			// Sekans butonlarını spawn et
			UISpawnManager.Instance?.SpawnSequenceButtons(currentLevel);

			PlayFabDataManager.Instance?.LogLevelStarted(currentLevel.levelID);

			// Level start event
			currentLevel.onLevelStart?.Invoke();

			// Ana kameraya dön
			CameraManager.Instance?.ResetToDefault();

			// Talimat
			SetSequenceSelectionInstruction();

			ClearProgressTexts();
		}

		public void RestartLevel()
		{
			_isCompletingSequence = false;
			ExitCurrentSequence(); // Önce mevcut sekansdan çık
			StartLevel();
		}

		// ═══════════════════════════════════════════════════════
		// SEQUENCE MANAGEMENT
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// <summary>
		/// Sequence butonunu manuel spawn eder (spawnButtonAtStart=false olan sequenceler için)
		/// </summary>
		public void SpawnSequenceButton(string sequenceID)
		{
			if (!_allSequences.TryGetValue(sequenceID, out SequenceData sequence))
			{
				Debug.LogError($"[SequenceManager] Sequence '{sequenceID}' not found for spawning!");
				return;
			}

			// Zaten spawn edilmiş mi?
			if (_sequenceButtons.ContainsKey(sequenceID))
			{
				if (debugMode)
					Debug.LogWarning($"[SequenceManager] Button already exists for '{sequence.sequenceName}'");
				return;
			}

			// UISpawnManager'a spawn et
			UISpawnManager.Instance?.SpawnSequenceButton(sequence);

			if (debugMode)
				Debug.Log($"[SequenceManager] Spawned button for sequence: '{sequence.sequenceName}'");
		}

		/// Sekans butonuna tıklandığında çağrılır
		/// </summary>
		public void OnSequenceButtonClicked(string sequenceID)
		{
			if (!_allSequences.TryGetValue(sequenceID, out SequenceData sequence))
			{
				Debug.LogError($"[SequenceManager] Sequence '{sequenceID}' not found!");
				return;
			}

			// Önkoşul kontrolü
			if (!CheckSequencePrerequisites(sequence))
			{
				HandleSequencePrerequisiteFail(sequence);
				return;
			}

			// Sekansa gir
			EnterSequence(sequence);
		}

		/// <summary>
		/// Bir sekansa girer
		/// </summary>
		private void EnterSequence(SequenceData sequence)
		{
			// Mevcut sekansdan çık (varsa)
			if (_inSequence && _currentSequence != null && _currentSequence != sequence)
			{
				ExitCurrentSequence();
			}

			_currentSequence = sequence;
			_currentSequenceState = _sequenceStates[sequence.sequenceID];
			_inSequence = true;
			_sequenceMistakes = 0;

			PlayFabDataManager.Instance?.LogSequenceStarted(sequence.sequenceID, currentLevel?.levelID);

			if (debugMode)
				Debug.Log($"<color=cyan>[SequenceManager] → Sekansa girildi: {sequence.sequenceName}</color>");


			// Tamamlanmış sekansa yeniden giriş yapılıyorsa state'i sıfırla
			bool isResuming = false;
			if (_currentSequenceState.isCompleted && sequence.allowRestart)
			{
				if (debugMode)
					Debug.Log($"[SequenceManager] Restarting completed sequence: {sequence.sequenceName}");

				// State'i sıfırla
				_currentSequenceState.isCompleted = false;
				_currentSequenceState.currentActionIndex = 0;
				_currentSequenceState.completedActionIDs.Clear();
				_currentSequenceState.lastCameraID = null;
				_currentActionIndex = 0;

				// Buton state'ini güncelle
				if (_sequenceButtons.TryGetValue(sequence.sequenceID, out UISequenceButton btn))
				{
					btn.SetCompleted(false);

					// Gizlenmişse tekrar göster
					if (!btn.gameObject.activeSelf)
					{
						btn.gameObject.SetActive(true);

						if (debugMode)
							Debug.Log($"[SequenceManager] Sequence button reactivated: '{sequence.sequenceName}'");
					}
				}
			}
			// Kaldığı yerden devam mı, yoksa baştan mı?
			else if (sequence.resumeFromLastAction && _currentSequenceState.currentActionIndex > 0)
			{
				_currentActionIndex = _currentSequenceState.currentActionIndex;
				isResuming = true;

				if (debugMode)
					Debug.Log($"<color=yellow>[SequenceManager] Kaldığı yerden devam: Action {_currentActionIndex}/{sequence.GetTotalActionCount()}</color>");
			}
			else
			{
				_currentActionIndex = 0;
			}

			// Entry kamera (sadece yeni başlıyorsa)
			if (!isResuming)
			{
				HandleSequenceEntryCamera(sequence);
			}
			else
			{
				// Resume: Kaydedilmiş kamerayı push et
				if (!string.IsNullOrEmpty(_currentSequenceState.lastCameraID) && CameraManager.Instance != null)
				{
					CameraManager.Instance.PushCamera(_currentSequenceState.lastCameraID);

					if (debugMode)
						Debug.Log($"[SequenceManager] Resume mode: Restored camera '{_currentSequenceState.lastCameraID}'");
				}
				else
				{
					if (debugMode)
						Debug.Log("[SequenceManager] Resume mode: No saved camera, will use action camera");
				}
			}

			// Events
			sequence.onSequenceEnter?.Invoke();

			// UI güncelle
			UpdateSequenceProgressText();

			if (backButton != null)
				backButton.gameObject.SetActive(true);

			// Sekans butonlarını gizle
			HideSequenceButtons();

			// Sekans survey'i varsa spawn et
			UISpawnManager.Instance?.OnSequenceStarted(_currentSequence);

			// İlk action'ı başlat
			StartNextActionInSequence();
		}

		/// <summary>
		/// Mevcut sekansdan çıkar (kullanıcı geri butonuna bastığında)
		/// </summary>
		public void ExitCurrentSequence()
		{
			if (!_inSequence || _currentSequence == null)
				return;

			if (debugMode)
				Debug.Log($"<color=orange>[SequenceManager] ← Sekanstan çıkıldı: {_currentSequence.sequenceName}</color>");

			// State kaydet (kamera reset edilmeden ÖNCE!)
			if (_currentSequenceState != null && CameraManager.Instance != null)
			{
				_currentSequenceState.currentActionIndex = _currentActionIndex;
				_currentSequenceState.lastCameraID = CameraManager.Instance.GetCurrentCameraID();

				if (debugMode)
					Debug.Log($"[SequenceManager] Saved state: actionIndex={_currentActionIndex}, cameraID={_currentSequenceState.lastCameraID}");
			}


			// Mevcut action'ı deaktif et
			if (_currentAction != null)
			{
				UISpawnManager.Instance?.DeactivateAction(_currentAction);
				// Sekans survey'i kapat
				UISpawnManager.Instance?.OnSequenceEnded(_currentSequence);
			}

			// Exit kamera
			if (_currentSequence.autoReturnOnExit)
			{
				CameraManager.Instance?.ResetToDefault();
			}

			// Events
			_currentSequence.onSequenceExit?.Invoke();

			// State kaydet

			// Reset
			_currentSequence = null;
			_currentSequenceState = null;
			_currentAction = null;
			_inSequence = false;
			UISpawnManager.Instance?.SetSurveyPhotoSlotFocus(-1, 0);

			// UI güncelle
			ClearProgressTexts();

			SetSequenceSelectionInstruction();

			if (backButton != null)
				backButton.gameObject.SetActive(false);

			// Sekans butonlarını göster
			StartCoroutine(ShowSequenceButtons());
		}

		// ═══════════════════════════════════════════════════════
		// ACTION FLOW (İÇERİDE BİR SEKANSTAYKEN)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Sekanstaki sıradaki action'ı başlatır
		/// </summary>
		private int GetSurveyPhotoCount(ActionData action)
		{
			if (action == null)
				return 0;

			if (action.surveyPhotoCount > 0)
				return action.surveyPhotoCount;

			return action.surveyPhotoRequired ? 1 : 0;
		}

		private void GetSurveyPhotoRangeForAction(int actionIndex, out int startIndex, out int slotCount)
		{
			startIndex = -1;
			slotCount = 0;

			if (_currentSequence == null || actionIndex < 0 || actionIndex >= _currentSequence.GetTotalActionCount())
				return;

			ActionData currentAction = _currentSequence.GetActionAt(actionIndex);
			int currentCount = GetSurveyPhotoCount(currentAction);
			if (currentCount <= 0)
				return;

			int currentStartIndex = 0;
			for (int i = 0; i < actionIndex; i++)
			{
				ActionData previousAction = _currentSequence.GetActionAt(i);
				currentStartIndex += GetSurveyPhotoCount(previousAction);
			}

			startIndex = currentStartIndex;
			slotCount = currentCount;
		}

		private void UpdateSurveyPhotoSlotFocus()
		{
			GetSurveyPhotoRangeForAction(_currentActionIndex, out int startIndex, out int slotCount);
			UISpawnManager.Instance?.SetSurveyPhotoSlotFocus(startIndex, slotCount);
		}

		private void StartNextActionInSequence()
		{
			if (_currentSequence == null)
			{
				Debug.LogError("[SequenceManager] StartNextActionInSequence called but no active sequence!");
				return;
			}

			// Sekans bitti mi?
			if (_currentActionIndex >= _currentSequence.GetTotalActionCount())
			{
				CompleteSequence();
				return;
			}

			_currentAction = _currentSequence.GetActionAt(_currentActionIndex);

			if (_currentAction == null)
			{
				Debug.LogError($"[SequenceManager] Action null at index {_currentActionIndex} in sequence '{_currentSequence.sequenceName}'. Sequence will stop here instead of skipping.");
				return;
			}

			if (debugMode)
				Debug.Log($"<color=yellow>[SequenceManager] Action başladı [{_currentActionIndex + 1}/{_currentSequence.GetTotalActionCount()}]: {_currentAction.actionName}</color>");

			// Sayaç başlangıçtan bitişe doğru ilerler: 1/15, 2/15, ...
			UpdateSequenceProgressText();

			PlayFabDataManager.Instance?.LogActionStarted(_currentAction.actionID);

			// Talimat
			if (instructionText)
				instructionText.text = _currentAction.instructionText;

			// Kamera geçişi (action bazlı)
			CameraManager.Instance?.HandleActionCamera(_currentAction);

			// Resume için kamera durumunu kaydet
			if (CameraManager.Instance != null && _currentSequenceState != null)
			{
				_currentSequenceState.lastCameraID = CameraManager.Instance.GetCurrentCameraID();
			}

			// UI aktif et
			StartCoroutine(UISpawnManager.Instance?.ActivateAction(_currentAction, 0f));

			// OnStart animasyonlar
			PlayAnimations(_currentAction, AnimationTiming.OnStart);

			// OnStart events
			_currentAction.onActionStart?.Invoke();

			// OnStart scene events (ActionEventHandler)
			TriggerSceneEvents(_currentAction.onStartEventIDs);

			// Survey foto slot focus
			UpdateSurveyPhotoSlotFocus();

			// CameraMove için otomatik tamamlama
			if (_currentAction.actionType == ActionType.CameraMove)
			{
				// CameraMove action'ları UI gerektirmez, kamera geçişi yapıldı
				// autoCompleteAfterDelay true ise delay sonra tamamla
				if (_currentAction.autoCompleteAfterDelay && _currentAction.completionDelay > 0)
				{
					if (debugMode)
						Debug.Log($"[SequenceManager] CameraMove will auto-complete after {_currentAction.completionDelay}s");

					StartCoroutine(CompleteCameraMoveAfterDelay(_currentAction.actionID, _currentAction.completionDelay));
				}
				else
				{
					// Anında tamamla
					if (debugMode)
						Debug.Log($"[SequenceManager] CameraMove auto-completed immediately");

					OnActionCompleted(_currentAction.actionID);
				}
			}

			if (_currentAction.actionType == ActionType.Fade)
			{
				if (debugMode)
					Debug.Log("[SequenceManager] Fade auto-play started");

				StartCoroutine(PlayFadeActionAndComplete(_currentAction.actionID));
				return;
			}

			// WearEquipment için tracking sıfırla
			if (_currentAction.actionType == ActionType.WearEquipment)
			{
				_wornEquipments.Clear();
			}

			// DragToWorld için tracking sıfırla
			if (_currentAction.actionType == ActionType.DragToWorld)
			{
				_droppedTools.Clear();
			}
		}

		/// <summary>
		/// Action tamamlandığında çağrılır
		/// </summary>
		public void OnActionCompleted(string actionID)
		{
			OnActionCompleted(actionID, true);
		}

		private void OnActionCompleted(string actionID, bool invokeCompletionEvents)
		{
			if (_currentAction == null || _currentAction.actionID != actionID)
			{
				if (debugMode)
					Debug.LogWarning($"[SequenceManager] OnActionCompleted '{actionID}' ama mevcut action '{_currentAction?.actionID}'");
				return;
			}

			if (debugMode)
				Debug.Log($"<color=lime>[SequenceManager] ✓ Action tamamlandı: {_currentAction.actionName}</color>");

			PlayFabDataManager.Instance?.LogActionCompleted(
				_currentAction.actionID,
				currentLevel?.levelID,
				_currentSequence?.sequenceID,
				GetActionTypeString(_currentAction.actionType),
				_currentAction.targetObjectID
			);

			// State'e kaydet
			_currentSequenceState.completedActionIDs.Add(_currentAction.actionID);

			// UI deaktif et
			UISpawnManager.Instance?.DeactivateAction(_currentAction);
			UISpawnManager.Instance?.OnActionFinished(_currentAction);

			// OnComplete animasyonlar
			PlayAnimations(_currentAction, AnimationTiming.OnComplete);

			// Ses
			PlaySound();

			// Partikül
			SpawnParticle();

			if (invokeCompletionEvents)
			{
				InvokeCurrentActionCompletionEvents(actionID);
			}

			// Kamera geri dön (varsa)
			if (_currentAction.autoReturnCameraOnComplete)
				CameraManager.Instance?.PopCamera();

			// activatesTablet = true ise akışı durdur
			// UITabletButton.OnTabletClosed() çağrılınca devam eder
			if (_currentAction.activatesTablet)
			{
				_waitingForTablet = true;
				_currentActionIndex++; // index'i ilerlet ama StartNext çağırma
				if (debugMode)
					Debug.Log($"[SequenceManager] Tablet bekleniyor — akış duraklatıldı.");
				return;
			}

			float completionDelay = _skipNextCompletionDelay ? 0f : _currentAction.completionDelay;
			ScheduleAdvanceToNextAction(completionDelay);
		}

		private IEnumerator CompleteCameraMoveAfterDelay(string actionID, float delay)
		{
			if (delay > 0f)
				yield return new WaitForSeconds(delay);

			if (_currentAction == null || _currentAction.actionID != actionID)
				yield break;

			_skipNextCompletionDelay = true;
			OnActionCompleted(actionID);
			_skipNextCompletionDelay = false;
		}

		/// <summary>
		/// Tablet kapatıldığında UITabletButton tarafından çağrılır.
		/// activatesTablet ile duraklatılmış akışı devam ettirir.
		/// </summary>
		public void OnTabletClosed()
		{
			Debug.Log($"[SequenceManager] OnTabletClosed çağrıldı. _waitingForTablet={_waitingForTablet}");
			if (!_waitingForTablet) return;

			_waitingForTablet = false;
			Debug.Log("[SequenceManager] Tablet kapatıldı — akış devam ediyor.");
			StartNextActionInSequence();
		}

		/// <summary>
		/// Survey tamamlandığında çağrılır.
		/// Eğer tüm actionlar bitti ise sekansı tamamlar,
		/// aksi halde sadece survey'i kapatır.
		/// </summary>
		public void CompleteSurveyAndAdvance()
		{
			if (_currentSequence == null) return;

			// Tablet bekleme durumunu temizle
			_waitingForTablet = false;

			// Tüm action'lar tamamlandıysa sekansı bitir
			bool allActionsComplete = _currentActionIndex >= _currentSequence.GetTotalActionCount();

			if (allActionsComplete)
			{
				CompleteSequence();
			}
			else
			{
				// Henüz devam eden action'lar var — akışı devam ettir
				if (debugMode)
					Debug.Log("[SequenceManager] Survey tamamlandı, sekans devam ediyor.");
				ScheduleAdvanceToNextAction();
			}
		}

		/// <summary>
		/// Quiz yanlış cevaplandığında çağrılır.
		/// Bu akış artık oyun bitirmez; yalnızca hata sayısını artırır.
		/// </summary>
		public void OnQuizFailed(string actionID)
		{
			if (_currentAction == null || _currentAction.actionID != actionID) return;

			if (debugMode)
				Debug.LogWarning($"[SequenceManager] Quiz başarısız: {_currentAction.actionName}");

			_mistakes++;
			_sequenceMistakes++;
			PlayFabDataManager.Instance?.LogMistakeRecorded(actionID, "wrong_answer", 1);

			PlayAnimations(_currentAction, AnimationTiming.OnFail);
			_currentAction.onActionFail?.Invoke();
			TriggerSceneEvents(_currentAction.onFailEventIDs);
		}

		private System.Collections.IEnumerator WaitAndContinueSequence(float delay)
		{
			yield return new WaitForSeconds(delay);
			MoveToNextActionInSequence();
		}

		private void ScheduleAdvanceToNextAction(float delay = 0f)
		{
			StartCoroutine(WaitAndContinueSequence(delay + nextActionTransitionDelay));
		}

		private IEnumerator PlayFadeActionAndComplete(string actionID)
		{
			if (sequenceFadeStartDelay > 0f)
				yield return new WaitForSeconds(sequenceFadeStartDelay);

			yield return PlaySequenceCompleteFade(() =>
			{
				if (_currentAction != null && _currentAction.actionID == actionID)
					InvokeCurrentActionCompletionEvents(actionID);
			});

			if (_currentAction != null && _currentAction.actionID == actionID)
				OnActionCompleted(actionID, false);
		}

		private void MoveToNextActionInSequence()
		{
			_currentActionIndex++;
			StartNextActionInSequence();
		}

		/// <summary>
		/// Sekans tamamlandı
		/// </summary>
		private void CompleteSequence()
		{
			if (_currentSequence == null || _isCompletingSequence) return;
			StartCoroutine(CompleteSequenceRoutine());
		}

		private IEnumerator CompleteSequenceRoutine()
		{
			if (_currentSequence == null)
				yield break;

			_isCompletingSequence = true;
			SequenceData completedSequence = _currentSequence;

			if (debugMode)
				Debug.Log($"<color=lime>[SequenceManager] ★ Sekans tamamlandı: {completedSequence.sequenceName} ★</color>");

			_currentSequenceState.isCompleted = true;

			PlayFabDataManager.Instance?.LogSequenceCompleted(
				completedSequence.sequenceID,
				currentLevel?.levelID,
				_sequenceMistakes
			);

			// Sekans survey'i kapat
			UISpawnManager.Instance?.OnSequenceEnded(completedSequence);
			UISpawnManager.Instance?.SetSurveyPhotoSlotFocus(-1, 0);
			_currentSequenceState.completionTime = Time.time;

			// Events
			completedSequence.onSequenceComplete?.Invoke();

			// Kamera geri dön
			if (completedSequence.autoReturnOnComplete)
			{
				CameraManager.Instance?.ResetToDefault();
			}

			// Sekans butonunu güncelle (completed state)
			if (_sequenceButtons.TryGetValue(completedSequence.sequenceID, out UISequenceButton btn))
			{
				// Her durumda completed state'i set et
				btn.SetCompleted(true);

				// Hide flag kontrolü - hemen gizle
				if (completedSequence.hideButtonAfterComplete)
				{
					btn.gameObject.SetActive(false);

					if (debugMode)
						Debug.Log($"[SequenceManager] Sequence button hidden: '{completedSequence.sequenceName}'");
				}
				// Restart izni yoksa kilitle
				else if (!completedSequence.allowRestart)
				{
					btn.SetLocked(true);

					if (debugMode)
						Debug.Log($"[SequenceManager] Sequence '{completedSequence.sequenceName}' locked (allowRestart=false)");
				}
				else
				{
					if (debugMode)
						Debug.Log($"[SequenceManager] Sequence '{completedSequence.sequenceName}' can be restarted");
				}
			}

			// Sekansdan çık
			_inSequence = false;
			_currentSequence = null;
			_currentSequenceState = null;
			_currentAction = null;

			// UI güncelle
			ClearProgressTexts();

			SetSequenceSelectionInstruction();

			if (backButton != null)
				backButton.gameObject.SetActive(false);

			// Sekans butonlarını göster
			StartCoroutine(ShowSequenceButtons());

			// Prerequisite'leri karşılayan sekansları spawn et
			CheckAndSpawnUnlockedSequences();

			// Tüm sekanslar tamamlandı mı?
			if (CheckAllSequencesCompleted())
			{
				CompleteLevel();
			}

			_isCompletingSequence = false;
		}

		// ═══════════════════════════════════════════════════════
		// LEVEL COMPLETION
		// ═══════════════════════════════════════════════════════

		private bool CheckAllSequencesCompleted()
		{
			foreach (var state in _sequenceStates.Values)
			{
				if (!state.isCompleted)
					return false;
			}
			return true;
		}

		private void CompleteLevel()
		{
			float elapsed = Time.time - _levelStartTime;

			if (debugMode)
				Debug.Log($"<color=lime>[SequenceManager] ★★★ LEVEL TAMAMLANDI ★★★ | Süre: {elapsed:F1}s | Hatalar: {_mistakes}</color>");

			PlayFabDataManager.Instance?.LogLevelCompleted(currentLevel.levelID, _score, _mistakes);

			currentLevel.onLevelComplete?.Invoke();

			// UI göster
			UIManager.Instance?.ShowLevelComplete(_score, elapsed, _mistakes);
		}

		// ═══════════════════════════════════════════════════════
		// PREREQUISITE CHECKS
		// ═══════════════════════════════════════════════════════

		private bool CheckSequencePrerequisites(SequenceData sequence)
		{
			if (sequence.prerequisiteSequences == null || sequence.prerequisiteSequences.Length == 0)
				return true;

			foreach (var prereqSeq in sequence.prerequisiteSequences)
			{
				if (prereqSeq == null) continue;

				if (!_sequenceStates.TryGetValue(prereqSeq.sequenceID, out SequenceState state) || !state.isCompleted)
				{
					return false;
				}
			}

			return true;
		}

		private void HandleSequencePrerequisiteFail(SequenceData sequence)
		{
			_mistakes++;

			if (debugMode)
				Debug.LogWarning($"<color=red>[SequenceManager] ✗ Sekans önkoşul hatası: {sequence.sequenceName}</color>");

			switch (sequence.onPrerequisiteFail)
			{
				case PrerequisiteFailAction.Block:
					// Hiçbir şey yapma
					break;

				case PrerequisiteFailAction.Warning:
					ShowWarning(sequence.prerequisiteFailMessage);
					break;

				case PrerequisiteFailAction.GameOver:
					ShowWarning(sequence.prerequisiteFailMessage);
					break;
			}
		}

		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Prerequisite'leri karşılayan ama spawn edilmemiş sekansları spawn eder
		/// </summary>
		private void CheckAndSpawnUnlockedSequences()
		{
			if (currentLevel == null || currentLevel.sequences == null)
				return;

			foreach (var sequence in currentLevel.sequences)
			{
				if (sequence == null) continue;

				// Zaten spawn edilmiş mi?
				if (_sequenceButtons.ContainsKey(sequence.sequenceID))
					continue;

				// spawnButtonAtStart = true ise zaten başta spawn edilmiş olmalı
				// Burada sadece spawnButtonAtStart = false olanları kontrol ediyoruz
				if (sequence.spawnButtonAtStart)
					continue;

				// Prerequisite'ler karşılanıyor mu?
				if (CheckSequencePrerequisites(sequence))
				{
					// Spawn et!
					SpawnSequenceButton(sequence.sequenceID);

					if (debugMode)
						Debug.Log($"<color=lime>[SequenceManager] ✓ Auto-spawned unlocked sequence: '{sequence.sequenceName}'</color>");
				}
			}
		}

		// UI HELPERS
		// ═══════════════════════════════════════════════════════

		private void HideSequenceButtons()
		{
			foreach (var btn in _sequenceButtons.Values)
			{
				if (btn != null)
				{
					// UISequenceButton'un SetActive metodunu kullan
					// Bu, UIWorldFollower'ı da devre dışı bırakır
					btn.SetActive(false);

					if (debugMode)
						Debug.Log($"[SequenceManager] Sequence button hidden: {btn.gameObject.name}");
				}
			}
		}

		public IEnumerator ShowSequenceButtons()
		{
			yield return new WaitForSeconds(1);
			foreach (var kvp in _sequenceButtons)
			{
				UISequenceButton btn = kvp.Value;
				if (btn == null) continue;

				// Sequence state'ini al
				if (_sequenceStates.TryGetValue(kvp.Key, out SequenceState state))
				{
					// Tamamlanmış ve hide flag'i true ise gösterme
					if (state.isCompleted)
					{
						// İlgili SequenceData'yı bul
						SequenceData sequence = currentLevel?.sequences?.FirstOrDefault(s => s.sequenceID == kvp.Key);

						if (sequence != null && sequence.hideButtonAfterComplete)
						{
							// Buton gizli kalmalı
							if (debugMode)
								Debug.Log($"[SequenceManager] Sequence button stays hidden: {btn.gameObject.name} (hideButtonAfterComplete=true)");

							continue; // Bu butonu aktif etme
						}
					}
				}

				// Normal akış: butonu göster
				btn.SetActive(true);

				if (debugMode)
					Debug.Log($"[SequenceManager] Sequence button shown: {btn.gameObject.name}");
			}
		}

		private void ShowWarning(string message)
		{
			if (warningPanel)
			{
				warningPanel.GetComponent<ModalWindowManager>().Open();
				if (warningMessageText) warningMessageText.text = message;
			}

			if (debugMode)
				Debug.Log($"<color=orange>[SequenceManager] ⚠ Warning: {message}</color>");
		}

		private void SetSequenceSelectionInstruction()
		{
			if (instructionText == null)
				return;

			string message = currentLevel != null && !string.IsNullOrWhiteSpace(currentLevel.sequenceSelectionInstructionText)
				? currentLevel.sequenceSelectionInstructionText
				: "Bir sekans seçin";

			instructionText.text = message;
		}

		private void UpdateSequenceProgressText()
		{
			if (_currentSequence == null)
				return;

			int totalActions = Mathf.Max(1, _currentSequence.GetTotalActionCount());
			int currentAction = Mathf.Clamp(_currentActionIndex + 1, 1, totalActions);

			if (levelText)
				levelText.text = $"Level {GetLevelNumber()}/{Mathf.Max(1, totalLevelCount)}";

			if (tmptext)
				tmptext.text = $"{currentAction}/{totalActions}";
		}

		private void ClearProgressTexts()
		{
			if (levelText)
				levelText.text = string.Empty;

			if (tmptext)
				tmptext.text = string.Empty;
		}

		private void ResolveProgressTextsFromCanvas()
		{
			GameObject canvasObject = GameObject.Find("Canvas");
			if (canvasObject == null)
			{
				Debug.LogWarning("[SequenceManager] Canvas bulunamadı; levelText/tmptext referansları boş kaldı.");
				return;
			}

			Transform canvasTransform = canvasObject.transform;
			if (canvasTransform.childCount > 10)
				levelText = canvasTransform.GetChild(10).GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

			if (canvasTransform.childCount > 11)
				tmptext = canvasTransform.GetChild(11).GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
		}

		private int GetLevelNumber()
		{
			string levelText = currentLevel != null
				? $"{currentLevel.levelName} {currentLevel.levelID}"
				: string.Empty;

			int number = 0;
			for (int i = 0; i < levelText.Length; i++)
			{
				if (!char.IsDigit(levelText[i]))
					continue;

				while (i < levelText.Length && char.IsDigit(levelText[i]))
				{
					number = number * 10 + (levelText[i] - '0');
					i++;
				}
				break;
			}

			return Mathf.Max(1, number);
		}

		private void EnsureFadeOverlay()
		{
			if (sequenceFadeOverlay != null)
			{
				PrepareFadeOverlay(sequenceFadeOverlay);
				return;
			}

			Canvas targetCanvas = FindObjectsOfType<Canvas>(true)
				.FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);

			if (targetCanvas == null)
			{
				if (debugMode)
					Debug.LogWarning("[SequenceManager] Sequence fade overlay oluşturulamadı: uygun Canvas bulunamadı.");
				return;
			}

			GameObject overlayObject = new GameObject("SequenceFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			overlayObject.transform.SetParent(targetCanvas.transform, false);
			overlayObject.transform.SetAsLastSibling();

			sequenceFadeOverlay = overlayObject.GetComponent<Image>();
			PrepareFadeOverlay(sequenceFadeOverlay);

			RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.offsetMin = Vector2.zero;
			rectTransform.offsetMax = Vector2.zero;
		}

		private void PrepareFadeOverlay(Image overlay)
		{
			if (overlay == null)
				return;

			overlay.color = new Color(0f, 0f, 0f, 0f);
			overlay.raycastTarget = false;
			overlay.gameObject.SetActive(true);
		}

		private void InvokeCurrentActionCompletionEvents(string actionID)
		{
			if (_currentAction == null || _currentAction.actionID != actionID)
				return;

			_currentAction.onActionComplete?.Invoke();
			TriggerSceneEvents(_currentAction.onCompleteEventIDs);
		}

		private IEnumerator PlaySequenceCompleteFade(System.Action midpointAction = null)
		{
			EnsureFadeOverlay();
			if (sequenceFadeOverlay == null)
				yield break;

			yield return FadeOverlayAlpha(0f, 1f, sequenceFadeDuration);
			midpointAction?.Invoke();

			if (sequenceFadeHoldDuration > 0f)
				yield return new WaitForSeconds(sequenceFadeHoldDuration);

			yield return FadeOverlayAlpha(1f, 0f, sequenceFadeDuration);
		}

		private IEnumerator FadeOverlayAlpha(float from, float to, float duration)
		{
			Color color = sequenceFadeOverlay.color;
			color.a = from;
			sequenceFadeOverlay.color = color;

			if (duration <= 0f)
			{
				color.a = to;
				sequenceFadeOverlay.color = color;
				yield break;
			}

			float elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				color.a = Mathf.Lerp(from, to, elapsed / duration);
				sequenceFadeOverlay.color = color;
				yield return null;
			}

			color.a = to;
			sequenceFadeOverlay.color = color;
		}

		private void ShowGameOver(string message)
		{
			if (gameOverPanel)
			{
				gameOverPanel.GetComponent<ModalWindowManager>().Open();
				if (gameOverMessageText) gameOverMessageText.text = message;
			}

			currentLevel.onLevelFail?.Invoke();

			if (debugMode)
				Debug.Log($"<color=red>[SequenceManager] ☠ GAME OVER: {message}</color>");
		}

		public void CloseWarning()
		{
			if (warningPanel) warningPanel.SetActive(false);
		}

		// ═══════════════════════════════════════════════════════
		// SEQUENCE BUTTON REGISTRATION
		// ═══════════════════════════════════════════════════════

		public void RegisterSequenceButton(string sequenceID, UISequenceButton button)
		{
			_sequenceButtons[sequenceID] = button;
		}

		// ═══════════════════════════════════════════════════════
		// CAMERA HANDLING
		// ═══════════════════════════════════════════════════════

		private void HandleSequenceEntryCamera(SequenceData sequence)
		{
			if (CameraManager.Instance == null) return;

			switch (sequence.entryCameraMode)
			{
				case CameraMode.Keep:
					// Hiçbir şey yapma
					break;

				case CameraMode.General:
					CameraManager.Instance.ResetToDefault();
					break;

				case CameraMode.Character:
					CameraManager.Instance.PushCamera("character");
					break;

				case CameraMode.ObjectClose:
				case CameraMode.Custom:
					if (!string.IsNullOrEmpty(sequence.entryCameraID))
					{
						CameraManager.Instance.PushCamera(sequence.entryCameraID);
					}
					break;
			}
		}

		// ═══════════════════════════════════════════════════════
		// ANIMATION & EFFECTS (ActionData'dan)
		// ═══════════════════════════════════════════════════════

		private void PlayAnimations(ActionData action, AnimationTiming timing)
		{
			if (action == null) return;

			foreach (var t in action.GetAnimationsForTiming(timing))
			{
				if (string.IsNullOrEmpty(t.animatorObjectID)) continue;

				GameObject obj = SceneObjectRegistry.Instance?.GetGameObjectByID(t.animatorObjectID);
				if (obj == null)
				{
					obj = GameObject.Find(t.animatorObjectID);
				}

				if (obj == null)
				{
					Debug.LogWarning($"[SequenceManager] Animator object '{t.animatorObjectID}' not found");
					continue;
				}

				Animator anim = obj.GetComponent<Animator>();
				if (anim == null)
				{
					Debug.LogWarning($"[SequenceManager] No Animator on '{t.animatorObjectID}'");
					continue;
				}

				switch (t.parameterType)
				{
					case AnimatorParameterType.Trigger:
						anim.SetTrigger(t.triggerName);

						// Trigger'ı hemen reset et (takılıp kalmasını önle)
						if (t.autoResetTrigger)
						{
							anim.ResetTrigger(t.triggerName);

							if (debugMode)
								Debug.Log($"[SequenceManager] Trigger reset: {t.animatorObjectID}.{t.triggerName}");
						}
						break;

					case AnimatorParameterType.Bool:
						anim.SetBool(t.triggerName, t.parameterValue > 0);
						break;

					case AnimatorParameterType.Float:
						anim.SetFloat(t.triggerName, t.parameterValue);
						break;

					case AnimatorParameterType.Int:
						anim.SetInteger(t.triggerName, (int)t.parameterValue);
						break;
				}

				if (debugMode)
					Debug.Log($"[SequenceManager] Anim: {t.animatorObjectID} → {t.triggerName}");
			}
		}

		private void PlaySound()
		{
			if (_currentAction?.soundClip && GetComponent<AudioSource>() is AudioSource audio)
			{
				audio.PlayOneShot(_currentAction.soundClip);
			}
		}

		private void SpawnParticle()
		{
			if (_currentAction == null || string.IsNullOrEmpty(_currentAction.particleEffectID)) return;

			GameObject prefab = Resources.Load<GameObject>($"Particles/{_currentAction.particleEffectID}");
			if (prefab == null)
			{
				Debug.LogWarning($"[SequenceManager] Particle '{_currentAction.particleEffectID}' not found");
				return;
			}

			Vector3 pos = _currentAction.particleSpawnOffset;

			if (!string.IsNullOrEmpty(_currentAction.targetObjectID))
			{
				Transform target = SceneObjectRegistry.Instance?.GetTransformByID(_currentAction.targetObjectID);
				if (target == null)
				{
					GameObject obj = GameObject.Find(_currentAction.targetObjectID);
					if (obj != null) target = obj.transform;
				}

				if (target != null)
					pos = target.position + _currentAction.particleSpawnOffset;
			}

			Destroy(Instantiate(prefab, pos, Quaternion.identity), 5f);
		}

		// ═══════════════════════════════════════════════════════
		// EQUIPMENT HANDLING (WearEquipment)
		// ═══════════════════════════════════════════════════════

		private HashSet<string> _wornEquipments = new HashSet<string>();

		public void OnEquipmentWorn(EquipmentData eq)
		{
			if (_currentAction == null || _currentAction.actionType != ActionType.WearEquipment)
				return;

			if (_currentAction.requiredEquipments == null || _currentAction.requiredEquipments.Length == 0)
				return;

			// Bu ekipman gerekli mi?
			bool isRequired = false;
			foreach (var required in _currentAction.requiredEquipments)
			{
				if (required == eq) { isRequired = true; break; }
			}

			if (!isRequired)
			{
				Debug.LogWarning($"[SequenceManager] Yanlış ekipman: {eq.equipmentName}");
				return;
			}

			if (_wornEquipments.Contains(eq.equipmentID)) return;

			_wornEquipments.Add(eq.equipmentID);

			// Sahnede 3D modeli göster
			if (!string.IsNullOrEmpty(eq.gameObjectID))
			{
				GameObject obj = SceneObjectRegistry.Instance?.GetGameObjectByID(eq.gameObjectID);
				if (obj != null) obj.SetActive(true);
			}

			if (debugMode)
				Debug.Log($"[SequenceManager] ✓ Ekipman giyildi: {eq.equipmentName} ({_wornEquipments.Count}/{_currentAction.requiredEquipments.Length})");

			if (_wornEquipments.Count >= _currentAction.requiredEquipments.Length)
			{
				_wornEquipments.Clear();
				OnActionCompleted(_currentAction.actionID);
			}
		}

		// ═══════════════════════════════════════════════════════
		// TOOL HANDLING (DragToWorld) - Equipment ile aynı mantık
		// ═══════════════════════════════════════════════════════

		private HashSet<string> _droppedTools = new HashSet<string>();

		public void OnToolDropped(string actionID, string uniqueDropID)
		{
			if (_currentAction == null || _currentAction.actionType != ActionType.DragToWorld)
				return;

			// Multiple drop zones kontrolü
			int totalDropZones = 0;
			if (_currentAction.toolDropMappings != null && _currentAction.toolDropMappings.Length > 0)
			{
				totalDropZones = _currentAction.toolDropMappings.Length;
			}
			else if (_currentAction.requiredTools != null)
			{
				totalDropZones = _currentAction.requiredTools.Length;
			}

			if (totalDropZones == 0) return;

			// Bu drop zone zaten tamamlandı mı?
			if (_droppedTools.Contains(uniqueDropID))
			{
				if (debugMode)
					Debug.Log($"[SequenceManager] Drop zone already completed: {uniqueDropID}");
				return;
			}

			_droppedTools.Add(uniqueDropID);

			if (debugMode)
				Debug.Log($"[SequenceManager] ✓ Tool dropped: {uniqueDropID} ({_droppedTools.Count}/{totalDropZones})");

			// Tümü bırakıldı mı?
			if (_droppedTools.Count >= totalDropZones)
			{
				_droppedTools.Clear();
				OnActionCompleted(_currentAction.actionID);
			}
		}
	

	// ═══════════════════════════════════════════════════════
		// ═══════════════════════════════════════════════════════
		// ANALYTICS HELPERS
		// ═══════════════════════════════════════════════════════

		private static string GetActionTypeString(ActionType type)
		{
			switch (type)
			{
				case ActionType.Quiz:            return "quiz";
				case ActionType.DragToWorld:
				case ActionType.WearEquipment:   return "drag_drop";
				case ActionType.Click:
				case ActionType.OpenClose:
				case ActionType.PanelInteraction:return "click";
				case ActionType.Survey:          return "survey";
				default:                         return "interaction";
			}
		}

		// ═══════════════════════════════════════════════════════
		// SCENE EVENT HELPERS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// ActionEventHandler üzerinden sahne event'lerini tetikler
		/// </summary>
		private void TriggerSceneEvents(string[] eventIDs)
		{
			if (eventIDs == null || eventIDs.Length == 0)
				return;

			ActionEventHandler handler = FindObjectOfType<ActionEventHandler>();
			if (handler == null)
			{
				Debug.LogWarning("[SequenceManager] ActionEventHandler bulunamadı! Scene'e ekleyin.");
				return;
			}

			handler.TriggerEvents(eventIDs);
		}

		/// <summary>
		/// Tek bir event ID'yi tetikler (public - tool drops için)
		/// </summary>
		public void TriggerSceneEvent(string eventID)
		{
			if (string.IsNullOrEmpty(eventID))
				return;

			TriggerSceneEvents(new string[] { eventID });
		}
	}

	// SEQUENCE STATE CLASS
	// ═══════════════════════════════════════════════════════

	[System.Serializable]
public class SequenceState
{
	public string sequenceID;
	public bool isCompleted;
	public int currentActionIndex;
	public HashSet<string> completedActionIDs;
	public float completionTime;
	public string lastCameraID;  // Resume için son kamera ID'si
}
}
