using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	public class UIDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public Image background;
		public Image icon;
		public TextMeshProUGUI label;

		[Header("━━━ BOYUT ━━━")]
		public Vector2 zoneSize = new Vector2(100, 100);
		public bool autoResizeOnSetup = true;

		[Header("━━━ RENKLER ━━━")]
		public Color normalColor = new Color(1f, 1f, 1f, 0.3f);
		public Color hoverValid = new Color(0.2f, 1f, 0.2f, 0.55f);
		public Color hoverInvalid = new Color(1f, 0.2f, 0.2f, 0.55f);
		public Color filledColor = new Color(0.2f, 0.85f, 1f, 0.8f);

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ─── Runtime ───
		private string _actionID;

		// Equipment slot
		private EquipmentSlotType _slotType;
		private bool _isEquipmentSlot;

		// Tool drop - ToolData referansı
		private ToolData _acceptedToolData;

		private bool _filled;
		private RectTransform _rectTransform;

		private void Awake()
		{
			_rectTransform = GetComponent<RectTransform>();
		}

		// ─── Setup: Equipment Slot ───
		public void SetupAsEquipmentSlot(string actionID, EquipmentSlotType slotType, string labelText, Vector2? customSize = null)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIDropZone] SetupAsEquipmentSlot: actionID boş!");
				return;
			}

			_actionID = actionID;
			_slotType = slotType;
			_isEquipmentSlot = true;
			_acceptedToolData = null;
			_filled = false;

			if (label != null) label.text = labelText;
			if (background != null) background.color = normalColor;
			if (customSize.HasValue) zoneSize = customSize.Value;
			if (autoResizeOnSetup) ApplySize();

			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log($"[UIDropZone] Equipment slot: {actionID} (slot: {slotType})");
		}

		// ─── Setup: Tool Drop ───
		public void SetupAsToolDrop(string actionID, ToolData toolData, string labelText, Vector2? customSize = null)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIDropZone] SetupAsToolDrop: actionID boş!");
				return;
			}

			if (toolData == null)
			{
				Debug.LogError($"[UIDropZone] SetupAsToolDrop: toolData null! (action: {actionID})");
				return;
			}

			_actionID = actionID;
			_acceptedToolData = toolData;
			_isEquipmentSlot = false;
			_filled = false;

			if (label != null) label.text = labelText;
			if (background != null) background.color = normalColor;
			if (customSize.HasValue) zoneSize = customSize.Value;
			if (autoResizeOnSetup) ApplySize();

			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log($"[UIDropZone] Tool drop: {actionID} (tool: {toolData.toolID})");
		}

		public void ApplySize()
		{
			if (_rectTransform != null)
				_rectTransform.sizeDelta = zoneSize;
		}

		public void SetActive(bool state) => gameObject.SetActive(state);

		// ─── Drop Logic ───
		public bool TryAcceptItem(UIDraggableItem item)
		{
			if (item == null || _filled) return false;

			bool accepted = false;

			if (_isEquipmentSlot)
			{
				// Ekipman: slot type eşleşmeli
				accepted = item.EquipmentData != null && item.EquipmentData.slotType == _slotType;
			}
			else
			{
				// Tool: ToolData ID eşleşmeli
				accepted = item.ToolData != null && item.ToolData.toolID == _acceptedToolData?.toolID;
			}

			if (!accepted) return false;

			AcceptItem(item);
			return true;
		}

		private void AcceptItem(UIDraggableItem item)
		{
			_filled = true;

			if (background != null) background.color = filledColor;

			if (icon != null && item.ItemIconSprite != null)
			{
				icon.sprite = item.ItemIconSprite;
				icon.enabled = true;
			}

			if (_isEquipmentSlot)
			{
				// Ekipman: SequenceManager'a bildir, item geri dönsün
				item.ReturnToOriginalPosition();

				if (SequenceManager.Instance != null)
					SequenceManager.Instance.OnEquipmentWorn(item.EquipmentData);
				else
					Debug.LogWarning("[UIDropZone] SequenceManager.Instance null!");
			}
			else
			{
				// Tool: SequenceManager'a bildir, item drop zone'da kalsın
				item.transform.SetParent(transform);
				RectTransform itemRT = item.GetComponent<RectTransform>();
				if (itemRT != null) itemRT.anchoredPosition = Vector2.zero;

				CanvasGroup itemCG = item.GetComponent<CanvasGroup>();
				if (itemCG != null) itemCG.blocksRaycasts = false;

				if (SequenceManager.Instance != null)
					SequenceManager.Instance.OnToolDropped(_actionID, item.ToolData);
				else
					Debug.LogWarning("[UIDropZone] SequenceManager.Instance null!");
			}

			if (debugMode)
				Debug.Log($"<color=green>[UIDropZone] Accepted: {item.ItemID}</color>");
		}

		// ─── Hover ───
		public void OnPointerEnter(PointerEventData e)
		{
			if (_filled || background == null) return;

			UIDraggableItem dragged = e.pointerDrag?.GetComponent<UIDraggableItem>();
			if (dragged == null) return;

			background.color = CanAcceptItem(dragged) ? hoverValid : hoverInvalid;
		}

		public void OnPointerExit(PointerEventData e)
		{
			if (background == null) return;
			background.color = _filled ? filledColor : normalColor;
		}

		private bool CanAcceptItem(UIDraggableItem item)
		{
			if (item == null) return false;

			if (_isEquipmentSlot)
				return item.EquipmentData != null && item.EquipmentData.slotType == _slotType;
			else
				return item.ToolData != null && item.ToolData.toolID == _acceptedToolData?.toolID;
		}

		public void Reset()
		{
			_filled = false;
			if (background != null) background.color = normalColor;
			if (icon != null) icon.enabled = false;
		}
	}
}
