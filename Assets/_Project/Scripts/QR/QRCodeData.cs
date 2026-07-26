using UnityEngine;

namespace SafetyTraining
{
	[CreateAssetMenu(fileName = "New QR Code Data", menuName = "Safety Training/QR Code Data")]
	public class QRCodeData : ScriptableObject
	{
		[Header("━━━ BİLGİLER ━━━")]
		[Tooltip("QR kodun içine gömülü tam metin (okunan içerik ile birebir eşleşmeli)")]
		public string qrCodeID;
		public string title;
		public Sprite icon;

		[Header("━━━ UI ━━━")]
		[TextArea(2, 5)]
		public string message;
	}
}
