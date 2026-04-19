using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining
{
	/// <summary>
	/// Ana kamerayı kopyalayan ayrı bir Camera ile termal görüntü efekti üretir.
	/// Bu bileşen, ThermalEffect shader'ı kullanan bir Material ve bir RawImage gerektirir.
	///
	/// KURULUM:
	///  1. Sahnede boş bir GameObject oluştur ("ThermalCamera") ve bu scripti ekle.
	///  2. Aynı GameObject'e bir Camera component ekle (Inspector'da otomatik eklenir).
	///  3. displayTarget alanına overlay panelindeki RawImage'ı sürükle.
	///  4. thermalMaterial alanına Custom/ThermalEffect shader'ı kullanan bir Material ata.
	///  5. SurveyCameraController → thermalRenderer alanına bu GameObject'i sürükle.
	/// </summary>
	[RequireComponent(typeof(Camera))]
	public class ThermalCameraRenderer : MonoBehaviour
	{
		// ═══════════════════════════════════════════════════════
		// INSPECTOR
		// ═══════════════════════════════════════════════════════

		[Header("━━━ BAĞLANTILAR ━━━")]
		[Tooltip("Termal görüntünün gösterileceği RawImage (overlay panelinde)")]
		public RawImage displayTarget;

		[Tooltip("Custom/ThermalEffect shader kullanan materyal")]
		public Material thermalMaterial;

		[Header("━━━ TERMAL PARAMETRELER ━━━")]
		[Range(0f, 1f)]
		[Tooltip("Termal efekt yoğunluğu (0 = orijinal, 1 = tam termal)")]
		public float intensity    = 1f;

		[Range(0.1f, 3f)]
		[Tooltip("Kontrast çarpanı — yüksek değer sıcak/soğuk bölgeleri belirginleştirir")]
		public float contrast     = 1.2f;

		[Range(0f, 0.05f)]
		[Tooltip("Termal sensör gürültüsü miktarı")]
		public float noiseStrength = 0.008f;

		// ═══════════════════════════════════════════════════════
		// RUNTIME
		// ═══════════════════════════════════════════════════════

		private Camera        _cam;
		private RenderTexture _rt;
		private Material      _matInstance;

		// ═══════════════════════════════════════════════════════
		// UNITY
		// ═══════════════════════════════════════════════════════

		private void Awake()
		{
			_cam = GetComponent<Camera>();
			_cam.enabled = false;

			if (thermalMaterial != null)
				_matInstance = new Material(thermalMaterial);
		}

		private void OnEnable()
		{
			Camera main = Camera.main;

			// Ana kamera ayarlarını kopyala
			if (main != null)
			{
				_cam.cullingMask  = main.cullingMask & ~(1 << LayerMask.NameToLayer("UI"));
				_cam.clearFlags   = main.clearFlags;
				_cam.backgroundColor = main.backgroundColor;
				_cam.nearClipPlane = main.nearClipPlane;
				_cam.farClipPlane  = main.farClipPlane;
				_cam.fieldOfView   = main.fieldOfView;
			}

			// RenderTexture oluştur
			_rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
			_cam.targetTexture = _rt;
			_cam.enabled = true;

			// RawImage'ı ayarla
			if (displayTarget != null && _matInstance != null)
			{
				displayTarget.texture  = _rt;
				displayTarget.material = _matInstance;
				displayTarget.gameObject.SetActive(true);
			}
		}

		private void OnDisable()
		{
			_cam.enabled       = false;
			_cam.targetTexture = null;

			if (displayTarget != null)
			{
				displayTarget.texture  = null;
				displayTarget.material = null;
				displayTarget.gameObject.SetActive(false);
			}

			if (_rt != null)
			{
				_rt.Release();
				Destroy(_rt);
				_rt = null;
			}
		}

		private void OnDestroy()
		{
			if (_matInstance != null)
				Destroy(_matInstance);
		}

		// LateUpdate: Cinemachine kamerasını güncellendikten sonra çalışır
		private void LateUpdate()
		{
			SyncWithMainCamera();
			UpdateMaterialParams();
		}

		// ═══════════════════════════════════════════════════════
		// PUBLIC API
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Termal efekt uygulanmış fotoğrafı Texture2D olarak döndürür.
		/// SurveyCameraController.CapturePhoto() bunun üzerinden çalışır.
		/// </summary>
		public Texture2D CaptureWithThermal()
		{
			if (_rt == null || _matInstance == null)
			{
				Debug.LogError("[ThermalCameraRenderer] CaptureWithThermal: RT veya materyal null!");
				return null;
			}

			RenderTexture temp = RenderTexture.GetTemporary(
				_rt.width, _rt.height, 0, RenderTextureFormat.ARGB32);

			Graphics.Blit(_rt, temp, _matInstance);

			RenderTexture.active = temp;
			Texture2D photo = new Texture2D(temp.width, temp.height, TextureFormat.RGB24, false);
			photo.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
			photo.Apply();
			RenderTexture.active = null;

			RenderTexture.ReleaseTemporary(temp);
			return photo;
		}

		// ═══════════════════════════════════════════════════════
		// YARDİMCI
		// ═══════════════════════════════════════════════════════

		private void SyncWithMainCamera()
		{
			Camera main = Camera.main;
			if (main == null) return;
			transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
			_cam.fieldOfView   = main.fieldOfView;
			_cam.nearClipPlane = main.nearClipPlane;
			_cam.farClipPlane  = main.farClipPlane;
		}

		private void UpdateMaterialParams()
		{
			if (_matInstance == null) return;
			_matInstance.SetFloat("_Intensity",     intensity);
			_matInstance.SetFloat("_Contrast",      contrast);
			_matInstance.SetFloat("_NoiseStrength", noiseStrength);
		}
	}
}
