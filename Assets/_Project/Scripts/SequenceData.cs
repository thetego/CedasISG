using UnityEngine;
using UnityEngine.Events;

namespace SafetyTraining
{
	/// <summary>
	/// Bir sekansı temsil eder (Ekipman Giyme, Trafo İşlemleri vb.)
	/// Her sekans kendi action'larını ve kamera ayarlarını içerir
	/// </summary>
	[CreateAssetMenu(fileName = "New Sequence", menuName = "Safety Training/Sequence Data")]
	public class SequenceData : ScriptableObject
	{
		[Header("━━━ SEKANS BİLGİLERİ ━━━")]
		[Tooltip("Benzersiz sekans ID")]
		public string sequenceID;

		[Tooltip("Sekans adı (UI'da görünecek)")]
		public string sequenceName;

		[TextArea(2, 4)]
		[Tooltip("Sekans açıklaması")]
		public string sequenceDescription;

		[TextArea(2, 4)]
		[Tooltip("Sekans ekranında gösterilecek talimat metni")]
		public string instructionText;

		[Header("━━━ AKSİYONLAR ━━━")]
		[Tooltip("Bu sekansın action'ları (sırayla)")]
		public ActionData[] actions;

		[Header("━━━ SAHNE REFERANSLARI ━━━")]
		[Tooltip("Sekans butonu hangi objeyi takip edecek? (boşsa genel UI'da gösterilir)")]
		public string targetObjectID;

		[Tooltip("Buton için offset")]
		public Vector3 buttonOffset = Vector3.up * 1.5f;

		[Header("━━━ KAMERA AYARLARI ━━━")]
		[Tooltip("Sekansa girildiğinde hangi kameraya geçilecek?")]
		public CameraMode entryCameraMode = CameraMode.Keep;

		[Tooltip("Entry kamera ID'si (ObjectClose/Custom modları için)")]
		public string entryCameraID;

		[Tooltip("Sekans tamamlandığında otomatik kamera geri dönsün mü?")]
		public bool autoReturnOnComplete = true;

		[Tooltip("Sekans iptal edildiğinde (geri dönülünce) otomatik kamera geri dönsün mü?")]
		public bool autoReturnOnExit = true;

		[Header("━━━ KALDIM YER ━━━")]
		[Tooltip("Kullanıcı sekansa tekrar girdiğinde kaldığı yerden devam etsin mi?")]
		public bool resumeFromLastAction = true;

		[Tooltip("Tamamlanmış sekansa tekrar girilebilir mi?")]
		public bool allowRestart = false;

		[Tooltip("Tamamlandıktan sonra buton tamamen gizlensin mi? (destroy/deactivate)")]
		public bool hideButtonAfterComplete = false;

		[Header("━━━ ÖNKOŞULLAR ━━━")]
		[Tooltip("Bu sekansa girmek için tamamlanması gereken diğer sekanslar")]
		public SequenceData[] prerequisiteSequences;

		[Tooltip("Önkoşul sağlanmadığında davranış")]
		public PrerequisiteFailAction onPrerequisiteFail = PrerequisiteFailAction.Warning;

		[Tooltip("Önkoşul hatası mesajı")]
		[TextArea(2, 3)]
		public string prerequisiteFailMessage = "Bu sekansa girmek için önce diğer sekansları tamamlayın.";

		[Header("━━━ SURVEY / TABLET ━━━")]
		[Tooltip("Bu sekans için tablet survey paneli var mı?")]
		public bool hasSurvey = false;

		[Tooltip("Survey panel prefabı (UISurveyPanel component'li)")]
		public GameObject surveyPanelPrefab;

		[Tooltip("Survey veri tanımı")]
		public SurveyActionData surveyData;

		[Header("━━━ UI AYARLARI ━━━")]
		[Tooltip("Level başlangıcında buton spawn edilsin mi? (false ise programmatically spawn edilmeli)")]
		public bool spawnButtonAtStart = true;

		[Tooltip("Sekans butonu ikonu")]
		public Sprite sequenceIcon;

		[Tooltip("Buton rengi (tamamlanmamış)")]
		public Color buttonColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

		[Tooltip("Buton rengi (tamamlanmış)")]
		public Color completedColor = new Color(0.2f, 0.6f, 1f, 0.9f);

		[Header("━━━ EVENTS ━━━")]
		public UnityEvent onSequenceEnter;
		public UnityEvent onSequenceComplete;
		public UnityEvent onSequenceExit;

		// ═══════════════════════════════════════════════════════
		// HELPER METHODS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Toplam action sayısı
		/// </summary>
		public int GetTotalActionCount() => actions?.Length ?? 0;

		/// <summary>
		/// Belirli index'teki action'ı döndürür
		/// </summary>
		public ActionData GetActionAt(int index)
		{
			if (actions == null || index < 0 || index >= actions.Length)
				return null;
			return actions[index];
		}

		/// <summary>
		/// Action ID'sine göre index bulur
		/// </summary>
		public int GetActionIndex(string actionID)
		{
			if (actions == null || string.IsNullOrEmpty(actionID))
				return -1;

			for (int i = 0; i < actions.Length; i++)
			{
				if (actions[i] != null && actions[i].actionID == actionID)
					return i;
			}

			return -1;
		}
	}
}
