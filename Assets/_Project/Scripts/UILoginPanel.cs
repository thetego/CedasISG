using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace SafetyTraining
{
    /// <summary>
    /// Oyun açılışında PlayFab Title Data'dan whitelist'i çeker,
    /// dropdown ile oyuncu seçtirip giriş yapar.
    /// </summary>
    public class UILoginPanel : MonoBehaviour
    {
        [Header("━━━ UI REFERANSLARI ━━━")]
        public TMP_Dropdown playerDropdown;
        public Button       loginButton;
        public TextMeshProUGUI statusText;
        public GameObject   loadingIndicator;

        [Header("━━━ DEBUG ━━━")]
        public bool debugMode = true;

        private List<PlayFabDataManager.PlayerEntry> _entries;
        private PlayFabDataManager.PlayerEntry       _selectedEntry;

        private void Start()
        {
            SetInteractable(false);
            SetStatus("Oyuncu listesi yükleniyor...");
            ShowLoading(true);

            PlayFabDataManager.Instance?.FetchWhitelist(OnWhitelistFetched, OnFetchFailed);
        }

        // ─── Whitelist yüklendi ───

        private void OnWhitelistFetched(List<PlayFabDataManager.PlayerEntry> entries)
        {
            _entries = entries;
            ShowLoading(false);
            PopulateDropdown();
            SetInteractable(true);
            SetStatus("Oyuncuyu seçip giriş yapın.");

            if (loginButton)
                loginButton.onClick.AddListener(OnLoginClicked);

            if (playerDropdown)
                playerDropdown.onValueChanged.AddListener(OnDropdownChanged);

            OnDropdownChanged(0);
        }

        private void OnFetchFailed(string error)
        {
            ShowLoading(false);
            SetStatus($"Liste yüklenemedi: {error}", isError: true);
            Debug.LogError($"[UILoginPanel] Whitelist hatası: {error}");
        }

        // ─── Dropdown ───

        private void PopulateDropdown()
        {
            if (playerDropdown == null || _entries == null) return;

            playerDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var e in _entries)
                options.Add(new TMP_Dropdown.OptionData(e.displayName));

            playerDropdown.AddOptions(options);
        }

        private void OnDropdownChanged(int index)
        {
            if (_entries == null || index >= _entries.Count) return;
            _selectedEntry = _entries[index];
        }

        // ─── Login ───

        private void OnLoginClicked()
        {
            if (_selectedEntry == null) return;

            SetInteractable(false);
            ShowLoading(true);
            SetStatus("Giriş yapılıyor...");

            PlayFabDataManager.Instance?.LoginWithPlayer(_selectedEntry, OnLoginSuccess, OnLoginFailed);
        }

        private void OnLoginSuccess()
        {
            ShowLoading(false);
            SetStatus($"Hoşgeldin, {_selectedEntry.displayName}!");

            if (debugMode)
                Debug.Log($"[UILoginPanel] ✓ {_selectedEntry.displayName} giriş yaptı.");

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
            if (loginButton)    loginButton.interactable    = state;
            if (playerDropdown) playerDropdown.interactable = state;
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
