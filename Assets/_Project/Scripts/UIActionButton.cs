using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	/// <summary>
	/// Action için etkileşim butonu
	/// Obje üstünde görünür, tıklanınca action başlar
	/// State: Available / Disabled / Completed
	/// </summary>
	public class UIActionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public Image background;
		public Image icon;
		public TextMeshProUGUI label;

		[Header("━━━ RENKLER ━━━")]
		public Color availableColor = new Color(0.2f, 0.85f, 0.2f, 0.9f);   // Yeşil
		public Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);     // Gri
		public Color completedColor = new Color(0.2f, 0.6f, 1f, 0.9f);      // Mavi
		public Color hoverColor = new Color(1f, 0.92f, 0.2f, 1f);           // Sarı

		[Header("━━━ SES ━━━")]
		public AudioClip clickSound;
		public AudioClip disabledClickSound;

		// Runtime
		private string _actionID;
		private ButtonState _state;
		private bool _hovered;
		private AudioSource _audio;

		public enum ButtonState
		{
			Available,   // Tıklanabilir, önkoşul sağlandı
			Disabled,    // Tıklanamaz, önkoşul yok
			Completed    // Tamamlandı, tekrar tıklanabilir
		}

		private void Awake()
		{
			_audio = GetComponent<AudioSource>();
			if (_audio == null && (clickSound || disabledClickSound))
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}
		}

		/// <summary>
		/// Spawn sonrası init
		/// </summary>
		public void Setup(string actionID, string labelText, Sprite iconSprite = null)
		{
			_actionID = actionID;

			if (label) label.text = labelText;
			if (icon && iconSprite) { icon.sprite = iconSprite; icon.enabled = true; }

			SetState(ButtonState.Disabled); // Başlangıçta disabled
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Buton state'ini değiştirir
		/// </summary>
		public void SetState(ButtonState newState)
		{
			_state = newState;
			UpdateVisuals();
		}

		private void UpdateVisuals()
		{
			if (background == null) return;

			Color targetColor = _state switch
			{
				ButtonState.Available => availableColor,
				ButtonState.Disabled => disabledColor,
				ButtonState.Completed => completedColor,
				_ => Color.white
			};

			if (_hovered && _state == ButtonState.Available)
			{
				background.color = hoverColor;
			}
			else
			{
				background.color = targetColor;
			}
		}

		public void SetActive(bool active) => gameObject.SetActive(active);

		public void OnPointerEnter(PointerEventData e)
		{
			_hovered = true;
			UpdateVisuals();
		}

		public void OnPointerExit(PointerEventData e)
		{
			_hovered = false;
			UpdateVisuals();
		}

		public void OnPointerClick(PointerEventData e)
		{
			if (_state == ButtonState.Disabled)
			{
				// Disabled → Ses çal + tooltip göster
				if (_audio && disabledClickSound)
					_audio.PlayOneShot(disabledClickSound);

				ActionManager.Instance?.ShowPrerequisiteWarning(_actionID);
				return;
			}

			// Available veya Completed → Action başlat
			if (_audio && clickSound)
				_audio.PlayOneShot(clickSound);

			ActionManager.Instance?.OnActionButtonClicked(_actionID);
		}
	}
}