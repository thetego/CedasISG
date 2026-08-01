using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SafetyTraining
{
    /// <summary>
    /// Sahne kurulumuna bağımlı olmayan açık telemetri tercihi ekranı.
    /// Tercih, aydınlatma metni sürümüyle birlikte cihazda saklanır.
    /// </summary>
    public static class TelemetryPrivacyGate
    {
        private static bool _spawned;
        private static Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ShowWhenRequired()
        {
            PlayFabDataManager manager = PlayFabDataManager.Instance;
            if (manager == null || !manager.requireTelemetryConsent) return;

            EnsureEventSystem();
            CreatePrivacyControl(manager);
            if (!manager.HasExplicitTelemetryDecision)
                ShowDialog(manager);
        }

        private static void ShowDialog(PlayFabDataManager manager)
        {
            if (_spawned || manager == null) return;

            _spawned = true;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject root = CreateObject("TelemetryPrivacyGate", null);
            Object.DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            RectTransform overlay = CreatePanel(
                "Overlay",
                root.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.08f, 0.92f));

            RectTransform card = CreatePanel(
                "PrivacyCard",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(860f, 560f),
                new Color(0.98f, 0.99f, 1f, 1f));
            card.anchoredPosition = Vector2.zero;

            CreateText(
                "Title",
                card,
                "Eğitim Verileri Aydınlatması",
                36,
                FontStyle.Bold,
                new Color(0.04f, 0.12f, 0.23f),
                new Vector2(56f, -48f),
                new Vector2(-112f, 64f),
                TextAnchor.MiddleLeft);

            CreateText(
                "Body",
                card,
                "Eğitim sürecinin güvenliğini ve gelişimini ölçmek için çalışan kimliği, rol, " +
                "tamamlanan seviye ve adımlar, cevaplar, süreler, sürükle-bırak denemeleri, " +
                "anket sonuçları ve hata kayıtları işlenir. Veriler CEDAŞ eğitim yöneticileri " +
                "tarafından yetki kapsamında görüntülenir ve PlayFab altyapısına aktarılır.\n\n" +
                "Geçici ağ kesintilerinde kayıtlar bu cihazda en fazla 7 gün tutulur ve başarılı " +
                "aktarımdan sonra silinir. Tercihinizi daha sonra uygulama ayarlarından geri " +
                "çekebilirsiniz. Reddetmeniz eğitime devam etmenizi engellemez; yalnız analitik " +
                "olaylar kaydedilmez.",
                23,
                FontStyle.Normal,
                new Color(0.16f, 0.22f, 0.31f),
                new Vector2(56f, -126f),
                new Vector2(-112f, 286f),
                TextAnchor.UpperLeft);

            CreateText(
                "Version",
                card,
                "Aydınlatma sürümü: " + manager.privacyNoticeVersion,
                17,
                FontStyle.Normal,
                new Color(0.38f, 0.44f, 0.53f),
                new Vector2(56f, -420f),
                new Vector2(-112f, 32f),
                TextAnchor.MiddleLeft);

            CreateButton(
                "DeclineButton",
                card,
                "Reddet",
                new Vector2(56f, 38f),
                new Vector2(260f, 68f),
                new Color(0.86f, 0.89f, 0.93f),
                new Color(0.12f, 0.18f, 0.27f),
                () => Complete(root, manager, false));

            CreateButton(
                "AcceptButton",
                card,
                "Kabul Et ve Devam Et",
                new Vector2(-56f, 38f),
                new Vector2(390f, 68f),
                new Color(0.05f, 0.48f, 0.72f),
                Color.white,
                () => Complete(root, manager, true),
                alignRight: true);
        }

        private static void CreatePrivacyControl(PlayFabDataManager manager)
        {
            if (GameObject.Find("TelemetryPrivacyControl") != null) return;
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject root = CreateObject("TelemetryPrivacyControl", null);
            Object.DontDestroyOnLoad(root);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.AddComponent<GraphicRaycaster>();

            CreateButton(
                "PrivacyPreferenceButton",
                root.transform,
                PreferenceLabel(manager),
                new Vector2(-24f, 24f),
                new Vector2(220f, 52f),
                new Color(0.04f, 0.12f, 0.23f, 0.92f),
                Color.white,
                () => ShowDialog(manager),
                alignRight: true);
        }

        private static string PreferenceLabel(PlayFabDataManager manager)
        {
            return manager.HasTelemetryConsent ? "Veri Tercihi: Açık" : "Veri Tercihi: Kapalı";
        }

        private static void RefreshPrivacyControl(PlayFabDataManager manager)
        {
            GameObject root = GameObject.Find("TelemetryPrivacyControl");
            Text label = root != null ? root.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = PreferenceLabel(manager);
        }

        private static void Complete(GameObject root, PlayFabDataManager manager, bool allowed)
        {
            manager.SetTelemetryConsent(allowed);
            RefreshPrivacyControl(manager);
            Object.Destroy(root);
            _spawned = false;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            GameObject eventSystem = new GameObject("Telemetry EventSystem");
            Object.DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Color color)
        {
            GameObject panel = CreateObject(name, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            Image image = panel.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment)
        {
            GameObject holder = CreateObject(name, parent);
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = holder.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 offset,
            Vector2 size,
            Color background,
            Color foreground,
            UnityEngine.Events.UnityAction onClick,
            bool alignRight = false)
        {
            GameObject holder = CreateObject(name, parent);
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.anchorMin = alignRight ? new Vector2(1f, 0f) : Vector2.zero;
            rect.anchorMax = rect.anchorMin;
            rect.pivot = alignRight ? new Vector2(1f, 0f) : Vector2.zero;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            Image image = holder.AddComponent<Image>();
            image.color = background;
            Button button = holder.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            GameObject labelObject = CreateObject("Label", holder.transform);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            Text text = labelObject.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.color = foreground;
            text.alignment = TextAnchor.MiddleCenter;
        }
    }
}
