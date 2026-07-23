using System.Collections.Generic;
using UnityEngine;

namespace SafetyTraining
{
	/// <summary>
	/// Attach this to a Canvas object.
	/// It creates/uses a child RectTransform named "SafeArea" and applies Screen.safeArea to it.
	/// Existing direct UI children can optionally be moved under that container.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	public class UISafeArea : MonoBehaviour
	{
		[Header("━━━ SETTINGS ━━━")]
		[SerializeField] private string safeAreaRootName = "SafeArea";
		[SerializeField] private bool autoRefresh = true;
		[SerializeField] private bool createSafeAreaRootIfMissing = true;
		[SerializeField] private bool moveExistingChildrenIntoSafeArea = true;
		[SerializeField] private Vector4 extraPadding = Vector4.zero;
		[SerializeField] private bool ignoreHorizontal = false;
		[SerializeField] private bool ignoreVertical = false;

		[Header("━━━ DEBUG ━━━")]
		[SerializeField] private bool showDebug = false;

		private RectTransform _canvasRect;
		private RectTransform _safeAreaRoot;
		private Rect _lastSafeArea;
		private Vector2Int _lastScreenSize;
		private bool _isApplying;

		public static RectTransform FindSafeAreaRoot(Transform canvasTransform, string safeAreaRootName = "SafeArea")
		{
			if (canvasTransform == null)
				return null;

			Transform existing = canvasTransform.Find(safeAreaRootName);
			return existing as RectTransform;
		}

		private void Awake()
		{
			_canvasRect = GetComponent<RectTransform>();
		}

		private void OnEnable()
		{
			EnsureSafeAreaRoot();
			ApplySafeArea();
		}

		private void Start()
		{
			EnsureSafeAreaRoot();
			ApplySafeArea();
		}

		private void Update()
		{
			if (!Application.isPlaying || !autoRefresh)
				return;

			if (_lastScreenSize.x != Screen.width ||
				_lastScreenSize.y != Screen.height ||
				_lastSafeArea != Screen.safeArea)
			{
				ApplySafeArea();
			}
		}

		[ContextMenu("Create/Find Safe Area Root")]
		public void EnsureSafeAreaRoot()
		{
			if (_canvasRect == null)
				_canvasRect = GetComponent<RectTransform>();

			if (_safeAreaRoot != null)
				return;

			Transform existing = transform.Find(safeAreaRootName);
			if (existing != null)
			{
				_safeAreaRoot = existing as RectTransform;
				if (_safeAreaRoot != null)
					return;
			}

			if (!createSafeAreaRootIfMissing)
				return;

			GameObject safeAreaObject = new GameObject(safeAreaRootName, typeof(RectTransform));
			safeAreaObject.transform.SetParent(transform, false);
			_safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
			_safeAreaRoot.anchorMin = Vector2.zero;
			_safeAreaRoot.anchorMax = Vector2.one;
			_safeAreaRoot.offsetMin = Vector2.zero;
			_safeAreaRoot.offsetMax = Vector2.zero;

			if (moveExistingChildrenIntoSafeArea)
			{
				MoveExistingChildrenToSafeArea();
			}

			if (showDebug)
				Debug.Log($"[UISafeArea] SafeArea root created under '{name}'", this);
		}

		[ContextMenu("Apply Safe Area")]
		public void ApplySafeArea()
		{
			if (_isApplying)
				return;

			EnsureSafeAreaRoot();

			if (_safeAreaRoot == null)
				return;

			_isApplying = true;
			try
			{
				Rect safeArea = Screen.safeArea;

				if (ignoreHorizontal)
				{
					safeArea.xMin = 0f;
					safeArea.xMax = Screen.width;
				}

				if (ignoreVertical)
				{
					safeArea.yMin = 0f;
					safeArea.yMax = Screen.height;
				}

				safeArea.xMin += extraPadding.x;
				safeArea.yMin += extraPadding.y;
				safeArea.xMax -= extraPadding.z;
				safeArea.yMax -= extraPadding.w;

				safeArea.xMin = Mathf.Clamp(safeArea.xMin, 0f, Screen.width);
				safeArea.xMax = Mathf.Clamp(safeArea.xMax, 0f, Screen.width);
				safeArea.yMin = Mathf.Clamp(safeArea.yMin, 0f, Screen.height);
				safeArea.yMax = Mathf.Clamp(safeArea.yMax, 0f, Screen.height);

				Vector2 anchorMin = safeArea.position;
				Vector2 anchorMax = safeArea.position + safeArea.size;

				if (Screen.width > 0)
				{
					anchorMin.x /= Screen.width;
					anchorMax.x /= Screen.width;
				}

				if (Screen.height > 0)
				{
					anchorMin.y /= Screen.height;
					anchorMax.y /= Screen.height;
				}

				_safeAreaRoot.anchorMin = anchorMin;
				_safeAreaRoot.anchorMax = anchorMax;
				_safeAreaRoot.offsetMin = Vector2.zero;
				_safeAreaRoot.offsetMax = Vector2.zero;

				_lastSafeArea = Screen.safeArea;
				_lastScreenSize = new Vector2Int(Screen.width, Screen.height);
			}
			finally
			{
				_isApplying = false;
			}
		}

		private void MoveExistingChildrenToSafeArea()
		{
			if (_safeAreaRoot == null)
				return;

			var children = new List<Transform>();
			for (int i = 0; i < transform.childCount; i++)
			{
				Transform child = transform.GetChild(i);
				if (child == _safeAreaRoot)
					continue;

				children.Add(child);
			}

			foreach (Transform child in children)
			{
				child.SetParent(_safeAreaRoot, false);
			}
		}
	}
}
