using UnityEngine;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Sahnedeki tüm SceneObject'leri kayıt eder ve ID ile bulunmasını sağlar.
	/// Tag kullanmadan obje bulma sistemi.
	/// </summary>
	public class SceneObjectRegistry : MonoBehaviour
	{
		public static SceneObjectRegistry Instance { get; private set; }

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// ID → SceneObject mapping
		private Dictionary<string, SceneObject> _registry = new Dictionary<string, SceneObject>();

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			else
			{
				Destroy(gameObject);
				return;
			}



			RegisterAllSceneObjects();
		}

		private void Start()
		{
			
		}

		/// <summary>
		/// Sahnedeki tüm SceneObject'leri bulur ve kayıt eder
		/// </summary>
		public void RegisterAllSceneObjects()
		{
			_registry.Clear();

			SceneObject[] allObjects = FindObjectsOfType<SceneObject>(true); // true = include inactive

			foreach (var obj in allObjects)
			{
				if (string.IsNullOrEmpty(obj.objectID))
				{
					Debug.LogWarning($"[SceneObjectRegistry] SceneObject on '{obj.gameObject.name}' has empty objectID!", obj);
					continue;
				}

				if (_registry.ContainsKey(obj.objectID))
				{
					Debug.LogError($"[SceneObjectRegistry] Duplicate objectID '{obj.objectID}' found! " +
								  $"Objects: '{_registry[obj.objectID].gameObject.name}' and '{obj.gameObject.name}'");
					continue;
				}

				_registry[obj.objectID] = obj;
			}

			if (debugMode)
			{
				Debug.Log($"<color=cyan>[SceneObjectRegistry] ✓ Registered {_registry.Count} scene objects</color>");

				// Liste yazdır
				foreach (var kvp in _registry)
				{
					Debug.Log($"  • '{kvp.Key}' → {kvp.Value.gameObject.name}");
				}
			}
		}

		/// <summary>
		/// ID ile SceneObject bulur
		/// </summary>
		public SceneObject GetObjectByID(string objectID)
		{
			if (string.IsNullOrEmpty(objectID))
			{
				Debug.LogWarning("[SceneObjectRegistry] GetObjectByID called with empty ID!");
				return null;
			}

			if (_registry.TryGetValue(objectID, out SceneObject obj))
			{
				return obj;
			}

			if (debugMode)
			{
				Debug.LogWarning($"[SceneObjectRegistry] Object with ID '{objectID}' not found!");
			}

			return null;
		}

		/// <summary>
		/// ID ile GameObject bulur (kısayol)
		/// </summary>
		public GameObject GetGameObjectByID(string objectID)
		{
			SceneObject obj = GetObjectByID(objectID);
			return obj != null ? obj.gameObject : null;
		}

		/// <summary>
		/// ID ile Transform bulur (kısayol)
		/// </summary>
		public Transform GetTransformByID(string objectID)
		{
			SceneObject obj = GetObjectByID(objectID);
			return obj != null ? obj.transform : null;
		}

		/// <summary>
		/// Bir obje runtime'da kayıt edilmek istenirse
		/// </summary>
		public void RegisterObject(SceneObject obj)
		{
			if (obj == null || string.IsNullOrEmpty(obj.objectID))
				return;

			if (_registry.ContainsKey(obj.objectID))
			{
				Debug.LogWarning($"[SceneObjectRegistry] Object '{obj.objectID}' already registered!");
				return;
			}

			_registry[obj.objectID] = obj;

			if (debugMode)
				Debug.Log($"[SceneObjectRegistry] ✓ Registered '{obj.objectID}'");
		}

		/// <summary>
		/// Bir objenin kaydını siler
		/// </summary>
		public void UnregisterObject(string objectID)
		{
			if (_registry.Remove(objectID))
			{
				if (debugMode)
					Debug.Log($"[SceneObjectRegistry] Unregistered '{objectID}'");
			}
		}

		/// <summary>
		/// Tüm kayıtlı obje ID'lerini döndürür
		/// </summary>
		public List<string> GetAllObjectIDs()
		{
			return new List<string>(_registry.Keys);
		}

		/// <summary>
		/// ID'nin kayıtlı olup olmadığını kontrol eder
		/// </summary>
		public bool IsRegistered(string objectID)
		{
			return _registry.ContainsKey(objectID);
		}
	}
}