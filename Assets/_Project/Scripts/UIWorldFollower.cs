using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	// ═══════════════════════════════════════════════════════════════
	// UIWorldFollower - FIXED VERSION
	//    → Herhangi bir UI element'i 3D objeyi takip ettirir
	//    → Manuel olarak enable/disable edilebilir
	//    → LateUpdate sadece enabled ise çalışır
	// ═══════════════════════════════════════════════════════════════
	public class UIWorldFollower : MonoBehaviour
	{
		[Header("━━━ AYARLAR ━━━")]
		[Tooltip("Takip edilen 3D objenin Tag veya Name'i (runtime'da bulunur)")]
		public string targetObjectID;

		[Tooltip("World offset (obje üstüne ne kadar yukarı)")]
		public Vector3 worldOffset = Vector3.zero;

		[Header("━━━ CONTROL ━━━")]
		[Tooltip("World follower aktif mi? (Manuel kontrol için)")]
		public bool isFollowingEnabled = true;

		[Header("━━━ DEBUG ━━━")]
		public bool showDebug = false;

		// Runtime
		private Transform _target;
		private RectTransform _rt;
		private Camera _cam;
		private bool _isValid = false;
		private bool _manuallyDisabled = false; // Manuel olarak kapatıldı mı?

		private void Awake()
		{
			_rt = GetComponent<RectTransform>();
			_cam = Camera.main;
		}

		/// <summary>
		/// Target'ı bul ve takibi başlat
		/// </summary>
		public void Initialize(string targetID, Vector3 offset)
		{
			targetObjectID = targetID;
			//worldOffset = offset;

			if (string.IsNullOrEmpty(targetID))
			{
				Debug.LogError("[UIWorldFollower] targetObjectID boş!");
				_isValid = false;
				_manuallyDisabled = true;
				gameObject.SetActive(false);
				return;
			}

			// SceneObjectRegistry üzerinden bul
			if (SceneObjectRegistry.Instance != null)
			{
				Transform targetTransform = SceneObjectRegistry.Instance.GetTransformByID(targetID);

				if (targetTransform != null)
				{
					_target = targetTransform;
					_isValid = true;
					_manuallyDisabled = false;

					if (showDebug)
						Debug.Log($"[UIWorldFollower] ✓ Target found via Registry: '{targetID}' at {targetTransform.position}");
					return;
				}
			}

			// Registry yoksa veya bulunamadıysa fallback: GameObject.Find
			GameObject obj = GameObject.Find(targetID);

			if (obj != null)
			{
				_target = obj.transform;
				_isValid = true;
				_manuallyDisabled = false;

				if (showDebug)
					Debug.Log($"[UIWorldFollower] ✓ Target found via Find: '{targetID}' (Fallback)");
			}
			else
			{
				Debug.LogError($"[UIWorldFollower] Target '{targetID}' not found! " +
							  $"Make sure a GameObject with SceneObject component has objectID='{targetID}'");
				_isValid = false;
				_manuallyDisabled = true;
				gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Takibi etkinleştir (manuel kontrol)
		/// </summary>
		public void EnableFollowing()
		{
			isFollowingEnabled = true;
			_manuallyDisabled = false;

			if (showDebug)
				Debug.Log($"[UIWorldFollower] Following enabled for '{targetObjectID}'");
		}

		/// <summary>
		/// Takibi devre dışı bırak (manuel kontrol)
		/// </summary>
		public void DisableFollowing()
		{
			isFollowingEnabled = false;
			_manuallyDisabled = true;
			gameObject.SetActive(false);

			if (showDebug)
				Debug.Log($"[UIWorldFollower] Following disabled for '{targetObjectID}'");
		}

		private void LateUpdate()
		{
			// Manuel olarak devre dışı bırakıldıysa hiçbir şey yapma
			if (!isFollowingEnabled || _manuallyDisabled)
			{
				return;
			}

			// Geçerlilik kontrolü
			if (!_isValid || _target == null || _rt == null || _cam == null)
			{
				// Sadece ilk kez kapatırken log
				if (gameObject.activeSelf && showDebug)
					Debug.Log($"[UIWorldFollower] Disabling due to invalid state: '{targetObjectID}'");

				gameObject.SetActive(false);
				return;
			}

			Vector3 worldPos = _target.position + worldOffset;
			Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

			// Kameranın arkasında → gizle
			if (screenPos.z < 0)
			{
				if (gameObject.activeSelf)
					gameObject.SetActive(false);
				return;
			}

			// Ekranın dışında → gizle (optional, isteğe göre)
			if (screenPos.x < -100 || screenPos.x > Screen.width + 100 ||
				screenPos.y < -100 || screenPos.y > Screen.height + 100)
			{
				if (gameObject.activeSelf)
					gameObject.SetActive(false);
				return;
			}

			// Her şey yolunda → göster ve pozisyonu güncelle
			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(true);

				if (showDebug)
					Debug.Log($"[UIWorldFollower] Showing button: '{targetObjectID}'");
			}

			_rt.position = screenPos;
		}

		/// <summary>
		/// GameObject aktif/pasif yapıldığında çağrılır
		/// </summary>
		private void OnEnable()
		{
			// Manuel disable durumunu sıfırla
			if (!_manuallyDisabled && _isValid)
			{
				isFollowingEnabled = true;
			}
		}

		private void OnDisable()
		{
			// Eğer manuel disable değilse, sadece geçici bir kapatma
			// _manuallyDisabled flag'i korunur
		}

		private void OnDrawGizmos()
		{
			if (!showDebug || _target == null) return;

			// World pozisyondan UI pozisyonuna çizgi çek (Scene view'de görünür)
			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(_target.position, _target.position + worldOffset);
			Gizmos.DrawWireSphere(_target.position + worldOffset, 0.2f);
		}
	}
}