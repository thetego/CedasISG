using UnityEngine;
using System.Collections.Generic;

namespace SafetyTraining
{
	/// <summary>
	/// Level'a ait QRCodeData asset'lerini kayıt eder ve okunan QR metniyle bulunmasını sağlar.
	/// </summary>
	public class QRCodeRegistry : MonoBehaviour
	{
		public static QRCodeRegistry Instance { get; private set; }

		[Header("━━━ QR KOD LİSTESİ ━━━")]
		[Tooltip("Bu level'da tanınacak QR kodları (Inspector'dan ata)")]
		public List<QRCodeData> qrCodeDataList = new List<QRCodeData>();

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		// qrCodeID → QRCodeData mapping
		private Dictionary<string, QRCodeData> _registry = new Dictionary<string, QRCodeData>();

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

			BuildRegistry();
		}

		/// <summary>
		/// qrCodeDataList'i Dictionary'ye derler
		/// </summary>
		public void BuildRegistry()
		{
			_registry.Clear();

			foreach (var data in qrCodeDataList)
			{
				if (data == null)
					continue;

				if (string.IsNullOrEmpty(data.qrCodeID))
				{
					Debug.LogWarning($"[QRCodeRegistry] QRCodeData '{data.name}' has empty qrCodeID!", data);
					continue;
				}

				if (_registry.ContainsKey(data.qrCodeID))
				{
					Debug.LogError($"[QRCodeRegistry] Duplicate qrCodeID '{data.qrCodeID}' found! " +
								  $"Assets: '{_registry[data.qrCodeID].name}' and '{data.name}'");
					continue;
				}

				_registry[data.qrCodeID] = data;
			}

			if (debugMode)
			{
				Debug.Log($"<color=cyan>[QRCodeRegistry] ✓ Registered {_registry.Count} QR codes</color>");

				foreach (var kvp in _registry)
				{
					Debug.Log($"  • '{kvp.Key}' → {kvp.Value.name}");
				}
			}
		}

		/// <summary>
		/// Okunan QR metniyle QRCodeData bulur
		/// </summary>
		public QRCodeData GetByID(string qrCodeID)
		{
			if (string.IsNullOrEmpty(qrCodeID))
				return null;

			if (_registry.TryGetValue(qrCodeID, out QRCodeData data))
			{
				return data;
			}

			if (debugMode)
			{
				Debug.LogWarning($"[QRCodeRegistry] QR code with ID '{qrCodeID}' not found in registry!");
			}

			return null;
		}

		/// <summary>
		/// ID'nin kayıtlı olup olmadığını kontrol eder
		/// </summary>
		public bool IsRegistered(string qrCodeID)
		{
			return !string.IsNullOrEmpty(qrCodeID) && _registry.ContainsKey(qrCodeID);
		}
	}
}
