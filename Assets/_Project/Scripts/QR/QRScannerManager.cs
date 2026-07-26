using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace SafetyTraining
{
	/// <summary>
	/// Cihaz kamerasını açar, canlı görüntüyü bir RawImage'a basar ve periyodik olarak QR kod arar.
	/// QR bulununca QRCodeRegistry'den karşılığını bulup bir UIQRPopupPanel prefabını Instantiate eder,
	/// popup QR ekranda hareket ettikçe onu takip eder; QR görüntüden kaybolunca popup Destroy edilir.
	/// </summary>
	public class QRScannerManager : MonoBehaviour
	{
		public static QRScannerManager Instance { get; private set; }

		[Header("━━━ REFERANSLAR ━━━")]
		[Tooltip("Canlı kamera görüntüsünün basılacağı RawImage")]
		public RawImage cameraDisplayImage;

		[Tooltip("Kamera en-boy oranını görüntüye uydurmak için (opsiyonel)")]
		public AspectRatioFitter aspectRatioFitter;

		[Tooltip("QR bulununca Instantiate edilecek popup prefabı")]
		public UIQRPopupPanel qrPopupPrefab;

		[Tooltip("Popup'ların Instantiate edileceği RectTransform (RawImage ile aynı Canvas altında, stretch)")]
		public RectTransform popupContainer;

		[Header("━━━ TARAMA AYARLARI ━━━")]
		[Tooltip("Her kaç saniyede bir kare taransın (performans için her frame taranmaz)")]
		public float scanInterval = 0.15f;

		[Tooltip("Zor okunabilen QR'larda daha fazla deneme yapar, biraz daha yavaştır")]
		public bool tryHarder = true;

		[Range(0.2f, 1f)]
		[Tooltip("Sadece kare merkezindeki bu orandaki alan taranır (ör. 0.55 = %55). " +
				 "Küçültmek taramayı hem hızlandırır hem de daha sık aralıkla taramaya izin verir.")]
		public float scanRegionScale = 0.55f;

		[Tooltip("Talep edilen kamera çözünürlüğü")]
		public int requestedWidth = 1280;
		public int requestedHeight = 720;

		[Tooltip("Talep edilen kamera FPS'i (belirtilmezse cihaz düşük bir varsayılan seçebilir)")]
		public int requestedFPS = 30;

		[Tooltip("QR birkaç ardışık taramada bulunamazsa popup bu kadar saniye sonra kaybolur (titremeyi önler)")]
		public float loseTrackingTimeout = 1.2f;

		[Tooltip("Kameranın odağını bu kadar saniyede bir yeniden tetikler (bazı cihazlarda odak kilitlenip yakın QR'a netlik sağlanamıyor)")]
		public float autoFocusInterval = 1.5f;

		[Header("━━━ DEBUG ━━━")]
		public bool debugMode = true;

		private WebCamTexture _webCamTexture;
		private BarcodeReaderGeneric _barcodeReader;
		private byte[] _frameBuffer;
		private Coroutine _scanRoutine;

		// Tarama bölgesi (ROI) — tam kare içinde ortalanmış, full-frame piksel koordinatlarında (bottom-up)
		private int _roiX, _roiY, _roiWidth, _roiHeight;

		// GetPixels32 için yeniden kullanılan buffer — her karede yeni dizi allocate etmemek için
		private Color32[] _pixelBuffer;

		// Crop+decode işi arka plan thread'inde yapılır; ana thread sadece sonucu tüketir
		private readonly object _resultLock = new object();
		private volatile bool _isDecoding;
		private bool _hasPendingResult;
		private Result _pendingResult;
		private int _pendingFullHeight;

		private UIQRPopupPanel _activePopup;
		private string _activeQRID;
		private string _dismissedQRID;
		private float _lastSeenTime;

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

			// Bu ZXing.Net build'i (netstandard2.0) Color32 tabanlı bir BarcodeReader sunmuyor;
			// somut BarcodeReaderGeneric sınıfı ham RGBA byte dizisinden decode ediyor.
			_barcodeReader = new BarcodeReaderGeneric
			{
				AutoRotate = true,
				Options = new DecodingOptions
				{
					TryHarder = tryHarder,
					PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
				}
			};
		}

		private void OnEnable()
		{
			StartScanning();
		}

		private void OnDisable()
		{
			StopScanning();
		}

		/// <summary>
		/// Cihaz kamerasını açar ve tarama coroutine'ini başlatır
		/// </summary>
		public void StartScanning()
		{
			if (_webCamTexture != null && _webCamTexture.isPlaying)
				return;

#if UNITY_ANDROID
			if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
			{
				RequestCameraPermission();
				return;
			}
#endif

			if (WebCamTexture.devices.Length == 0)
			{
				Debug.LogError("[QRScannerManager] Cihazda kamera bulunamadı!");
				return;
			}

			string deviceName = GetBackCameraName();
			_webCamTexture = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFPS);
			_webCamTexture.Play();

			if (cameraDisplayImage != null)
			{
				cameraDisplayImage.texture = _webCamTexture;
			}

			_scanRoutine = StartCoroutine(ScanLoop());

			if (debugMode)
				Debug.Log($"<color=cyan>[QRScannerManager] Kamera başlatıldı: {deviceName}</color>");
		}

		/// <summary>
		/// Kamerayı ve taramayı durdurur
		/// </summary>
		public void StopScanning()
		{
			if (_scanRoutine != null)
			{
				StopCoroutine(_scanRoutine);
				_scanRoutine = null;
			}

			if (_webCamTexture != null)
			{
				_webCamTexture.Stop();
				_webCamTexture = null;
			}

			if (cameraDisplayImage != null)
			{
				cameraDisplayImage.texture = null;
			}

			DestroyActivePopup();
			_dismissedQRID = null;

			lock (_resultLock)
			{
				_hasPendingResult = false;
			}
		}

		/// <summary>
		/// Popup'ın kendi kapat butonu tarafından çağrılır — QR aynı karede kalmaya devam etse
		/// bile QR değişene/görüntüden çıkıp tekrar girene kadar aynı ID tekrar açılmaz.
		/// </summary>
		public void NotifyPopupDismissed(string qrCodeID)
		{
			_dismissedQRID = qrCodeID;

			if (_activeQRID == qrCodeID)
			{
				_activePopup = null;
				_activeQRID = null;
			}
		}

#if UNITY_ANDROID
		/// <summary>
		/// Android 6+ runtime kamera izni ister; kullanıcı izin verirse taramayı otomatik başlatır.
		/// </summary>
		private void RequestCameraPermission()
		{
			var callbacks = new PermissionCallbacks();
			callbacks.PermissionGranted += _ => StartScanning();
			callbacks.PermissionDenied += _ =>
				Debug.LogError("[QRScannerManager] Kamera izni reddedildi, QR tarama başlatılamıyor!");
			callbacks.PermissionDeniedAndDontAskAgain += _ =>
				Debug.LogError("[QRScannerManager] Kamera izni kalıcı olarak reddedildi (Ayarlar'dan verilmeli)!");

			Permission.RequestUserPermission(Permission.Camera, callbacks);
		}
#endif

		private string GetBackCameraName()
		{
			foreach (var device in WebCamTexture.devices)
			{
				if (!device.isFrontFacing)
					return device.name;
			}

			// Arka kamera bulunamazsa ilk cihazı kullan (ör. Editor'da PC webcam)
			return WebCamTexture.devices[0].name;
		}

		private IEnumerator ScanLoop()
		{
			// Kamera boyutlarının netleşmesini bekle (ilk birkaç frame genelde 16x16 placeholder döner)
			while (_webCamTexture != null && _webCamTexture.width <= 16)
			{
				yield return null;
			}

			ApplyDisplayOrientation();
			ComputeScanRegion();

			var wait = new WaitForSeconds(scanInterval);
			float nextAutoFocusTime = Time.time + autoFocusInterval;

			while (true)
			{
				if (_webCamTexture != null && _webCamTexture.isPlaying)
				{
					ConsumePendingDecodeResult();
					TryDecodeCurrentFrame();

					if (autoFocusInterval > 0f && Time.time >= nextAutoFocusTime)
					{
						NudgeAutoFocus();
						nextAutoFocusTime = Time.time + autoFocusInterval;
					}
				}

				yield return wait;
			}
		}

		private bool _autoFocusToggle;

		/// <summary>
		/// Kamerayı yeniden odaklanmaya zorlar. Bazı Android cihazlarda WebCamTexture ilk odaktan
		/// sonra kilitleniyor ve yakın mesafedeki QR'a bir daha net bakamıyor; bu periyodik
		/// yeniden tetikleme odağın QR'ın bulunduğu mesafeye takip etmesini sağlar.
		/// Set edilen değer her seferinde ufak bir miktar değiştirilir, çünkü bazı implementasyonlar
		/// aynı değer tekrar atandığında yeni bir odaklanma tetiklemeyebiliyor.
		/// </summary>
		private void NudgeAutoFocus()
		{
			try
			{
				_autoFocusToggle = !_autoFocusToggle;
				float offset = _autoFocusToggle ? 0.001f : -0.001f;
				_webCamTexture.autoFocusPoint = new Vector2(0.5f + offset, 0.5f + offset);
			}
			catch (NotSupportedException)
			{
				autoFocusInterval = 0f; // Cihaz desteklemiyor, tekrar denemeye gerek yok
			}
		}

		private void ApplyDisplayOrientation()
		{
			if (cameraDisplayImage == null || _webCamTexture == null)
				return;

			var rectTransform = cameraDisplayImage.rectTransform;
			rectTransform.localEulerAngles = new Vector3(0f, 0f, -_webCamTexture.videoRotationAngle);

			float scaleY = _webCamTexture.videoVerticallyMirrored ? -1f : 1f;
			rectTransform.localScale = new Vector3(rectTransform.localScale.x, scaleY, 1f);

			if (aspectRatioFitter != null)
			{
				aspectRatioFitter.aspectRatio = (float)_webCamTexture.width / _webCamTexture.height;
			}
		}

		/// <summary>
		/// Ekranın ortasında, tam kare boyutunun scanRegionScale kadarını kaplayan bir tarama
		/// bölgesi (ROI) hesaplar. Sadece bu bölge decode edilir — hem çok daha hızlı olur
		/// hem de daha sık tarama (küçük scanInterval) yapılabilir hale gelir.
		/// </summary>
		private void ComputeScanRegion()
		{
			int width = _webCamTexture.width;
			int height = _webCamTexture.height;

			_roiWidth = Mathf.Clamp(Mathf.RoundToInt(width * scanRegionScale), 16, width);
			_roiHeight = Mathf.Clamp(Mathf.RoundToInt(height * scanRegionScale), 16, height);
			_roiX = (width - _roiWidth) / 2;
			_roiY = (height - _roiHeight) / 2;
		}

		/// <summary>
		/// GetPixels32 (Unity API, ana thread zorunlu) hariç tüm ağır işi (crop + ZXing decode)
		/// arka plan thread'ine devreder. Önceki decode bitmeden yenisi başlatılmaz, böylece
		/// iş birikip ana thread'i tıkamaz.
		/// </summary>
		private void TryDecodeCurrentFrame()
		{
			if (_isDecoding)
				return;

			int fullWidth = _webCamTexture.width;
			int fullHeight = _webCamTexture.height;

			if (_pixelBuffer == null || _pixelBuffer.Length != fullWidth * fullHeight)
			{
				_pixelBuffer = new Color32[fullWidth * fullHeight];
			}

			_webCamTexture.GetPixels32(_pixelBuffer);

			_isDecoding = true;
			Color32[] pixelsSnapshot = _pixelBuffer;
			int roiX = _roiX, roiY = _roiY, roiWidth = _roiWidth, roiHeight = _roiHeight;

			Task.Run(() =>
			{
				try
				{
					byte[] rgbaBytes = CropAndFlipToRGBA32(pixelsSnapshot, fullWidth, roiX, roiY, roiWidth, roiHeight);
					Result result = _barcodeReader.Decode(rgbaBytes, roiWidth, roiHeight, RGBLuminanceSource.BitmapFormat.RGBA32);

					lock (_resultLock)
					{
						_pendingResult = result;
						_pendingFullHeight = fullHeight;
						_hasPendingResult = true;
					}
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
				finally
				{
					_isDecoding = false;
				}
			});
		}

		/// <summary>
		/// Arka plan thread'inde tamamlanan son decode sonucunu ana thread'de işler
		/// (popup Instantiate/Destroy gibi Unity API çağrıları burada, ana thread'de yapılır).
		/// </summary>
		private void ConsumePendingDecodeResult()
		{
			Result result;
			int fullHeight;

			lock (_resultLock)
			{
				if (!_hasPendingResult)
					return;

				result = _pendingResult;
				fullHeight = _pendingFullHeight;
				_hasPendingResult = false;
			}

			if (result == null || string.IsNullOrEmpty(result.Text))
			{
				CheckLostTracking();
				return;
			}

			OnQRCodeDecoded(result, fullHeight);
		}

		/// <summary>
		/// WebCamTexture.GetPixels32() satırları alttan üste sıralı verir; ZXing üstten alta bekler.
		/// Bu yüzden sadece ROI'yi kırpıp satırları çevirerek RGBA32 byte dizisine çeviriyoruz.
		/// </summary>
		private byte[] CropAndFlipToRGBA32(Color32[] colors, int fullWidth, int roiX, int roiY, int roiWidth, int roiHeight)
		{
			int neededLength = roiWidth * roiHeight * 4;
			if (_frameBuffer == null || _frameBuffer.Length != neededLength)
			{
				_frameBuffer = new byte[neededLength];
			}

			for (int y = 0; y < roiHeight; y++)
			{
				int srcY = roiY + roiHeight - 1 - y;
				int srcRowStart = srcY * fullWidth + roiX;
				int dstRowStart = y * roiWidth;

				for (int x = 0; x < roiWidth; x++)
				{
					Color32 c = colors[srcRowStart + x];
					int di = (dstRowStart + x) * 4;
					_frameBuffer[di] = c.r;
					_frameBuffer[di + 1] = c.g;
					_frameBuffer[di + 2] = c.b;
					_frameBuffer[di + 3] = c.a;
				}
			}

			return _frameBuffer;
		}

		private void OnQRCodeDecoded(Result result, int fullHeight)
		{
			string decodedText = result.Text;
			_lastSeenTime = Time.time;

			if (decodedText == _dismissedQRID)
			{
				// Kullanıcı bu QR'ı elle kapattı; aynı QR karede kaldığı sürece tekrar açma
				return;
			}

			if (QRCodeRegistry.Instance == null)
			{
				Debug.LogWarning("[QRScannerManager] QRCodeRegistry.Instance is null!");
				return;
			}

			QRCodeData data = QRCodeRegistry.Instance.GetByID(decodedText);
			if (data == null)
				return; // Registry zaten kendi uyarısını logluyor

			if (decodedText != _activeQRID)
			{
				SpawnPopup(data, decodedText);
			}

			if (_activePopup != null)
			{
				GetFullFrameTopDownCenter(result.ResultPoints, fullHeight, out float px, out float py);
				PositionPopup(_activePopup, px, py);
			}
		}

		/// <summary>
		/// ResultPoints, sadece taranan ROI'nin (kırpılmış+çevrilmiş) piksel uzayında gelir.
		/// Bunu ROI ofsetini geri ekleyip tam kare için "üstten alta" piksel koordinatına çeviriyoruz.
		/// </summary>
		private void GetFullFrameTopDownCenter(ResultPoint[] resultPoints, int fullHeight, out float px, out float py)
		{
			float sumX = 0f, sumY = 0f;
			foreach (var point in resultPoints)
			{
				sumX += point.X;
				sumY += point.Y;
			}

			float avgXRoi = sumX / resultPoints.Length;
			float avgYRoi = sumY / resultPoints.Length;

			px = avgXRoi + _roiX;
			py = avgYRoi + (fullHeight - _roiY - _roiHeight);
		}

		private void SpawnPopup(QRCodeData data, string qrCodeID)
		{
			DestroyActivePopup();

			if (qrPopupPrefab == null)
			{
				Debug.LogWarning("[QRScannerManager] qrPopupPrefab atanmamış!");
				return;
			}

			Transform parent = popupContainer != null ? popupContainer : transform;
			_activePopup = Instantiate(qrPopupPrefab, parent);
			_activePopup.Setup(data);
			_activeQRID = qrCodeID;
			_dismissedQRID = null;

			if (debugMode)
				Debug.Log($"<color=lime>[QRScannerManager] Popup instantiate edildi: '{qrCodeID}'</color>");
		}

		private void CheckLostTracking()
		{
			if (Time.time - _lastSeenTime <= loseTrackingTimeout)
				return;

			if (_activePopup != null)
			{
				if (debugMode)
					Debug.Log($"<color=orange>[QRScannerManager] QR kayboldu, popup destroy ediliyor: '{_activeQRID}'</color>");

				DestroyActivePopup();
			}

			// QR görüntüden yeterince uzun süre çıktı; elle kapatma hafızasını da sıfırla
			_dismissedQRID = null;
		}

		private void DestroyActivePopup()
		{
			if (_activePopup != null)
			{
				Destroy(_activePopup.gameObject);
				_activePopup = null;
			}

			_activeQRID = null;
		}

		/// <summary>
		/// Tam kare "üstten alta" piksel koordinatını (px, py) ekran/dünya koordinatına çevirip
		/// popup'ı oraya konumlandırır. cameraDisplayImage'ın RectTransform'u zaten rotasyon/mirror
		/// için döndürülmüş olduğundan, TransformPoint kullanmak bu rotasyonu otomatik hesaba katar.
		/// </summary>
		private void PositionPopup(UIQRPopupPanel popup, float px, float py)
		{
			if (popup == null || cameraDisplayImage == null || _webCamTexture == null)
				return;

			float u = px / _webCamTexture.width;
			float v = 1f - (py / _webCamTexture.height);

			Rect rect = cameraDisplayImage.rectTransform.rect;
			Vector3 localPoint = new Vector3(rect.x + u * rect.width, rect.y + v * rect.height, 0f);
			Vector3 worldPoint = cameraDisplayImage.rectTransform.TransformPoint(localPoint);

			RectTransform popupRT = popup.transform as RectTransform;
			if (popupRT != null)
			{
				popupRT.position = worldPoint;
			}
		}
	}
}
