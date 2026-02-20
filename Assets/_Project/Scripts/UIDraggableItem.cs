using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	public class UIDraggableItem : MonoBehaviour,
			IBeginDragHandler, IDragHandler, IEndDragHandler,
			IPointerEnterHandler, IPointerExitHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public Image background;
		public Image itemIcon;
		public TextMeshProUGUI itemLabel;

		[Header("━━━ RENKLER ━━━")]
		public Color normalColor = Color.white;
		public Color hoverColor = new Color(0.2f, 0.85f, 1f, 1f);

		[Header("━━━ SES ━━━")]
		public AudioClip pickupSound;
		public AudioClip invalidSound;

		[Header("━━━ SETTINGS ━━━")]
		public float hoverScale = 1.1f;

		[Range(0f, 1f)]
		public float dragAlpha = 0.6f;

		// ─── Public Data ───
		public string ItemID { get; private set; }
		public EquipmentData EquipmentData { get; private set; }
		public ToolData ToolData { get; private set; }
		public Sprite ItemIconSprite { get; private set; }

		// ─── Runtime ───
		private RectTransform _rt;
		private Canvas _canvas;
		private CanvasGroup _cg;
		private Vector2 _originalPos;
		private Transform _originalParent;
		private Vector3 _originalScale;
		private AudioSource _audio;

		private void Awake()
		{
			_rt = GetComponent<RectTransform>();
			_canvas = GetComponentInParent<Canvas>();
			_cg = GetComponent<CanvasGroup>();

			if (_cg == null)
				_cg = gameObject.AddComponent<CanvasGroup>();

			_originalPos = _rt.anchoredPosition;
			_originalParent = transform.parent;
			_originalScale = transform.localScale;

			_audio = GetComponent<AudioSource>();
			if (_audio == null && (pickupSound != null || invalidSound != null))
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}
		}

		// ─── Setup: Ekipman ───
		public void Setup(string itemID, string name, Sprite icon, EquipmentData eqData)
		{
			if (string.IsNullOrEmpty(itemID))
			{
				Debug.LogError("[UIDraggableItem] Setup called with empty itemID!");
				return;
			}

			ItemID = itemID;
			EquipmentData = eqData;
			ToolData = null;
			ItemIconSprite = icon;

			if (itemLabel != null) itemLabel.text = name;
			if (itemIcon != null && icon != null) itemIcon.sprite = icon;
			if (background != null) background.color = normalColor;

			gameObject.SetActive(false);
		}

		// ─── Setup: Tool ───
		public void SetupAsTool(string itemID, string name, Sprite icon, ToolData toolData)
		{
			if (string.IsNullOrEmpty(itemID))
			{
				Debug.LogError("[UIDraggableItem] SetupAsTool called with empty itemID!");
				return;
			}

			ItemID = itemID;
			ToolData = toolData;
			EquipmentData = null;
			ItemIconSprite = icon;

			if (itemLabel != null) itemLabel.text = name;
			if (itemIcon != null && icon != null) itemIcon.sprite = icon;
			if (background != null) background.color = normalColor;

			gameObject.SetActive(false);
		}

		public bool IsTool => ToolData != null;
		public bool IsEquipment => EquipmentData != null;

		public void SetActive(bool state) => gameObject.SetActive(state);

		// ─── Hover ───
		public void OnPointerEnter(PointerEventData e)
		{
			transform.localScale = _originalScale * hoverScale;
			if (background != null) background.color = hoverColor;
		}

		public void OnPointerExit(PointerEventData e)
		{
			if (_cg != null && !_cg.blocksRaycasts) return;
			transform.localScale = _originalScale;
			if (background != null) background.color = normalColor;
		}

		// ─── Drag ───
		public void OnBeginDrag(PointerEventData e)
		{
			if (_cg != null)
			{
				_cg.alpha = dragAlpha;
				_cg.blocksRaycasts = false;
			}

			var csf = _originalParent.GetComponent<ContentSizeFitter>();
			var glg = _originalParent.GetComponent<GridLayoutGroup>();
			if (csf != null) csf.enabled = false;
			if (glg != null) glg.enabled = false;

			transform.SetAsLastSibling();
			PlaySound(pickupSound);
		}

		public void OnDrag(PointerEventData e)
		{
			if (_rt == null || _canvas == null) return;
			_rt.anchoredPosition += e.delta / _canvas.scaleFactor;
		}

		public void OnEndDrag(PointerEventData e)
		{
			if (_cg != null)
			{
				_cg.alpha = 1f;
				_cg.blocksRaycasts = true;
			}

			var csf = _originalParent.GetComponent<ContentSizeFitter>();
			var glg = _originalParent.GetComponent<GridLayoutGroup>();
			if (csf != null) csf.enabled = true;
			if (glg != null) glg.enabled = true;

			transform.localScale = _originalScale;

			bool accepted = false;

			if (e.pointerEnter != null)
			{
				UIDropZone zone = e.pointerEnter.GetComponent<UIDropZone>();
				if (zone != null)
					accepted = zone.TryAcceptItem(this);
			}

			if (!accepted)
			{
				ReturnToOriginalPosition();
				PlaySound(invalidSound);
			}
		}

		public void ReturnToOriginalPosition()
		{
			if (_originalParent != null)
			{
				transform.SetParent(_originalParent);
				if (_rt != null) _rt.anchoredPosition = _originalPos;
			}
			else
			{
				Debug.LogWarning("[UIDraggableItem] Original parent was destroyed!");
			}

			if (background != null) background.color = normalColor;
		}

		public void UpdateOriginalPosition()
		{
			if (_rt != null) _originalPos = _rt.anchoredPosition;
			_originalParent = transform.parent;
		}

		private void PlaySound(AudioClip clip)
		{
			if (_audio != null && clip != null)
				_audio.PlayOneShot(clip);
		}
	}
}
