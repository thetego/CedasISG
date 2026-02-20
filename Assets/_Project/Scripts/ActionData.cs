using SafetyTraining;
using UnityEngine.Events;
using UnityEngine;

namespace SafetyTraining
{
	public enum ActionType
	{
		WearEquipment,   // Ekipman giyme → Drag item → Drop zone (sabit panel)
		OpenClose,       // Kapı/kapak → UI buton (world to screen)
		DragToWorld,     // Tool sürükle → Drop zone (world to screen)
		Click,           // Basit tıklama → UI buton (world to screen)
		CameraMove,      // Otomatik kamera geçiş
		PanelInteraction // Panel aç → Panel içinde buton tıkla → Action tamamlanır
	}

	public enum PrerequisiteFailAction
	{
		Block,      // Butonu disabled yap, tıklanmaz
		Warning,    // Uyarı göster ama devam edilebilir
		GameOver    // Kritik hata, oyun biter
	}

	public enum CameraMode
	{
		Keep,           // Mevcut kamerayı koru
		General,        // Genel sahne kamerası
		Character,      // Karakter kamerası
		ObjectClose,    // Obje yakın plan
		Custom          // Özel virtual camera (virtualCameraID ile)
	}

	public enum AnimatorParameterType { Trigger, Bool, Float, Int }
	public enum AnimationTiming { OnStart, OnComplete, OnFail }

	[System.Serializable]
	public class AnimationTriggerData
	{
		public string animatorObjectID;
		public string triggerName;
		public AnimatorParameterType parameterType = AnimatorParameterType.Trigger;
		public float parameterValue = 1f;
		public AnimationTiming timing = AnimationTiming.OnComplete;

		[Tooltip("Trigger tipi için: Set edildikten sonra otomatik reset et (önerilir)")]
		public bool autoResetTrigger = true;
	}

	[CreateAssetMenu(fileName = "New Action", menuName = "Safety Training/Action Data")]
	public class ActionData : ScriptableObject
	{
		[Header("━━━ GENEL ━━━")]
		public string actionID;
		public string actionName;
		[TextArea(2, 5)]
		public string instructionText;
		public ActionType actionType;

		[Header("━━━ PREREQUISITE SYSTEM ━━━")]
		[Tooltip("Bu actiondan önce tamamlanması gereken action ID'leri")]
		public string[] prerequisiteActionIDs;

		[Tooltip("Prerequisite sağlanmadığında ne olacak?")]
		public PrerequisiteFailAction onPrerequisiteFail = PrerequisiteFailAction.Block;

		[Tooltip("Prerequisite hatası mesajı")]
		[TextArea(2, 4)]
		public string prerequisiteFailMessage = "Bu aksiyonu yapmak için önce gerekli adımları tamamlamalısınız.";

		[Header("━━━ TAMAMLANMA AYARLARI ━━━")]
		[Tooltip("Action tamamlandıktan sonra bekleme süresi")]
		[Range(0f, 10f)]
		public float completionDelay = 0f;

		[Tooltip("Delay sonunda otomatik devam et")]
		public bool autoCompleteAfterDelay = false;

		[Header("━━━ CAMERA SETTINGS ━━━")]
		[Tooltip("Action için kamera modu")]
		public CameraMode cameraMode = CameraMode.Keep;

		[Tooltip("Özel kamera ID'si (Custom veya ObjectClose modunda)")]
		public string virtualCameraID;

		[Tooltip("Action tamamlandığında otomatik kamera geri dönsün mü?")]
		public bool autoReturnCameraOnComplete = false;

		[Header("━━━ KAMERA HAREKETİ (CameraMove için - DEPRECATED) ━━━")]
		[Tooltip("Kameranın gideceği hedef objenin Tag/Name'i")]
		public string cameraTargetID;

		[Tooltip("Kamera hareket hızı")]
		[Range(0.5f, 5f)]
		public float cameraMoveSpeed = 2f;

		[Tooltip("Kamera hareketinden sonra otomatik tamamlansın mı?")]
		public bool autoCompleteAfterCamera = true;

		[Header("━━━ KOŞULLAR (DEPRECATED) ━━━")]
		[Tooltip("Önceki action tamamlanmadan bu başlamasın mı?")]
		public bool requiresPreviousAction = true;

		[Tooltip("Bu actiondan önce tamamlanması gereken actionlar (deprecated)")]
		public ActionData[] prerequisiteActions;

		[Header("━━━ HEDEF OBJE (Sahne) ━━━")]
		[Tooltip("Sahnedeki 3D objenin ID'si. UI otomatik burada belirir.")]
		public string targetObjectID;

		// ───────────────────────────────────────
		// WearEquipment
		// ───────────────────────────────────────
		[Header("━━━ EKIPMAN (WearEquipment) ━━━")]
		[Tooltip("Giyilmesi gereken ekipmanlar (sıra önemsiz, hepsi giyilmeli)")]
		public EquipmentData[] requiredEquipments;

		[Tooltip("Yanlış ekipmanlar (inventory'de görünür ama drop zone'u yok)")]
		public EquipmentData[] distractorEquipments;

		// ───────────────────────────────────────
		// DragToWorld
		// ───────────────────────────────────────
		[Header("━━━ DRAG TO WORLD ━━━")]
		[Tooltip("Kullanılacak tool'lar (WearEquipment mantığıyla aynı, hepsi drop edilmeli)")]
		public ToolData[] requiredTools;

		[Tooltip("Yanlış tool'lar (inventory'de görünür ama drop zone'u yok)")]
		public ToolData[] distractorTools;

		[Tooltip("Tool başına özel drop location (boşsa tool.dropTargetObjectID kullanılır)")]
		public ToolDropMapping[] toolDropMappings;

		[Tooltip("Drop zone boyutu (piksel)")]
		public Vector2 dropZoneSize = new Vector2(120, 120);

		// ───────────────────────────────────────
		// PanelInteraction
		// ───────────────────────────────────────
		[Header("━━━ PANEL INTERACTION ━━━")]
		[Tooltip("Açılacak panel prefab'ı")]
		public GameObject interactionPanelPrefab;

		[Tooltip("Panel ID (runtime tracking için - boş bırakılabilir)")]
		public string panelID;

		// ───────────────────────────────────────
		// UI BEHAVIOR
		// ───────────────────────────────────────
		[Header("━━━ UI BEHAVIOR ━━━")]
		[Tooltip("Tamamlandıktan sonra butonu gizle")]
		public bool hideButtonAfterComplete = false;

		// ───────────────────────────────────────
		// ANİMASYONLAR & EFEKTLER
		// ───────────────────────────────────────
		[Header("━━━ ANİMASYONLAR ━━━")]
		public AnimationTriggerData[] animationTriggers;

		[Header("━━━ EFEKTLER ━━━")]
		public AudioClip soundClip;
		public string particleEffectID;
		public Vector3 particleSpawnOffset;

		[Header("━━━ EVENTS ━━━")]
		public UnityEvent onActionStart;
		public UnityEvent onActionComplete;
		public UnityEvent onActionFail;

		[Header("━━━ SCENE EVENT IDS (ActionEventHandler) ━━━")]
		[Tooltip("OnStart'ta tetiklenecek event ID'leri (ActionEventHandler'dan)")]
		public string[] onStartEventIDs;

		[Tooltip("OnComplete'te tetiklenecek event ID'leri (ActionEventHandler'dan)")]
		public string[] onCompleteEventIDs;

		[Tooltip("OnFail'de tetiklenecek event ID'leri (ActionEventHandler'dan)")]
		public string[] onFailEventIDs;

		public AnimationTriggerData[] GetAnimationsForTiming(AnimationTiming timing)
		{
			if (animationTriggers == null) return new AnimationTriggerData[0];
			return System.Array.FindAll(animationTriggers, a => a.timing == timing);
		}
	}

	/// <summary>
	/// Tool ID ve drop location eşleştirmesi (ActionData içinde kullanılır)
	/// </summary>
	[System.Serializable]
	public class ToolDropMapping
	{
		[Tooltip("Tool ID (requiredTools array'indeki tool'un ID'si)")]
		public string toolID;

		[Tooltip("Bu tool'un bırakılacağı sahne objesinin ID'si")]
		public string dropTargetObjectID;
	}
}