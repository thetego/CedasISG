using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	/// <summary>
	/// Click ve OpenClose actionlar için buton
	/// SequenceManager ile çalışır
	/// </summary>
	public class UIClickButton : MonoBehaviour,
			IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public Image background;
		public Image icon;
		public TextMeshProUGUI label;

		[Header("━━━ RENKLER ━━━")]
		public Color normalColor = new Color(1f, 1f, 1f, 0.85f);
		public Color hoverColor = new Color(0.2f, 0.85f, 1f, 1f);
		public Color pressColor = new Color(0.1f, 0.6f, 0.9f, 1f);

		[Header("━━━ SES ━━━")]
		public AudioClip hoverSound;
		public AudioClip clickSound;

		// Runtime
		private string _actionID;
		private AudioSource _audio;
		private bool _hovered;

		private void Awake()
		{
			_audio = GetComponent<AudioSource>();
			if (_audio == null && (hoverSound != null || clickSound != null))
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}
		}

		public void Setup(string actionID, string labelText, Sprite iconSprite = null)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIClickButton] Setup called with empty actionID!");
				return;
			}

			_actionID = actionID;

			if (label != null)
				label.text = labelText;

			if (icon != null && iconSprite != null)
			{
				icon.sprite = iconSprite;
				icon.enabled = true;
			}

			if (background != null)
				background.color = normalColor;

			gameObject.SetActive(false);
		}

		public void SetActive(bool state) => gameObject.SetActive(state);

		public void OnPointerEnter(PointerEventData e)
		{
			_hovered = true;
			if (background != null)
				background.color = hoverColor;

			PlaySound(hoverSound);
		}

		public void OnPointerExit(PointerEventData e)
		{
			_hovered = false;
			if (background != null)
				background.color = normalColor;
		}

		public void OnPointerClick(PointerEventData e)
		{
			if (background != null)
				background.color = pressColor;

			PlaySound(clickSound);
			Invoke(nameof(ResetColor), 0.12f);

			// SequenceManager'a bildir
			if (SequenceManager.Instance != null)
			{
				SequenceManager.Instance.OnActionCompleted(_actionID);
			}
			else
			{
				Debug.LogWarning("[UIClickButton] SequenceManager.Instance is null!");
			}
		}

		private void ResetColor()
		{
			if (background != null)
				background.color = _hovered ? hoverColor : normalColor;
		}

		private void PlaySound(AudioClip clip)
		{
			if (_audio != null && clip != null)
				_audio.PlayOneShot(clip);
		}
	}
}