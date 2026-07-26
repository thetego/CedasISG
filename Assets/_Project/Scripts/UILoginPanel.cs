using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using Michsky.MUIP;

namespace SafetyTraining
{
    /// <summary>
    /// Oyun açılışında PlayFab Title Data'dan çalışan whitelist'ini çeker.
    /// Çalışan kendi ID'sini bir input field'a yazıp giriş yapar — tüm çalışan
    /// listesi ekrana hiç basılmaz, sadece girilen ID whitelist'te aranır.
    /// </summary>
    public class UILoginPanel : MonoBehaviour
    {
        [Header("━━━ UI REFERANSLARI ━━━")]
        public TMP_InputField playerIdInput;
        public ButtonManager   loginButton;
        public TextMeshProUGUI statusText;
        public GameObject      loadingIndicator;

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        private List<PlayFabDataManager.PlayerEntry> _entries;

        private void Start()
        {
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
            string enteredId = playerIdInput != null ? playerIdInput.text : string.Empty;
            PlayFabDataManager.PlayerEntry entry = FindEntry(enteredId);

            if (entry == null)
            {
                SetStatus("ID bulunamadı. Lütfen kontrol edip tekrar deneyin.", isError: true);
                return;
            }

            SetInteractable(false);
            ShowLoading(true);
            SetStatus("Giriş yapılıyor...");

            PlayFabDataManager.Instance?.LoginWithPlayer(entry,
                () => OnLoginSuccess(entry),
                OnLoginFailed);
        }

        private void OnLoginSuccess(PlayFabDataManager.PlayerEntry entry)
        {
            ShowLoading(false);
            SetStatus($"Hoşgeldin, {entry.displayName}!");

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
            if (loginButton)     loginButton.Interactable(state);
            if (playerIdInput)   playerIdInput.interactable  = state;
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
