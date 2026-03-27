using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Survey action tipi için ana panel.
	/// Scroll alanında dropdown sorular + fotoğraf slotları.
	/// Altta sabit tamamla butonu.
	/// Kamera modu ayrı bir overlay olarak açılır.
	/// </summary>
	public class UISurveyPanel : MonoBehaviour
	{
		// ═══════════════════════════════════════════════════════
		// INSPECTOR
		// ═══════════════════════════════════════════════════════

		[Header("━━━ PANEL REFERANSLARI ━━━")]
		public TextMeshProUGUI panelTitleText;
		public ScrollRect      scrollRect;
		public RectTransform   contentContainer;    // Scroll içeriği — sadece sorular
		public RectTransform   photoSlotsContainer; // Scroll dışı sabit alan
		public Button          completeButton;
		public TextMeshProUGUI completeButtonText;

		[Header("━━━ PREFABLAR ━━━")]
		[Tooltip("Soru satırı prefabı (SurveyQuestionRow component'li)")]
		public GameObject questionRowPrefab;

		[Tooltip("Fotoğraf slot prefabı (SurveyPhotoSlotUI component'li)")]
		public GameObject photoSlotPrefab;

		[Header("━━━ KAMERA OVERLAY ━━━")]
		[Tooltip("Kamera görünümü overlay paneli (başta kapalı)")]
		public GameObject cameraOverlayPanel;

		[Tooltip("Tablet RenderTexture'ını gösteren RawImage")]
		public RawImage cameraRawImage;

		[Tooltip("Fotoğraf çek butonu")]
		public Button captureButton;

		[Tooltip("Kamera overlay'ini kapat butonu")]
		public Button closeCameraButton;

		[Tooltip("Hizalama durumu göstergesi text (\"Hizalayın\" / \"Hazır\")")]
		public TextMeshProUGUI alignmentStatusText;

		[Tooltip("Göstergeler için parent RectTransform (cameraRawImage üstünde)")]
		public RectTransform indicatorContainer;

		[Header("━━━ SES ━━━")]
		public AudioClip captureSound;
		public AudioClip completeSound;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ═══════════════════════════════════════════════════════
		// RUNTIME
		// ═══════════════════════════════════════════════════════

		private string              _actionID;
		private SurveyActionData    _data;
		private SurveyCameraController _cameraController;
		private AudioSource         _audio;

		// Oluşturulan UI satırları
		private List<SurveyQuestionRow>  _questionRows  = new List<SurveyQuestionRow>();
		private List<SurveyPhotoSlotUI>  _photoSlotUIs  = new List<SurveyPhotoSlotUI>();

		// Aktif kamera slot indeksi
		private int _activeCameraSlot = -1;

		// ═══════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════

		public void Setup(string actionID, SurveyActionData data,
			SurveyCameraController cameraController)
		{
			if (string.IsNullOrEmpty(actionID) || data == null)
			{
				Debug.LogError("[UISurveyPanel] Setup: actionID veya data null!");
				return;
			}

			_actionID         = actionID;
			_data             = data;
			_cameraController = cameraController;

			_audio = GetComponent<AudioSource>();
			if (_audio == null && (captureSound != null || completeSound != null))
			{
				_audio = gameObject.AddComponent<AudioSource>();
				_audio.playOnAwake = false;
			}

			// cameraOverlayPanel içindeki RawImage'ı otomatik bul
			// (spawn edilen prefabda Inspector ataması çalışmaz)
			if (cameraOverlayPanel == null)
				Debug.LogWarning("[UISurveyPanel] cameraOverlayPanel atanmamış!");

			if (cameraRawImage == null && cameraOverlayPanel != null)
				cameraRawImage = cameraOverlayPanel.GetComponentInChildren<RawImage>(true);

			if (cameraRawImage == null)
				cameraRawImage = GetComponentInChildren<RawImage>(true);

			// RenderTexture'ı hemen bağla
			if (_cameraController?.renderTexture != null)
			{
				if (cameraRawImage != null)
					cameraRawImage.texture = _cameraController.renderTexture;
				else
					Debug.LogError("[UISurveyPanel] RawImage bulunamadı — " +
						"CameraOverlayPanel altında bir RawImage olmalı.");
			}

			// SurveyCameraController'a cameraViewImage'ı bildir
			if (_cameraController != null && cameraRawImage != null)
				_cameraController.cameraViewImage = cameraRawImage;

			// Başlık
			if (panelTitleText != null)
				panelTitleText.text = data.panelTitle;

			if (completeButtonText != null)
				completeButtonText.text = data.completeButtonText;

			// Kamera overlay başta kapalı
			if (cameraOverlayPanel != null)
				cameraOverlayPanel.SetActive(false);

			// Buton listener'ları
			if (completeButton != null)
			{
				completeButton.onClick.RemoveAllListeners();
				completeButton.onClick.AddListener(OnCompleteClicked);
			}

			if (captureButton != null)
			{
				captureButton.onClick.RemoveAllListeners();
				captureButton.onClick.AddListener(OnCaptureClicked);
			}

			if (closeCameraButton != null)
			{
				closeCameraButton.onClick.RemoveAllListeners();
				closeCameraButton.onClick.AddListener(CloseCameraOverlay);
			}

			// Camera controller başlat
			if (_cameraController != null)
			{
				_cameraController.Initialize(data.photoSlots, indicatorContainer);
			}

			// Analytics oturumu başlat
			string levelID    = SequenceManager.Instance?.currentLevel?.levelID ?? "";
			string seqID      = actionID; // actionID artık sequenceID'dir
			SurveyResultTracker.Instance?.BeginSession(actionID, levelID, seqID, data);

			// İçerik oluştur
			ClearContent();
			BuildPhotoSlots();   // Önce photo row (en üstte)
			BuildQuestions();    // Sonra sorular (photo row'un altında)

			// Layout yenile
			StartCoroutine(RebuildLayout());
		}

		// ═══════════════════════════════════════════════════════
		// İÇERİK OLUŞTURMA
		// ═══════════════════════════════════════════════════════

		private void ClearContent()
		{
			_questionRows.Clear();
			_photoSlotUIs.Clear();

			if (contentContainer == null) return;

			for (int i = contentContainer.childCount - 1; i >= 0; i--)
				Destroy(contentContainer.GetChild(i).gameObject);
		}

		private void BuildPhotoSlots()
		{
			if (_data.photoSlots == null || photoSlotPrefab == null) return;
			if (photoSlotsContainer == null)
			{
				Debug.LogWarning("[UISurveyPanel] photoSlotsContainer atanmamış!");
				return;
			}

			int count = Mathf.Min(_data.photoSlotCount, _data.photoSlots.Count);

			// Önceki slotları temizle
			for (int i = photoSlotsContainer.childCount - 1; i >= 0; i--)
				Destroy(photoSlotsContainer.GetChild(i).gameObject);

			// Slotları photoSlotsContainer'a ekle
			// Container'da HorizontalLayoutGroup olması yeterli
			for (int i = 0; i < count; i++)
			{
				PhotoSlot slot = _data.photoSlots[i];
				if (slot == null) continue;

				GameObject slotObj = Instantiate(photoSlotPrefab, photoSlotsContainer);
				SurveyPhotoSlotUI slotUI = slotObj.GetComponent<SurveyPhotoSlotUI>();
				if (slotUI != null)
				{
					int capturedIndex = i;
					slotUI.Setup(i, slot, () => OpenCameraForSlot(capturedIndex));
					_photoSlotUIs.Add(slotUI);
				}
				else
					Debug.LogWarning("[UISurveyPanel] SurveyPhotoSlotUI component eksik!");
			}

			// Fotoğraf slotu yoksa container'ı gizle
			photoSlotsContainer.gameObject.SetActive(count > 0);
		}

		private void BuildQuestions()
		{
			if (_data.questions == null || questionRowPrefab == null) return;

			for (int i = 0; i < _data.questions.Count; i++)
			{
				SurveyQuestion question = _data.questions[i];
				if (question == null) continue;

				// Direkt contentContainer'a ekle
				// VerticalLayoutGroup + ContentSizeFitter sıralamayı yönetir
				GameObject rowObj = Instantiate(questionRowPrefab, contentContainer);

				SurveyQuestionRow row = rowObj.GetComponent<SurveyQuestionRow>();
				if (row != null)
				{
					int capturedIndex = i;
					row.Setup(i, question, (qIndex, optionIndex, optionText) =>
						OnAnswerSelected(qIndex, optionIndex, optionText));
					_questionRows.Add(row);
				}
				else
					Debug.LogWarning("[UISurveyPanel] SurveyQuestionRow component eksik!");
			}
		}

		// ═══════════════════════════════════════════════════════
		// SORU CEVAPLAMA
		// ═══════════════════════════════════════════════════════

		private void OnAnswerSelected(int questionIndex, int optionIndex, string optionText)
		{
			if (_data.questions == null || questionIndex >= _data.questions.Count) return;

			SurveyQuestion question = _data.questions[questionIndex];
			bool isCorrect = question.correctOptionIndices != null &&
				question.correctOptionIndices.Contains(optionIndex);

			SurveyResultTracker.Instance?.RecordAnswer(questionIndex, optionIndex, optionText, isCorrect);

			if (debugMode)
				Debug.Log($"[UISurveyPanel] Q{questionIndex} cevaplandı: {optionText} ({(isCorrect ? "DOĞRU" : "YANLIŞ")})");
		}

		// ═══════════════════════════════════════════════════════
		// KAMERA OVERLAY
		// ═══════════════════════════════════════════════════════

		private void OpenCameraForSlot(int slotIndex)
		{
			_activeCameraSlot = slotIndex;

			// cameraController null ise sahnede ara
			if (_cameraController == null)
			{
				_cameraController = FindObjectOfType<SurveyCameraController>();
				if (_cameraController != null && _data?.photoSlots != null)
					_cameraController.Initialize(_data.photoSlots, indicatorContainer);
			}

			if (_cameraController != null)
			{
				// cameraViewImage her açılışta tekrar bağla
				if (cameraRawImage != null)
					_cameraController.cameraViewImage = cameraRawImage;

				_cameraController.ActivateForSlot(slotIndex);
				Debug.Log($"[UISurveyPanel] ActivateForSlot({slotIndex}) çağrıldı.");
			}
			else
			{
				Debug.LogError("[UISurveyPanel] SurveyCameraController sahnede BULUNAMADI! " +
					"Sahnedeki objeye SurveyCameraController component ekle.");
			}

			if (cameraOverlayPanel != null)
				cameraOverlayPanel.SetActive(true);
			else
				Debug.LogError("[UISurveyPanel] cameraOverlayPanel atanmamış!");

			// RenderTexture bağla
			if (cameraRawImage != null && _cameraController?.renderTexture != null)
				cameraRawImage.texture = _cameraController.renderTexture;
			else if (cameraRawImage == null)
				Debug.LogError("[UISurveyPanel] cameraRawImage atanmamış!");
			else if (_cameraController?.renderTexture == null)
				Debug.LogError("[UISurveyPanel] SurveyCameraController.renderTexture atanmamış!");

			UpdateAlignmentStatus();

			if (debugMode)
				Debug.Log($"[UISurveyPanel] Kamera açıldı: slot {slotIndex}");
		}

		private void CloseCameraOverlay()
		{
			if (cameraOverlayPanel != null)
				cameraOverlayPanel.SetActive(false);

			_activeCameraSlot = -1;
		}

		private void Update()
		{
			if (cameraOverlayPanel != null && cameraOverlayPanel.activeSelf)
				UpdateAlignmentStatus();
		}

		private void UpdateAlignmentStatus()
		{
			if (alignmentStatusText == null || _cameraController == null) return;

			if (_cameraController.IsAligned)
			{
				alignmentStatusText.text  = "✓ Hazır — Fotoğraf çekebilirsiniz";
				alignmentStatusText.color = new Color(0.1f, 0.9f, 0.2f);
			}
			else
			{
				alignmentStatusText.text  = "Hedefi çerçeve içine alın";
				alignmentStatusText.color = new Color(0.9f, 0.6f, 0.1f);
			}
		}

		// ═══════════════════════════════════════════════════════
		// FOTOĞRAF ÇEKME
		// ═══════════════════════════════════════════════════════

		private void OnCaptureClicked()
		{
			Debug.Log($"[UISurveyPanel] Capture clicked. slot={_activeCameraSlot} cam={_cameraController != null}");
			if (_cameraController == null || _activeCameraSlot < 0)
			{
				Debug.LogError($"[UISurveyPanel] Capture iptal: cameraController={_cameraController != null}, activeSlot={_activeCameraSlot}");
				return;
			}

			float alignmentScore = _cameraController.AlignmentScore;
			bool  isAligned      = _cameraController.IsAligned;

			// Fotoğrafı çek
			Texture2D photo = _cameraController.CapturePhoto();

			if (photo == null)
			{
				Debug.LogError("[UISurveyPanel] Fotoğraf çekilemedi!");
				return;
			}

			// Ses
			if (_audio != null && captureSound != null)
				_audio.PlayOneShot(captureSound);

			// Analytics'e kaydet
			SurveyResultTracker.Instance?.RecordPhoto(
				_activeCameraSlot, photo, alignmentScore, isAligned);

			// Slot UI'ını güncelle (preview göster)
			if (_activeCameraSlot < _photoSlotUIs.Count)
				_photoSlotUIs[_activeCameraSlot].SetPreview(photo, isAligned);

			if (debugMode)
				Debug.Log($"[UISurveyPanel] Fotoğraf çekildi: slot {_activeCameraSlot} " +
					$"score={alignmentScore:F2} aligned={isAligned}");

			// Kamera overlay'i kapat
			CloseCameraOverlay();
		}

		// ═══════════════════════════════════════════════════════
		// TAMAMLA
		// ═══════════════════════════════════════════════════════

		private void OnCompleteClicked()
		{
			// Cevaplanmamış zorunlu soruları "yanlış" olarak işaretle
			FinalizeUnansweredQuestions();

			// Oturumu kapat (analytics)
			SurveyResultTracker.Instance?.EndSession();

			// Ses
			if (_audio != null && completeSound != null)
				_audio.PlayOneShot(completeSound);

			// Panel kapat
			gameObject.SetActive(false);

			// Tablet butonunu da kapat
			UITabletButton tabletBtn = FindObjectOfType<UITabletButton>();
			if (tabletBtn != null) tabletBtn.SetInteractable(false);

			// SequenceManager'a bildir — sekansı sonlandır
			if (SequenceManager.Instance != null)
				SequenceManager.Instance.CompleteSurveyAndAdvance();
			else
				Debug.LogWarning("[UISurveyPanel] SequenceManager.Instance null!");

			if (debugMode)
				Debug.Log($"<color=lime>[UISurveyPanel] Survey tamamlandı: {_actionID}</color>");
		}

		private void FinalizeUnansweredQuestions()
		{
			if (_data.questions == null) return;

			for (int i = 0; i < _data.questions.Count; i++)
			{
				SurveyQuestion question = _data.questions[i];
				if (!question.isRequired) continue;

				// SurveyResultTracker'da bu sorunun cevaplanıp cevaplanmadığını kontrol et
				var session = SurveyResultTracker.Instance?.GetResultByActionID(_actionID);
				if (session == null) continue;

				if (i < session.questionResults.Count && !session.questionResults[i].isAnswered)
				{
					// Cevaplanmadı → yanlış olarak kaydet
					SurveyResultTracker.Instance?.RecordAnswer(i, -1, "Cevaplanmadı", false);
				}
			}
		}

		// ═══════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════

		private IEnumerator RebuildLayout()
		{
			yield return null;
			yield return null;

			// VerticalLayoutGroup scroll'u yukarıdan başlatsın
			if (scrollRect != null)
				scrollRect.verticalNormalizedPosition = 1f;

			if (debugMode)
				Debug.Log($"[UISurveyPanel] Layout: {contentContainer?.childCount} soru, " +
					$"{_photoSlotUIs.Count} foto slotu");
		}

		public void OpenPanel()  => gameObject.SetActive(true);
		public void ClosePanel() => gameObject.SetActive(false);

		private void OnDestroy()
		{
			if (completeButton != null)    completeButton.onClick.RemoveAllListeners();
			if (captureButton != null)     captureButton.onClick.RemoveAllListeners();
			if (closeCameraButton != null) closeCameraButton.onClick.RemoveAllListeners();
		}
	}
}