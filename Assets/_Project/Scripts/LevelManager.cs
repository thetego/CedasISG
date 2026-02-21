using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

namespace SafetyTraining
{
	/// <summary>
	/// Level akışının tek kontrolcüsü.
	/// 
	/// Akış:
	///   StartLevel()
	///     → UISpawnManager.SpawnAllUI()    (hepsi spawn + deaktif)
	///     → StartNextAction()
	///         → UISpawnManager.ActivateAction()   (sadece mevcut action'ın UI'ı aktif)
	///         → Kullanıcı etkileşim yapar
	///         → OnActionCompleted()
	///             → UISpawnManager.DeactivateAction()
	///             → Animasyon, ses, efekt
	///             → StartNextAction() (döngü)
	/// </summary>
	public class LevelManager : MonoBehaviour
	{
		public static LevelManager Instance { get; private set; }

		[Header("━━━ LEVEL ━━━")]
		public LevelData currentLevel;

		[Header("━━━ REFERANSLAR ━━━")]
		public CinemachineCamera virtualCamera;
		public AudioSource audioSource;

		[Header("━━━ UI ━━━")]
		[Tooltip("Talimat text'i (her action'da güncellenir)")]
		public TMPro.TextMeshProUGUI instructionText;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Runtime State ───
		private int _currentIndex;
		private ActionData _currentAction;
		private bool _levelCompleted;
		private float _levelStartTime;
		private int _score;
		private int _mistakes;

		// ─── WearEquipment için tracking ───
		private HashSet<string> _wornEquipments = new HashSet<string>();

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else { Destroy(gameObject); return; }
		}

		private void Start()
		{
			if (currentLevel != null)
				StartLevel();
			else
				Debug.LogError("[LevelManager] currentLevel atanmamış!");
		}

		// ═══════════════════════════════════════════════════════
		// LEVEL LIFECYCLE
		// ═══════════════════════════════════════════════════════

		public void StartLevel()
		{
			_currentIndex = 0;
			_levelCompleted = false;
			_levelStartTime = Time.time;
			_score = currentLevel.maxScore;
			_mistakes = 0;
			_wornEquipments.Clear();

			// Tüm UI'ları spawn et → deaktif
			UISpawnManager.Instance?.SpawnAllUI(currentLevel);

			currentLevel.onLevelStart?.Invoke();

			if (debugMode) Debug.Log($"<color=lime>[LM] Level başladı: {currentLevel.levelName}</color>");

			StartNextAction();
		}

		public void RestartLevel() => StartLevel();

		// ═══════════════════════════════════════════════════════
		// ACTION AKIŞI
		// ═══════════════════════════════════════════════════════

		private void StartNextAction()
		{
			// Bitti mi?
			if (_currentIndex >= currentLevel.GetTotalActionCount())
			{
				CompleteLevel();
				return;
			}

			_currentAction = currentLevel.GetActionAt(_currentIndex);
			if (_currentAction == null)
			{
				_currentIndex++;
				StartNextAction();
				return;
			}

			// WearEquipment ise tracking'i sıfırla
			if (_currentAction.actionType == ActionType.WearEquipment)
			{
				_wornEquipments.Clear();
			}

			if (debugMode)
				Debug.Log($"<color=yellow>[LM] Action [{_currentIndex + 1}/{currentLevel.GetTotalActionCount()}]: {_currentAction.actionName}</color>");

			// Talimat
			if (instructionText)
				instructionText.text = _currentAction.instructionText;

			// UI aktif et
			StartCoroutine(UISpawnManager.Instance?.ActivateAction(_currentAction, 1f));

			// OnStart animasyonlar
			PlayAnimations(_currentAction,AnimationTiming.OnStart);

			// OnStart events
			_currentAction.onActionStart?.Invoke();

			// CameraMove → özel handling
			if (_currentAction.actionType == ActionType.CameraMove)
				HandleCameraMove();
		}

		// ═══════════════════════════════════════════════════════
		// ACTION TAMAMLANDI
		// Çağrılan: UIClickButton, UIDropZone (LevelManager.Instance?.OnActionCompleted)
		// ═══════════════════════════════════════════════════════

		public void OnActionCompleted(string actionID)
		{
			// Mevcut action mı kontrol
			if (_currentAction == null || _currentAction.actionID != actionID)
			{
				if (debugMode)
					Debug.LogWarning($"[LM] OnActionCompleted '{actionID}' çağrıldı ama mevcut action '{_currentAction?.actionID}'");
				return;
			}

			if (debugMode)
				Debug.Log($"<color=lime>[LM] ✓ Tamamlandı: {_currentAction.actionName}</color>");

			// 1) UI deaktif et
			UISpawnManager.Instance?.DeactivateAction(_currentAction);

			// 2) OnComplete animasyonlar
			PlayAnimations(_currentAction, AnimationTiming.OnComplete);

			// 3) Ses
			PlaySound();

			// 4) Partikül
			SpawnParticle();

			// 5) Events
			_currentAction.onActionComplete?.Invoke();

			// 6) Delay varsa bekle sonra devam
			if (_currentAction.completionDelay > 0)
				StartCoroutine(WaitAndContinue(_currentAction.completionDelay));
			else
				MoveToNext();
		}

		/// <summary>
		/// Ekipman giyilince çağrılır (UIDropZone → OnEquipmentWorn)
		/// Tüm ekipmanlar giyildiğinde action tamamlanır
		/// </summary>
		public void OnEquipmentWorn(EquipmentData eq)
		{
			if (_currentAction == null || _currentAction.actionType != ActionType.WearEquipment)
				return;

			if (_currentAction.requiredEquipments == null || _currentAction.requiredEquipments.Length == 0)
				return;

			// Bu ekipman gerekli ekipmanlardan biri mi?
			bool isRequired = false;
			foreach (var required in _currentAction.requiredEquipments)
			{
				if (required == eq)
				{
					isRequired = true;
					break;
				}
			}

			if (!isRequired)
			{
				Debug.LogWarning($"[LM] Yanlış ekipman giyildi: {eq.equipmentName}");
				return;
			}

			// Zaten giyilmiş mi kontrol et
			if (_wornEquipments.Contains(eq.equipmentID))
			{
				if (debugMode)
					Debug.Log($"[LM] {eq.equipmentName} zaten giyilmişti");
				return;
			}

			// Kaydet
			_wornEquipments.Add(eq.equipmentID);

			if (debugMode)
				Debug.Log($"[LM] ✓ Ekipman giyildi: {eq.equipmentName} ({_wornEquipments.Count}/{_currentAction.requiredEquipments.Length})");

			// Tüm ekipmanlar giyildi mi?
			if (_wornEquipments.Count >= _currentAction.requiredEquipments.Length)
			{
				if (debugMode)
					Debug.Log($"<color=lime>[LM] ✓ Tüm ekipmanlar giyildi! Action tamamlandı.</color>");

				OnActionCompleted(_currentAction.actionID);
			}
		}

		/// <summary>
		/// Tool drop edilince çağrılır (UIDropZone → OnToolDropped)
		/// </summary>
		public void OnToolDropped(string actionID)
		{
			OnActionCompleted(actionID);
		}

		// ═══════════════════════════════════════════════════════
		// KAMERA
		// ═══════════════════════════════════════════════════════

		private void HandleCameraMove()
		{
			if (string.IsNullOrEmpty(_currentAction.cameraTargetID) || virtualCamera == null)
			{
				if (_currentAction.autoCompleteAfterCamera)
					OnActionCompleted(_currentAction.actionID);
				return;
			}

			Transform target = null;

			// SceneObjectRegistry üzerinden bul
			if (SceneObjectRegistry.Instance != null)
			{
				target = SceneObjectRegistry.Instance.GetTransformByID(_currentAction.cameraTargetID);
			}

			// Bulunamadıysa fallback: GameObject.Find
			if (target == null)
			{
				GameObject obj = GameObject.Find(_currentAction.cameraTargetID);
				if (obj != null)
					target = obj.transform;
			}

			if (target != null)
			{
				virtualCamera.Follow = target;
				virtualCamera.LookAt = target;

				if (_currentAction.autoCompleteAfterCamera)
					StartCoroutine(AutoCompleteAfterDelay(1f / _currentAction.cameraMoveSpeed));
			}
			else
			{
				Debug.LogError($"[LM] Camera target '{_currentAction.cameraTargetID}' not found! " +
							  $"Make sure a GameObject with SceneObject has objectID='{_currentAction.cameraTargetID}'");

				if (_currentAction.autoCompleteAfterCamera)
					OnActionCompleted(_currentAction.actionID);
			}
		}

		private IEnumerator AutoCompleteAfterDelay(float delay)
		{
			yield return new WaitForSeconds(delay);
			OnActionCompleted(_currentAction.actionID);
		}

		// ═══════════════════════════════════════════════════════
		// LEVEL TAMAMLANDI
		// ═══════════════════════════════════════════════════════

		private void CompleteLevel()
		{
			if (_levelCompleted) return;
			_levelCompleted = true;

			float elapsed = Time.time - _levelStartTime;
			if (currentLevel.timeLimit > 0 && elapsed < currentLevel.timeLimit)
				_score += currentLevel.bonusForSpeed;

			if (debugMode)
				Debug.Log($"<color=lime>[LM] ★ LEVEL TAMAMLANDI ★ | Skor: {_score} | Süre: {elapsed:F1}s | Hatalar: {_mistakes}</color>");

			currentLevel.onLevelComplete?.Invoke();
		}

		// ═══════════════════════════════════════════════════════
		// ANİMASYON / SES / PARTIKÜL
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Animasyonları oynatır (public - ActionManager'dan çağrılır)
		/// </summary>
		public void PlayAnimations(ActionData action = null, AnimationTiming timing = AnimationTiming.OnStart)
		{
			if (action == null) return;

			foreach (var t in action.GetAnimationsForTiming(timing))
			{
				if (string.IsNullOrEmpty(t.animatorObjectID)) continue;

				GameObject obj = null;

				// SceneObjectRegistry üzerinden bul
				if (SceneObjectRegistry.Instance != null)
				{
					obj = SceneObjectRegistry.Instance.GetGameObjectByID(t.animatorObjectID);
				}

				// Bulunamadıysa fallback: GameObject.Find
				if (obj == null)
				{
					obj = GameObject.Find(t.animatorObjectID);
				}

				if (obj == null)
				{
					Debug.LogWarning($"[LM] Animator object '{t.animatorObjectID}' not found");
					continue;
				}

				Animator anim = obj.GetComponent<Animator>();
				if (anim == null)
				{
					Debug.LogWarning($"[LM] No Animator component on '{t.animatorObjectID}'");
					continue;
				}

				switch (t.parameterType)
				{
					case AnimatorParameterType.Trigger: anim.SetTrigger(t.triggerName); break;
					case AnimatorParameterType.Bool: anim.SetBool(t.triggerName, t.parameterValue > 0); break;
					case AnimatorParameterType.Float: anim.SetFloat(t.triggerName, t.parameterValue); break;
					case AnimatorParameterType.Int: anim.SetInteger(t.triggerName, (int)t.parameterValue); break;
				}

				if (debugMode) Debug.Log($"[LM] Anim: {t.animatorObjectID} → {t.triggerName}");
			}
		}

		private void PlaySound()
		{
			if (_currentAction?.soundClip && audioSource)
				audioSource.PlayOneShot(_currentAction.soundClip);
		}

		private void SpawnParticle()
		{
			if (_currentAction == null || string.IsNullOrEmpty(_currentAction.particleEffectID)) return;

			GameObject prefab = Resources.Load<GameObject>($"Particles/{_currentAction.particleEffectID}");
			if (prefab == null) { Debug.LogWarning($"[LM] Particle '{_currentAction.particleEffectID}' not found"); return; }

			Vector3 pos = _currentAction.particleSpawnOffset;

			// Hedef obje varsa onun üstüne
			if (!string.IsNullOrEmpty(_currentAction.targetObjectID))
			{
				Transform target = null;

				// SceneObjectRegistry üzerinden bul
				if (SceneObjectRegistry.Instance != null)
				{
					target = SceneObjectRegistry.Instance.GetTransformByID(_currentAction.targetObjectID);
				}

				// Bulunamadıysa fallback
				if (target == null)
				{
					GameObject obj = GameObject.Find(_currentAction.targetObjectID);
					if (obj != null) target = obj.transform;
				}

				if (target != null)
				{
					pos = target.position + _currentAction.particleSpawnOffset;
				}
			}

			Destroy(Instantiate(prefab, pos, Quaternion.identity), 5f);
		}

		// ═══════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════

		private IEnumerator WaitAndContinue(float delay)
		{
			yield return new WaitForSeconds(delay);
			MoveToNext();
		}

		private void MoveToNext()
		{
			_currentIndex++;
			StartNextAction();
		}
	}
}