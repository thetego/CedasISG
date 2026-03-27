using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace SafetyTraining
{
	public class SurveyQuestionRow : MonoBehaviour
	{
		[Header("━━━ REFERANSLAR ━━━")]
		public TextMeshProUGUI questionLabel;
		public TMP_Dropdown    dropdown;

		[Header("━━━ RENKLER ━━━")]
		public Color answeredColor   = new Color(0.15f, 0.75f, 0.25f, 0.8f);
		public Color unansweredColor = new Color(1f, 1f, 1f, 0.85f);

		// ─── Runtime ───
		private int _questionIndex;
		private SurveyQuestion _question;
		private System.Action<int, int, string> _onAnswerSelected;
		private bool _isAnswered;

		// ─── Başlangıç seçeneği (placeholder) ───
		private const string PLACEHOLDER = "Seçiniz...";

		public void Setup(int questionIndex, SurveyQuestion question,
			System.Action<int, int, string> onAnswerSelected)
		{
			_questionIndex    = questionIndex;
			_question         = question;
			_onAnswerSelected = onAnswerSelected;
			_isAnswered       = false;

			// RectTransform — anchor stretch yatay, pivot üst-orta
			RectTransform rt = GetComponent<RectTransform>();
			if (rt != null)
			{
				rt.anchorMin = new Vector2(0, 1);
				rt.anchorMax = new Vector2(1, 1);
				rt.pivot     = new Vector2(0.5f, 1f);
				rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y);
				rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y);
			}

			// Soru metni
			if (questionLabel != null)
				questionLabel.text = question.questionText;

			// Dropdown seçeneklerini doldur
			if (dropdown != null)
			{
				dropdown.ClearOptions();

				var optionList = new List<TMP_Dropdown.OptionData>
				{
					new TMP_Dropdown.OptionData(PLACEHOLDER)
				};

				if (question.options != null)
				{
					foreach (var opt in question.options)
						optionList.Add(new TMP_Dropdown.OptionData(opt));
				}

				dropdown.AddOptions(optionList);
				dropdown.value = 0; // Placeholder seçili

				dropdown.onValueChanged.RemoveAllListeners();
				dropdown.onValueChanged.AddListener(OnDropdownChanged);

				// Başlangıç rengi
				SetDropdownColor(unansweredColor);
			}
		}

		private void OnDropdownChanged(int index)
		{
			// 0 = placeholder, asıl seçenekler 1'den başlıyor
			if (index == 0) return;

			int    realIndex  = index - 1; // Placeholder offset'i çıkar
			string optionText = _question.options != null && realIndex < _question.options.Count
				? _question.options[realIndex]
				: "";

			_isAnswered = true;
			SetDropdownColor(answeredColor);

			_onAnswerSelected?.Invoke(_questionIndex, realIndex, optionText);
		}

		private void SetDropdownColor(Color color)
		{
			if (dropdown == null) return;
			Image bg = dropdown.GetComponent<Image>();
			if (bg != null) bg.color = color;
		}

		private void OnDestroy()
		{
			if (dropdown != null)
				dropdown.onValueChanged.RemoveAllListeners();
		}
	}

	// ═══════════════════════════════════════════════════════
}
