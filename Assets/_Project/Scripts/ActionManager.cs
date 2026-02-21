using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Michsky.MUIP;

namespace SafetyTraining
{
	/// <summary>
	/// Serbest action sistemi - Kullanıcı istediği sırada action yapabilir
	/// Önkoşul kontrolü yapar, hata durumlarını yönetir
	/// </summary>
	public class ActionManager : MonoBehaviour
	{
		public static ActionManager Instance { get; private set; }

		[Header("━━━ LEVEL ━━━")]
		public LevelData currentLevel;

		[Header("━━━ UI ━━━")]
		public TMPro.TextMeshProUGUI instructionText;
		public GameObject gameOverPanel;
		public TMPro.TextMeshProUGUI gameOverMessageText;
		public GameObject warningPanel;
		public TMPro.TextMeshProUGUI warningMessageText;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Runtime State ───
		private Dictionary<string, ActionData> _allActions = new Dictionary<string, ActionData>();
		private HashSet<string> _completedActions = new HashSet<string>();
		private HashSet<string> _availableActions = new HashSet<string>();
		private Dictionary<string, UIActionButton> _actionButtons = new Dictionary<string, UIActionButton>();

		private ActionData _currentAction;
		private bool _levelStarted;
		private float _levelStartTime;
		private int _score;
		private int _mistakes;

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
		}

		private void Start()
		{
			if (currentLevel != null)
				StartLevel();
			else
				Debug.LogError("[ActionManager] currentLevel atanmamış!");
		}

		/// <summary>
		/// Level'ı başlatır - tüm actionları kaydeder
		/// </summary>
		public void StartLevel()
		{
			_allActions.Clear();
			_completedActions.Clear();
			_availableActions.Clear();
			_levelStarted = true;
			_levelStartTime = Time.time;
			_score = currentLevel.maxScore;
			_mistakes = 0;

			// Tüm actionları kaydet
			foreach (var action in currentLevel.actionSequence)
			{
				if (action == null) continue;
				_allActions[action.actionID] = action;
			}

			if (debugMode)
				Debug.Log($"<color=lime>[ActionManager] Level başladı: {currentLevel.levelName} ({_allActions.Count} actions)</color>");

			// UI spawn (tüm actionlar için butonlar)
			UISpawnManager.Instance?.SpawnAllActionButtons(currentLevel);

			// Available actionları hesapla
			UpdateAvailableActions();

			// Level start event
			currentLevel.onLevelStart?.Invoke();

			// Talimat
			if (instructionText)
				instructionText.text = "Bir aksiyon seçin";
		}

		/// <summary>
		/// Available actionları günceller (prerequisite kontrolü)
		/// </summary>
		private void UpdateAvailableActions()
		{
			_availableActions.Clear();

			foreach (var kvp in _allActions)
			{
				string actionID = kvp.Key;
				ActionData action = kvp.Value;

				// Zaten tamamlandıysa
				if (_completedActions.Contains(actionID))
				{
					// Completed olarak işaretle
					if (_actionButtons.TryGetValue(actionID, out UIActionButton btn))
					{
						btn.SetState(UIActionButton.ButtonState.Completed);

						// Tamamlandıktan sonra gizlensin mi?
						if (action.hideButtonAfterComplete)
							btn.SetActive(false);
					}
					continue;
				}

				// Prerequisite kontrolü
				bool prerequisitesMet = CheckPrerequisites(action);

				if (prerequisitesMet)
				{
					_availableActions.Add(actionID);

					// Buton state: Available
					if (_actionButtons.TryGetValue(actionID, out UIActionButton btn))
					{
						btn.SetState(UIActionButton.ButtonState.Available);
						btn.SetActive(true);
					}
				}
				else
				{
					// Buton state: Disabled
					if (_actionButtons.TryGetValue(actionID, out UIActionButton btn))
					{
						btn.SetState(UIActionButton.ButtonState.Disabled);
						btn.SetActive(true); // Göster ama disabled
					}
				}
			}

			if (debugMode)
				Debug.Log($"[ActionManager] Available actions: {_availableActions.Count}/{_allActions.Count}");
		}

		/// <summary>
		/// Prerequisite kontrolü
		/// </summary>
		private bool CheckPrerequisites(ActionData action)
		{
			if (action.prerequisiteActionIDs == null || action.prerequisiteActionIDs.Length == 0)
				return true; // Önkoşul yok

			foreach (string prereqID in action.prerequisiteActionIDs)
			{
				if (!_completedActions.Contains(prereqID))
					return false; // Bir önkoşul eksik
			}

			return true; // Tüm önkoşullar sağlandı
		}

		/// <summary>
		/// Buton tıklandığında çağrılır
		/// </summary>
		public void OnActionButtonClicked(string actionID)
		{
			if (!_allActions.TryGetValue(actionID, out ActionData action))
			{
				Debug.LogError($"[ActionManager] Action '{actionID}' not found!");
				return;
			}

			// Prerequisite kontrolü
			if (!CheckPrerequisites(action))
			{
				HandlePrerequisiteFail(action);
				return;
			}

			// Action başlat
			StartAction(action);
		}

		/// <summary>
		/// Action'ı başlatır
		/// </summary>
		private void StartAction(ActionData action)
		{
			_currentAction = action;

			if (debugMode)
				Debug.Log($"<color=yellow>[ActionManager] Action başladı: {action.actionName}</color>");

			// Talimat
			if (instructionText)
				instructionText.text = action.instructionText;

			// Kamera geçişi
			CameraManager.Instance?.HandleActionCamera(action);

			// UI aktif et (ekipman paneli vb.)
			StartCoroutine(UISpawnManager.Instance?.ActivateAction(_currentAction, 1f));

			// OnStart animasyonlar
			if (LevelManager.Instance != null)
				LevelManager.Instance.PlayAnimations(action, AnimationTiming.OnStart);
			else
				Debug.LogWarning("[ActionManager] LevelManager.Instance null!");

			// OnStart events
			action.onActionStart?.Invoke();
		}

		/// <summary>
		/// Action tamamlandığında çağrılır
		/// </summary>
		public void OnActionCompleted(string actionID)
		{
			if (_currentAction == null || _currentAction.actionID != actionID)
			{
				// Farklı bir action tamamlandı (ekipman gibi, sıra dışı)
				if (!_allActions.TryGetValue(actionID, out ActionData action))
					return;

				_currentAction = action;
			}

			if (debugMode)
				Debug.Log($"<color=lime>[ActionManager] ✓ Action tamamlandı: {_currentAction.actionName}</color>");

			// Kaydet
			_completedActions.Add(_currentAction.actionID);

			// UI deaktif et
			UISpawnManager.Instance?.DeactivateAction(_currentAction);

			// OnComplete animasyonlar
			if (LevelManager.Instance != null)
				LevelManager.Instance.PlayAnimations(_currentAction, AnimationTiming.OnComplete);

			// OnComplete events
			_currentAction.onActionComplete?.Invoke();

			// Kamera geri dön (varsa)
			if (_currentAction.autoReturnCameraOnComplete)
			{
				CameraManager.Instance?.PopCamera();
			}

			// Available actionları güncelle
			UpdateAvailableActions();

			// Tüm actionlar tamamlandı mı?
			if (_completedActions.Count >= _allActions.Count)
			{
				CompleteLevel();
			}
			else
			{
				// Talimat güncelle
				if (instructionText)
					instructionText.text = "Sonraki aksiyonu seçin";
			}

			_currentAction = null;
		}

		/// <summary>
		/// Prerequisite hatası
		/// </summary>
		private void HandlePrerequisiteFail(ActionData action)
		{
			_mistakes++;

			if (debugMode)
				Debug.LogWarning($"<color=red>[ActionManager] ✗ Prerequisite fail: {action.actionName}</color>");

			switch (action.onPrerequisiteFail)
			{
				case PrerequisiteFailAction.Block:
					// Hiçbir şey yapma, buton zaten disabled
					break;

				case PrerequisiteFailAction.Warning:
					ShowWarning(action.prerequisiteFailMessage);
					break;

				case PrerequisiteFailAction.GameOver:
					ShowGameOver(action.prerequisiteFailMessage);
					break;
			}

			// OnFail animasyonlar
			if (LevelManager.Instance != null)
				LevelManager.Instance.PlayAnimations(action, AnimationTiming.OnFail);

			// OnFail events
			action.onActionFail?.Invoke();
		}

		/// <summary>
		/// Uyarı göster (Warning)
		/// </summary>
		private void ShowWarning(string message)
		{
			if (warningPanel)
			{
				warningPanel.SetActive(true);
				if (warningMessageText) warningMessageText.text = message;
			}

			if (debugMode)
				Debug.Log($"<color=orange>[ActionManager] ⚠ Warning: {message}</color>");
		}

		/// <summary>
		/// GameOver göster
		/// </summary>
		private void ShowGameOver(string message)
		{
			if (gameOverPanel)
			{
				gameOverPanel.SetActive(true);
				if (gameOverMessageText) gameOverMessageText.text = message;
			}

			currentLevel.onLevelFail?.Invoke();

			if (debugMode)
				Debug.Log($"<color=red>[ActionManager] ☠ GAME OVER: {message}</color>");
		}

		/// <summary>
		/// Level tamamlandı
		/// </summary>
		private void CompleteLevel()
		{
			float elapsed = Time.time - _levelStartTime;

			if (debugMode)
				Debug.Log($"<color=lime>[ActionManager] ★ LEVEL TAMAMLANDI ★ | Süre: {elapsed:F1}s | Hatalar: {_mistakes}</color>");

			currentLevel.onLevelComplete?.Invoke();

			// UI göster
			UIManager.Instance?.ShowLevelComplete(_score, elapsed, _mistakes);
		}

		/// <summary>
		/// Warning panel kapatma (button callback)
		/// </summary>
		public void CloseWarning()
		{
			if (warningPanel) warningPanel.SetActive(false);
		}

		/// <summary>
		/// Prerequisite uyarısı göster (disabled button tıklandığında)
		/// </summary>
		public void ShowPrerequisiteWarning(string actionID)
		{
			if (!_allActions.TryGetValue(actionID, out ActionData action))
				return;

			// Hangi önkoşullar eksik?
			List<string> missingActions = new List<string>();

			if (action.prerequisiteActionIDs != null)
			{
				foreach (string prereqID in action.prerequisiteActionIDs)
				{
					if (!_completedActions.Contains(prereqID))
					{
						if (_allActions.TryGetValue(prereqID, out ActionData prereqAction))
						{
							missingActions.Add(prereqAction.actionName);
						}
						else
						{
							missingActions.Add(prereqID);
						}
					}
				}
			}

			string message = "Bu aksiyonu yapmak için önce:\n";
			foreach (string name in missingActions)
			{
				message += $"• {name}\n";
			}
			message += "tamamlanmalı.";

			ShowWarning(message);
		}

		/// <summary>
		/// Action buton referansını kaydet
		/// </summary>
		public void RegisterActionButton(string actionID, UIActionButton button)
		{
			_actionButtons[actionID] = button;
		}

		/// <summary>
		/// Level yeniden başlat (button callback)
		/// </summary>
		public void RestartLevel()
		{
			if (gameOverPanel) gameOverPanel.SetActive(false);
			CameraManager.Instance?.ResetToDefault();
			StartLevel();
		}
	}
}