using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace SafetyTraining
{
	/// <summary>
	/// UI elementlerini spawn eder ve yönetir
	/// Hem eski ActionManager hem yeni SequenceManager ile uyumlu
	/// </summary>
	public class UISpawnManager : MonoBehaviour
	{
		public static UISpawnManager Instance { get; private set; }

		[Header("━━━ PREFABS ━━━")]
		[Tooltip("Sekans butonu prefabı (UISequenceButton)")]
		public GameObject sequenceButtonPrefab;

		[Tooltip("Action butonu prefabı (UIActionButton)")]
		public GameObject actionButtonPrefab;

		[Tooltip("Click butonu prefabı (UIClickButton)")]
		public GameObject clickButtonPrefab;

		[Tooltip("Drop zone prefabı (UIDropZone)")]
		public GameObject dropZonePrefab;

		[Tooltip("Draggable item prefabı (UIDraggableItem)")]
		public GameObject draggableItemPrefab;

		[Header("━━━ CONTAINERS ━━━")]
		[Tooltip("World to screen butonlar için container (Canvas altında)")]
		public Transform worldButtonContainer;

		[Tooltip("Ekipman paneli container")]
		public RectTransform equipmentPanelContainer;

		[Tooltip("Inventory container (draggable itemlar)")]
		public Transform inventoryContainer;

		[Header("━━━ PANELS ━━━")]
		[Tooltip("Ekipman paneli (WearEquipment actionları için)")]
		public GameObject equipmentPanel;

		[Header("━━━ SETTINGS ━━━")]
		[Tooltip("World follower için default offset")]
		public Vector3 defaultWorldOffset = Vector3.zero;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Runtime Storage ───
		private Dictionary<string, GameObject> _spawnedSequenceButtons = new Dictionary<string, GameObject>();
		private Dictionary<string, GameObject> _spawnedActionButtons = new Dictionary<string, GameObject>();
		private Dictionary<string, GameObject> _spawnedButtons = new Dictionary<string, GameObject>();
		private Dictionary<string, GameObject> _spawnedDropZones = new Dictionary<string, GameObject>();
		private Dictionary<string, GameObject> _spawnedItems = new Dictionary<string, GameObject>();
		private Dictionary<string, List<GameObject>> _actionUIElements = new Dictionary<string, List<GameObject>>();

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
		}

		// ═══════════════════════════════════════════════════════
		// YENİ SİSTEM: SEQUENCE BUTTON SPAWNING
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Level başladığında tüm sekans butonlarını spawn eder (YENİ SİSTEM)
		/// </summary>
		public void SpawnSequenceButtons(LevelData level)
		{
			if (level == null || level.sequences == null)
			{
				Debug.LogError("[UISpawnManager] Level veya sequences null!");
				return;
			}

			CleanupSequenceButtons();

			foreach (var sequence in level.sequences)
			{
				if (sequence == null) continue;

				// spawnButtonAtStart kontrolü
				if (sequence.spawnButtonAtStart)
				{
					SpawnSequenceButton(sequence);
				}
				else
				{
					if (debugMode)
						Debug.Log($"[UISpawnManager] Skipping button spawn for '{sequence.sequenceName}' (spawnButtonAtStart=false)");
				}
			}

			if (debugMode)
				Debug.Log($"<color=cyan>[UISpawnManager] Spawned {_spawnedSequenceButtons.Count} sequence buttons</color>");
		}

		/// <summary>
		/// Tek bir sequence butonu spawn eder (programmatically çağrılabilir)
		/// </summary>
		public void SpawnSequenceButton(SequenceData sequence)
		{
			if (sequenceButtonPrefab == null || worldButtonContainer == null)
			{
				Debug.LogError("[UISpawnManager] sequenceButtonPrefab veya worldButtonContainer null!");
				return;
			}

			// Zaten spawn edilmiş mi?
			if (_spawnedSequenceButtons.ContainsKey(sequence.sequenceID))
			{
				if (debugMode)
					Debug.LogWarning($"[UISpawnManager] Button already spawned for '{sequence.sequenceName}'");
				return;
			}

			GameObject btnObj = Instantiate(sequenceButtonPrefab, worldButtonContainer);
			UISequenceButton btn = btnObj.GetComponent<UISequenceButton>();

			if (btn != null)
			{
				btn.Setup(
					sequence.sequenceID,
					sequence.sequenceName,
					sequence.sequenceIcon,
					sequence.buttonColor
				);

				// World follower ekle (eğer hedef obje varsa)
				if (!string.IsNullOrEmpty(sequence.targetObjectID))
				{
					UIWorldFollower follower = btnObj.GetComponent<UIWorldFollower>();
					if (follower == null)
						follower = btnObj.AddComponent<UIWorldFollower>();

					follower.Initialize(sequence.targetObjectID, sequence.buttonOffset);
				}

				// SequenceManager'a kaydet
				if (SequenceManager.Instance != null)
					SequenceManager.Instance.RegisterSequenceButton(sequence.sequenceID, btn);

				_spawnedSequenceButtons[sequence.sequenceID] = btnObj;

				if (debugMode)
					Debug.Log($"<color=lime>[UISpawnManager] ✓ Spawned sequence button: '{sequence.sequenceName}'</color>");
			}
		}

		private void CleanupSequenceButtons()
		{
			foreach (var kvp in _spawnedSequenceButtons)
			{
				if (kvp.Value != null)
					Destroy(kvp.Value);
			}
			_spawnedSequenceButtons.Clear();
		}

		// ═══════════════════════════════════════════════════════
		// ESKİ SİSTEM: ACTION BUTTON SPAWNING (BACKWARD COMPATIBILITY)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// ActionManager için: Tüm action butonlarını spawn eder (ESKİ SİSTEM)
		/// </summary>
		public void SpawnAllActionButtons(LevelData level)
		{
			if (level == null || level.actionSequence == null)
			{
				Debug.LogError("[UISpawnManager] Level veya actionSequence null!");
				return;
			}

			CleanupActionButtons();

			foreach (var action in level.actionSequence)
			{
				if (action == null) continue;
				SpawnActionButton(action);
			}

			if (debugMode)
				Debug.Log($"<color=cyan>[UISpawnManager] Spawned {_spawnedActionButtons.Count} action buttons</color>");
		}

		private void SpawnActionButton(ActionData action)
		{
			if (actionButtonPrefab == null || worldButtonContainer == null)
			{
				Debug.LogError("[UISpawnManager] actionButtonPrefab veya worldButtonContainer null!");
				return;
			}

			GameObject btnObj = Instantiate(actionButtonPrefab, worldButtonContainer);
			UIActionButton btn = btnObj.GetComponent<UIActionButton>();

			if (btn != null)
			{
				btn.Setup(action.actionID, action.actionName);

				// World follower ekle (eğer hedef obje varsa)
				if (!string.IsNullOrEmpty(action.targetObjectID))
				{
					UIWorldFollower follower = btnObj.GetComponent<UIWorldFollower>();
					if (follower == null)
						follower = btnObj.AddComponent<UIWorldFollower>();

					follower.Initialize(action.targetObjectID, defaultWorldOffset);
				}

				// ActionManager'a kaydet
				if (ActionManager.Instance != null)
					ActionManager.Instance.RegisterActionButton(action.actionID, btn);

				_spawnedActionButtons[action.actionID] = btnObj;
			}
		}

		private void CleanupActionButtons()
		{
			foreach (var kvp in _spawnedActionButtons)
			{
				if (kvp.Value != null)
					Destroy(kvp.Value);
			}
			_spawnedActionButtons.Clear();
		}

		/// <summary>
		/// LevelManager için: Tüm UI'ları spawn eder ama deaktif tutar (ESKİ SİSTEM)
		/// </summary>
		public void SpawnAllUI(LevelData level)
		{
			if (level == null || level.actionSequence == null)
			{
				Debug.LogError("[UISpawnManager] Level veya actionSequence null!");
				return;
			}

			CleanupAllUI();

			foreach (var action in level.actionSequence)
			{
				if (action == null) continue;
				SpawnUIForAction(action);
			}

			// Ekipman panelini kapat
			if (equipmentPanel)
				equipmentPanel.SetActive(false);

			if (debugMode)
				Debug.Log($"<color=cyan>[UISpawnManager] Spawned UI for {level.actionSequence.Length} actions</color>");
		}

		// ═══════════════════════════════════════════════════════
		// ORTAK: ACTION UI SPAWNING (HER İKİ SİSTEM İÇİN)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Action aktif olduğunda ilgili UI'ları gösterir
		/// </summary>
		public IEnumerator ActivateAction(ActionData action, float Delay = 0f)
		{
			yield return new WaitForSeconds(Delay);

			if (action == null)  yield return null;

			//if (action.actionType == ActionType.CameraMove) return;

			// Eğer UI daha önce spawn edilmemişse, şimdi spawn et
			if (!_actionUIElements.ContainsKey(action.actionID))
			{
				SpawnUIForAction(action);
			}

			// Action'a ait UI'ları aktif et
			if (_actionUIElements.TryGetValue(action.actionID, out List<GameObject> elements))
			{
				foreach (var element in elements)
				{
					if (element != null)
						element.SetActive(true);
				}
			}

			// Ekipman panelini aç (WearEquipment için)
			if (action.actionType == ActionType.WearEquipment && equipmentPanel != null)
			{
				equipmentPanel.SetActive(true);
			}

			// Layout'u güncelle (inventory ve equipment panel için)
			if (inventoryContainer != null)
			{
				StartCoroutine(RebuildLayoutNextFrame(inventoryContainer.GetComponent<RectTransform>()));
			}
			if (equipmentPanelContainer != null)
			{
				StartCoroutine(RebuildLayoutNextFrame(equipmentPanelContainer.GetComponent<RectTransform>()));
			}

			if (debugMode)
				Debug.Log($"<color=green>[UISpawnManager] Activated UI for action: {action.actionName}</color>");
		}

		private System.Collections.IEnumerator RebuildLayoutNextFrame(RectTransform rectTransform)
		{
			if (rectTransform == null) yield break;

			yield return null; // Bir frame bekle
			UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

			if (debugMode)
				Debug.Log($"[UISpawnManager] Layout rebuilt for: {rectTransform.name}");
		}

		/// <summary>
		/// Action tamamlandığında UI'ları deaktif eder
		/// </summary>
		public void DeactivateAction(ActionData action)
		{
			if (action == null) return;

			// Action'a ait UI'ları deaktif et
			if (_actionUIElements.TryGetValue(action.actionID, out List<GameObject> elements))
			{
				foreach (var element in elements)
				{
					if (element != null)
						element.SetActive(false);
				}
			}

			// Ekipman panelini kapat (WearEquipment için)
			if (action.actionType == ActionType.WearEquipment && equipmentPanel != null)
			{
				equipmentPanel.SetActive(false);
			}

			if (debugMode)
				Debug.Log($"<color=yellow>[UISpawnManager] Deactivated UI for action: {action.actionName}</color>");
		}

		private void SpawnUIForAction(ActionData action)
		{
			List<GameObject> uiElements = new List<GameObject>();

			switch (action.actionType)
			{
				case ActionType.Click:
				case ActionType.OpenClose:
					GameObject clickBtn = SpawnClickButton(action);
					if (clickBtn != null) uiElements.Add(clickBtn);
					break;

				case ActionType.WearEquipment:
					uiElements.AddRange(SpawnEquipmentUI(action));
					break;

				case ActionType.DragToWorld:
					uiElements.AddRange(SpawnDragToWorldUI(action));
					break;

				case ActionType.PanelInteraction:
					GameObject panel = SpawnInteractionPanel(action);
					if (panel != null) uiElements.Add(panel);
					break;

				case ActionType.CameraMove:
					// Kamera hareketi için UI gerekmez
					break;
			}

			if (uiElements.Count > 0)
			{
				_actionUIElements[action.actionID] = uiElements;

				// Başlangıçta deaktif
				foreach (var ui in uiElements)
				{
					if (ui != null)
						ui.SetActive(false);
				}
			}
		}

		private GameObject SpawnClickButton(ActionData action)
		{
			if (clickButtonPrefab == null || worldButtonContainer == null)
			{
				Debug.LogError("[UISpawnManager] clickButtonPrefab veya worldButtonContainer null!");
				return null;
			}

			GameObject btnObj = Instantiate(clickButtonPrefab, worldButtonContainer);
			UIClickButton btn = btnObj.GetComponent<UIClickButton>();

			if (btn != null)
			{
				btn.Setup(action.actionID, action.actionName);
			}

			// World follower ekle
			if (!string.IsNullOrEmpty(action.targetObjectID))
			{
				UIWorldFollower follower = btnObj.GetComponent<UIWorldFollower>();
				if (follower == null)
					follower = btnObj.AddComponent<UIWorldFollower>();

				follower.Initialize(action.targetObjectID, defaultWorldOffset);
			}

			_spawnedButtons[action.actionID] = btnObj;
			return btnObj;
		}

		private List<GameObject> SpawnEquipmentUI(ActionData action)
		{
			List<GameObject> elements = new List<GameObject>();

			if (action.requiredEquipments == null || action.requiredEquipments.Length == 0)
			{
				Debug.LogWarning($"[UISpawnManager] Action '{action.actionID}' has no required equipments!");
				return elements;
			}

			// Drop zone'lar spawn et (equipment panel içinde)
			if (dropZonePrefab != null && equipmentPanelContainer != null)
			{
				foreach (var eq in action.requiredEquipments)
				{
					if (eq == null) continue;

					GameObject zoneObj = Instantiate(dropZonePrefab, equipmentPanelContainer);
					UIDropZone zone = zoneObj.GetComponent<UIDropZone>();
					UIWorldFollower follower = zoneObj.GetComponent<UIWorldFollower>();

					if (zone != null)
					{
						zone.SetupAsEquipmentSlot(action.actionID, eq.slotType, eq.equipmentName);
						follower.Initialize(eq.dropID, Vector3.zero);
					}

					elements.Add(zoneObj);
					_spawnedDropZones[$"{action.actionID}_{eq.equipmentID}"] = zoneObj;
				}
			}

			// Draggable itemlar spawn et (inventory içinde)
			if (draggableItemPrefab != null && inventoryContainer != null)
			{
				foreach (var eq in action.requiredEquipments)
				{
					if (eq == null) continue;

					GameObject itemObj = Instantiate(draggableItemPrefab, inventoryContainer);
					UIDraggableItem item = itemObj.GetComponent<UIDraggableItem>();

					if (item != null)
					{
						item.Setup(eq.equipmentID, eq.equipmentName, eq.equipmentIcon, eq);
					}

					elements.Add(itemObj);
					_spawnedItems[$"{action.actionID}_{eq.equipmentID}"] = itemObj;
				}
			}

			// ─── Distractor Equipments (sadece draggable item, drop zone yok) ───
			if (action.distractorEquipments != null && action.distractorEquipments.Length > 0)
			{
				if (debugMode)
					Debug.Log($"[UISpawnManager] Spawning {action.distractorEquipments.Length} distractor equipments...");

				if (draggableItemPrefab != null && inventoryContainer != null)
				{
					foreach (var eq in action.distractorEquipments)
					{
						if (eq == null) continue;

						GameObject itemObj = Instantiate(draggableItemPrefab, inventoryContainer);
						UIDraggableItem item = itemObj.GetComponent<UIDraggableItem>();

						if (item != null)
							item.Setup(eq.equipmentID, eq.equipmentName, eq.equipmentIcon, eq);

						elements.Add(itemObj);
						_spawnedItems[$"{action.actionID}_distractor_{eq.equipmentID}"] = itemObj;

						if (debugMode)
							Debug.Log($"  → Distractor equipment created: {eq.equipmentName} (no drop zone)");
					}
				}
			}

			return elements;
		}

		private List<GameObject> SpawnDragToWorldUI(ActionData action)
		{
			List<GameObject> elements = new List<GameObject>();

			if (action.requiredTools == null || action.requiredTools.Length == 0)
			{
				Debug.LogWarning($"[UISpawnManager] Action '{action.actionID}' has no requiredTools!");
				return elements;
			}

			// Her tool için: drop zone (worldButtonContainer içinde) + draggable item (inventory)
			// Tool drop mappings varsa kullan (aynı tool, farklı drop zone'lar)
			if (action.toolDropMappings != null && action.toolDropMappings.Length > 0)
			{
				foreach (var mapping in action.toolDropMappings)
				{
					if (mapping == null || string.IsNullOrEmpty(mapping.uniqueID))
						continue;

					// Tool'u bul
					ToolData tool = System.Array.Find(action.requiredTools, t => t != null && t.toolID == mapping.toolID);
					if (tool == null)
					{
						Debug.LogWarning($"[UISpawnManager] Tool '{mapping.toolID}' not found in requiredTools!");
						continue;
					}

					// Drop zone spawn et (uniqueID ile)
					if (dropZonePrefab != null && worldButtonContainer != null)
					{
						GameObject zoneObj = Instantiate(dropZonePrefab, worldButtonContainer);
						UIDropZone zone = zoneObj.GetComponent<UIDropZone>();

						if (zone != null)
						{
							zone.SetupAsToolDrop(action.actionID, tool.toolID, tool.toolName, action.dropZoneSize, action.toolDropMode, mapping.uniqueID, mapping.onDropEventID);
						}

						// World follower
						if (!string.IsNullOrEmpty(mapping.dropTargetObjectID))
						{
							UIWorldFollower follower = zoneObj.GetComponent<UIWorldFollower>();
							if (follower == null)
								follower = zoneObj.AddComponent<UIWorldFollower>();

							follower.Initialize(mapping.dropTargetObjectID, defaultWorldOffset);
						}

						elements.Add(zoneObj);
						_spawnedDropZones[$"{action.actionID}_{mapping.uniqueID}"] = zoneObj;

						if (debugMode)
							Debug.Log($"  → Tool drop zone created: {tool.toolName} (uniqueID: {mapping.uniqueID}, target: {mapping.dropTargetObjectID})");
					}

					// Draggable item (sadece ilk mapping için spawn et)
					string itemKey = $"{action.actionID}_tool_{tool.toolID}";
					if (!_spawnedItems.ContainsKey(itemKey) && draggableItemPrefab != null && inventoryContainer != null)
					{
						GameObject itemObj = Instantiate(draggableItemPrefab, inventoryContainer);
						UIDraggableItem item = itemObj.GetComponent<UIDraggableItem>();

						if (item != null)
							item.SetupAsTool(tool.toolID, tool.toolName, tool.toolIcon, tool);

						// Layout'u güncelle
						UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryContainer.GetComponent<RectTransform>());

						elements.Add(itemObj);
						_spawnedItems[itemKey] = itemObj;

						if (debugMode)
							Debug.Log($"  → Tool draggable item created: {tool.toolName}");
					}
				}
			}
			else
			{
				// Eski sistem: Her tool için tek drop zone
			}

			// ─── Distractor Tools (sadece draggable item, drop zone yok) ───
			if (action.distractorTools != null && action.distractorTools.Length > 0)
			{
				if (debugMode)
					Debug.Log($"[UISpawnManager] Spawning {action.distractorTools.Length} distractor tools...");

				foreach (var tool in action.distractorTools)
				{
					if (tool == null) continue;

					if (draggableItemPrefab != null && inventoryContainer != null)
					{
						GameObject itemObj = Instantiate(draggableItemPrefab, inventoryContainer);
						UIDraggableItem item = itemObj.GetComponent<UIDraggableItem>();

						if (item != null)
							item.SetupAsTool(tool.toolID, tool.toolName, tool.toolIcon, tool);
						else
							Debug.LogError("[UISpawnManager] UIDraggableItem component missing on prefab!");

						elements.Add(itemObj);
						_spawnedItems[$"{action.actionID}_distractor_{tool.toolID}"] = itemObj;

						if (debugMode)
							Debug.Log($"  → Distractor tool created: {tool.toolName} (no drop zone)");
					}
				}
			}

			if (debugMode)
				Debug.Log($"<color=lime>[UISpawnManager] ✓ Spawned {elements.Count} tool UI elements</color>");

			return elements;
		}

		private GameObject SpawnInteractionPanel(ActionData action)
		{
			if (action.interactionPanelPrefab == null)
			{
				Debug.LogError($"[UISpawnManager] interactionPanelPrefab null for action '{action.actionID}'!");
				return null;
			}

			// Panel'i Canvas'a spawn et
			Canvas canvas = worldButtonContainer?.GetComponentInParent<Canvas>();
			if (canvas == null)
			{
				Debug.LogError("[UISpawnManager] Canvas not found!");
				return null;
			}

			GameObject panelObj = Instantiate(action.interactionPanelPrefab, canvas.transform);

			// UIInteractionPanel component'ini setup et
			UIInteractionPanel panelComponent = panelObj.GetComponent<UIInteractionPanel>();
			if (panelComponent != null)
			{
				panelComponent.Setup(action.actionID);

				if (debugMode)
					Debug.Log($"  → UIInteractionPanel setup: {panelObj.name}");
			}
			else
			{
				Debug.LogWarning($"[UISpawnManager] No UIInteractionPanel component on prefab '{panelObj.name}'! Add UIInteractionPanel component.");
			}

			_spawnedButtons[action.actionID] = panelObj;

			if (debugMode)
				Debug.Log($"<color=lime>[UISpawnManager] ✓ Interaction panel spawned: {panelObj.name}</color>");

			return panelObj;
		}

		// ═══════════════════════════════════════════════════════
		// CLEANUP
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Tüm spawn edilmiş UI'ları temizler
		/// </summary>
		public void CleanupAllUI()
		{
			// Sekans butonlarını temizle
			CleanupSequenceButtons();

			// Action butonlarını temizle
			CleanupActionButtons();

			// Click butonlarını temizle
			foreach (var kvp in _spawnedButtons)
			{
				if (kvp.Value != null)
					Destroy(kvp.Value);
			}
			_spawnedButtons.Clear();

			// Drop zone'ları temizle
			foreach (var kvp in _spawnedDropZones)
			{
				if (kvp.Value != null)
					Destroy(kvp.Value);
			}
			_spawnedDropZones.Clear();

			// Item'ları temizle
			foreach (var kvp in _spawnedItems)
			{
				if (kvp.Value != null)
					Destroy(kvp.Value);
			}
			_spawnedItems.Clear();

			// UI element listelerini temizle
			_actionUIElements.Clear();

			if (debugMode)
				Debug.Log("[UISpawnManager] All UI cleaned up");
		}

		// ═══════════════════════════════════════════════════════
		// HELPER METHODS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Tool için drop target bulur (mapping varsa onu, yoksa tool.dropTargetObjectID)
		/// </summary>
		private string GetToolDropTarget(ActionData action, ToolData tool)
		{
			// Önce mapping'e bak
			if (action.toolDropMappings != null)
			{
				foreach (var mapping in action.toolDropMappings)
				{
					if (mapping.toolID == tool.toolID)
						return mapping.dropTargetObjectID;
				}
			}

			// Mapping yoksa tool'un default değerini kullan
			return tool.dropTargetObjectID;
		}
	}
}