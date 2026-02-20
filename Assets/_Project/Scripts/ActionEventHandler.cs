using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Sahne objesi - ActionData'dan gelen event ID'lerine göre Unity Event'leri tetikler
	/// ScriptableObject'lerden sahne objelerine erişim için köprü
	/// </summary>
	public class ActionEventHandler : MonoBehaviour
	{
		[Header("━━━ EVENT MAPPINGS ━━━")]
		[Tooltip("Action event ID'lerine karşılık gelen Unity Event'ler")]
		public List<ActionEventMapping> eventMappings = new List<ActionEventMapping>();

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Runtime lookup ───
		private Dictionary<string, UnityEvent> _eventLookup = new Dictionary<string, UnityEvent>();

		private void Awake()
		{
			// Lookup table oluştur
			foreach (var mapping in eventMappings)
			{
				if (string.IsNullOrEmpty(mapping.eventID))
				{
					Debug.LogWarning("[ActionEventHandler] Event mapping with empty eventID!", this);
					continue;
				}

				if (_eventLookup.ContainsKey(mapping.eventID))
				{
					Debug.LogWarning($"[ActionEventHandler] Duplicate eventID: '{mapping.eventID}'", this);
					continue;
				}

				_eventLookup[mapping.eventID] = mapping.onEventTriggered;

				if (debugMode)
					Debug.Log($"[ActionEventHandler] Registered event: '{mapping.eventID}' with {mapping.onEventTriggered.GetPersistentEventCount()} listener(s)");
			}
		}

		/// <summary>
		/// Event ID'sine göre event tetikler
		/// </summary>
		public void TriggerEvent(string eventID)
		{
			if (string.IsNullOrEmpty(eventID))
				return;

			if (_eventLookup.TryGetValue(eventID, out UnityEvent evt))
			{
				if (debugMode)
					Debug.Log($"<color=cyan>[ActionEventHandler] ✓ Triggering event: '{eventID}'</color>");

				evt?.Invoke();
			}
			else
			{
				if (debugMode)
					Debug.LogWarning($"[ActionEventHandler] Event '{eventID}' not found in mappings!");
			}
		}

		/// <summary>
		/// Birden fazla event tetikler
		/// </summary>
		public void TriggerEvents(string[] eventIDs)
		{
			if (eventIDs == null || eventIDs.Length == 0)
				return;

			foreach (string eventID in eventIDs)
			{
				TriggerEvent(eventID);
			}
		}
	}

	/// <summary>
	/// Event ID ve UnityEvent eşleştirmesi
	/// </summary>
	[System.Serializable]
	public class ActionEventMapping
	{
		[Tooltip("Event ID (ActionData'da kullanılacak)")]
		public string eventID;

		[Tooltip("Tetiklenecek event (Inspector'dan atanır)")]
		public UnityEvent onEventTriggered;
	}
}
