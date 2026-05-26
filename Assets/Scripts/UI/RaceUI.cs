using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        [Header("Icons (assign Polygon Icons prefabs in Inspector)")]
        [SerializeField] private GameObject clockIconPrefab;
        [SerializeField] private GameObject pauseIconPrefab;

        private Canvas _canvas;
        private Text _countdownText;
        private Text _lapText;
        private Text _timerText;
        private Text _positionText;
        private Text _finishText;
        private Text _spectateText;

        private CheckpointTracker _playerTracker;
        private CheckpointTracker _spectateTarget;
        private CameraFollow _cameraFollow;
        private Coroutine _waitSpectateRoutine;
        private GameObject _pauseOverlay;
        private bool _isPaused;
        private bool _uiCreated;

        private RawImage _timerIconImage;
        private RawImage _pauseIconImage;
        private readonly List<CheckpointTracker> _spectateCandidates = new List<CheckpointTracker>();
        private int _spectateIndex = -1;
        private int _totalRacers;

        private void Awake()
        {
            CreateUI();
        }

        private void Start()
        {
            EnsureEventSystem();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
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

            if (raceManager != null)
                raceManager.RacerFinished += OnRacerFinished;
            _cameraFollow = FindFirstObjectByType<CameraFollow>();
            _totalRacers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID).Length;

            ApplyIcons();
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
            if (raceManager != null)
            {
                raceManager.RaceStateChanged -= OnRaceStateChanged;
                raceManager.RacerFinished -= OnRacerFinished;
            }
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

            _spectateText = CreateText(_canvas.transform, "SpectateText", "", new Vector2(0, -390), new Vector2(700, 52),
                30, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f), true);
            _spectateText.gameObject.SetActive(false);

            _timerIconImage = CreateRawImage(_canvas.transform, "TimerIcon", new Vector2(-130, 400), new Vector2(80, 80));
            _timerIconImage.gameObject.SetActive(false);

            CreateText(_canvas.transform, "PauseHint", "ESC — Pausar / Reanudar",
                new Vector2(0, -470), new Vector2(500, 36),
                20, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f));

            CreatePauseOverlay();
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

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                TogglePause();

            if (_spectateTarget != null && Keyboard.current != null
                && Keyboard.current.tabKey.wasPressedThisFrame)
                CycleSpectateTarget(1);

            if (raceManager.CurrentState == RaceState.Racing || raceManager.CurrentState == RaceState.Finished)
            {
                var elapsed = raceManager.RaceElapsedTime;
                var minutes = Mathf.FloorToInt(elapsed / 60f);
                var seconds = elapsed % 60f;
                _timerText.text = $"{minutes:0}:{seconds:00.00}";
            }

            if (_spectateTarget != null)
            {
                if (raceManager.TrackData != null)
                {
                    int totalLaps = raceManager.TrackData.LapsToWin;
                    int currentLap = Mathf.Min(_spectateTarget.CompletedLaps + 1, totalLaps);
                    _lapText.text = $"Lap {currentLap}/{totalLaps}";
                }
                if (positionManager != null)
                {
                    int pos = positionManager.GetPosition(_spectateTarget);
                    string suffix = pos switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
                    _positionText.text = $"{pos}{suffix} / {_totalRacers}";
                }
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
                    _countdownText.gameObject.SetActive(false);
                    _lapText.text = $"Lap 1/{raceManager?.TrackData?.LapsToWin ?? 3}";
                    _lapText.gameObject.SetActive(true);
                    _timerText.gameObject.SetActive(true);
                    _positionText.gameObject.SetActive(true);
                    if (_timerIconImage != null && _timerIconImage.texture != null)
                        _timerIconImage.gameObject.SetActive(true);
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

            // GO! stays visible until RaceState.Racing fires (see OnRaceStateChanged)
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
            _timerIconImage?.gameObject.SetActive(false);
            _spectateText?.gameObject.SetActive(false);
            _spectateTarget = null;

            if (_waitSpectateRoutine != null)
            {
                StopCoroutine(_waitSpectateRoutine);
                _waitSpectateRoutine = null;
            }

            if (_cameraFollow != null && _playerTracker != null)
                _cameraFollow.SetTarget(_playerTracker.transform);

            // If player finished last (no spectate phase), show finish text now so they see their result
            if (_playerTracker != null && !_finishText.gameObject.activeSelf)
                ShowPlayerFinish();
        }

        private System.Collections.IEnumerator FinishPulseRoutine()
        {
            while (_finishText != null && _finishText.gameObject.activeSelf)
            {
                var pulse = Mathf.Sin(Time.unscaledTime * 4f) * 0.15f + 0.85f;
                _finishText.rectTransform.localScale = Vector3.one * pulse;
                yield return null;
            }
        }

        private void OnRacerFinished(CheckpointTracker tracker)
        {
            if (tracker == null) return;
            if (tracker.IsPlayer)
            {
                ShowPlayerFinish();
                _waitSpectateRoutine = StartCoroutine(WaitThenSpectate(3f));
            }
            else if (_spectateTarget != null)
            {
                SwitchSpectateTarget();
            }
        }

        private void ShowPlayerFinish()
        {
            if (_playerTracker == null) return;

            var placement = _playerTracker.FinishPlacement;
            var suffix = placement switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            _finishText.text = placement <= 3
                ? $"FINISH! {placement}{suffix} Place!"
                : $"Finished - {placement}{suffix}";
            _finishText.gameObject.SetActive(true);
            StartCoroutine(FinishPulseRoutine());
        }

        private System.Collections.IEnumerator WaitThenSpectate(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _waitSpectateRoutine = null;
            _finishText?.gameObject.SetActive(false);
            SwitchSpectateTarget();
        }

        // ── Pause ────────────────────────────────────────────────────────────

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

        private void CreatePauseOverlay()
        {
            var overlay = new GameObject("PauseOverlay");
            overlay.transform.SetParent(_canvas.transform, false);
            var or_ = overlay.AddComponent<RectTransform>();
            or_.anchorMin = Vector2.zero;
            or_.anchorMax = Vector2.one;
            or_.offsetMin = or_.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // Icon floats centered on the dim overlay — ESC to resume
            var ct = overlay.transform;

            _pauseIconImage = CreateRawImage(ct, "PauseIcon", new Vector2(0, 0), new Vector2(200, 200));

            _pauseOverlay = overlay;
            overlay.SetActive(false);
        }

        private void TogglePause()
        {
            if (raceManager == null || raceManager.CurrentState != RaceState.Racing) return;
            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            _pauseOverlay.SetActive(_isPaused);
            Cursor.visible = _isPaused;
            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // ── Spectate ─────────────────────────────────────────────────────────

        private void RefreshSpectateCandidates()
        {
            _spectateCandidates.Clear();
            var all = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            foreach (var t in all)
                if (!t.IsPlayer && !t.HasFinishedRace) _spectateCandidates.Add(t);
            if (positionManager != null)
                _spectateCandidates.Sort((a, b) =>
                    positionManager.GetPosition(a).CompareTo(positionManager.GetPosition(b)));
        }

        private void SwitchSpectateTarget()
        {
            if (_cameraFollow == null) return;

            RefreshSpectateCandidates();

            if (_spectateCandidates.Count == 0)
            {
                _spectateText?.gameObject.SetActive(false);
                _spectateTarget = null;
                return;
            }

            // Keep watching the same target if it's still racing; otherwise go to best position
            int newIndex = 0;
            if (_spectateTarget != null && !_spectateTarget.HasFinishedRace)
            {
                int found = _spectateCandidates.IndexOf(_spectateTarget);
                if (found >= 0) newIndex = found;
            }
            _spectateIndex = newIndex;
            SetSpectateTarget(_spectateCandidates[_spectateIndex]);
        }

        private void CycleSpectateTarget(int delta)
        {
            if (_spectateCandidates.Count == 0) return;
            _spectateIndex = (_spectateIndex + delta + _spectateCandidates.Count) % _spectateCandidates.Count;
            SetSpectateTarget(_spectateCandidates[_spectateIndex]);
        }

        private void SetSpectateTarget(CheckpointTracker target)
        {
            _spectateTarget = target;
            _cameraFollow.SetTarget(target.transform);
            string n = target.gameObject.name;
            if (n.Length > 18) n = n.Substring(0, 18);
            int total = _spectateCandidates.Count;
            _spectateText.text = $"ESPECTANDO: {n}  [{_spectateIndex + 1}/{total}]   Tab→siguiente";
            _spectateText.gameObject.SetActive(true);
        }

        // ── Icons ─────────────────────────────────────────────────────────────

        private void ApplyIcons()
        {
            if (clockIconPrefab != null && _timerIconImage != null)
            {
                var rt = UIIconRenderer.Render(clockIconPrefab, 96, 20f, -20f);
                if (rt != null)
                {
                    _timerIconImage.texture = rt;
                    _timerIconImage.gameObject.SetActive(true);
                }
            }
            if (pauseIconPrefab != null && _pauseIconImage != null)
            {
                var rt = UIIconRenderer.Render(pauseIconPrefab, 128, 0f, 0f);
                if (rt != null)
                    _pauseIconImage.texture = rt;
            }
        }

        private RawImage CreateRawImage(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return go.AddComponent<RawImage>();
        }
    }
}
