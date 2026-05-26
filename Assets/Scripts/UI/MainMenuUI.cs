using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KartGame.UI
{
    // Setup: attach to any GameObject in the MainMenu scene.
    // Set raceSceneName to your race scene name in the Inspector.
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private string raceSceneName = "SampleScene";
        [SerializeField] private GameObject gearIconPrefab;

        private Canvas _canvas;
        private GameObject _configPanel;

        private void Awake()
        {
            EnsureEventSystem();
            if (Camera.main != null)
                Camera.main.backgroundColor = new Color(0.04f, 0.04f, 0.14f);
            AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            if (PlayerPrefs.HasKey("GraphicsQuality"))
                QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("GraphicsQuality"), true);
            BuildUI();
        }

        // Unity UI requires an EventSystem in the scene to handle button clicks.
        // Uses the new InputSystem module when available, falls back to legacy otherwise.
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildUI()
        {
            _canvas = CreateCanvas("MenuCanvas", 10);
            var root = _canvas.transform;

            bool titleDone = false;
#if UNITY_EDITOR
            var titleTex = UIIconRenderer.RenderWord("KART AI RACING", 160);
            if (titleTex != null)
            {
                var tGo = new GameObject("Title");
                tGo.transform.SetParent(root, false);
                var tRect = tGo.AddComponent<RectTransform>();
                tRect.anchorMin = tRect.anchorMax = new Vector2(0.5f, 0.5f);
                tRect.sizeDelta = new Vector2(1300f, 160f);
                tRect.anchoredPosition = new Vector2(0, 230);
                tGo.AddComponent<RawImage>().texture = titleTex;
                titleDone = true;
            }
#endif
            if (!titleDone)
                MakeText(root, "Title", "KART AI RACING",
                    new Vector2(0, 230), new Vector2(960, 180),
                    88, FontStyle.Bold, Color.yellow, TextAnchor.MiddleCenter, outline: true);

            MakeButton(root, "BtnPlay", "JUGAR",
                new Vector2(0, 60), new Vector2(320, 70),
                new Color(0.12f, 0.55f, 0.12f), 44, OnPlay);

            MakeButton(root, "BtnConfig", "CONFIGURACION",
                new Vector2(0, -40), new Vector2(320, 70),
                new Color(0.15f, 0.38f, 0.68f), 32, OnConfig);

            MakeButton(root, "BtnQuit", "SALIR",
                new Vector2(0, -140), new Vector2(320, 70),
                new Color(0.55f, 0.12f, 0.12f), 44, OnQuit);

            MakeText(root, "Version", "v0.1",
                new Vector2(0, -460), new Vector2(300, 50),
                22, FontStyle.Normal, new Color(0.40f, 0.40f, 0.40f), TextAnchor.MiddleCenter);

            BuildConfigPanel(root);
        }

        private void BuildConfigPanel(Transform root)
        {
            var overlay = new GameObject("ConfigOverlay");
            overlay.transform.SetParent(root, false);
            var or_ = overlay.AddComponent<RectTransform>();
            or_.anchorMin = Vector2.zero;
            or_.anchorMax = Vector2.one;
            or_.offsetMin = or_.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(overlay.transform, false);
            var pr = panel.AddComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(620, 500);
            panel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.97f);

            var pt = panel.transform;

            MakeText(pt, "CTitle", "CONFIGURACION",
                new Vector2(20, 220), new Vector2(520, 64),
                40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, outline: true);

            if (gearIconPrefab != null)
            {
                var rt = UIIconRenderer.Render(gearIconPrefab, 96, 10f, -15f);
                if (rt != null)
                {
                    var gGo = new GameObject("GearIcon");
                    gGo.transform.SetParent(pt, false);
                    var gRect = gGo.AddComponent<RectTransform>();
                    gRect.anchorMin = gRect.anchorMax = new Vector2(0.5f, 0.5f);
                    gRect.anchoredPosition = new Vector2(-260, 220);
                    gRect.sizeDelta = new Vector2(52, 52);
                    gGo.AddComponent<RawImage>().texture = rt;
                }
            }

            // ── Volume ────────────────────────────────────────────────────
            MakeText(pt, "VolLbl", "VOLUMEN",
                new Vector2(-130, 130), new Vector2(180, 42),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);
            float initVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
            MakeSlider(pt, "VolSlider", new Vector2(110, 130), new Vector2(260, 26),
                0f, 1f, initVol,
                v => { AudioListener.volume = v; PlayerPrefs.SetFloat("MasterVolume", v); });

            // ── Laps ──────────────────────────────────────────────────────
            MakeText(pt, "LapsLbl", "VUELTAS",
                new Vector2(-130, 40), new Vector2(180, 42),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);
            int curLaps = PlayerPrefs.GetInt("LapsToWin", 3);
            int[] lapVals = { 1, 3, 5 };
            var lapBtns = new Button[3];
            var lapOn  = new Color(0.15f, 0.48f, 0.78f);
            var lapOff = new Color(0.18f, 0.18f, 0.28f);
            for (int i = 0; i < 3; i++)
            {
                int lv = lapVals[i]; int li = i;
                lapBtns[i] = MakeButton(pt, $"Laps{lv}", lv.ToString(),
                    new Vector2(30f + li * 86f, 40f), new Vector2(74f, 42f),
                    lv == curLaps ? lapOn : lapOff, 26, () =>
                    {
                        PlayerPrefs.SetInt("LapsToWin", lv);
                        for (int j = 0; j < lapBtns.Length; j++)
                            lapBtns[j].GetComponent<Image>().color =
                                lapVals[j] == lv ? lapOn : lapOff;
                    });
            }

            // ── Graphics quality ──────────────────────────────────────────
            MakeText(pt, "QualLbl", "CALIDAD",
                new Vector2(-130, -60), new Vector2(180, 42),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);
            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            int[] qualVals   = { 0, Mathf.Clamp(maxQ / 2, 0, maxQ), maxQ };
            string[] qualLabels = { "BAJA", "MEDIA", "ALTA" };
            int curQual = PlayerPrefs.GetInt("GraphicsQuality", QualitySettings.GetQualityLevel());
            var qOn  = new Color(0.55f, 0.28f, 0.05f);
            var qOff = new Color(0.18f, 0.18f, 0.28f);
            var qualBtns = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int qv = qualVals[i]; int qi = i;
                qualBtns[i] = MakeButton(pt, $"Qual{qi}", qualLabels[qi],
                    new Vector2(30f + qi * 100f, -60f), new Vector2(88f, 42f),
                    qv == curQual ? qOn : qOff, 22, () =>
                    {
                        PlayerPrefs.SetInt("GraphicsQuality", qv);
                        QualitySettings.SetQualityLevel(qv, true);
                        for (int j = 0; j < qualBtns.Length; j++)
                            qualBtns[j].GetComponent<Image>().color =
                                qualVals[j] == qv ? qOn : qOff;
                    });
            }

            var overlayRef = overlay;
            MakeButton(pt, "BtnClose", "CERRAR",
                new Vector2(0, -185), new Vector2(220, 54),
                new Color(0.55f, 0.15f, 0.15f), 30, () => overlayRef.SetActive(false));

            _configPanel = overlay;
            overlay.SetActive(false);
        }

        // ── Callbacks ───────────────────────────────────────────────────────────

        private void OnPlay() => SceneManager.LoadScene(raceSceneName);
        private void OnConfig() => _configPanel?.SetActive(true);

        private void OnQuit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private Canvas CreateCanvas(string goName, int sortOrder)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            go.AddComponent<GraphicRaycaster>();
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return c;
        }

        private static Text MakeText(Transform parent, string name, string text,
            Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color,
            TextAnchor anchor, bool outline = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = anchor;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.8f);
                o.effectDistance = new Vector2(3, -3);
            }
            return txt;
        }

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, Color bgColor, int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.35f;
            colors.pressedColor = bgColor * 0.65f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            var lGo = new GameObject("Label");
            lGo.transform.SetParent(go.transform, false);
            var lRect = lGo.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = lRect.offsetMax = Vector2.zero;
            var txt = lGo.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return btn;
        }

        private static void MakeSlider(Transform parent, string name, Vector2 pos, Vector2 size,
            float minVal, float maxVal, float initVal, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var slider = go.AddComponent<Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.direction = Slider.Direction.LeftToRight;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.28f);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-15, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.9f);
            slider.fillRect = fillRect;

            var handleArea = new GameObject("HandleSlideArea");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            slider.value = initVal;
            slider.onValueChanged.AddListener(onChanged);
        }
    }
}
