using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SafetyTraining
{
	public class SurveyPhotoSlotUI : MonoBehaviour
	{
		[Header("REFERANSLAR")]
		public TextMeshProUGUI slotLabel;
		public RawImage previewImage;
		public Button openCameraButton;
		public TextMeshProUGUI buttonLabel;
		public Image statusIcon;
		public TextMeshProUGUI statusText;

		[Header("VARSAYILAN GORUNUM")]
		[Tooltip("Fotoğraf çekilmeden önce gösterilecek placeholder texture")]
		public Texture placeholderTexture;

		[Header("RENKLER")]
		public Color alignedColor = new Color(0.1f, 0.8f, 0.2f);
		public Color misalignedColor = new Color(0.9f, 0.5f, 0.1f);
		public Color defaultColor = new Color(0.5f, 0.5f, 0.5f);

		private int _slotIndex;
		private PhotoSlot _slot;
		private System.Action _onOpenCamera;
		private bool _isCaptured;

		public int SlotIndex => _slotIndex;
		public bool IsCaptured => _isCaptured;

		public void Setup(int slotIndex, PhotoSlot slot, System.Action onOpenCamera)
		{
			_slotIndex = slotIndex;
			_slot = slot;
			_onOpenCamera = onOpenCamera;
			_isCaptured = false;

			RectTransform rt = GetComponent<RectTransform>();
			if (rt != null)
			{
				rt.anchorMin = new Vector2(0, 1);
				rt.anchorMax = new Vector2(0, 1);
				rt.pivot = new Vector2(0, 1f);
			}

			if (slotLabel != null)
				slotLabel.text = slot != null ? slot.slotLabel : string.Empty;

			if (buttonLabel != null)
				buttonLabel.text = "Fotoğraf Çek";

			if (previewImage != null)
			{
				previewImage.texture = placeholderTexture;
				previewImage.color = new Color(1, 1, 1, placeholderTexture != null ? 1f : 0.3f);
			}

			SetStatus("Fotoğraf çekilmedi", defaultColor);

			if (openCameraButton != null)
			{
				openCameraButton.onClick.RemoveAllListeners();
				openCameraButton.onClick.AddListener(() => _onOpenCamera?.Invoke());
			}
		}

		public void SetInteractable(bool interactable)
		{
			if (openCameraButton != null)
				openCameraButton.interactable = interactable;
		}

		public void SetPreview(Texture2D photo, bool isAligned)
		{
			_isCaptured = true;

			if (previewImage != null)
			{
				previewImage.texture = photo;
				previewImage.color = Color.white;
			}

			if (isAligned)
			{
				SetStatus("✓ Çekildi", alignedColor);
				if (buttonLabel != null) buttonLabel.text = "Yeniden Çek";
			}
			else
			{
				SetStatus("⚠ Hizalama Zayıf", misalignedColor);
				if (buttonLabel != null) buttonLabel.text = "Tekrar Çek";
			}
		}

		private void SetStatus(string text, Color color)
		{
			if (statusText != null)
			{
				statusText.text = text;
				statusText.color = color;
			}

			if (statusIcon != null)
				statusIcon.color = color;
		}

		private void OnDestroy()
		{
			if (openCameraButton != null)
				openCameraButton.onClick.RemoveAllListeners();
		}
	}
}
