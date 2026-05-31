using System.Collections;
using System.Collections.Generic;
using KartGame.Core;
using KartGame.Data;
using KartGame.Kart;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KartGame.UI
{
    // Setup: Add this script to the RaceSystems GameObject alongside RaceUI.
    // Set mainMenuSceneName to your menu scene name. Leave empty to reload the race scene.
    // The leaderboard appears automatically 2.5 s after the race finishes.
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float showDelay = 2.5f;
        [SerializeField] private GameObject trophyIconPrefab;
        [SerializeField] private TextAsset playerClassifierJson;

        private RaceManager _raceManager;
        private PositionManager _positionManager;
        private Canvas _canvas;
        private GameObject _panel;
        private Coroutine _showRoutine;
        private string _playerClassificationLine = "Perfil jugador: sin datos";

        private readonly Dictionary<CheckpointTracker, float> _finishTimes =
            new Dictionary<CheckpointTracker, float>();

        private void Start()
        {
            EnsureEventSystem();
            _raceManager = FindFirstObjectByType<RaceManager>();
            _positionManager = FindFirstObjectByType<PositionManager>();

            if (_raceManager != null)
            {
                _raceManager.RaceStateChanged += OnRaceStateChanged;
                _raceManager.RacerFinished += OnRacerFinished;
            }

            BuildCanvas();
            _canvas.gameObject.SetActive(false);

            if (_raceManager != null && _raceManager.CurrentState == RaceState.Finished)
            {
                OnRaceStateChanged(RaceState.Finished);
            }
        }

        private void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                go.AddComponent<StandaloneInputModule>();
#endif
                return;
            }
#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#endif
        }

        private void OnDestroy()
        {
            if (_raceManager == null) return;
            _raceManager.RaceStateChanged -= OnRaceStateChanged;
            _raceManager.RacerFinished -= OnRacerFinished;
        }

        private void OnRacerFinished(CheckpointTracker tracker)
        {
            if (tracker != null && _raceManager != null && !_finishTimes.ContainsKey(tracker))
                _finishTimes[tracker] = _raceManager.RaceElapsedTime;
        }

        private void OnRaceStateChanged(RaceState state)
        {
            if (state == RaceState.Finished)
            {
                _playerClassificationLine = BuildPlayerClassificationLine();
                if (_showRoutine != null)
                {
                    StopCoroutine(_showRoutine);
                }

                _showRoutine = StartCoroutine(ShowAfterDelay());
            }
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(showDelay);
            _showRoutine = null;
            BuildLeaderboard();
        }

        // ── Leaderboard construction ────────────────────────────────────────────

        private void BuildLeaderboard()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            var racers = CollectSortedRacers();

            _canvas.gameObject.SetActive(true);

            foreach (Transform child in _panel.transform)
                Destroy(child.gameObject);

            const float rowH = 54f;
            const float titleH = 72f;
            const float classifierH = 42f;
            const float headerH = 48f;
            const float buttonH = 58f;
            const float pad = 28f;
            const float panelW = 720f;

            float panelH = pad + titleH + classifierH + headerH + 6f + racers.Count * rowH + pad + buttonH + pad;
            _panel.GetComponent<RectTransform>().sizeDelta = new Vector2(panelW, panelH);

            float topY = panelH * 0.5f;

            // Trophy icon to the left of the title
            if (trophyIconPrefab != null)
            {
                var rt = UIIconRenderer.Render(trophyIconPrefab, 96, 15f, -20f);
                if (rt != null)
                {
                    var iconGo = new GameObject("TrophyIcon");
                    iconGo.transform.SetParent(_panel.transform, false);
                    var iconRect = iconGo.AddComponent<RectTransform>();
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(-295, topY - pad - titleH * 0.5f);
                    iconRect.sizeDelta = new Vector2(58, 58);
                    iconGo.AddComponent<RawImage>().texture = rt;
                }
            }

            // Title
            float titleCY = topY - pad - titleH * 0.5f;
            AddText(_panel.transform, "Title", "CLASIFICACION FINAL",
                new Vector2(20, titleCY), new Vector2(620, titleH),
                52, FontStyle.Bold, Color.yellow, TextAnchor.MiddleCenter, outline: true);

            float classifierCY = titleCY - titleH * 0.5f - classifierH * 0.5f;
            AddText(_panel.transform, "PlayerClassifier", _playerClassificationLine,
                new Vector2(0f, classifierCY), new Vector2(panelW - 56f, classifierH),
                26, FontStyle.Bold, new Color(0.7f, 1f, 0.75f), TextAnchor.MiddleCenter);

            // Column headers
            float hCY = classifierCY - classifierH * 0.5f - headerH * 0.5f;
            AddText(_panel.transform, "HPos",    "Pos",
                new Vector2(-240f, hCY), new Vector2(80f, headerH),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);
            AddText(_panel.transform, "HName",   "Corredor",
                new Vector2(10f, hCY), new Vector2(280f, headerH),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleLeft);
            AddText(_panel.transform, "HTime",   "Tiempo",
                new Vector2(255f, hCY), new Vector2(160f, headerH),
                26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);

            float lineY = hCY - headerH * 0.5f - 2f;
            AddLine(_panel.transform, lineY, panelW - 40f);

            // Racer rows
            float firstRowCY = lineY - rowH * 0.5f - 2f;
            for (int i = 0; i < racers.Count; i++)
            {
                var tracker = racers[i];
                float rowCY = firstRowCY - i * rowH;
                bool isPlayer = tracker.IsPlayer;
                Color rowColor = isPlayer ? Color.yellow : Color.white;

                if (isPlayer)
                    AddHighlight(_panel.transform, rowCY, panelW - 24f, rowH - 4f);

                string posStr  = OrdinalStr(i + 1);
                string nameStr = isPlayer ? "Jugador" : TruncateName(tracker.gameObject.name, 16);
                string timeStr = FormatTime(tracker);

                AddText(_panel.transform, $"P{i}",  posStr,
                    new Vector2(-240f, rowCY), new Vector2(80f, rowH),
                    30, FontStyle.Bold, rowColor, TextAnchor.MiddleCenter);
                AddText(_panel.transform, $"N{i}",  nameStr,
                    new Vector2(10f, rowCY), new Vector2(280f, rowH),
                    28, FontStyle.Normal, rowColor, TextAnchor.MiddleLeft);
                AddText(_panel.transform, $"T{i}",  timeStr,
                    new Vector2(255f, rowCY), new Vector2(160f, rowH),
                    28, FontStyle.Normal, rowColor, TextAnchor.MiddleRight);
            }

            // Buttons
            float buttonCY = -panelH * 0.5f + pad + buttonH * 0.5f;
            AddButton(_panel.transform, "BtnAgain", "VOLVER A JUGAR",
                new Vector2(-130f, buttonCY), new Vector2(240f, 50f),
                new Color(0.12f, 0.55f, 0.12f), 26, OnPlayAgain);
            AddButton(_panel.transform, "BtnMenu", "MENU PRINCIPAL",
                new Vector2(130f, buttonCY), new Vector2(240f, 50f),
                new Color(0.15f, 0.35f, 0.70f), 26, OnMainMenu);

            _panel.SetActive(true);
        }

        // ── Data helpers ────────────────────────────────────────────────────────

        private List<CheckpointTracker> CollectSortedRacers()
        {
            var list = new List<CheckpointTracker>(
                FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID));

            list.Sort((a, b) =>
            {
                bool aF = a.HasFinishedRace, bF = b.HasFinishedRace;
                if (aF && bF) return a.FinishPlacement.CompareTo(b.FinishPlacement);
                if (aF) return -1;
                if (bF) return 1;
                if (_positionManager != null)
                    return _positionManager.GetPosition(a).CompareTo(_positionManager.GetPosition(b));
                return 0;
            });

            return list;
        }

        private string FormatTime(CheckpointTracker tracker)
        {
            if (!_finishTimes.TryGetValue(tracker, out float t)) return "DNF";
            int m = Mathf.FloorToInt(t / 60f);
            float s = t % 60f;
            return $"{m:0}:{s:00.00}";
        }

        private string BuildPlayerClassificationLine()
        {
            var recorder = FindPlayerRecorder();
            if (recorder == null)
            {
                return "Perfil jugador: sin datos";
            }

            var metrics = recorder.GetRaceMetrics();
            if (!PlayerBehaviorRandomForestClassifier.TryClassify(metrics, playerClassifierJson, out var result, out var error))
            {
                Debug.LogWarning($"No se pudo clasificar al jugador: {error}", this);
                return "Perfil jugador: no clasificado";
            }

            return $"Perfil jugador: {result.Label} ({Mathf.RoundToInt(result.Confidence * 100f)}%)";
        }

        private static PlayerLapDataRecorder FindPlayerRecorder()
        {
            var recorders = FindObjectsByType<PlayerLapDataRecorder>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < recorders.Length; index++)
            {
                var recorder = recorders[index];
                if (recorder == null)
                {
                    continue;
                }

                var tracker = recorder.GetComponent<CheckpointTracker>() ?? recorder.GetComponentInParent<CheckpointTracker>();
                var playerInput = recorder.GetComponent<PlayerKartInput>() ?? recorder.GetComponentInParent<PlayerKartInput>();
                if ((tracker != null && tracker.IsPlayer) || playerInput != null)
                {
                    return recorder;
                }
            }

            return recorders.Length > 0 ? recorders[0] : null;
        }

        private static string OrdinalStr(int pos) => pos switch
        {
            1 => "1er",
            2 => "2do",
            3 => "3er",
            _ => $"{pos}mo"
        };

        private static string TruncateName(string name, int max) =>
            name.Length > max ? name.Substring(0, max) : name;

        // ── Canvas / UI builders ────────────────────────────────────────────────

        private void BuildCanvas()
        {
            var go = new GameObject("ResultsCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            go.AddComponent<GraphicRaycaster>();
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dimmer
            var dim = new GameObject("Dimmer");
            dim.transform.SetParent(_canvas.transform, false);
            var dimRect = dim.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            // Leaderboard panel
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(720f, 580f);
            _panel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.97f);
        }

        private static void AddText(Transform parent, string name, string text,
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
                o.effectColor = new Color(0, 0, 0, 0.7f);
                o.effectDistance = new Vector2(2, -2);
            }
        }

        private static void AddLine(Transform parent, float yPos, float width)
        {
            var go = new GameObject("Sep");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, yPos);
            rect.sizeDelta = new Vector2(width, 2f);
            go.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.7f, 0.7f);
        }

        private static void AddHighlight(Transform parent, float yPos, float width, float height)
        {
            var go = new GameObject("Hl");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, yPos);
            rect.sizeDelta = new Vector2(width, height);
            go.AddComponent<Image>().color = new Color(0.85f, 0.72f, 0f, 0.22f);
        }

        private static void AddButton(Transform parent, string name, string label,
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
            btn.onClick.AddListener(onClick);

            var lGo = new GameObject("L");
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
        }

        // ── Button callbacks ────────────────────────────────────────────────────

        private void OnPlayAgain() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        private void OnMainMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
