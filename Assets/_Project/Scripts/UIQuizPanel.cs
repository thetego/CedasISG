using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace SafetyTraining
{
	/// <summary>
	/// Quiz action tipi için panel.
	/// Soru ve 4 seçenek gösterir.
	/// Doğru → action tamamlanır.
	/// Yanlış → feedback gösterilir, aynı soruyu tekrar denemeye izin verilir.
	/// </summary>
	public class UIQuizPanel : MonoBehaviour
	{
		[Header("━━━ SORU ━━━")]
		[Tooltip("Soru metni")]
		public TextMeshProUGUI questionText;

		[Header("━━━ SEÇENEK BUTONLARI ━━━")]
		[Tooltip("4 seçenek butonu (sırayla A, B, C, D)")]
		public Button[] optionButtons;

		[Tooltip("Her butonun label'ı (optionButtons ile aynı sırada)")]
		public TextMeshProUGUI[] optionLabels;

		[Header("━━━ FEEDBACK ━━━")]
		[Tooltip("Feedback alanı (doğru/yanlış mesajı)")]
		public GameObject feedbackPanel;

		[Tooltip("Feedback metni")]
		public TextMeshProUGUI feedbackText;

		[Tooltip("Feedback arka plan rengi - doğru")]
		public Color correctFeedbackColor = new Color(0.1f, 0.8f, 0.2f, 0.95f);

		[Tooltip("Feedback arka plan rengi - yanlış")]
		public Color wrongFeedbackColor = new Color(0.85f, 0.15f, 0.15f, 0.95f);

		[Tooltip("Feedback image (renk değişimi için)")]
		public Image feedbackBackground;

		[Header("━━━ BUTON RENKLERİ ━━━")]
		public Color normalColor    = new Color(0.95f, 0.95f, 0.95f, 1f);
		public Color correctColor   = new Color(0.1f,  0.8f,  0.2f,  1f);
		public Color wrongColor     = new Color(0.85f, 0.15f, 0.15f, 1f);
		public Color disabledColor  = new Color(0.6f,  0.6f,  0.6f,  0.6f);

		[Header("━━━ ZAMANLAMA ━━━")]
		[Tooltip("Feedback gösterme süresi (saniye) — ardından panel kapanır / game over tetiklenir")]
		[Range(0.5f, 5f)]
		public float feedbackDuration = 2f;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ─── Runtime ───
		private string _actionID;
		private int    _correctIndex;
		private bool   _answered;
		private string _correctFeedback;
		private string _wrongFeedback;
		private int    _quizAttempts;
		private float  _quizStartTime;

		// ─── Prefab Letter Prefixes ───
		private static readonly string[] _prefixes = { "A) ", "B) ", "C) ", "D) " , "E) "};

		// ═══════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Panel'i QuizActionData ile doldurur.
		/// UISpawnManager tarafından spawn sonrasında çağrılır.
		/// </summary>
		public void Setup(string actionID, QuizActionData quizData)
		{
			if (string.IsNullOrEmpty(actionID))
			{
				Debug.LogError("[UIQuizPanel] Setup called with empty actionID!");
				return;
			}

			if (quizData == null)
			{
				Debug.LogError("[UIQuizPanel] Setup called with null quizData!");
				return;
			}

			_actionID        = actionID;
			_correctIndex    = quizData.correctOptionIndex;
			_correctFeedback = quizData.correctFeedbackText;
			_wrongFeedback   = quizData.wrongFeedbackText;
			_answered        = false;
			_quizAttempts    = 0;
			_quizStartTime   = Time.time;

			// Soru metni
			if (questionText != null)
				questionText.text = quizData.questionText;

			// Seçenek butonları
			int optionCount = Mathf.Min(
				quizData.options?.Length ?? 0,
				optionButtons?.Length   ?? 0
			);

			for (int i = 0; i < optionButtons.Length; i++)
			{
				if (optionButtons[i] == null) continue;

				if (i < optionCount && !string.IsNullOrEmpty(quizData.options[i]))
				{
					// Butonu doldur ve aktif et
					if (optionLabels != null && i < optionLabels.Length && optionLabels[i] != null)
						optionLabels[i].text = _prefixes[i] + quizData.options[i];

					SetButtonColor(optionButtons[i], normalColor);
					optionButtons[i].interactable = true;
					optionButtons[i].gameObject.SetActive(true);

					// Listener: index'i closure ile yakala
					int capturedIndex = i;
					optionButtons[i].onClick.RemoveAllListeners();
					optionButtons[i].onClick.AddListener(() => OnOptionClicked(capturedIndex));
				}
				else
				{
					// Seçenek yok → butonu gizle
					optionButtons[i].gameObject.SetActive(false);
				}
			}

			// Feedback panel başlangıçta kapalı
			if (feedbackPanel != null)
				feedbackPanel.SetActive(false);

			if (debugMode)
				Debug.Log($"[UIQuizPanel] Setup complete — actionID: {actionID}, correctIndex: {_correctIndex}");
		}

		// ═══════════════════════════════════════════════════════
		// OPTION CLICK
		// ═══════════════════════════════════════════════════════

		private void OnOptionClicked(int index)
		{
			// Zaten cevaplandıysa tekrar tıklamayı engelle
			if (_answered) return;
			_answered = true;
			_quizAttempts++;

			// Tüm butonları kilitle
			LockAllButtons();

			bool isCorrect = (index == _correctIndex);

			string levelId    = SequenceManager.Instance?.CurrentLevelID    ?? string.Empty;
			string sequenceId = SequenceManager.Instance?.CurrentSequenceID ?? string.Empty;
			string answer     = (optionLabels != null && index < optionLabels.Length && optionLabels[index] != null)
				? optionLabels[index].text
				: index.ToString();
			int timeSpent = Mathf.RoundToInt(Time.time - _quizStartTime);

			PlayFabDataManager.Instance?.LogQuizAnswered(
				_actionID, levelId, sequenceId,
				_actionID, answer, isCorrect, _quizAttempts, timeSpent
			);

			if (!isCorrect)
				SequenceManager.Instance?.OnQuizFailed(_actionID);

			// Seçilen butonun rengini güncelle
			if (index < optionButtons.Length && optionButtons[index] != null)
				SetButtonColor(optionButtons[index], isCorrect ? correctColor : wrongColor);

			// Yanlışsa doğru cevabı da göster
			if (!isCorrect && _correctIndex < optionButtons.Length && optionButtons[_correctIndex] != null)
				SetButtonColor(optionButtons[_correctIndex], correctColor);

			if (isCorrect)
			{
				ShowFeedback(_correctFeedback, correct: true);
				StartCoroutine(CompleteAfterDelay());
			}
			else
			{
				ShowFeedback(_wrongFeedback, correct: false);
				StartCoroutine(RetryAfterDelay());
			}

			if (debugMode)
				Debug.Log($"[UIQuizPanel] Option {index} clicked — correct: {isCorrect}");
		}

		// ═══════════════════════════════════════════════════════
		// FEEDBACK
		// ═══════════════════════════════════════════════════════

		private void ShowFeedback(string message, bool correct)
		{
			if (feedbackPanel == null) return;

			feedbackPanel.SetActive(true);

			if (feedbackText != null)
				feedbackText.text = message;

			if (feedbackBackground != null)
				feedbackBackground.color = correct ? correctFeedbackColor : wrongFeedbackColor;
		}

		// ═══════════════════════════════════════════════════════
		// COROUTINES
		// ═══════════════════════════════════════════════════════

		private IEnumerator CompleteAfterDelay()
		{
			yield return new WaitForSeconds(feedbackDuration);

			ClosePanel();

			if (SequenceManager.Instance != null)
				SequenceManager.Instance.OnActionCompleted(_actionID);
			else
				Debug.LogWarning("[UIQuizPanel] SequenceManager.Instance is null!");
		}

		private IEnumerator RetryAfterDelay()
		{
			yield return new WaitForSeconds(feedbackDuration);

			if (feedbackPanel != null)
				feedbackPanel.SetActive(false);

			ResetButtonsForRetry();
			_answered = false;
			_quizStartTime = Time.time;

			if (debugMode)
				Debug.Log("[UIQuizPanel] Wrong answer -> retry enabled.");
		}

		// ═══════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════

		private void LockAllButtons()
		{
			if (optionButtons == null) return;

			foreach (var btn in optionButtons)
			{
				if (btn != null)
					btn.interactable = false;
			}
		}

		private void SetButtonColor(Button btn, Color color)
		{
			if (btn == null) return;

			Image img = btn.GetComponent<Image>();
			if (img != null)
				img.color = color;
		}

		private void ResetButtonsForRetry()
		{
			if (optionButtons == null) return;

			for (int i = 0; i < optionButtons.Length; i++)
			{
				if (optionButtons[i] == null) continue;

				optionButtons[i].interactable = true;
				SetButtonColor(optionButtons[i], normalColor);
			}
		}

		public void ClosePanel()
		{
			gameObject.SetActive(false);
		}

		public void OpenPanel()
		{
			gameObject.SetActive(true);
		}

		private void OnDestroy()
		{
			if (optionButtons == null) return;

			foreach (var btn in optionButtons)
			{
				if (btn != null)
					btn.onClick.RemoveAllListeners();
			}
		}
	}
}
