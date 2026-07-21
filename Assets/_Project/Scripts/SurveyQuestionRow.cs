using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining
{
	public class SurveyQuestionRow : MonoBehaviour
	{
		[Header("REFERANSLAR")]
		public TextMeshProUGUI questionLabel;
		public CustomDropdown dropdown;

		[Header("RENKLER")]
		public Color answeredColor = new Color(0.15f, 0.75f, 0.25f, 0.8f);
		public Color unansweredColor = new Color(1f, 1f, 1f, 0.85f);

		private int _questionIndex;
		private SurveyQuestion _question;
		private System.Action<int, int, string> _onAnswerSelected;

		private const string PLACEHOLDER = "Seciniz...";

		public void Setup(int questionIndex, SurveyQuestion question, System.Action<int, int, string> onAnswerSelected)
		{
			_questionIndex = questionIndex;
			_question = question;
			_onAnswerSelected = onAnswerSelected;

			RectTransform rt = GetComponent<RectTransform>();
			if (rt != null)
			{
				rt.anchorMin = new Vector2(0, 1);
				rt.anchorMax = new Vector2(1, 1);
				rt.pivot = new Vector2(0.5f, 1f);
			}

			if (questionLabel != null)
				questionLabel.text = question.questionText;

			if (dropdown == null)
				dropdown = GetComponent<CustomDropdown>() ?? GetComponentInChildren<CustomDropdown>(true);

			if (dropdown != null)
			{
				dropdown.saveSelected = false;
				dropdown.onValueChanged.RemoveAllListeners();
				dropdown.items.Clear();

				dropdown.CreateNewItem(PLACEHOLDER, false);

				if (question.options != null)
				{
					foreach (var opt in question.options)
						dropdown.CreateNewItem(opt, false);
				}

				dropdown.selectedItemIndex = 0;
				dropdown.SetupDropdown();
				dropdown.onValueChanged.AddListener(OnDropdownChanged);
			}

			SetDropdownColor(unansweredColor);
		}

		private void OnDropdownChanged(int index)
		{
			if (index == 0)
				return;

			int realIndex = index - 1;
			string optionText = _question.options != null && realIndex < _question.options.Count
				? _question.options[realIndex]
				: "";

			SetDropdownColor(answeredColor);

			_onAnswerSelected?.Invoke(_questionIndex, realIndex, optionText);
		}

		private void SetDropdownColor(Color color)
		{
			if (dropdown == null)
				return;

			Image bg = dropdown.GetComponent<Image>();
			if (bg != null)
				bg.color = color;
		}

		private void OnDestroy()
		{
			if (dropdown != null)
				dropdown.onValueChanged.RemoveAllListeners();
		}
	}
}
