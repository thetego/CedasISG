using UnityEngine;
using UnityEngine.Events;

namespace SafetyTraining
{
	/// <summary>
	/// Sahne içindeki 3D objelere eklenecek marker.
	/// Artık etkileşim logic yok, sadece:
	///   - ID ile bulunabilir
	///   - Animator tetiklenir
	///   - Events tetiklenir
	/// </summary>
	public class SceneObject : MonoBehaviour
	{
		[Header("━━━ İDENTİTY ━━━")]
		[Tooltip("Benzersiz ID → ActionData.targetObjectID ile eşleşmeli")]
		public string objectID;

		[Tooltip("Obje adı (debug için)")]
		public string objectName;

		[Header("━━━ ANİMATÖR ━━━")]
		[Tooltip("Bu objenin animator'ü (varsa)")]
		public Animator animator;

		[Header("━━━ EVENTS ━━━")]
		public UnityEvent onActivated;
		public UnityEvent onCompleted;

		[Header("━━━ DEBUG ━━━")]
		public bool showGizmo = true;

		private void OnDrawGizmos()
		{
			if (!showGizmo) return;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position, 0.3f);
		}

		private void OnDrawGizmosSelected()
		{
			if (!showGizmo) return;
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, 0.5f);

			// ID label için (Scene view'de görmek için)
			if (Gizmos.matrix != Matrix4x4.identity) return;
		}
	}
}