using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SafetyTraining
{
	/// <summary>
	/// UI panellerini ve ekranlarını yönetir
	/// Level complete, game over, pause gibi ekranlar
	/// </summary>
	public class UIManager : MonoBehaviour
	{
		public static UIManager Instance { get; private set; }

		[Header("━━━ PANELS ━━━")]
		[Tooltip("Level tamamlandı paneli")]
		public GameObject levelCompletePanel;

		[Tooltip("Game over paneli")]
		public GameObject gameOverPanel;

		[Tooltip("Pause paneli")]
		public GameObject pausePanel;

		[Header("━━━ LEVEL COMPLETE UI ━━━")]
		public TextMeshProUGUI scoreText;
		public TextMeshProUGUI timeText;
		public TextMeshProUGUI mistakesText;
		public Image[] stars; // Yıldız sistemi (3 yıldız)

		[Header("━━━ BUTTONS ━━━")]
		public Button restartButton;
		public Button nextLevelButton;
		public Button mainMenuButton;

		[Header("━━━ SETTINGS ━━━")]
		[Tooltip("3 yıldız için minimum skor")]
		public int threeStarThreshold = 90;

		[Tooltip("2 yıldız için minimum skor")]
		public int twoStarThreshold = 70;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			else
			{
				Destroy(gameObject);
				return;
			}
		}

		private void Start()
		{
			// Başlangıçta tüm panelleri kapat
			HideAllPanels();

			// Buton event'lerini bağla
			SetupButtons();
		}

		private void SetupButtons()
		{
			if (restartButton != null)
				restartButton.onClick.AddListener(OnRestartClicked);

			if (nextLevelButton != null)
				nextLevelButton.onClick.AddListener(OnNextLevelClicked);

			if (mainMenuButton != null)
				mainMenuButton.onClick.AddListener(OnMainMenuClicked);
		}

		/// <summary>
		/// Level tamamlandı ekranını gösterir
		/// </summary>
		public void ShowLevelComplete(int score, float time, int mistakes)
		{
			if (levelCompletePanel == null)
			{
				Debug.LogWarning("[UIManager] levelCompletePanel null!");
				return;
			}

			levelCompletePanel.SetActive(true);

			// Skorları göster
			if (scoreText != null)
				scoreText.text = $"Score: {score}";

			if (timeText != null)
			{
				int minutes = Mathf.FloorToInt(time / 60f);
				int seconds = Mathf.FloorToInt(time % 60f);
				timeText.text = $"Time: {minutes:00}:{seconds:00}";
			}

			if (mistakesText != null)
				mistakesText.text = $"Mistakes: {mistakes}";

			// Yıldızları göster
			ShowStars(score);

			if (debugMode)
				Debug.Log($"<color=cyan>[UIManager] Level Complete - Score: {score}, Time: {time:F1}s, Mistakes: {mistakes}</color>");
		}

		/// <summary>
		/// Yıldızları skor bazlı gösterir
		/// </summary>
		private void ShowStars(int score)
		{
			if (stars == null || stars.Length == 0) return;

			int starCount = 0;

			if (score >= threeStarThreshold)
				starCount = 3;
			else if (score >= twoStarThreshold)
				starCount = 2;
			else if (score > 0)
				starCount = 1;

			for (int i = 0; i < stars.Length; i++)
			{
				if (stars[i] != null)
				{
					stars[i].enabled = (i < starCount);
				}
			}

			if (debugMode)
				Debug.Log($"[UIManager] Stars: {starCount}/3");
		}

		/// <summary>
		/// Game over ekranını gösterir
		/// </summary>
		public void ShowGameOver(string message = "Game Over!")
		{
			if (gameOverPanel == null)
			{
				Debug.LogWarning("[UIManager] gameOverPanel null!");
				return;
			}

			gameOverPanel.SetActive(true);

			TextMeshProUGUI messageText = gameOverPanel.GetComponentInChildren<TextMeshProUGUI>();
			if (messageText != null)
				messageText.text = message;

			if (debugMode)
				Debug.Log($"<color=red>[UIManager] Game Over: {message}</color>");
		}

		/// <summary>
		/// Pause ekranını gösterir/gizler
		/// </summary>
		public void TogglePause()
		{
			if (pausePanel == null) return;

			bool isActive = !pausePanel.activeSelf;
			pausePanel.SetActive(isActive);

			// Oyunu duraklat/devam ettir
			Time.timeScale = isActive ? 0f : 1f;

			if (debugMode)
				Debug.Log($"[UIManager] Pause: {isActive}");
		}

		/// <summary>
		/// Tüm panelleri gizler
		/// </summary>
		public void HideAllPanels()
		{
			if (levelCompletePanel != null)
				levelCompletePanel.SetActive(false);

			if (gameOverPanel != null)
				gameOverPanel.SetActive(false);

			if (pausePanel != null)
				pausePanel.SetActive(false);

			// Oyunu devam ettir
			Time.timeScale = 1f;
		}

		// ═══════════════════════════════════════════════════════
		// BUTTON CALLBACKS
		// ═══════════════════════════════════════════════════════

		private void OnRestartClicked()
		{
			HideAllPanels();

			// ActionManager veya LevelManager üzerinden restart
			if (ActionManager.Instance != null)
				ActionManager.Instance.RestartLevel();
			else if (LevelManager.Instance != null)
				LevelManager.Instance.RestartLevel();

			if (debugMode)
				Debug.Log("[UIManager] Restart clicked");
		}

		private void OnNextLevelClicked()
		{
			// TODO: Sonraki level'ı yükle (SceneManager kullanarak)
			if (debugMode)
				Debug.Log("[UIManager] Next Level clicked - Not implemented yet");
		}

		private void OnMainMenuClicked()
		{
			// TODO: Ana menüye dön (SceneManager kullanarak)
			if (debugMode)
				Debug.Log("[UIManager] Main Menu clicked - Not implemented yet");

			// Geçici: Sadece panelleri kapat
			HideAllPanels();
		}

		/// <summary>
		/// Talimat textini günceller (harici kullanım için)
		/// </summary>
		public void UpdateInstruction(string text)
		{
			// Bu method ActionManager veya LevelManager tarafından kullanılabilir
			// Eğer UI'da bir instruction text varsa buradan güncellenebilir
			if (debugMode)
				Debug.Log($"[UIManager] Instruction: {text}");
		}
	}
}
