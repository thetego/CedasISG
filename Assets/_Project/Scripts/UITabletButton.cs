using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SafetyTraining
{
	/// <summary>
	/// Ekranın alt ortasında sabit duran tablet açma butonu.
	/// Survey action aktif olduğunda görünür olur.
	/// Tıklanınca UISurveyPanel'i açar/kapatır.
	/// </summary>
	public class UITabletButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[Header("━━━ REFERANSLAR ━━━")]
		[Tooltip("Tablet ikonu")]
		public Image tabletIcon;

		[Tooltip("Opsiyonel label")]
		public TextMeshProUGUI label;

		[Tooltip("Arka plan image")]
		public Image background;

		[Header("━━━ RENKLER ━━━")]
		public Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);
		public Color hoverColor  = new Color(0.25f, 0.55f, 0.85f, 1f);
		public Color activeColor = new Color(0.15f, 0.65f, 0.35f, 1f); // Panel açıkken

		[Header("━━━ ANİMASYON ━━━")]
		[Tooltip("Hover'da scale büyümesi")]
		public float hoverScale = 1.08f;

		[Tooltip("Scale animasyon hızı")]
		public float scaleSpeed = 8f;

		[Header("━━━ SES ━━━")]
		public AudioClip clickSound;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ─── Runtime ───
		private UISurveyPanel _surveyPanel;
		private bool          _isPanelOpen;
		private bool          _isInteractable = false; // SetInteractable(true) ile açılır
		private bool          _hovered;
		private Vector3       _originalScale;
		private AudioSource   _audio;

		private void Awake()
		{
			_originalScale = transform.localScale;

			_audio = GetComponent<AudioSource>();
			if (_audio == null && clickSound != null)
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}
		}

		private void Update()
		{
			// Scale animasyonu
			float targetScale = _hovered ? hoverScale : 1f;
			transform.localScale = Vector3.Lerp(
				transform.localScale,
				_originalScale * targetScale,
				Time.deltaTime * scaleSpeed
			);
		}

		// ═══════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Survey action başladığında çağrılır
		/// </summary>
		public void Activate(UISurveyPanel panel)
		{
			_surveyPanel    = panel;
			_isPanelOpen    = false;
			_isInteractable = false; // SetInteractable(true) çağrılana kadar kilitli

			UpdateVisuals();
			gameObject.SetActive(true);

			if (debugMode)
				Debug.Log("[UITabletButton] Activated (kilitli)");
		}

		/// <summary>
		/// Survey action bittiğinde çağrılır
		/// </summary>
		public void Deactivate()
		{
			_surveyPanel = null;
			_isPanelOpen = false;
			gameObject.SetActive(false);

			if (debugMode)
				Debug.Log("[UITabletButton] Deactivated");
		}

		/// <summary>
		/// Tablet butonunu aktif/pasif yapar.
		/// false = görünür ama tıklanamaz (gri), true = tıklanabilir.
		/// </summary>
		public void SetInteractable(bool interactable)
		{
			_isInteractable = interactable;

			// Tablet yeniden aktifleştirilince panel durumunu sıfırla
			// (önceki oturumdan _isPanelOpen=true kalabilir)
			if (interactable)
			{
				_isPanelOpen = false;
				_surveyPanel?.ClosePanel();
			}

			UpdateVisuals();

			if (debugMode)
				Debug.Log($"[UITabletButton] SetInteractable: {interactable}, isPanelOpen reset={interactable}");
		}

		// ═══════════════════════════════════════════════════════
		// POINTER EVENTS
		// ═══════════════════════════════════════════════════════

		public void OnPointerEnter(PointerEventData e)
		{
			_hovered = true;
			if (background != null && !_isPanelOpen)
				background.color = hoverColor;
		}

		public void OnPointerExit(PointerEventData e)
		{
			_hovered = false;
			UpdateVisuals();
		}

		public void OnPointerClick(PointerEventData e)
		{
			if (!_isInteractable) return;

			if (_audio != null && clickSound != null)
				_audio.PlayOneShot(clickSound);

			TogglePanel();
		}

		// ═══════════════════════════════════════════════════════
		// PANEL TOGGLE
		// ═══════════════════════════════════════════════════════

		private void TogglePanel()
		{
			if (_surveyPanel == null)
			{
				Debug.LogError("[UITabletButton] _surveyPanel null!");
				return;
			}

			_isPanelOpen = !_isPanelOpen;

			Debug.Log($"[UITabletButton] TogglePanel → isPanelOpen={_isPanelOpen} waitingForTablet={SequenceManager.Instance?._waitingForTablet}");

			if (_isPanelOpen)
			{
				_surveyPanel.OpenPanel();
				_isInteractable = false; // Panel açıkken buton deaktif
			}
			else
			{
				_surveyPanel.ClosePanel();
				SequenceManager.Instance?.OnTabletClosed();
			}

			UpdateVisuals();
		}

		// ─── Panel dışarıdan kapatıldıysa senkronize et ───
		public void SyncPanelState(bool isOpen)
		{
			_isPanelOpen = isOpen;
			UpdateVisuals();
		}

		private static readonly Color COL_DISABLED = new Color(0.3f, 0.3f, 0.3f, 0.6f);

		private void UpdateVisuals()
		{
			if (background == null) return;

			if (!_isInteractable)
			{
				background.color = COL_DISABLED;
				if (tabletIcon != null) tabletIcon.color = new Color(1f, 1f, 1f, 0.4f);
				return;
			}

			if (tabletIcon != null) tabletIcon.color = Color.white;

			if (_isPanelOpen)
				background.color = activeColor;
			else
				background.color = _hovered ? hoverColor : normalColor;
		}
	}
}