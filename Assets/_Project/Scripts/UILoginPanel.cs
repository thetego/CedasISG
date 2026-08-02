using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace SafetyTraining
{
    /// <summary>
    /// Oyun açılışında PlayFab Title Data'dan çalışan whitelist'ini çeker.
    /// Çalışan kendi ID'sini ve şifresini girip giriş yapar — tüm çalışan
    /// listesi ekrana hiç basılmaz, sadece girilen ID whitelist'te aranır.
    /// Bir ID için ilk kez şifre giriliyorsa (PlayFab hesabı henüz yoksa) o şifre
    /// kalıcı olarak kaydedilir; sonraki girişlerde aynı şifre doğrulanır
    /// (bkz. PlayFabDataManager.LoginWithPlayer).
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
        [Tooltip("PlayFab'ın izin verdiği minimum şifre uzunluğu")]
        public int minPasswordLength = 6;

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        private List<PlayFabDataManager.PlayerEntry> _entries;

        private void Start()
        {
            // Şifre alanı yanlışlıkla düz metin olarak bırakılmışsa bile ekranda gizli kalsın.
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            SetInteractable(false);
            SetStatus("Çalışan listesi yükleniyor...");
            ShowLoading(true);

            PlayFabDataManager.Instance?.FetchWhitelist(OnWhitelistFetched, OnFetchFailed);
        }

        // ─── Whitelist yüklendi ───

        private void OnWhitelistFetched(List<PlayFabDataManager.PlayerEntry> entries)
        {
            _entries = entries;
            ShowLoading(false);
            SetInteractable(true);
            SetStatus("Çalışan ID'nizi girip giriş yapın.");

            if (loginButton)
                loginButton.onClick.AddListener(OnLoginClicked);

            if (playerIdInput)
                playerIdInput.onSubmit.AddListener(_ => OnLoginClicked());

            if (passwordInput)
                passwordInput.onSubmit.AddListener(_ => OnLoginClicked());
        }

        private void OnFetchFailed(string error)
        {
            ShowLoading(false);
            SetStatus($"Liste yüklenemedi: {error}", isError: true);
            Debug.LogError($"[UILoginPanel] Whitelist hatası: {error}");
        }

        // ─── ID arama ───

        private PlayFabDataManager.PlayerEntry FindEntry(string enteredId)
        {
            if (_entries == null || string.IsNullOrWhiteSpace(enteredId))
                return null;

            string trimmed = enteredId.Trim();
            return _entries.Find(e =>
                string.Equals(e.playerId, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        // ─── Login ───

        private void OnLoginClicked()
        {
            string enteredId       = playerIdInput != null ? playerIdInput.text : string.Empty;
            string enteredPassword = passwordInput  != null ? passwordInput.text  : string.Empty;

            PlayFabDataManager.PlayerEntry entry = FindEntry(enteredId);

            if (entry == null)
            {
                SetStatus("ID bulunamadı. Lütfen kontrol edip tekrar deneyin.", isError: true);
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

            PlayFabDataManager.Instance?.LoginWithPlayer(entry, enteredPassword,
                () => OnLoginSuccess(entry),
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
