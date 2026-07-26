using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SafetyTraining
{
	/// <summary>
	/// QR kod okunduğunda QRScannerManager tarafından Instantiate edilen, QR'ı ekranda takip eden
	/// bağımsız bilgi popup'ı. SequenceManager/ActionEventHandler akışına dokunmaz.
	/// QR görüntüden kaybolunca QRScannerManager tarafından Destroy edilir.
	/// </summary>
	public class UIQRPopupPanel : MonoBehaviour
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public TextMeshProUGUI titleText;
		public TextMeshProUGUI messageText;
		public Image iconImage;

		[Tooltip("Paneli elle kapatma butonu (opsiyonel)")]
		public Button closeButton;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		private string _qrCodeID;

		private void Awake()
		{
			if (closeButton != null)
			{
				closeButton.onClick.RemoveAllListeners();
				closeButton.onClick.AddListener(OnCloseButtonClicked);
			}
		}

		/// <summary>
		/// Panel içeriğini QRCodeData'dan doldurur
		/// </summary>
		public void Setup(QRCodeData data)
		{
			if (data == null)
			{
				Debug.LogError("[UIQRPopupPanel] Setup called with null QRCodeData!");
				return;
			}

			_qrCodeID = data.qrCodeID;

			if (titleText != null)
				titleText.text = data.title;

			if (messageText != null)
				messageText.text = data.message;

			if (iconImage != null)
			{
				iconImage.sprite = data.icon;
				iconImage.enabled = data.icon != null;
			}

			if (debugMode)
				Debug.Log($"[UIQRPopupPanel] Setup complete for QR: {data.qrCodeID}");
		}

		/// <summary>
		/// Close button tıklandı — panel yok edilir, aynı QR karede kaldığı sürece tekrar açılmaz
		/// </summary>
		private void OnCloseButtonClicked()
		{
			if (QRScannerManager.Instance != null)
			{
				QRScannerManager.Instance.NotifyPopupDismissed(_qrCodeID);
			}

			if (debugMode)
				Debug.Log($"[UIQRPopupPanel] Panel elle kapatıldı: {_qrCodeID}");

			Destroy(gameObject);
		}

		private void OnDestroy()
		{
			if (closeButton != null)
				closeButton.onClick.RemoveAllListeners();
		}
	}
}
