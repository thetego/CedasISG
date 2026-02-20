using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining
{
	/// <summary>
	/// PanelInteraction action tipi için panel
	/// Panel açılır, kullanıcı panel içindeki butona tıklar, action tamamlanır
	/// </summary>
	public class UIInteractionPanel : MonoBehaviour
	{
		[Header("━━━ REFERANSLAR ━━━")]
		[Tooltip("Action tamamlama butonu (Inspector'dan ata)")]
		public Button completeButton;

		[Tooltip("Paneli kapatma butonu (opsiyonel)")]
		public Button closeButton;

		[Header("━━━ AYARLAR ━━━")]
		[Tooltip("Tamamlandıktan sonra paneli kapat")]
		public bool closeOnComplete = true;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// Runtime
		private string _actionID;
		private bool _isSetup = false;

		/// <summary>
		/// Panel'i setup et
		/// </summary>
		public void Setup(string actionID)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIInteractionPanel] Setup called with empty actionID!");
				return;
			}

			_actionID = actionID;
			_isSetup = true;

			// Complete button setup
			if (completeButton != null)
			{
				completeButton.onClick.RemoveAllListeners();
				completeButton.onClick.AddListener(OnCompleteButtonClicked);

				if (debugMode)
					Debug.Log($"[UIInteractionPanel] Complete button configured for action: {actionID}");
			}
			else
			{
				Debug.LogWarning("[UIInteractionPanel] Complete button is not assigned! Panel won't be functional.");
			}

			// Close button setup (opsiyonel)
			if (closeButton != null)
			{
				closeButton.onClick.RemoveAllListeners();
				closeButton.onClick.AddListener(OnCloseButtonClicked);
			}

			if (debugMode)
				Debug.Log($"[UIInteractionPanel] Setup complete for action: {actionID}");
		}

		/// <summary>
		/// Complete button tıklandı
		/// </summary>
		private void OnCompleteButtonClicked()
		{
			if (!_isSetup)
			{
				Debug.LogWarning("[UIInteractionPanel] OnCompleteButtonClicked called but panel not setup!");
				return;
			}

			if (debugMode)
				Debug.Log($"<color=lime>[UIInteractionPanel] ✓ Complete button clicked for action: {_actionID}</color>");

			// SequenceManager'a bildir
			if (SequenceManager.Instance != null)
			{
				SequenceManager.Instance.OnActionCompleted(_actionID);
			}
			else
			{
				Debug.LogWarning("[UIInteractionPanel] SequenceManager.Instance is null!");
			}

			// Panel'i kapat (opsiyonel)
			if (closeOnComplete)
			{
				ClosePanel();
			}
		}

		/// <summary>
		/// Close button tıklandı (action tamamlanmaz)
		/// </summary>
		private void OnCloseButtonClicked()
		{
			if (debugMode)
				Debug.Log("[UIInteractionPanel] Close button clicked (action NOT completed)");

			ClosePanel();
		}

		/// <summary>
		/// Panel'i kapat
		/// </summary>
		public void ClosePanel()
		{
			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log("[UIInteractionPanel] Panel closed");
		}

		/// <summary>
		/// Panel'i aç
		/// </summary>
		public void OpenPanel()
		{
			gameObject.SetActive(true);

			if (debugMode)
				Debug.Log("[UIInteractionPanel] Panel opened");
		}

		private void OnDestroy()
		{
			// Listener'ları temizle
			if (completeButton != null)
				completeButton.onClick.RemoveAllListeners();

			if (closeButton != null)
				closeButton.onClick.RemoveAllListeners();
		}
	}
}
