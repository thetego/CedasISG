using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace SafetyTraining
{
	/// <summary>
	/// Tablet kamera kontrolcüsü.
	/// - RenderTexture üzerinden ayrı bir Camera'yı kullanır
	/// - Dokunuşla pan ve pinch-to-zoom
	/// - Hedef obje göstergesi (dünya → ekran projeksiyon)
	/// - Fotoğraf çekme + hizalama skoru hesaplama
	/// </summary>
	public class SurveyCameraController : MonoBehaviour
	{
		// ═══════════════════════════════════════════════════════
		// INSPECTOR
		// ═══════════════════════════════════════════════════════

		[Header("━━━ KAMERA ━━━")]
		[Tooltip("Tablet kamera (Main Camera'nın child'ı önerilir)")]
		public Camera surveyCamera;

		[Tooltip("Kameranın render edeceği RenderTexture (512x512)")]
		public RenderTexture renderTexture;

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

		[Header("━━━ GÖSTERGELer ━━━")]
		[Tooltip("Hedef göstergesi prefab'ı (Image component'li)")]
		public GameObject targetIndicatorPrefab;

		[Tooltip("Gösterge büyüklüğü (piksel)")]
		public float indicatorSize = 32f;

		[Tooltip("RenderTexture'ın gösterildiği UI RawImage")]
		public RawImage cameraViewImage;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ═══════════════════════════════════════════════════════
		// RUNTIME
		// ═══════════════════════════════════════════════════════

		private float _currentPitch;
		private float _currentYaw;
		private float _baseYaw;
		private float _basePitch;

		private List<GameObject>   _indicators    = new List<GameObject>();
		private List<PhotoSlot>    _activeSlots   = new List<PhotoSlot>();
		private Transform          _indicatorParent;

		// Aktif slot indeksi (hangi slot için fotoğraf çekiliyor)
		private int _activeSlotIndex = -1;

		// Şu anki slot'un hizalama durumu
		public bool  IsAligned      { get; private set; }
		public float AlignmentScore { get; private set; }

		// ═══════════════════════════════════════════════════════
		// INIT
		// ═══════════════════════════════════════════════════════

		private void Awake()
		{
			surveyCamera = Camera.main.transform.GetChild(0).GetComponent<Camera>();
			if (surveyCamera != null && renderTexture != null)
				surveyCamera.targetTexture = renderTexture;

			if (surveyCamera != null)
			{
				_basePitch    = surveyCamera.transform.localEulerAngles.x;
				_baseYaw      = surveyCamera.transform.localEulerAngles.y;
				_currentPitch = _basePitch;
				_currentYaw   = _baseYaw;
			}

			// New Input System — Enhanced Touch API'yi aktifleştir
			EnhancedTouchSupport.Enable();
		}

		private void OnDestroy()
		{
			EnhancedTouchSupport.Disable();
			ClearIndicators();

			if (surveyCamera != null)
				surveyCamera.targetTexture = null;
		}

		/// <summary>
		/// Slot listesini ve gösterge parent'ını dışarıdan set et
		/// </summary>
		public void Initialize(List<PhotoSlot> slots, Transform indicatorParent)
		{
			_activeSlots     = slots ?? new List<PhotoSlot>();
			_indicatorParent = indicatorParent;
			ClearIndicators();

			// indicatorParent'ın her zaman en üstte render edilmesini garantile
			if (indicatorParent != null)
			{
				// Ayrı bir Canvas yoksa ekle — Override Sorting ile RawImage'ın üstüne çıkar
				Canvas indicatorCanvas = indicatorParent.GetComponent<Canvas>();
				if (indicatorCanvas == null)
					indicatorCanvas = indicatorParent.gameObject.AddComponent<Canvas>();

				indicatorCanvas.overrideSorting = true;
				indicatorCanvas.sortingOrder    = 100;

				// GraphicRaycaster da ekle — UI event'leri için
				if (indicatorParent.GetComponent<GraphicRaycaster>() == null)
					indicatorParent.gameObject.AddComponent<GraphicRaycaster>();
			}
		}

		// ═══════════════════════════════════════════════════════
		// SLOT AKTİVASYON
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Belirli bir slot için kamera modunu aktifleştirir
		/// </summary>
		public void ActivateForSlot(int slotIndex)
		{
			_activeSlotIndex = slotIndex;
			ClearIndicators();
			ResetCamera();

			if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;

			PhotoSlot slot = _activeSlots[slotIndex];

			if (!string.IsNullOrEmpty(slot.targetObjectID))
				SpawnIndicator(slot);
		}

		/// <summary>
		/// Kamerayı başlangıç açısına sıfırlar
		/// </summary>
		public void ResetCamera()
		{
			_currentPitch = 0f;
			_currentYaw   = 0f;
			ApplyRotation();

			if (surveyCamera != null)
				surveyCamera.fieldOfView = (minFOV + maxFOV) * 0.5f;
		}

		// ═══════════════════════════════════════════════════════
		// UPDATE — DOKUNUŞ KONTROLÜ
		// ═══════════════════════════════════════════════════════

		private void Update()
		{
			if (_activeSlotIndex < 0) return;

			HandleTouchInput();
			UpdateIndicators();
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

					Vector2 delta     = touch.delta;
					float deltaPitch  = -delta.y * panSensitivity * 100f;
					float deltaYaw    =  delta.x * panSensitivity * 100f;

					_currentPitch = Mathf.Clamp(_currentPitch + deltaPitch, minPitch, maxPitch);
					_currentYaw   = Mathf.Clamp(_currentYaw   + deltaYaw,   minYaw,   maxYaw);

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

				float delta = currMag - prevMag;

				if (surveyCamera != null)
				{
					float newFOV = surveyCamera.fieldOfView - delta * zoomSensitivity;
					surveyCamera.fieldOfView = Mathf.Clamp(newFOV, minFOV, maxFOV);
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
			if (mouse == null)
			{
				Debug.LogWarning("[SurveyCameraController] Mouse.current null!");
				return;
			}

			Vector2 mousePos = mouse.position.ReadValue();
			bool overView    = IsTouchOnCameraView(mousePos);

			// Her frame debug
			if (mouse.leftButton.isPressed)
				Debug.Log($"[CAM] mousePos={mousePos} overView={overView} mouseDown={_mouseDown} cameraViewImage={cameraViewImage != null}");

			if (mouse.leftButton.wasPressedThisFrame)
			{
				Debug.Log($"[CAM] Press — overView={overView} cameraViewImage={cameraViewImage?.name ?? "NULL"}");
				if (overView)
				{
					_lastMousePos = mousePos;
					_mouseDown    = true;
				}
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

			if (overView)
			{
				float scroll = mouse.scroll.ReadValue().y;
				if (scroll != 0f && surveyCamera != null)
				{
					float newFOV = surveyCamera.fieldOfView - scroll * 0.05f * zoomSensitivity * 100f;
					surveyCamera.fieldOfView = Mathf.Clamp(newFOV, minFOV, maxFOV);
				}
			}
		}
		#endif

		private void ApplyRotation()
		{
			if (surveyCamera == null) return;
			surveyCamera.transform.localEulerAngles = new Vector3(_currentPitch, _currentYaw, 0f);
		}

		private bool IsTouchOnCameraView(Vector2 screenPos)
		{
			if (cameraViewImage == null) return true;

			// Canvas camera'sını bul (Screen Space - Camera ise kamera lazım, Overlay ise null)
			Canvas canvas = cameraViewImage.canvas;
			Camera uiCam  = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
				? canvas.worldCamera : null;

			return RectTransformUtility.RectangleContainsScreenPoint(
				cameraViewImage.rectTransform, screenPos, uiCam);
		}

		// ═══════════════════════════════════════════════════════
		// GÖSTERGELER
		// ═══════════════════════════════════════════════════════

		private void SpawnIndicator(PhotoSlot slot)
		{
			if (targetIndicatorPrefab == null || _indicatorParent == null) return;

			GameObject ind = Instantiate(targetIndicatorPrefab, _indicatorParent);
			RectTransform rt = ind.GetComponent<RectTransform>();
			if (rt != null)
				rt.sizeDelta = new Vector2(indicatorSize, indicatorSize);

			_indicators.Add(ind);
		}

		private void UpdateIndicators()
		{
			if (_activeSlotIndex < 0 || _activeSlotIndex >= _activeSlots.Count) return;
			if (_indicators.Count == 0) return;

			PhotoSlot slot      = _activeSlots[_activeSlotIndex];
			GameObject indicator = _indicators[0];

			if (string.IsNullOrEmpty(slot.targetObjectID) || surveyCamera == null || indicator == null)
				return;

			// Hedef objeyi bul
			Transform target = SceneObjectRegistry.Instance?.GetTransformByID(slot.targetObjectID);
			if (target == null) return;

			// Dünya → viewport koordinatı
			Vector3 viewportPos = surveyCamera.WorldToViewportPoint(target.position);

			// Kameranın arkasındaysa gizle
			if (viewportPos.z < 0)
			{
				indicator.SetActive(false);
				IsAligned      = false;
				AlignmentScore = 0f;
				return;
			}

			indicator.SetActive(true);

			// Viewport (0-1) → ekran pikseli → indicatorContainer lokal koordinatı
			if (cameraViewImage != null)
			{
				RectTransform rawRT = cameraViewImage.rectTransform;

				// RawImage'ın dünya köşelerini al
				Vector3[] corners = new Vector3[4];
				rawRT.GetWorldCorners(corners);
				// corners: [0]=bottomLeft [1]=topLeft [2]=topRight [3]=bottomRight

				Vector3 bottomLeft = corners[0];
				Vector3 topRight   = corners[2];

				// Viewport pozisyonunu dünya koordinatına çevir
				float worldX = Mathf.Lerp(bottomLeft.x, topRight.x, viewportPos.x);
				float worldY = Mathf.Lerp(bottomLeft.y, topRight.y, viewportPos.y);
				Vector3 worldPos = new Vector3(worldX, worldY, 0f);

				// Dünya koordinatını indicatorContainer lokal koordinatına çevir
				RectTransform containerRT = _indicatorParent as RectTransform;
				if (containerRT != null)
				{
					Vector2 localPos;
					RectTransformUtility.ScreenPointToLocalPointInRectangle(
						containerRT,
						RectTransformUtility.WorldToScreenPoint(null, worldPos),
						null,
						out localPos
					);

					RectTransform indRT = indicator.GetComponent<RectTransform>();
					if (indRT != null)
					{
						indRT.anchorMin        = new Vector2(0.5f, 0.5f);
						indRT.anchorMax        = new Vector2(0.5f, 0.5f);
						indRT.pivot            = new Vector2(0.5f, 0.5f);
						indRT.anchoredPosition = localPos;
					}
				}
			}

			// Hizalama skoru — viewport merkezine mesafe (0-1 arası, 1=mükemmel)
			float distFromCenter = Vector2.Distance(
				new Vector2(viewportPos.x, viewportPos.y),
				new Vector2(0.5f, 0.5f)
			);

			// Normalize: threshold mesafesinde skor 0, merkezde skor 1
			AlignmentScore = Mathf.Clamp01(1f - (distFromCenter / slot.alignmentThreshold));
			IsAligned      = distFromCenter <= slot.alignmentThreshold;

			// Renk güncelle
			Image img = indicator.GetComponent<Image>();
			if (img != null)
			{
				img.color = Color.Lerp(slot.indicatorOffColor, slot.indicatorAlignedColor, AlignmentScore);
			}

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Slot {_activeSlotIndex}: " +
					$"dist={distFromCenter:F3} score={AlignmentScore:F2} aligned={IsAligned}");
		}

		private void ClearIndicators()
		{
			foreach (var ind in _indicators)
				if (ind != null) Destroy(ind);
			_indicators.Clear();
		}

		// ═══════════════════════════════════════════════════════
		// FOTOĞRAF ÇEKME
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// RenderTexture'dan anlık fotoğraf çeker ve Texture2D döndürür
		/// </summary>
		public Texture2D CapturePhoto()
		{
			if (surveyCamera == null || renderTexture == null)
			{
				Debug.LogError("[SurveyCameraController] CapturePhoto: Camera veya RenderTexture null!");
				return null;
			}

			// RenderTexture'ı aktif et ve oku
			RenderTexture prevActive = RenderTexture.active;
			RenderTexture.active = renderTexture;

			Texture2D photo = new Texture2D(
				renderTexture.width,
				renderTexture.height,
				TextureFormat.RGB24,
				false
			);
			photo.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
			photo.Apply();

			RenderTexture.active = prevActive;

			if (debugMode)
				Debug.Log($"[SurveyCameraController] Photo captured: {renderTexture.width}x{renderTexture.height}");

			return photo;
		}

		// ═══════════════════════════════════════════════════════
		// CLEANUP
		// ═══════════════════════════════════════════════════════


	}
}