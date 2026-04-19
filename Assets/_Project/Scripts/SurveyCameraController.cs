using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Cinemachine;
using System.Collections.Generic;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace SafetyTraining
{
	/// <summary>
	/// Tablet survey kamera kontrolcüsü.
	/// - RenderTexture/RawImage yerine overlay'deki şeffaf alan üzerinden ana kamera görüntüsü kullanılır
	/// - O an aktif olan Cinemachine kameranın transform'unu kontrol eder
	/// - Kontrol bitince kamera orijinal transform ve FOV değerlerine döner
	/// - Hedef göstergesi olarak dünya uzayında yarı saydam küre spawn edilir
	/// </summary>
	public class SurveyCameraController : MonoBehaviour
	{
		// ═══════════════════════════════════════════════════════
		// INSPECTOR
		// ═══════════════════════════════════════════════════════

		[Header("━━━ PAN AYARLARI ━━━")]
		[Tooltip("Pan hassasiyeti")]
		public float panSensitivity = 0.003f;

		[Tooltip("Minimum euler X (dikey sınır alt)")]
		public float minPitch = -30f;

		[Tooltip("Maximum euler X (dikey sınır üst)")]
		public float maxPitch = 30f;

		[Tooltip("Minimum euler Y (yatay sınır sol)")]
		public float minYaw = -45f;

		[Tooltip("Maximum euler Y (yatay sınır sağ)")]
		public float maxYaw = 45f;

		[Header("━━━ ZOOM AYARLARI ━━━")]
		[Tooltip("Minimum FOV")]
		public float minFOV = 20f;

		[Tooltip("Maximum FOV")]
		public float maxFOV = 70f;

		[Tooltip("Zoom hassasiyeti (pinch)")]
		public float zoomSensitivity = 0.05f;

		[Header("━━━ GÖSTERGELER ━━━")]
		[Tooltip("Hedef noktasına dünya uzayında spawn edilecek küre prefabı (yarı saydam materyal olmalı)")]
		public GameObject worldIndicatorPrefab;

		[Tooltip("Küre boyutu (world space scale)")]
		public float indicatorScale = 0.2f;

		[Header("━━━ DOKUNUŞ ALANI ━━━")]
		[Tooltip("Dokunuş girişinin aktif olacağı RectTransform (overlay'deki şeffaf kamera alanı)")]
		public RectTransform cameraViewRect;

		[Header("━━━ TERMAL KAMERA ━━━")]
		[Tooltip("ThermalCameraRenderer bileşenini taşıyan obje — null ise termal efekt devre dışı")]
		public ThermalCameraRenderer thermalRenderer;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ═══════════════════════════════════════════════════════
		// RUNTIME
		// ═══════════════════════════════════════════════════════

		private float _currentPitch;
		private float _currentYaw;

		// Aktif Cinemachine kamera ve kaydedilen durum
		private CinemachineCamera          _activeVcam;
		private Quaternion                 _savedRotation;
		private float                      _savedFOV;
		private CinemachineComponentBase[] _vcamComponents;
		private bool[]                     _componentEnabledStates;

		// Dünya uzayı göstergeler — key: slot index, value: indicator GameObject
		private Dictionary<int, GameObject> _indicatorsBySlot = new Dictionary<int, GameObject>();
		private List<PhotoSlot>             _activeSlots      = new List<PhotoSlot>();
		private int                         _activeSlotIndex  = -1;
		private HashSet<int>                _photographedSlots = new HashSet<int>();

		public bool  IsAligned      { get; private set; }
		public float AlignmentScore { get; private set; }

		// ═══════════════════════════════════════════════════════
		// INIT
		// ═══════════════════════════════════════════════════════

		private void Awake()
		{
			EnhancedTouchSupport.Enable();
		}

		private void OnDestroy()
		{
			EnhancedTouchSupport.Disable();
			RestoreCameraState();
			ClearIndicators();
		}

		/// <summary>
		/// Slot listesini dışarıdan set et.
		/// indicatorParent parametresi geriye dönük uyumluluk için korundu, artık kullanılmıyor.
		/// </summary>
		public void Initialize(List<PhotoSlot> slots, Transform indicatorParent)
		{
			_activeSlots = slots ?? new List<PhotoSlot>();
			_photographedSlots.Clear();
			ClearIndicators();

			// Tüm slotların indicator'larını önceden spawn et (başta hepsi gizli)
			for (int i = 0; i < _activeSlots.Count; i++)
				SpawnWorldIndicator(i, _activeSlots[i]);
		}

		/// <summary>
		/// Fotoğrafı çekilen slotun indicator'ını kalıcı olarak sil.
		/// </summary>
		public void MarkSlotPhotographed(int slotIndex)
		{
			_photographedSlots.Add(slotIndex);

			if (_indicatorsBySlot.TryGetValue(slotIndex, out GameObject ind))
			{
				if (ind != null) Destroy(ind);
				_indicatorsBySlot.Remove(slotIndex);
			}
		}

		// ═══════════════════════════════════════════════════════
		// CİNEMACHİNE KAMERA YÖNETİMİ
		// ═══════════════════════════════════════════════════════

		private void GrabActiveCinemachineCamera()
		{
			if (CameraManager.Instance == null)
			{
				Debug.LogError("[SurveyCameraController] CameraManager.Instance null!");
				return;
			}

			_activeVcam = CameraManager.Instance.GetCurrentCamera();
			if (_activeVcam == null)
			{
				Debug.LogError("[SurveyCameraController] Aktif Cinemachine kamera bulunamadı!");
				return;
			}

			// Mevcut durumu kaydet
			_savedRotation = _activeVcam.transform.rotation;
			_savedFOV      = _activeVcam.Lens.FieldOfView;

			// Cinemachine pipeline bileşenlerini geçici olarak devre dışı bırak
			// (kameranın transform'unu her frame overwrite etmesini önlemek için)
			_vcamComponents         = _activeVcam.GetComponents<CinemachineComponentBase>();
			_componentEnabledStates = new bool[_vcamComponents.Length];
			for (int i = 0; i < _vcamComponents.Length; i++)
			{
				_componentEnabledStates[i] = _vcamComponents[i].enabled;
				_vcamComponents[i].enabled = false;
			}

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Vcam yakalandı: '{_activeVcam.name}', " +
					$"{_vcamComponents.Length} pipeline bileşen devre dışı bırakıldı");
		}

		private void RestoreCameraState()
		{
			if (_activeVcam == null) return;

			// Pipeline bileşenlerini eski haline getir
			if (_vcamComponents != null)
			{
				for (int i = 0; i < _vcamComponents.Length; i++)
				{
					if (_vcamComponents[i] != null)
						_vcamComponents[i].enabled = _componentEnabledStates[i];
				}
			}

			// Transform ve FOV'u orijinal değerlerine döndür
			_activeVcam.transform.rotation = _savedRotation;
			var lens = _activeVcam.Lens;
			lens.FieldOfView = _savedFOV;
			_activeVcam.Lens = lens;

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Kamera orijinal haline döndürüldü: '{_activeVcam.name}'");

			_activeVcam             = null;
			_vcamComponents         = null;
			_componentEnabledStates = null;
		}

		// ═══════════════════════════════════════════════════════
		// SLOT AKTİVASYON
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Belirli bir slot için kamera modunu aktifleştirir.
		/// </summary>
		public void ActivateForSlot(int slotIndex)
		{
			RestoreCameraState();

			_activeSlotIndex = slotIndex;

			GrabActiveCinemachineCamera();
			ResetCamera();

			if (thermalRenderer != null)
				thermalRenderer.enabled = true;

			// Sadece aktif slotun indicator'ını göster, diğerlerini gizle
			foreach (var kvp in _indicatorsBySlot)
				kvp.Value.SetActive(kvp.Key == slotIndex);
		}

		/// <summary>
		/// Kamera modunu kapat ve orijinal duruma döndür.
		/// </summary>
		public void DeactivateCamera()
		{
			_activeSlotIndex = -1;
			RestoreCameraState();

			// Kamera kapanınca tüm indicator'ları gizle
			foreach (var kvp in _indicatorsBySlot)
				if (kvp.Value != null) kvp.Value.SetActive(false);

			if (thermalRenderer != null)
				thermalRenderer.enabled = false;

			if (debugMode)
				Debug.Log("[SurveyCameraController] Kamera deaktif edildi.");
		}

		/// <summary>
		/// Kamerayı kaydedilen başlangıç rotasyonuna sıfırlar.
		/// </summary>
		public void ResetCamera()
		{
			_currentPitch = 0f;
			_currentYaw   = 0f;
			ApplyRotation();

			if (_activeVcam != null)
			{
				var lens = _activeVcam.Lens;
				lens.FieldOfView = (minFOV + maxFOV) * 0.5f;
				_activeVcam.Lens = lens;
			}
		}

		// ═══════════════════════════════════════════════════════
		// UPDATE — DOKUNUŞ KONTROLÜ
		// ═══════════════════════════════════════════════════════

		private void Update()
		{
			if (_activeSlotIndex < 0) return;
			HandleTouchInput();
			UpdateAlignmentScore();
		}

		private void HandleTouchInput()
		{
			var activeTouches = Touch.activeTouches;
			int touchCount    = activeTouches.Count;

			// Tek parmak → Pan
			if (touchCount == 1)
			{
				Touch touch = activeTouches[0];
				if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
				{
					if (!IsTouchOnCameraView(touch.screenPosition)) return;

					Vector2 delta = touch.delta;
					_currentPitch = Mathf.Clamp(_currentPitch + (-delta.y * panSensitivity * 100f), minPitch, maxPitch);
					_currentYaw   = Mathf.Clamp(_currentYaw   + ( delta.x * panSensitivity * 100f), minYaw,   maxYaw);
					ApplyRotation();
				}
			}
			// İki parmak → Pinch zoom
			else if (touchCount == 2)
			{
				Touch t0 = activeTouches[0];
				Touch t1 = activeTouches[1];

				Vector2 prevT0 = t0.screenPosition - t0.delta;
				Vector2 prevT1 = t1.screenPosition - t1.delta;

				float prevMag = (prevT0 - prevT1).magnitude;
				float currMag = (t0.screenPosition - t1.screenPosition).magnitude;
				float delta   = currMag - prevMag;

				if (_activeVcam != null)
				{
					var lens = _activeVcam.Lens;
					lens.FieldOfView = Mathf.Clamp(lens.FieldOfView - delta * zoomSensitivity, minFOV, maxFOV);
					_activeVcam.Lens = lens;
				}
			}

			#if UNITY_EDITOR
			HandleMouseInput();
			#endif
		}

		#if UNITY_EDITOR
		private Vector2 _lastMousePos;
		private bool    _mouseDown;

		private void HandleMouseInput()
		{
			var mouse = Mouse.current;
			if (mouse == null) return;

			Vector2 mousePos = mouse.position.ReadValue();
			bool overView    = IsTouchOnCameraView(mousePos);

			if (mouse.leftButton.wasPressedThisFrame && overView)
			{
				_lastMousePos = mousePos;
				_mouseDown    = true;
			}

			if (mouse.leftButton.wasReleasedThisFrame)
				_mouseDown = false;

			if (_mouseDown && mouse.leftButton.isPressed)
			{
				Vector2 delta = mousePos - _lastMousePos;
				_currentPitch = Mathf.Clamp(_currentPitch - delta.y * panSensitivity * 100f, minPitch, maxPitch);
				_currentYaw   = Mathf.Clamp(_currentYaw   + delta.x * panSensitivity * 100f, minYaw,   maxYaw);
				ApplyRotation();
				_lastMousePos = mousePos;
			}

			if (overView && _activeVcam != null)
			{
				float scroll = mouse.scroll.ReadValue().y;
				if (scroll != 0f)
				{
					var lens = _activeVcam.Lens;
					lens.FieldOfView = Mathf.Clamp(lens.FieldOfView - scroll * 0.05f * zoomSensitivity * 100f, minFOV, maxFOV);
					_activeVcam.Lens = lens;
				}
			}
		}
		#endif

		private void ApplyRotation()
		{
			if (_activeVcam == null) return;
			// Kaydedilen rotasyona göre pitch/yaw offset'i uygula (local euler gibi davranır)
			_activeVcam.transform.rotation = _savedRotation * Quaternion.Euler(_currentPitch, _currentYaw, 0f);
		}

		private bool IsTouchOnCameraView(Vector2 screenPos)
		{
			if (cameraViewRect == null) return true;
			return RectTransformUtility.RectangleContainsScreenPoint(cameraViewRect, screenPos, null);
		}

		// ═══════════════════════════════════════════════════════
		// DÜNYA UZAYI GÖSTERGELERİ
		// ═══════════════════════════════════════════════════════

		private void SpawnWorldIndicator(int slotIndex, PhotoSlot slot)
		{
			if (worldIndicatorPrefab == null || string.IsNullOrEmpty(slot.targetObjectID)) return;

			Transform target = SceneObjectRegistry.Instance?.GetTransformByID(slot.targetObjectID);
			if (target == null)
			{
				Debug.LogWarning($"[SurveyCameraController] Hedef obje bulunamadı: '{slot.targetObjectID}'");
				return;
			}

			GameObject ind = Instantiate(worldIndicatorPrefab, target.position, Quaternion.identity);
			ind.transform.localScale = Vector3.one * indicatorScale;
			ind.SetActive(false); // Başta gizli — ActivateForSlot açar

			_indicatorsBySlot[slotIndex] = ind;

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Indicator spawn: slot {slotIndex} → '{slot.targetObjectID}'");
		}

		private void UpdateAlignmentScore()
		{
			if (_activeSlotIndex < 0 || _activeSlotIndex >= _activeSlots.Count) return;

			PhotoSlot slot = _activeSlots[_activeSlotIndex];

			if (!_indicatorsBySlot.TryGetValue(_activeSlotIndex, out GameObject indicator) ||
			    indicator == null ||
			    string.IsNullOrEmpty(slot.targetObjectID) ||
			    Camera.main == null)
			{
				IsAligned      = false;
				AlignmentScore = 0f;
				return;
			}

			Transform target = SceneObjectRegistry.Instance?.GetTransformByID(slot.targetObjectID);
			if (target == null) return;

			// Ana kamera üzerinden viewport koordinatı hesapla
			Vector3 viewportPos = Camera.main.WorldToViewportPoint(target.position);

			if (viewportPos.z < 0)
			{
				indicator.SetActive(false);
				IsAligned      = false;
				AlignmentScore = 0f;
				return;
			}

			indicator.SetActive(true);

			// Viewport merkezinden uzaklık → hizalama skoru
			float distFromCenter = Vector2.Distance(
				new Vector2(viewportPos.x, viewportPos.y),
				new Vector2(0.5f, 0.5f)
			);

			AlignmentScore = Mathf.Clamp01(1f - (distFromCenter / slot.alignmentThreshold));
			IsAligned      = distFromCenter <= slot.alignmentThreshold;

			// Kürenin rengini güncelle (yarı saydam)
			Renderer rend = indicator.GetComponent<Renderer>();
			if (rend != null)
			{
				Color c = Color.Lerp(slot.indicatorOffColor, slot.indicatorAlignedColor, AlignmentScore);
				c.a = 0.6f;
				rend.material.color = c;
			}

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Slot {_activeSlotIndex}: " +
					$"dist={distFromCenter:F3} score={AlignmentScore:F2} aligned={IsAligned}");
		}

		private void ClearIndicators()
		{
			foreach (var kvp in _indicatorsBySlot)
				if (kvp.Value != null) Destroy(kvp.Value);
			_indicatorsBySlot.Clear();
		}

		// ═══════════════════════════════════════════════════════
		// FOTOĞRAF ÇEKME
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Ana kamerayı (UI olmadan) bir RenderTexture'a render eder ve Texture2D döndürür.
		/// Canvas/UI elemanları Camera.Render() tarafından çizilmez — sadece 3D sahne yakalanır.
		/// </summary>
		public Texture2D CapturePhoto()
		{
			// Termal renderer aktifse termal efektli fotoğraf çek
			if (thermalRenderer != null && thermalRenderer.enabled)
			{
				Texture2D thermalPhoto = thermalRenderer.CaptureWithThermal();
				if (thermalPhoto != null)
				{
					if (debugMode)
						Debug.Log($"[SurveyCameraController] CapturePhoto (thermal): " +
							$"{thermalPhoto.width}x{thermalPhoto.height} — score={AlignmentScore:F2} aligned={IsAligned}");
					return thermalPhoto;
				}
			}

			// Fallback: normal fotoğraf
			Camera cam = Camera.main;
			if (cam == null)
			{
				Debug.LogError("[SurveyCameraController] CapturePhoto: Camera.main null!");
				return null;
			}

			int w = Screen.width;
			int h = Screen.height;

			RenderTexture rt   = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
			RenderTexture prev = cam.targetTexture;

			cam.targetTexture = rt;
			cam.Render();
			cam.targetTexture = prev;

			RenderTexture.active = rt;
			Texture2D photo = new Texture2D(w, h, TextureFormat.RGB24, false);
			photo.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			photo.Apply();
			RenderTexture.active = null;

			Destroy(rt);

			if (debugMode)
				Debug.Log($"[SurveyCameraController] CapturePhoto: {w}x{h} — score={AlignmentScore:F2} aligned={IsAligned}");

			return photo;
		}
	}
}
