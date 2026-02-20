using UnityEngine;

namespace SafetyTraining
{
	[CreateAssetMenu(fileName = "New Tool", menuName = "Safety Training/Tool Data")]
	public class ToolData : ScriptableObject
	{
		[Header("━━━ BİLGİLER ━━━")]
		public string toolID;
		public string toolName;
		public Sprite toolIcon;

		[Header("━━━ DROP AYARLARI ━━━")]
		[Tooltip("Default drop target (ActionData'da override edilebilir)")]
		public string dropTargetObjectID;

		[Header("━━━ SAHNE REFERANSI ━━━")]
		[Tooltip("Tool bırakılınca sahnede aktif olacak GameObject ID'si (opsiyonel)")]
		public string gameObjectID;

		[Header("━━━ UI ━━━")]
		[TextArea(1, 3)]
		public string tooltipText;
	}
}