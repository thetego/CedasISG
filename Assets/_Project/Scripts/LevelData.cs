using UnityEngine;
using UnityEngine.Events;

namespace SafetyTraining
{
	/// <summary>
	/// Level yapısı - SequenceManager ile kullanılır
	/// </summary>
	[CreateAssetMenu(fileName = "New Level", menuName = "Safety Training/Level Data")]
	public class LevelData : ScriptableObject
	{
		[Header("━━━ LEVEL BİLGİLERİ ━━━")]
		public string levelID;
		public string levelName;

		[Header("━━━ TELEMETRİ ━━━")]
		[Tooltip("Telemetri API'sine gönderilen sabit levelId (yalnızca level-1 / level-2 / level-3 kabul edilir). " +
			"Yukarıdaki levelID alanından bağımsızdır, oyun içi mantığı etkilemez.")]
		public string telemetryLevelId = "level-1";
		[TextArea(2, 4)]
		public string levelDescription;

		[TextArea(1, 3)]
		[Tooltip("Level genel ekranında sekans seçimi sırasında gösterilecek talimat")]
		public string sequenceSelectionInstructionText = "Bir sekans seçin";

		[Header("━━━ YENİ SİSTEM: SEKANSLAR ━━━")]
		[Tooltip("Sekans bazlı sistem için (SequenceManager)")]
		public SequenceData[] sequences;

		[Header("━━━ AYARLAR ━━━")]
		[Tooltip("Zaman limiti (0 = limitsiz)")]
		[Range(0f, 3600f)]
		public float timeLimit = 0f;

		[Tooltip("Kullanıcı sekanslar arası serbestçe geçiş yapabilir mi? (sadece SequenceManager)")]
		public bool allowSequenceSwitching = true;

		[Header("━━━ SKORLAMA ━━━")]
		[Range(0, 1000)] public int maxScore = 100;
		[Range(0, 50)] public int penaltyPerMistake = 5;
		[Range(0, 100)] public int bonusForSpeed = 20;

		[Header("━━━ EVENTS ━━━")]
		public UnityEvent onLevelStart;
		public UnityEvent onLevelComplete;
		public UnityEvent onLevelFail;

		// ═══════════════════════════════════════════════════════
		// SEQUENCE METHODS (YENİ SİSTEM)
		// ═══════════════════════════════════════════════════════

		public int GetSequenceCount() => sequences?.Length ?? 0;

		public SequenceData GetSequenceAt(int index)
		{
			if (sequences == null || index < 0 || index >= sequences.Length)
				return null;
			return sequences[index];
		}

		public SequenceData GetSequenceByID(string sequenceID)
		{
			if (sequences == null || string.IsNullOrEmpty(sequenceID))
				return null;

			foreach (var seq in sequences)
			{
				if (seq != null && seq.sequenceID == sequenceID)
					return seq;
			}

			return null;
		}

	}
}
