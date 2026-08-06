using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SafetyTraining
{
    /// <summary>
    /// Çalışan CEDAŞ panelinde oluşturulan ID'sini ve şifresini girip giriş yapar.
    /// Doğrulama CEDAŞ backend'inde (PlayFabDataManager.LoginWithCredentials) yapılır.
    /// </summary>
    public class UILoginPanel : MonoBehaviour
    {
        [Header("━━━ UI REFERANSLARI ━━━")]
        public TMP_InputField playerIdInput;
        public TMP_InputField passwordInput;
        public Button         loginButton;
        public TextMeshProUGUI statusText;
        public GameObject      loadingIndicator;

        [Tooltip("Giriş başarılı olduğunda çalışanın adının yazılacağı text (ör. ana menüdeki kullanıcı alanı)")]
        public TextMeshProUGUI displayNameText;

        [Header("━━━ AYARLAR ━━━")]
        [Tooltip("CEDAŞ panelinin izin verdiği minimum şifre uzunluğu")]
        public int minPasswordLength = 12;

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        private void Start()
        {
            // Zaten giriş yapılmışsa (ör. bir level bitirip menüye dönüldüğünde
            // bu panel yeniden aktive oluyor) tekrar login sormadan direkt kapat.
            if (PlayFabDataManager.Instance != null && PlayFabDataManager.Instance.IsLoggedIn)
            {
                if (displayNameText != null)
                    displayNameText.text = PlayFabDataManager.Instance.CurrentDisplayName;

                gameObject.SetActive(false);
                return;
            }

            // Şifre alanı yanlışlıkla düz metin olarak bırakılmışsa bile ekranda gizli kalsın.
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            SetInteractable(true);
            SetStatus("Çalışan ID'nizi girip giriş yapın.");

            if (loginButton)
                loginButton.onClick.AddListener(OnLoginClicked);

            if (playerIdInput)
                playerIdInput.onSubmit.AddListener(_ => OnLoginClicked());

            if (passwordInput)
                passwordInput.onSubmit.AddListener(_ => OnLoginClicked());
        }

        // ─── Login ───

        private void OnLoginClicked()
        {
            string enteredId       = playerIdInput != null ? playerIdInput.text : string.Empty;
            string enteredPassword = passwordInput  != null ? passwordInput.text  : string.Empty;

            if (string.IsNullOrWhiteSpace(enteredId))
            {
                SetStatus("ID boş olamaz.", isError: true);
                return;
            }

            if (string.IsNullOrEmpty(enteredPassword) || enteredPassword.Length < minPasswordLength)
            {
                SetStatus($"Şifre en az {minPasswordLength} karakter olmalı.", isError: true);
                return;
            }

            SetInteractable(false);
            ShowLoading(true);
            SetStatus("Giriş yapılıyor...");

            PlayFabDataManager.Instance?.LoginWithCredentials(enteredId, enteredPassword,
                OnLoginSuccess,
                OnLoginFailed);
        }

        private void OnLoginSuccess(PlayFabDataManager.PlayerEntry entry)
        {
            ShowLoading(false);
            SetStatus($"Hoşgeldin, {entry.displayName}!");

            if (displayNameText != null)
                displayNameText.text = entry.displayName;

            if (debugMode)
                Debug.Log($"[UILoginPanel] ✓ {entry.displayName} ({entry.playerId}) giriş yaptı.");

            Invoke(nameof(ClosePanel), 1f);
        }

        private void OnLoginFailed(string error)
        {
            ShowLoading(false);
            SetInteractable(true);
            SetStatus($"Giriş başarısız: {error}", isError: true);
            Debug.LogError($"[UILoginPanel] Giriş hatası: {error}");
        }

        // ─── UI Helpers ───

        private void ClosePanel() => gameObject.SetActive(false);

        private void SetInteractable(bool state)
        {
            if (loginButton)     loginButton.interactable    = state;
            if (playerIdInput)   playerIdInput.interactable  = state;
            if (passwordInput)   passwordInput.interactable  = state;
        }

        private void ShowLoading(bool state)
        {
            if (loadingIndicator) loadingIndicator.SetActive(state);
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (statusText == null) return;
            statusText.text  = message;
            statusText.color = isError ? new Color(0.9f, 0.2f, 0.2f) : Color.white;
        }
    }
}
