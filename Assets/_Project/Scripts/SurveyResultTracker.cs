using UnityEngine;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Tüm survey sonuçlarını tutar.
	/// Level boyunca singleton olarak yaşar.
	/// PlayFab veya başka analytics SDK'ya bağlanmak için
	/// GetAllResults() ile tüm veri alınabilir.
	/// </summary>
	public class SurveyResultTracker : MonoBehaviour
	{
		public static SurveyResultTracker Instance { get; private set; }

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ─── Tüm oturumların sonuçları ───
		private List<SurveySessionResult> _allResults = new List<SurveySessionResult>();

		// ─── Aktif oturum ───
		private SurveySessionResult _activeSession;

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			else
			{
				Destroy(gameObject);
			}
		}

		// ═══════════════════════════════════════════════════════
		// OTURUM YÖNETİMİ
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Yeni bir survey oturumu başlatır
		/// </summary>
		public void BeginSession(string actionID, string levelID, string sequenceID,
			SurveyActionData data)
		{
			_activeSession = new SurveySessionResult
			{
				actionID   = actionID,
				levelID    = levelID,
				sequenceID = sequenceID,
				isCompleted = false
			};

			// Soru sonuçları için yer aç
			if (data.questions != null)
			{
				foreach (var q in data.questions)
				{
					_activeSession.questionResults.Add(new SurveyQuestionResult
					{
						questionText = q.questionText,
						isAnswered   = false,
						selectedOptionIndex = -1
					});
				}
			}

			// Fotoğraf sonuçları için yer aç
			if (data.photoSlots != null)
			{
				foreach (var slot in data.photoSlots)
				{
					_activeSession.photoResults.Add(new SurveyPhotoResult
					{
						slotLabel      = slot.slotLabel,
						targetObjectID = slot.targetObjectID,
						wasCaptured    = false,
						isAligned      = false,
						alignmentScore = 0f
					});
				}
			}

			if (debugMode)
				Debug.Log($"<color=cyan>[SurveyResultTracker] Session started: {actionID}</color>");
		}

		// ═══════════════════════════════════════════════════════
		// SORU CEVAPLAMA
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Bir sorunun cevabını kaydeder
		/// </summary>
		public void RecordAnswer(int questionIndex, int selectedOptionIndex,
			string selectedOptionText, bool isCorrect)
		{
			if (_activeSession == null)
			{
				Debug.LogWarning("[SurveyResultTracker] RecordAnswer: Aktif oturum yok!");
				return;
			}

			if (questionIndex < 0 || questionIndex >= _activeSession.questionResults.Count)
			{
				Debug.LogWarning($"[SurveyResultTracker] RecordAnswer: Geçersiz index {questionIndex}");
				return;
			}

			var result = _activeSession.questionResults[questionIndex];
			result.selectedOptionIndex = selectedOptionIndex;
			result.selectedOptionText  = selectedOptionText;
			result.isCorrect           = isCorrect;
			result.isAnswered          = true;

			if (debugMode)
				Debug.Log($"[SurveyResultTracker] Q{questionIndex}: '{selectedOptionText}' " +
					$"→ {(isCorrect ? "<color=lime>DOĞRU</color>" : "<color=red>YANLIŞ</color>")}");
		}

		// ═══════════════════════════════════════════════════════
		// FOTOĞRAF KAYDETME
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Çekilen fotoğrafın sonucunu kaydeder
		/// </summary>
		public void RecordPhoto(int slotIndex, Texture2D texture,
			float alignmentScore, bool isAligned)
		{
			if (_activeSession == null)
			{
				Debug.LogWarning("[SurveyResultTracker] RecordPhoto: Aktif oturum yok!");
				return;
			}

			if (slotIndex < 0 || slotIndex >= _activeSession.photoResults.Count)
			{
				Debug.LogWarning($"[SurveyResultTracker] RecordPhoto: Geçersiz index {slotIndex}");
				return;
			}

			var result = _activeSession.photoResults[slotIndex];
			result.wasCaptured    = true;
			result.capturedTexture = texture;
			result.alignmentScore = alignmentScore;
			result.isAligned      = isAligned;

			if (debugMode)
				Debug.Log($"[SurveyResultTracker] Photo slot {slotIndex}: " +
					$"score={alignmentScore:F2} " +
					$"{(isAligned ? "<color=lime>HİZALI</color>" : "<color=orange>HİZASIZ</color>")}");
		}

		// ═══════════════════════════════════════════════════════
		// OTURUM KAPATMA
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Aktif oturumu tamamlar ve sonuçlara ekler
		/// </summary>
		public SurveySessionResult EndSession()
		{
			if (_activeSession == null)
			{
				Debug.LogWarning("[SurveyResultTracker] EndSession: Aktif oturum yok!");
				return null;
			}

			_activeSession.isCompleted   = true;
			_activeSession.completionTime = Time.time;

			_allResults.Add(_activeSession);

			if (debugMode)
			{
				Debug.Log($"<color=lime>[SurveyResultTracker] Session ended: {_activeSession.actionID} | " +
					$"Doğru: {_activeSession.CorrectAnswerCount}/{_activeSession.questionResults.Count} | " +
					$"Fotoğraf: {_activeSession.CapturedAndAlignedPhotoCount}/{_activeSession.photoResults.Count}</color>");
			}

			SurveySessionResult completed = _activeSession;
			_activeSession = null;
			return completed;
		}

		// ═══════════════════════════════════════════════════════
		// VERİ ERİŞİMİ (Analytics için)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Tüm oturum sonuçlarını döndürür (PlayFab vs. için)
		/// </summary>
		public List<SurveySessionResult> GetAllResults() => _allResults;

		/// <summary>
		/// Belirli bir action'ın sonucunu döndürür
		/// </summary>
		public SurveySessionResult GetResultByActionID(string actionID)
		{
			return _allResults.Find(r => r.actionID == actionID);
		}

		/// <summary>
		/// Tüm sonuçları temizler (level restart için)
		/// </summary>
		public void ClearAll()
		{
			_allResults.Clear();
			_activeSession = null;
		}
	}
}
