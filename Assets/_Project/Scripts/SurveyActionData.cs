using UnityEngine;
using System.Collections.Generic;

namespace SafetyTraining
{
	// ═══════════════════════════════════════════════════════
	// SURVEY DATA (ScriptableObject field — ActionData içinde)
	// ═══════════════════════════════════════════════════════

	[System.Serializable]
	public class SurveyActionData
	{
		[Header("━━━ GENEL ━━━")]
		[Tooltip("Panel başlığı")]
		public string panelTitle = "Kontrol Formu";

		[Tooltip("Tamamla buton metni")]
		public string completeButtonText = "Tamamla";

		[Header("━━━ SORULAR ━━━")]
		[Tooltip("Dropdown sorular listesi")]
		public List<SurveyQuestion> questions = new List<SurveyQuestion>();

		[Header("━━━ FOTOĞRAF SLOTLARI ━━━")]
		[Tooltip("Çekilecek fotoğraf slotları (max 5)")]
		[Range(0, 10)]
		public int photoSlotCount = 0;

		public List<PhotoSlot> photoSlots = new List<PhotoSlot>();
	}

	// ═══════════════════════════════════════════════════════
	// SORU
	// ═══════════════════════════════════════════════════════

	[System.Serializable]
	public class SurveyQuestion
	{
		[Tooltip("Soru metni")]
		[TextArea(1, 3)]
		public string questionText;

		[Tooltip("Dropdown seçenekleri")]
		public List<string> options = new List<string>();

		[Tooltip("Doğru cevap indexleri (birden fazla olabilir)")]
		public List<int> correctOptionIndices = new List<int>();

		[Tooltip("Sorunun zorunlu olup olmadığı (analytics için)")]
		public bool isRequired = true;
	}

	// ═══════════════════════════════════════════════════════
	// FOTOĞRAF SLOTU
	// ═══════════════════════════════════════════════════════

	[System.Serializable]
	public class PhotoSlot
	{
		[Tooltip("Slot başlığı / açıklaması")]
		public string slotLabel = "Fotoğraf";

		[Tooltip("Sahnedeki hedef objenin SceneObject ID'si (görünmez obje)")]
		public string targetObjectID;

		[Tooltip("Kabul edilebilir hizalama eşiği — ekran genişliğinin yüzdesi (0.0 - 0.5)")]
		[Range(0.01f, 0.5f)]
		public float alignmentThreshold = 0.10f;

		[Tooltip("Hedef göstergesi rengi (tam hizalandığında)")]
		public Color indicatorAlignedColor = new Color(0.1f, 0.9f, 0.2f, 0.85f);

		[Tooltip("Hedef göstergesi rengi (hizalanmadığında)")]
		public Color indicatorOffColor = new Color(0.9f, 0.2f, 0.1f, 0.65f);
	}

	// ═══════════════════════════════════════════════════════
	// RUNTIME SONUÇ — Analytics için tutulan veri
	// ═══════════════════════════════════════════════════════

	[System.Serializable]
	public class SurveyQuestionResult
	{
		public string questionText;
		public int    selectedOptionIndex = -1;   // -1 = cevaplanmadı
		public string selectedOptionText;
		public bool   isCorrect;
		public bool   isAnswered;
	}

	[System.Serializable]
	public class SurveyPhotoResult
	{
		public string  slotLabel;
		public string  targetObjectID;
		public bool    wasCaptured;
		public bool    isAligned;           // Doğru hizalamada mı çekildi
		public float   alignmentScore;      // 0-1 arası (1 = tam merkez)
		public Texture2D capturedTexture;   // Çekilen görüntü (null ise çekilmedi)
	}

	[System.Serializable]
	public class SurveySessionResult
	{
		public string   actionID;
		public string   levelID;
		public string   sequenceID;
		public float    completionTime;
		public bool     isCompleted;

		public List<SurveyQuestionResult> questionResults = new List<SurveyQuestionResult>();
		public List<SurveyPhotoResult>    photoResults    = new List<SurveyPhotoResult>();

		// ─── Hesaplanan skorlar ───
		public int CorrectAnswerCount
		{
			get
			{
				int count = 0;
				foreach (var r in questionResults)
					if (r.isCorrect) count++;
				return count;
			}
		}

		public int CapturedAndAlignedPhotoCount
		{
			get
			{
				int count = 0;
				foreach (var r in photoResults)
					if (r.wasCaptured && r.isAligned) count++;
				return count;
			}
		}
	}
}