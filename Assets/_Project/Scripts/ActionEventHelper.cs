using UnityEngine;

namespace SafetyTraining
{
	/// <summary>
	/// ActionEventHandler ile kullanılmak üzere hazır metodlar
	/// Inspector'dan kolayca atanabilir
	/// </summary>
	public class ActionEventHelper : MonoBehaviour
	{
		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = false;

		// ═══════════════════════════════════════════════════════
		// GAMEOBJECT OPERATIONS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// GameObject'i aktif eder
		/// </summary>
		public void ActivateGameObject(GameObject obj)
		{
			if (obj == null)
			{
				Debug.LogWarning("[ActionEventHelper] ActivateGameObject: GameObject is null!");
				return;
			}

			obj.SetActive(true);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Activated: {obj.name}");
		}

		/// <summary>
		/// GameObject'i deaktif eder
		/// </summary>
		public void DeactivateGameObject(GameObject obj)
		{
			if (obj == null)
			{
				Debug.LogWarning("[ActionEventHelper] DeactivateGameObject: GameObject is null!");
				return;
			}

			obj.SetActive(false);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Deactivated: {obj.name}");
		}

		/// <summary>
		/// GameObject'i toggle eder
		/// </summary>
		public void ToggleGameObject(GameObject obj)
		{
			if (obj == null)
			{
				Debug.LogWarning("[ActionEventHelper] ToggleGameObject: GameObject is null!");
				return;
			}

			obj.SetActive(!obj.activeSelf);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Toggled: {obj.name} → {obj.activeSelf}");
		}

		// ═══════════════════════════════════════════════════════
		// TRANSFORM OPERATIONS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Transform'u belirli pozisyona taşır
		/// </summary>
		public void MoveToPosition(Transform target, Vector3 position)
		{
			if (target == null)
			{
				Debug.LogWarning("[ActionEventHelper] MoveToPosition: Target is null!");
				return;
			}

			target.position = position;

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Moved {target.name} to {position}");
		}

		/// <summary>
		/// Transform'u belirli rotasyona döndürür
		/// </summary>
		public void RotateToEuler(Transform target, Vector3 eulerAngles)
		{
			if (target == null)
			{
				Debug.LogWarning("[ActionEventHelper] RotateToEuler: Target is null!");
				return;
			}

			target.eulerAngles = eulerAngles;

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Rotated {target.name} to {eulerAngles}");
		}

		/// <summary>
		/// Transform'u belirli ölçeğe ayarlar
		/// </summary>
		public void SetScale(Transform target, Vector3 scale)
		{
			if (target == null)
			{
				Debug.LogWarning("[ActionEventHelper] SetScale: Target is null!");
				return;
			}

			target.localScale = scale;

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Scaled {target.name} to {scale}");
		}

		// ═══════════════════════════════════════════════════════
		// ANIMATOR OPERATIONS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Animator trigger tetikler
		/// </summary>
		public void TriggerAnimation(Animator animator, string triggerName)
		{
			if (animator == null)
			{
				Debug.LogWarning("[ActionEventHelper] TriggerAnimation: Animator is null!");
				return;
			}

			animator.SetTrigger(triggerName);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Triggered animation: {animator.gameObject.name} → {triggerName}");
		}

		/// <summary>
		/// Animator bool set eder
		/// </summary>
		public void SetAnimatorBool(Animator animator, string paramName, bool value)
		{
			if (animator == null)
			{
				Debug.LogWarning("[ActionEventHelper] SetAnimatorBool: Animator is null!");
				return;
			}

			animator.SetBool(paramName, value);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Set bool: {animator.gameObject.name}.{paramName} = {value}");
		}

		/// <summary>
		/// Animator trigger tetikler ve hemen reset eder (takılıp kalmasını önler)
		/// </summary>
		public void TriggerAnimationAndReset(Animator animator, string triggerName)
		{
			if (animator == null)
			{
				Debug.LogWarning("[ActionEventHelper] TriggerAnimationAndReset: Animator is null!");
				return;
			}

			animator.SetTrigger(triggerName);
			animator.ResetTrigger(triggerName);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Triggered and reset: {animator.gameObject.name} → {triggerName}");
		}

		/// <summary>
		/// Animator trigger'ı reset eder
		/// </summary>
		public void ResetAnimatorTrigger(Animator animator, string triggerName)
		{
			if (animator == null)
			{
				Debug.LogWarning("[ActionEventHelper] ResetAnimatorTrigger: Animator is null!");
				return;
			}

			animator.ResetTrigger(triggerName);

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Reset trigger: {animator.gameObject.name}.{triggerName}");
		}

		// ═══════════════════════════════════════════════════════
		// AUDIO OPERATIONS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// AudioSource'u çalar
		/// </summary>
		public void PlayAudio(AudioSource audioSource)
		{
			if (audioSource == null)
			{
				Debug.LogWarning("[ActionEventHelper] PlayAudio: AudioSource is null!");
				return;
			}

			audioSource.Play();

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Playing audio: {audioSource.gameObject.name}");
		}

		/// <summary>
		/// AudioSource'u durdurur
		/// </summary>
		public void StopAudio(AudioSource audioSource)
		{
			if (audioSource == null)
			{
				Debug.LogWarning("[ActionEventHelper] StopAudio: AudioSource is null!");
				return;
			}

			audioSource.Stop();

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Stopped audio: {audioSource.gameObject.name}");
		}

		// ═══════════════════════════════════════════════════════
		// PARTICLE OPERATIONS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Particle system'i başlatır
		/// </summary>
		public void PlayParticles(ParticleSystem particles)
		{
			if (particles == null)
			{
				Debug.LogWarning("[ActionEventHelper] PlayParticles: ParticleSystem is null!");
				return;
			}

			particles.Play();

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Playing particles: {particles.gameObject.name}");
		}

		/// <summary>
		/// Particle system'i durdurur
		/// </summary>
		public void StopParticles(ParticleSystem particles)
		{
			if (particles == null)
			{
				Debug.LogWarning("[ActionEventHelper] StopParticles: ParticleSystem is null!");
				return;
			}

			particles.Stop();

			if (debugMode)
				Debug.Log($"[ActionEventHelper] ✓ Stopped particles: {particles.gameObject.name}");
		}

		// ═══════════════════════════════════════════════════════
		// REGISTRY OPERATIONS (SceneObjectRegistry ile)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// ID'ye göre GameObject'i aktif eder
		/// </summary>
		public void ActivateByID(string objectID)
		{
			if (string.IsNullOrEmpty(objectID))
			{
				Debug.LogWarning("[ActionEventHelper] ActivateByID: objectID is empty!");
				return;
			}

			GameObject obj = SceneObjectRegistry.Instance?.GetGameObjectByID(objectID);
			if (obj != null)
			{
				obj.SetActive(true);

				if (debugMode)
					Debug.Log($"[ActionEventHelper] ✓ Activated by ID: {objectID}");
			}
			else
			{
				Debug.LogWarning($"[ActionEventHelper] ActivateByID: GameObject '{objectID}' not found!");
			}
		}

		/// <summary>
		/// ID'ye göre GameObject'i deaktif eder
		/// </summary>
		public void DeactivateByID(string objectID)
		{
			if (string.IsNullOrEmpty(objectID))
			{
				Debug.LogWarning("[ActionEventHelper] DeactivateByID: objectID is empty!");
				return;
			}

			GameObject obj = SceneObjectRegistry.Instance?.GetGameObjectByID(objectID);
			if (obj != null)
			{
				obj.SetActive(false);

				if (debugMode)
					Debug.Log($"[ActionEventHelper] ✓ Deactivated by ID: {objectID}");
			}
			else
			{
				Debug.LogWarning($"[ActionEventHelper] DeactivateByID: GameObject '{objectID}' not found!");
			}
		}

		// ═══════════════════════════════════════════════════════
		// DEBUG HELPERS
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Console'a log basar
		/// </summary>
		public void LogMessage(string message)
		{
			Debug.Log($"[ActionEventHelper] {message}");
		}

		/// <summary>
		/// Console'a warning basar
		/// </summary>
		public void LogWarning(string message)
		{
			Debug.LogWarning($"[ActionEventHelper] {message}");
		}
	}
}