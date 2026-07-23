using UnityEngine;

namespace SafetyTraining
{
	/// <summary>
	/// Tracks a world target and keeps the UI alive while hiding it offscreen.
	/// The button follows the target directly without any safe-area clamping.
	/// </summary>
	public class UIWorldFollower : MonoBehaviour
	{
		[Header("SETTINGS")]
		[Tooltip("Target object's Tag or Name, resolved at runtime")]
		public string targetObjectID;

		[Tooltip("World offset applied above the target")]
		public Vector3 worldOffset = Vector3.zero;

		[Header("CONTROL")]
		[Tooltip("Is world following enabled?")]
		public bool isFollowingEnabled = true;

		[Header("DEBUG")]
		public bool showDebug = false;

		private Transform _target;
		private RectTransform _rt;
		private Camera _cam;
		private CanvasGroup _canvasGroup;
		private bool _isValid = false;
		private bool _manuallyDisabled = false;
		private bool _hiddenByVisibility = false;

		private void Awake()
		{
			_rt = GetComponent<RectTransform>();
			_cam = Camera.main;

			_canvasGroup = GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
				_canvasGroup = gameObject.AddComponent<CanvasGroup>();
		}

		/// <summary>
		/// Resolve the target and start following.
		/// </summary>
		public void Initialize(string targetID, Vector3 offset)
		{
			targetObjectID = targetID;
			worldOffset = offset;

			if (string.IsNullOrEmpty(targetID))
			{
				Debug.LogError("[UIWorldFollower] targetObjectID is empty!");
				_isValid = false;
				_manuallyDisabled = true;
				SetVisible(false);
				return;
			}

			if (SceneObjectRegistry.Instance != null)
			{
				Transform targetTransform = SceneObjectRegistry.Instance.GetTransformByID(targetID);
				if (targetTransform != null)
				{
					_target = targetTransform;
					_isValid = true;
					_manuallyDisabled = false;
					SetVisible(true);

					if (showDebug)
						Debug.Log($"[UIWorldFollower] Target found via Registry: '{targetID}'");
					return;
				}
			}

			GameObject obj = GameObject.Find(targetID);
			if (obj != null)
			{
				_target = obj.transform;
				_isValid = true;
				_manuallyDisabled = false;
				SetVisible(true);

				if (showDebug)
					Debug.Log($"[UIWorldFollower] Target found via Find: '{targetID}' (Fallback)");
			}
			else
			{
				Debug.LogError($"[UIWorldFollower] Target '{targetID}' not found! Make sure a GameObject with SceneObject component has objectID='{targetID}'");
				_isValid = false;
				_manuallyDisabled = true;
				SetVisible(false);
			}
		}

		public void EnableFollowing()
		{
			isFollowingEnabled = true;
			_manuallyDisabled = false;
			SetVisible(true);

			if (showDebug)
				Debug.Log($"[UIWorldFollower] Following enabled for '{targetObjectID}'");
		}

		public void DisableFollowing()
		{
			isFollowingEnabled = false;
			_manuallyDisabled = true;
			SetVisible(false);

			if (showDebug)
				Debug.Log($"[UIWorldFollower] Following disabled for '{targetObjectID}'");
		}

		private void LateUpdate()
		{
			if (!isFollowingEnabled || _manuallyDisabled)
				return;

			if (_cam == null)
				_cam = Camera.main;

			if (!_isValid || _target == null || _rt == null || _cam == null)
			{
				if (!_hiddenByVisibility && showDebug)
					Debug.Log($"[UIWorldFollower] Hidden due to invalid state: '{targetObjectID}'");

				SetVisible(false);
				return;
			}

			Vector3 worldPos = _target.position + worldOffset;
			Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

			if (screenPos.z < 0f ||
				screenPos.x < -100f || screenPos.x > Screen.width + 100f ||
				screenPos.y < -100f || screenPos.y > Screen.height + 100f)
			{
				SetVisible(false);
				return;
			}

			if (_hiddenByVisibility)
			{
				SetVisible(true);

				if (showDebug)
					Debug.Log($"[UIWorldFollower] Showing button: '{targetObjectID}'");
			}

			_rt.position = screenPos;
		}

		private void OnEnable()
		{
			if (!_manuallyDisabled && _isValid)
			{
				isFollowingEnabled = true;
				SetVisible(true);
			}
		}

		private void OnDrawGizmos()
		{
			if (!showDebug || _target == null) return;

			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(_target.position, _target.position + worldOffset);
			Gizmos.DrawWireSphere(_target.position + worldOffset, 0.2f);
		}

		private void SetVisible(bool visible)
		{
			_hiddenByVisibility = !visible;

			if (_canvasGroup == null)
				return;

			_canvasGroup.alpha = visible ? 1f : 0f;
			_canvasGroup.interactable = visible;
			_canvasGroup.blocksRaycasts = visible;
		}
	}
}
