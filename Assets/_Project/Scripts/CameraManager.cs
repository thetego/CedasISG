using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Cinemachine Virtual Camera'ları Priority sistemi ile yönetir.
	/// Stack bazlı: Push → Priority artar, Pop → Önceki kameraya döner
	/// </summary>
	public class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		[Header("━━━ KAMERALAR ━━━")]
		[Tooltip("Varsayılan genel sahne kamerası")]
		public CinemachineCamera defaultCamera;

		[Tooltip("Karakter kamerası")]
		public CinemachineCamera characterCamera;

		[Header("━━━ PRİORİTY AYARLARI ━━━")]
		[Tooltip("Başlangıç priority değeri")]
		public int basePriority = 10;

		[Tooltip("Her push'ta priority artışı")]
		public int priorityIncrement = 10;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Camera Stack ───
		private Stack<CameraState> _cameraStack = new Stack<CameraState>();

		// ─── Registered Cameras (ID → VirtualCamera) ───
		private Dictionary<string, CinemachineCamera> _registeredCameras = new Dictionary<string, CinemachineCamera>();

		// ─── Current Priority ───
		private int _currentPriority;

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

			_currentPriority = basePriority;
		}

		private void Start()
		{
			RegisterAllCameras();
			SetDefaultCamera();
		}

		/// <summary>
		/// Sahnedeki tüm virtual camera'ları kayıt eder
		/// </summary>
		private void RegisterAllCameras()
		{
			// Manuel atananları kaydet
			if (defaultCamera != null)
			{
				_registeredCameras["general"] = defaultCamera;
				defaultCamera.Priority = basePriority;
			}

			if (characterCamera != null)
			{
				_registeredCameras["character"] = characterCamera;
				characterCamera.Priority = 0; // Başlangıçta deaktif
			}

			// Sahnedeki SceneObject'leri ara (virtual camera olabilirler)
			if (SceneObjectRegistry.Instance != null)
			{
				foreach (string objID in SceneObjectRegistry.Instance.GetAllObjectIDs())
				{
					GameObject obj = SceneObjectRegistry.Instance.GetGameObjectByID(objID);
					if (obj == null) continue;

					CinemachineCamera vcam = obj.GetComponent<CinemachineCamera>();
					if (vcam != null)
					{
						_registeredCameras[objID] = vcam;
						vcam.Priority = 0; // Başlangıçta deaktif

						if (debugMode)
							Debug.Log($"[CameraManager] ✓ Registered camera: '{objID}'");
					}
				}
			}

			if (debugMode)
				Debug.Log($"<color=cyan>[CameraManager] Total cameras registered: {_registeredCameras.Count}</color>");
		}

		/// <summary>
		/// Default kamerayı aktif eder
		/// </summary>
		private void SetDefaultCamera()
		{
			if (defaultCamera != null)
			{
				defaultCamera.Priority = basePriority;
				_currentPriority = basePriority;

				var initialState = new CameraState
				{
					cameraID = "general",
					camera = defaultCamera,
					priority = basePriority
				};

				_cameraStack.Push(initialState);

				if (debugMode)
					Debug.Log("[CameraManager] Default camera set: general");
			}
		}

		/// <summary>
		/// Kameraya geçiş yap (Stack'e ekle, priority artır)
		/// </summary>
		public void PushCamera(string cameraID)
		{
			if (string.IsNullOrEmpty(cameraID))
			{
				Debug.LogWarning("[CameraManager] PushCamera called with empty ID!");
				return;
			}

			// Kamerayı bul
			if (!_registeredCameras.TryGetValue(cameraID, out CinemachineCamera vcam))
			{
				Debug.LogError($"[CameraManager] Camera '{cameraID}' not registered!");
				return;
			}

			// Priority artır
			_currentPriority += priorityIncrement;
			vcam.Priority = _currentPriority;

			// Stack'e ekle
			var newState = new CameraState
			{
				cameraID = cameraID,
				camera = vcam,
				priority = _currentPriority

			}; 

			//SequenceManager.Instance.OnActionCompleted(SequenceManager.Instance.CurrentAction.actionID);
			_cameraStack.Push(newState);

			if (debugMode)
				Debug.Log($"<color=yellow>[CameraManager] → Pushed camera: '{cameraID}' (Priority: {_currentPriority})</color>");
		}

		/// <summary>
		/// Önceki kameraya dön (Stack'ten çıkar, priority düşür)
		/// </summary>
		public void PopCamera()
		{
			if (_cameraStack.Count <= 1)
			{
				Debug.LogWarning("[CameraManager] Cannot pop camera, already at base!");
				return;
			}

			// Mevcut kamerayı stack'ten çıkar
			CameraState currentState = _cameraStack.Pop();
			currentState.camera.Priority = 0; // Deaktif et

			// Önceki kamerayı aktif et
			CameraState previousState = _cameraStack.Peek();
			_currentPriority = previousState.priority;

			if (debugMode)
				Debug.Log($"<color=green>[CameraManager] ← Popped to camera: '{previousState.cameraID}' (Priority: {_currentPriority})</color>");
		}

		/// <summary>
		/// Stack'i temizle ve default kameraya dön
		/// </summary>
		public void ResetToDefault()
		{
			// Tüm kameraları deaktif et
			foreach (var vcam in _registeredCameras.Values)
			{
				vcam.Priority = 0;
			}

			// Stack'i temizle
			_cameraStack.Clear();

			// Default'u aktif et
			SetDefaultCamera();

			if (debugMode)
				Debug.Log("[CameraManager] Reset to default camera");
		}

		/// <summary>
		/// Belirli bir kamerayı direkt aktif eder (stack bypass)
		/// </summary>
		public void SetCamera(string cameraID, bool addToStack = true)
		{
			if (!_registeredCameras.TryGetValue(cameraID, out CinemachineCamera vcam))
			{
				Debug.LogError($"[CameraManager] Camera '{cameraID}' not registered!");
				return;
			}

			if (addToStack)
			{
				PushCamera(cameraID);
			}
			else
			{
				// Direkt set et (stack'e ekleme)
				vcam.Priority = _currentPriority + priorityIncrement;
			}
		}

		/// <summary>
		/// ActionData'dan kamera moduna göre geçiş yapar
		/// </summary>
		public void HandleActionCamera(ActionData action)
		{
			if (action == null) return;

			switch (action.cameraMode)
			{
				case CameraMode.Keep:
					// Hiçbir şey yapma
					break;

				case CameraMode.General:
					// Genel kameraya dön (pop until base)
					while (_cameraStack.Count > 1)
						PopCamera();
					break;

				case CameraMode.Character:
					PushCamera("character");
					break;

				case CameraMode.ObjectClose:
				case CameraMode.Custom:
					if (!string.IsNullOrEmpty(action.virtualCameraID))
					{
						PushCamera(action.virtualCameraID);
					}
					else
					{
						Debug.LogWarning($"[CameraManager] Action '{action.actionID}' has ObjectClose/Custom mode but no virtualCameraID!");
					}
					break;
			}
		}

		/// <summary>
		/// Mevcut kamera ID'sini döndürür
		/// </summary>
		public string GetCurrentCameraID()
		{
			if (_cameraStack.Count > 0)
				return _cameraStack.Peek().cameraID;

			return "none";
		}

		/// <summary>
		/// Stack'teki aktif (en üstteki) CinemachineCamera'yı döndürür.
		/// SurveyCameraController gibi sistemler bunu kullanarak aktif vcam'i kontrol eder.
		/// </summary>
		public CinemachineCamera GetCurrentCamera()
		{
			if (_cameraStack.Count > 0)
				return _cameraStack.Peek().camera;
			return null;
		}

		/// <summary>
		/// Stack depth'i döndürür (kaç seviye derinlik)
		/// </summary>
		public int GetStackDepth() => _cameraStack.Count;

		/// <summary>
		/// Back button gösterilmeli mi? (stack depth > 1)
		/// </summary>
		public bool ShouldShowBackButton() => _cameraStack.Count > 1;
	}

	/// <summary>
	/// Kamera stack state
	/// </summary>
	[System.Serializable]
	public class CameraState
	{
		public string cameraID;
		public CinemachineCamera camera;
		public int priority;
	}
}