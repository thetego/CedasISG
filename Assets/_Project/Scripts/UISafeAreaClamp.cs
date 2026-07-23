using UnityEngine;

namespace SafetyTraining
{
	/// <summary>
	/// Attach this directly to a UI element that must stay inside Screen.safeArea.
	/// It clamps the element's anchored position using its current parent RectTransform.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	public class UISafeAreaClamp : MonoBehaviour
	{
		[SerializeField] private bool autoRefresh = true;
		[SerializeField] private Vector2 extraPadding = Vector2.zero;
		[SerializeField] private bool ignoreHorizontal = false;
		[SerializeField] private bool ignoreVertical = false;

		private RectTransform _rectTransform;
		private RectTransform _parentRect;
		private Canvas _canvas;
		private Rect _lastSafeArea;
		private Vector2Int _lastScreenSize;
		private bool _isApplying;

		private void Awake()
		{
			_rectTransform = GetComponent<RectTransform>();
			_parentRect = _rectTransform.parent as RectTransform;
			_canvas = GetComponentInParent<Canvas>();
		}

		private void OnEnable()
		{
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

		[ContextMenu("Apply Safe Area Clamp")]
		public void ApplySafeArea()
		{
			if (_isApplying)
				return;

			if (_rectTransform == null)
				_rectTransform = GetComponent<RectTransform>();

			_parentRect = _rectTransform != null ? _rectTransform.parent as RectTransform : null;
			if (_rectTransform == null || _parentRect == null)
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
				safeArea.xMax -= extraPadding.x;
				safeArea.yMax -= extraPadding.y;

				var cam = GetCanvasCamera();

				Vector2 localMin;
				Vector2 localMax;

				RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, new Vector2(safeArea.xMin, safeArea.yMin), cam, out localMin);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, new Vector2(safeArea.xMax, safeArea.yMax), cam, out localMax);

				Rect parentRect = _parentRect.rect;
				float minX = Mathf.Min(localMin.x, localMax.x);
				float maxX = Mathf.Max(localMin.x, localMax.x);
				float minY = Mathf.Min(localMin.y, localMax.y);
				float maxY = Mathf.Max(localMin.y, localMax.y);

				Vector2 size = _rectTransform.rect.size;
				Vector2 pivot = _rectTransform.pivot;

				Vector2 anchored = _rectTransform.anchoredPosition;

				if (_rectTransform.anchorMin == _rectTransform.anchorMax)
				{
					if (!ignoreHorizontal)
					{
						float left = anchored.x - size.x * pivot.x;
						float right = anchored.x + size.x * (1f - pivot.x);
						float delta = 0f;

						if (left < minX) delta = minX - left;
						else if (right > maxX) delta = maxX - right;

						anchored.x += delta;
					}

					if (!ignoreVertical)
					{
						float bottom = anchored.y - size.y * pivot.y;
						float top = anchored.y + size.y * (1f - pivot.y);
						float delta = 0f;

						if (bottom < minY) delta = minY - bottom;
						else if (top > maxY) delta = maxY - top;

						anchored.y += delta;
					}
				}
				else
				{
					// Stretch anchors: keep the element inside by snapping offsets to safe area bounds.
					if (!ignoreHorizontal)
					{
						_rectTransform.offsetMin = new Vector2(
							Mathf.Max(_rectTransform.offsetMin.x, minX - parentRect.xMin + extraPadding.x),
							_rectTransform.offsetMin.y);
						_rectTransform.offsetMax = new Vector2(
							Mathf.Min(_rectTransform.offsetMax.x, maxX - parentRect.xMax - extraPadding.x),
							_rectTransform.offsetMax.y);
					}

					if (!ignoreVertical)
					{
						_rectTransform.offsetMin = new Vector2(
							_rectTransform.offsetMin.x,
							Mathf.Max(_rectTransform.offsetMin.y, minY - parentRect.yMin + extraPadding.y));
						_rectTransform.offsetMax = new Vector2(
							_rectTransform.offsetMax.x,
							Mathf.Min(_rectTransform.offsetMax.y, maxY - parentRect.yMax - extraPadding.y));
					}
				}

				_rectTransform.anchoredPosition = anchored;
				_lastSafeArea = Screen.safeArea;
				_lastScreenSize = new Vector2Int(Screen.width, Screen.height);
			}
			finally
			{
				_isApplying = false;
			}
		}

		private Camera GetCanvasCamera()
		{
			if (_canvas == null)
				_canvas = GetComponentInParent<Canvas>();

			if (_canvas == null)
				return null;

			if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
				return null;

			return _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
		}
	}
}
