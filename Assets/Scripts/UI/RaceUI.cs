using KartGame.Core;
using KartGame.Kart;
using UnityEngine;
using UnityEngine.UI;

namespace KartGame.UI
{
    public class RaceUI : MonoBehaviour
    {
        [Header("Semaphore")]
        [SerializeField] private Color countdownColor = Color.white;
        [SerializeField] private Color goColor = Color.green;
        [SerializeField] private float countdownScale = 2.5f;
        [SerializeField] private float goScale = 3f;

        [Header("References")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private LapManager lapManager;
        [SerializeField] private PositionManager positionManager;

        private Canvas _canvas;
        private Text _countdownText;
        private Text _lapText;
        private Text _timerText;
        private Text _positionText;
        private Text _finishText;

        private CheckpointTracker _playerTracker;
        private bool _uiCreated;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
            raceManager ??= FindFirstObjectByType<RaceManager>();
            lapManager ??= FindFirstObjectByType<LapManager>();
            positionManager ??= FindFirstObjectByType<PositionManager>();

            if (raceManager != null)
            {
                raceManager.RaceStateChanged += OnRaceStateChanged;
                OnRaceStateChanged(raceManager.CurrentState);
            }

            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            foreach (var t in trackers)
            {
                if (t.IsPlayer)
                {
                    _playerTracker = t;
                    break;
                }
            }

            if (_playerTracker != null)
            {
                _playerTracker.LapCompleted += OnLapCompleted;
                _playerTracker.CheckpointPassed += OnCheckpointPassed;
            }

            if (positionManager != null)
            {
                positionManager.PositionsUpdated += OnPositionsUpdated;
            }
        }

        private void OnDestroy()
        {
            if (raceManager != null)
                raceManager.RaceStateChanged -= OnRaceStateChanged;
            if (_playerTracker != null)
            {
                _playerTracker.LapCompleted -= OnLapCompleted;
                _playerTracker.CheckpointPassed -= OnCheckpointPassed;
            }
            if (positionManager != null)
                positionManager.PositionsUpdated -= OnPositionsUpdated;
        }

        private void CreateUI()
        {
            _canvas = new GameObject("RaceCanvas").AddComponent<Canvas>();
            _canvas.gameObject.AddComponent<CanvasScaler>();
            _canvas.gameObject.AddComponent<GraphicRaycaster>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            _canvas.transform.SetParent(transform, false);

            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _countdownText = CreateText(_canvas.transform, "CountdownText", "", new Vector2(0, 50), new Vector2(400, 200),
                120, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
            _countdownText.gameObject.SetActive(false);

            _lapText = CreateText(_canvas.transform, "LapText", "", new Vector2(-750, 400), new Vector2(300, 80),
                48, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);

            _timerText = CreateText(_canvas.transform, "TimerText", "", new Vector2(0, 400), new Vector2(300, 80),
                48, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);

            _positionText = CreateText(_canvas.transform, "PositionText", "", new Vector2(750, 400), new Vector2(300, 80),
                48, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);

            _finishText = CreateText(_canvas.transform, "FinishText", "", new Vector2(0, -50), new Vector2(800, 150),
                72, FontStyle.Bold, TextAnchor.MiddleCenter, Color.yellow, true);
            _finishText.gameObject.SetActive(false);

            _uiCreated = true;
        }

        private Text CreateText(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size,
            int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, bool hasOutline = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = fontStyle;
            txt.alignment = alignment;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (hasOutline)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.8f);
                outline.effectDistance = new Vector2(3, -3);
            }

            return txt;
        }

        private void Update()
        {
            if (!_uiCreated) return;

            if (raceManager == null) return;

            if (raceManager.CurrentState == RaceState.Racing || raceManager.CurrentState == RaceState.Finished)
            {
                var elapsed = raceManager.RaceElapsedTime;
                var minutes = Mathf.FloorToInt(elapsed / 60f);
                var seconds = elapsed % 60f;
                _timerText.text = $"{minutes:0}:{seconds:00.00}";
            }
        }

        private void OnRaceStateChanged(RaceState state)
        {
            switch (state)
            {
                case RaceState.Countdown:
                    StartCoroutine(CountdownRoutine());
                    break;
                case RaceState.Racing:
                    _lapText.gameObject.SetActive(true);
                    _timerText.gameObject.SetActive(true);
                    _positionText.gameObject.SetActive(true);
                    break;
                case RaceState.Finished:
                    ShowFinishScreen();
                    break;
            }
        }

        private System.Collections.IEnumerator CountdownRoutine()
        {
            _countdownText.gameObject.SetActive(true);
            _lapText.gameObject.SetActive(false);
            _timerText.gameObject.SetActive(false);
            _positionText.gameObject.SetActive(false);

            var numbers = new[] { "3", "2", "1" };

            foreach (var num in numbers)
            {
                _countdownText.text = num;
                _countdownText.color = countdownColor;
                _countdownText.fontSize = (int)(80 * countdownScale);

                var startScale = 0.3f;
                var elapsed = 0f;
                var duration = 0.7f;

                while (elapsed < duration)
                {
                    var t = elapsed / duration;
                    var scale = Mathf.Lerp(startScale, 1f, t);
                    _countdownText.rectTransform.localScale = Vector3.one * scale;
                    var alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                    _countdownText.color = new Color(countdownColor.r, countdownColor.g, countdownColor.b, alpha);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            _countdownText.text = "GO!";
            _countdownText.color = goColor;
            _countdownText.fontSize = (int)(80 * goScale);

            var goElapsed = 0f;
            var goDuration = 0.45f;
            while (goElapsed < goDuration)
            {
                var t = goElapsed / goDuration;
                var scale = Mathf.Lerp(1.5f, 1f, t);
                _countdownText.rectTransform.localScale = Vector3.one * scale;
                goElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _countdownText.gameObject.SetActive(false);
        }

        private void OnLapCompleted(CheckpointTracker tracker, int completedLaps)
        {
            if (tracker != _playerTracker) return;
            if (raceManager?.TrackData == null) return;

            var totalLaps = raceManager.TrackData.LapsToWin;
            if (completedLaps <= totalLaps)
            {
                _lapText.text = $"Lap {completedLaps}/{totalLaps}";
            }
        }

        private void OnCheckpointPassed(CheckpointTracker tracker, Checkpoint checkpoint)
        {
            if (tracker != _playerTracker) return;
            if (raceManager?.TrackData == null) return;

            var totalLaps = raceManager.TrackData.LapsToWin;
            var currentLap = Mathf.Min(tracker.CompletedLaps + 1, totalLaps);
            _lapText.text = $"Lap {currentLap}/{totalLaps}";
        }

        private void OnPositionsUpdated()
        {
            if (_playerTracker == null || positionManager == null) return;

            var pos = positionManager.GetPosition(_playerTracker);
            var total = 0;
            if (raceManager != null)
            {
                var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
                total = trackers.Length;
            }

            var suffix = pos switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

            _positionText.text = $"{pos}{suffix} / {total}";
        }

        private void ShowFinishScreen()
        {
            _lapText.gameObject.SetActive(false);
            _timerText.gameObject.SetActive(false);
            _positionText.gameObject.SetActive(false);

            if (_playerTracker == null) return;

            var placement = _playerTracker.FinishPlacement;
            var suffix = placement switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

            _finishText.text = placement <= 3
                ? $"FINISH! {placement}{suffix} Place!"
                : $"Finished - {placement}{suffix}";

            _finishText.gameObject.SetActive(true);
            StartCoroutine(FinishPulseRoutine());
        }

        private System.Collections.IEnumerator FinishPulseRoutine()
        {
            while (true)
            {
                var pulse = Mathf.Sin(Time.unscaledTime * 4f) * 0.15f + 0.85f;
                _finishText.rectTransform.localScale = Vector3.one * pulse;
                yield return null;
            }
        }
    }
}
