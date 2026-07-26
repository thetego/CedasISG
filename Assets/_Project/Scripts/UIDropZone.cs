using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Drop zone - ekipman slotu veya tool drop alanı
	/// SequenceManager ile çalışır
	/// </summary>
	public class UIDropZone : MonoBehaviour,
			IPointerEnterHandler, IPointerExitHandler
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

		// Runtime
		private string _actionID;
		private string _acceptedItemID;
		private EquipmentSlotType _slotType;
		private bool _isEquipmentSlot;
		private bool _filled;
		private RectTransform _rectTransform;

		// Multi-equipment support
		private HashSet<string> _acceptedEquipmentIDs = new HashSet<string>();
		private int _maxCapacity = 1; // Varsayılan: 1 item

		// Tool drop mode
		private ToolDropMode _toolDropMode = ToolDropMode.DragAndDrop;

		// Multiple drop zones tracking
		private string _uniqueID; // Her drop zone için benzersiz
		private string _onDropEventID; // Drop edildiğinde tetiklenecek event
		private int _dropAttempts;

		private void Awake()
		{
			_rectTransform = GetComponent<RectTransform>();
		}

		public void SetupAsToolDrop(string actionID, string acceptedItemID, string labelText, Vector2? customSize = null, ToolDropMode dropMode = ToolDropMode.DragAndDrop, string uniqueID = null, string onDropEventID = null)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIDropZone] SetupAsToolDrop called with empty actionID!");
				return;
			}

			_actionID = actionID;
			_acceptedItemID = acceptedItemID;
			_isEquipmentSlot = false;
			_filled = false;
			_toolDropMode = dropMode;
			_uniqueID = uniqueID ?? acceptedItemID; // uniqueID yoksa itemID kullan
			_onDropEventID = onDropEventID; // Event ID
			_dropAttempts = 0;

			if (label != null) label.text = labelText;
			if (background != null) background.color = normalColor;

			if (customSize.HasValue)
				zoneSize = customSize.Value;

			if (autoResizeOnSetup)
				ApplySize();

			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log($"[UIDropZone] Setup as tool drop: {actionID} (accepts: {acceptedItemID})");
		}

		public void SetupAsEquipmentSlot(string actionID, EquipmentSlotType slotType, string labelText, Vector2? customSize = null, int maxCapacity = 999)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIDropZone] SetupAsEquipmentSlot called with empty actionID!");
				return;
			}

			_actionID = actionID;
			_slotType = slotType;
			_isEquipmentSlot = true;
			_filled = false;
			_maxCapacity = maxCapacity;
			_acceptedEquipmentIDs.Clear();

			if (label != null) label.text = labelText;
			if (background != null) background.color = normalColor;

			if (customSize.HasValue)
				zoneSize = customSize.Value;

			if (autoResizeOnSetup)
				ApplySize();

			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log($"[UIDropZone] Setup as equipment slot: {actionID} (slot: {slotType})");
		}

		public void ApplySize()
		{
			if (_rectTransform != null)
			{
				_rectTransform.sizeDelta = zoneSize;
			}
		}

		public void SetSize(Vector2 newSize)
		{
			zoneSize = newSize;
			ApplySize();
		}

		public void SetActive(bool state) => gameObject.SetActive(state);

		public bool TryAcceptItem(UIDraggableItem item)
		{
			if (item == null) return false;

			// Capacity kontrolü
			if (_acceptedEquipmentIDs.Count >= _maxCapacity)
			{
				if (debugMode)
					Debug.Log($"[UIDropZone] Slot full ({_acceptedEquipmentIDs.Count}/{_maxCapacity})");
				return false;
			}

			bool accepted = false;

			if (_isEquipmentSlot)
			{
				// Equipment slot: Aynı tip ve daha önce eklenmemiş olmalı
				if (item.EquipmentData != null &&
					item.EquipmentData.slotType == _slotType &&
					!_acceptedEquipmentIDs.Contains(item.EquipmentData.equipmentID))
				{
					accepted = true;
				}
			}
			else
			{
				// Tool slot: Sadece specific item ID
				accepted = item.ItemID == _acceptedItemID && !_filled;
			}

			if (!accepted)
		{
			_dropAttempts++;
			string lvl = SequenceManager.Instance?.CurrentLevelID    ?? string.Empty;
			string seq = SequenceManager.Instance?.CurrentSequenceID ?? string.Empty;
			PlayFabDataManager.Instance?.LogDragDropAttempt(
				_actionID, lvl, seq, item.ItemID, gameObject.name, false, _dropAttempts);
			PlayFabDataManager.Instance?.LogMistakeRecorded(_actionID, "wrong_drop", 1);
			return false;
		}

			AcceptItem(item);
			return true;
		}

		private void AcceptItem(UIDraggableItem item)
		{
			_dropAttempts++;
			_filled = true;

			string lvl = SequenceManager.Instance?.CurrentLevelID    ?? string.Empty;
			string seq = SequenceManager.Instance?.CurrentSequenceID ?? string.Empty;
			PlayFabDataManager.Instance?.LogDragDropAttempt(
				_actionID, lvl, seq, item.ItemID, gameObject.name, true, _dropAttempts);

			item.transform.SetParent(transform);

			RectTransform itemRT = item.GetComponent<RectTransform>();
			if (itemRT != null)
				itemRT.anchoredPosition = Vector2.zero;

			CanvasGroup itemCG = item.GetComponent<CanvasGroup>();
			if (itemCG != null)
				itemCG.blocksRaycasts = false;

			if (background != null)
				background.color = filledColor;

			if (icon != null && item.ItemIconSprite != null)
			{
				icon.sprite = item.ItemIconSprite;
				icon.enabled = true;
			}

			// SequenceManager'a bildir
			if (SequenceManager.Instance != null)
			{
				if (item.EquipmentData != null)
				{
					SequenceManager.Instance.OnEquipmentWorn(item.EquipmentData);
					item.ReturnToOriginalPosition();
				}

				else
				{
					SequenceManager.Instance.OnToolDropped(_actionID, _uniqueID);

					// Drop event varsa tetikle
					if (!string.IsNullOrEmpty(_onDropEventID))
					{
						SequenceManager.Instance.TriggerSceneEvent(_onDropEventID);
						Debug.Log($"[UIDropZone] Drop event triggered: {_onDropEventID}");
						if (debugMode)
							Debug.Log($"[UIDropZone] Drop event triggered: {_onDropEventID}");
					}
				}

			}
			else
			{
				Debug.LogWarning("[UIDropZone] SequenceManager.Instance is null!");
			}

			if (debugMode)
				Debug.Log($"<color=green>[UIDropZone] Item accepted: {item.ItemID}</color>");
		}

		public void OnPointerEnter(PointerEventData e)
		{
			if (_filled || background == null) return;

			UIDraggableItem dragged = e.pointerDrag?.GetComponent<UIDraggableItem>();
			if (dragged == null) return;

			bool canAccept = CanAcceptItem(dragged);
			background.color = canAccept ? hoverValid : hoverInvalid;

			// HoverOnly modu: Üzerine gelince otomatik drop
			if (_toolDropMode == ToolDropMode.HoverOnly && canAccept && !_isEquipmentSlot)
			{
				if (TryAcceptItem(dragged))
				{
					if (debugMode)
						Debug.Log($"[UIDropZone] HoverOnly auto-drop: {dragged.ItemID}");
				}
			}
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
				return item.ItemID == _acceptedItemID;
		}

		public void Reset()
		{
			_filled = false;
			if (background != null) background.color = normalColor;
			if (icon != null) icon.enabled = false;
		}
	}
}