using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	/// <summary>
	/// Sekans giriş butonu
	/// Level başladığında spawn edilir, ilgili obje üzerinde durur
	/// Tıklanınca o sekansa girer
	/// </summary>
	public class UISequenceButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public Image background;
		public Image icon;
		public TextMeshProUGUI label;
		public GameObject completedIndicator; // Tik işareti vb.
		public GameObject lockedIndicator; // Kilit ikonu

		[Header("━━━ RENKLER ━━━")]
		public Color normalColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
		public Color hoverColor = new Color(1f, 0.92f, 0.2f, 1f);
		public Color completedColor = new Color(0.2f, 0.6f, 1f, 0.9f);
		public Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

		[Header("━━━ SCALE ━━━")]
		public float normalScale = 1f;
		public float hoverScale = 1.15f;

		[Header("━━━ SES ━━━")]
		public AudioClip clickSound;
		public AudioClip hoverSound;

		// Runtime
		private string _sequenceID;
		private bool _isCompleted;
		private bool _isLocked;
		private bool _hovered;
		private AudioSource _audio;
		private Vector3 _originalScale;

		private void Awake()
		{
			_audio = GetComponent<AudioSource>();
			if (_audio == null && (clickSound || hoverSound))
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}

			_originalScale = transform.localScale;
		}

		/// <summary>
		/// Spawn sonrası init
		/// </summary>
		public void Setup(string sequenceID, string labelText, Sprite iconSprite = null, Color? customColor = null)
		{
			_sequenceID = sequenceID;

			if (label) label.text = labelText;
			if (icon && iconSprite) { icon.sprite = iconSprite; icon.enabled = true; }

			Color targetColor = customColor ?? normalColor;
			if (background) background.color = targetColor;

			if (completedIndicator) completedIndicator.SetActive(false);
			if (lockedIndicator) lockedIndicator.SetActive(false);

			_isCompleted = false;
			_isLocked = false;

			gameObject.SetActive(true);
		}

		/// <summary>
		/// Sekans tamamlandı durumuna geçir
		/// </summary>
		public void SetCompleted(bool completed)
		{
			_isCompleted = completed;

			if (background)
				background.color = completed ? completedColor : normalColor;

			if (completedIndicator)
				completedIndicator.SetActive(completed);
		}

		/// <summary>
		/// Sekans kilitli durumuna geçir
		/// </summary>
		public void SetLocked(bool locked)
		{
			_isLocked = locked;

			if (background)
				background.color = locked ? lockedColor : (_isCompleted ? completedColor : normalColor);

			if (lockedIndicator)
				lockedIndicator.SetActive(locked);
		}

		public void OnPointerEnter(PointerEventData e)
		{
			if (_isLocked) return;

			_hovered = true;
			transform.localScale = _originalScale * hoverScale;

			if (background && !_isCompleted)
				background.color = hoverColor;

			PlaySound(hoverSound);
		}

		public void OnPointerExit(PointerEventData e)
		{
			_hovered = false;
			transform.localScale = _originalScale * normalScale;

			if (background)
			{
				if (_isLocked)
					background.color = lockedColor;
				else if (_isCompleted)
					background.color = completedColor;
				else
					background.color = normalColor;
			}
		}

		public void OnPointerClick(PointerEventData e)
		{
			if (_isLocked)
			{
				// Kilitli sekansa tıklama - ses çal
				PlaySound(clickSound);
				return;
			}

			PlaySound(clickSound);

			// Sekansa gir
			SequenceManager.Instance?.OnSequenceButtonClicked(_sequenceID);
		}

		private void PlaySound(AudioClip clip)
		{
			if (_audio != null && clip != null)
				_audio.PlayOneShot(clip);
		}

		public void SetActive(bool active)
		{
			gameObject.SetActive(active);

			// UIWorldFollower'ı da kontrol et
			UIWorldFollower follower = GetComponent<UIWorldFollower>();
			if (follower != null)
			{
				if (active)
					follower.EnableFollowing();  // ✅ Aç
				else
					follower.DisableFollowing(); // ✅ Kapat
			}
		}
	}
}
