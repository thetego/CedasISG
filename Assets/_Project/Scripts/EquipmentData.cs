using UnityEngine;

namespace SafetyTraining
{
	public enum EquipmentSlotType { Head, Body, Hands, Feet, Eyes }

	[CreateAssetMenu(fileName = "New Equipment", menuName = "Safety Training/Equipment Data")]
	public class EquipmentData : ScriptableObject
	{
		[Header("━━━ BİLGİLER ━━━")]
		public string equipmentID;
		public string equipmentName;
		public string dropID;
		public string gameObjectID;
		public Sprite equipmentIcon;
		public EquipmentSlotType slotType;

		[Header("━━━ GEREKLİLİK ━━━")]
		public bool isRequired = true;

		[Header("━━━ UI ━━━")]
		[TextArea(1, 3)]
		public string tooltipText;
	}
}