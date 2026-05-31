using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using KartGame.PowerUps;
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
        [SerializeField] private PlayerKartInput playerInput;
        [SerializeField] private CheckpointTracker playerTracker;
        [SerializeField] private KartController playerKartController;
        [SerializeField] private KartPowerUpController playerPowerUpController;

        [Header("Icons (assign Polygon Icons prefabs in Inspector)")]
        [SerializeField] private GameObject clockIconPrefab;
        [SerializeField] private GameObject pauseIconPrefab;

        [Header("UI Setup")]
        [SerializeField] private bool preferExistingUiHierarchy = true;

        private Canvas _canvas;
        private Text _countdownText;
        private Text _coinsText;
        private Text _powerUpPointsText;
        private Text _lapText;
        private Text _timerText;
        private Text _positionText;
        private Text _finishText;
        private Text _spectateText;

        private RawImage _timerIconImage;
        private RawImage _pauseIconImage;

        private PlayerKartInput _playerInput;
        private CheckpointTracker _playerTracker;
        private KartController _playerKartController;
        private KartPowerUpController _playerPowerUpController;
        private CheckpointTracker _spectateTarget;
        private CameraFollow _cameraFollow;
        private Coroutine _waitSpectateRoutine;
        private GameObject _pauseOverlay;
        private bool _isPaused;
        private bool _uiCreated;
        private int _totalRacers = 1;
        private RaceManager _subscribedRaceManager;
        private CheckpointTracker _subscribedPlayerTracker;
        private PositionManager _subscribedPositionManager;

        private readonly List<CheckpointTracker> _spectateCandidates = new List<CheckpointTracker>();
        private int _spectateIndex = -1;

        private void Awake()
        {
            if (!TryBindExistingUiHierarchy())
            {
                CreateUI();
            }
        }

        private void Start()
        {
            EnsureEventSystem();
            EnsureResultsScreen();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            ResolveCoreReferences();
            ResolvePlayerReferences();

            SubscribeRuntimeEvents();

            _cameraFollow = FindFirstObjectByType<CameraFollow>();
            _totalRacers = Mathf.Max(1, FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID).Length);

            ApplyIcons();
            UpdatePlayerHud();
        }

        private void EnsureResultsScreen()
        {
            if (FindFirstObjectByType<ResultsScreen>() != null)
            {
                return;
            }

            gameObject.AddComponent<ResultsScreen>();
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;

            if (_subscribedRaceManager != null)
            {
                _subscribedRaceManager.RaceStateChanged -= OnRaceStateChanged;
                _subscribedRaceManager.RacerFinished -= OnRacerFinished;
            }

            if (_subscribedPlayerTracker != null)
            {
                _subscribedPlayerTracker.LapCompleted -= OnLapCompleted;
                _subscribedPlayerTracker.CheckpointPassed -= OnCheckpointPassed;
            }

            if (_subscribedPositionManager != null)
            {
                _subscribedPositionManager.PositionsUpdated -= OnPositionsUpdated;
            }
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

            _countdownText = CreateText(_canvas.transform, "CountdownText", "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(420f, 200f),
                120, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
            _countdownText.gameObject.SetActive(false);

            _coinsText = CreateText(_canvas.transform, "CoinsText", "", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 56f),
                40, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);

            _powerUpPointsText = CreateText(_canvas.transform, "PowerUpPointsText", "", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -96f), new Vector2(420f, 56f),
                34, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);

            _timerText = CreateText(_canvas.transform, "TimerText", "", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(320f, 70f),
                48, FontStyle.Normal, TextAnchor.UpperCenter, Color.white);

            _lapText = CreateText(_canvas.transform, "LapText", "", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -96f), new Vector2(380f, 70f),
                34, FontStyle.Bold, TextAnchor.UpperRight, Color.white);

            _positionText = CreateText(_canvas.transform, "PositionText", "", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(380f, 84f),
                40, FontStyle.Bold, TextAnchor.UpperRight, Color.white);

            _finishText = CreateText(_canvas.transform, "FinishText", "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(800f, 150f),
                72, FontStyle.Bold, TextAnchor.MiddleCenter, Color.yellow, true);
            _finishText.gameObject.SetActive(false);

            _spectateText = CreateText(_canvas.transform, "SpectateText", "", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(720f, 52f),
                30, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f), true);
            _spectateText.gameObject.SetActive(false);

            _timerIconImage = CreateRawImage(_canvas.transform, "TimerIcon", new Vector2(-130f, -40f), new Vector2(80f, 80f));
            _timerIconImage.gameObject.SetActive(false);

            CreateText(_canvas.transform, "PauseHint", "ESC - Pausar / Reanudar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(500f, 36f),
                20, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f));

            CreatePauseOverlay();
            _uiCreated = true;
        }

        private bool TryBindExistingUiHierarchy()
        {
            if (!preferExistingUiHierarchy)
            {
                return false;
            }

            _canvas = FindCanvasChild("RaceCanvas") ?? GetComponentInChildren<Canvas>(true);
            if (_canvas == null)
            {
                return false;
            }

            _countdownText = FindTextInHierarchy("CountdownText");
            _coinsText = FindTextInHierarchy("CoinsText");
            _powerUpPointsText = FindTextInHierarchy("PowerUpPointsText");
            _lapText = FindTextInHierarchy("LapText");
            _timerText = FindTextInHierarchy("TimerText");
            _positionText = FindTextInHierarchy("PositionText");
            _finishText = FindTextInHierarchy("FinishText");
            _spectateText = FindTextInHierarchy("SpectateText");
            _timerIconImage = FindRawImageInHierarchy("TimerIcon");
            _pauseIconImage = FindRawImageInHierarchy("PauseIcon");
            _pauseOverlay = FindGameObjectInHierarchy("PauseOverlay");

            var hasCoreHud =
                _countdownText != null &&
                _coinsText != null &&
                _powerUpPointsText != null &&
                _lapText != null &&
                _timerText != null &&
                _positionText != null &&
                _finishText != null &&
                _spectateText != null &&
                _timerIconImage != null &&
                _pauseIconImage != null &&
                _pauseOverlay != null;

            if (!hasCoreHud)
            {
                return false;
            }

            _uiCreated = true;
            return true;
        }

        private Text CreateText(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size,
            int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, bool hasOutline = false)
        {
            return CreateText(parent, name, text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size, fontSize, fontStyle, alignment, color, hasOutline);
        }

        private Text CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
            int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, bool hasOutline = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
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

        private Canvas FindCanvasChild(string canvasName)
        {
            var canvasTransform = FindTransformByName(transform, canvasName);
            return canvasTransform != null ? canvasTransform.GetComponent<Canvas>() : null;
        }

        private void Update()
        {
            if (raceManager == null || lapManager == null || positionManager == null)
            {
                ResolveCoreReferences();
            }

            ResolvePlayerReferences();
            SubscribeRuntimeEvents();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (_spectateTarget != null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CycleSpectateTarget(1);
            }

            UpdateTimerText();

            UpdatePlayerHud();
        }

        private void OnRaceStateChanged(RaceState state)
        {
            switch (state)
            {
                case RaceState.Countdown:
                    SetHudVisible(false);
                    StartCoroutine(CountdownRoutine());
                    break;
                case RaceState.Racing:
                    _countdownText.gameObject.SetActive(false);
                    SetHudVisible(true);
                    if (_timerIconImage != null && _timerIconImage.texture != null)
                    {
                        _timerIconImage.gameObject.SetActive(true);
                    }
                    UpdatePlayerHud();
                    break;
                case RaceState.Finished:
                    ShowFinishScreen();
                    break;
            }
        }

        private System.Collections.IEnumerator CountdownRoutine()
        {
            _countdownText.gameObject.SetActive(true);

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
        }

        private void OnLapCompleted(CheckpointTracker tracker, int completedLaps)
        {
            if (tracker != _playerTracker)
            {
                return;
            }

            UpdatePlayerHud();
        }

        private void OnCheckpointPassed(CheckpointTracker tracker, Checkpoint checkpoint)
        {
            if (tracker != _playerTracker)
            {
                return;
            }

            UpdatePlayerHud();
        }

        private void OnPositionsUpdated()
        {
            UpdatePlayerHud();
        }

        private void ShowFinishScreen()
        {
            SetHudVisible(false);
            _timerIconImage?.gameObject.SetActive(false);
            _spectateText?.gameObject.SetActive(false);
            _spectateTarget = null;

            if (_waitSpectateRoutine != null)
            {
                StopCoroutine(_waitSpectateRoutine);
                _waitSpectateRoutine = null;
            }

            if (_cameraFollow != null && _playerTracker != null)
            {
                _cameraFollow.SetTarget(_playerTracker.transform);
            }

            if (_playerTracker != null && !_finishText.gameObject.activeSelf)
            {
                ShowPlayerFinish();
            }
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
            if (tracker == null)
            {
                return;
            }

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
            if (_playerTracker == null)
            {
                return;
            }

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
                if (legacy != null)
                {
                    Destroy(legacy);
                }

                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#endif
        }

        private void CreatePauseOverlay()
        {
            var overlay = new GameObject("PauseOverlay");
            overlay.transform.SetParent(_canvas.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            _pauseIconImage = CreateRawImage(overlay.transform, "PauseIcon", new Vector2(0f, 0f), new Vector2(200f, 200f));

            _pauseOverlay = overlay;
            overlay.SetActive(false);
        }

        private void TogglePause()
        {
            if (raceManager == null || raceManager.CurrentState != RaceState.Racing)
            {
                return;
            }

            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            _pauseOverlay.SetActive(_isPaused);
            Cursor.visible = _isPaused;
            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void RefreshSpectateCandidates()
        {
            _spectateCandidates.Clear();
            var all = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            foreach (var tracker in all)
            {
                if (tracker != null && !tracker.IsPlayer && !tracker.HasFinishedRace)
                {
                    _spectateCandidates.Add(tracker);
                }
            }

            if (positionManager != null)
            {
                _spectateCandidates.Sort((a, b) => positionManager.GetPosition(a).CompareTo(positionManager.GetPosition(b)));
            }
        }

        private void SwitchSpectateTarget()
        {
            if (_cameraFollow == null)
            {
                return;
            }

            RefreshSpectateCandidates();

            if (_spectateCandidates.Count == 0)
            {
                _spectateText?.gameObject.SetActive(false);
                _spectateTarget = null;
                return;
            }

            var newIndex = 0;
            if (_spectateTarget != null && !_spectateTarget.HasFinishedRace)
            {
                var found = _spectateCandidates.IndexOf(_spectateTarget);
                if (found >= 0)
                {
                    newIndex = found;
                }
            }

            _spectateIndex = newIndex;
            SetSpectateTarget(_spectateCandidates[_spectateIndex]);
        }

        private void CycleSpectateTarget(int delta)
        {
            if (_spectateCandidates.Count == 0)
            {
                return;
            }

            _spectateIndex = (_spectateIndex + delta + _spectateCandidates.Count) % _spectateCandidates.Count;
            SetSpectateTarget(_spectateCandidates[_spectateIndex]);
        }

        private void SetSpectateTarget(CheckpointTracker target)
        {
            _spectateTarget = target;
            _cameraFollow.SetTarget(target.transform);

            var name = target.gameObject.name;
            if (name.Length > 18)
            {
                name = name.Substring(0, 18);
            }

            var total = _spectateCandidates.Count;
            _spectateText.text = $"ESPECTANDO: {name}  [{_spectateIndex + 1}/{total}]   Tab->siguiente";
            _spectateText.gameObject.SetActive(true);
        }

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
                {
                    _pauseIconImage.texture = rt;
                }
            }
        }

        private void CachePlayerReferences()
        {
            if (playerInput != null)
            {
                _playerInput = playerInput;
            }

            if (playerTracker != null)
            {
                _playerTracker = playerTracker;
            }

            if (playerKartController != null)
            {
                _playerKartController = playerKartController;
            }

            if (playerPowerUpController != null)
            {
                _playerPowerUpController = playerPowerUpController;
            }

            if (_playerInput == null)
            {
                _playerInput = FindFirstObjectByType<PlayerKartInput>();
            }

            if (_playerInput != null)
            {
                _playerTracker ??= _playerInput.GetComponent<CheckpointTracker>() ?? _playerInput.GetComponentInParent<CheckpointTracker>();
                _playerKartController ??= _playerInput.GetComponent<KartController>() ?? _playerInput.GetComponentInParent<KartController>();
                _playerPowerUpController ??= _playerInput.GetComponent<KartPowerUpController>() ?? _playerInput.GetComponentInParent<KartPowerUpController>();
            }

            if (_playerTracker != null && (_playerTracker.IsPlayer || _playerInput != null || playerTracker != null))
            {
                _playerKartController ??= _playerTracker.GetComponent<KartController>() ?? _playerTracker.GetComponentInChildren<KartController>(true);
                _playerPowerUpController ??= _playerTracker.GetComponent<KartPowerUpController>() ?? _playerTracker.GetComponentInChildren<KartPowerUpController>(true);
                return;
            }

            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < trackers.Length; index++)
            {
                var tracker = trackers[index];
                if (tracker == null)
                {
                    continue;
                }

                var hasPlayerInput = tracker.GetComponent<PlayerKartInput>() != null || tracker.GetComponentInChildren<PlayerKartInput>(true) != null;
                if (!tracker.IsPlayer &&
                    !hasPlayerInput &&
                    tracker.name.IndexOf("player", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _playerTracker = tracker;
                _playerKartController = tracker.GetComponent<KartController>() ?? tracker.GetComponentInChildren<KartController>(true);
                _playerPowerUpController = tracker.GetComponent<KartPowerUpController>() ?? tracker.GetComponentInChildren<KartPowerUpController>(true);
                break;
            }
        }

        private void ResolvePlayerReferences()
        {
            if (playerInput != null)
            {
                _playerInput = playerInput;
            }

            if (playerTracker != null)
            {
                _playerTracker = playerTracker;
            }

            if (playerKartController != null)
            {
                _playerKartController = playerKartController;
            }

            if (playerPowerUpController != null)
            {
                _playerPowerUpController = playerPowerUpController;
            }

            CachePlayerReferences();
        }

        private void ResolveCoreReferences()
        {
            raceManager ??= RaceManager.Instance ?? FindFirstObjectByType<RaceManager>();
            lapManager ??= FindFirstObjectByType<LapManager>();
            positionManager ??= FindFirstObjectByType<PositionManager>();
        }

        private void SubscribeRuntimeEvents()
        {
            if (_subscribedRaceManager != raceManager)
            {
                if (_subscribedRaceManager != null)
                {
                    _subscribedRaceManager.RaceStateChanged -= OnRaceStateChanged;
                    _subscribedRaceManager.RacerFinished -= OnRacerFinished;
                }

                _subscribedRaceManager = raceManager;

                if (_subscribedRaceManager != null)
                {
                    _subscribedRaceManager.RaceStateChanged += OnRaceStateChanged;
                    _subscribedRaceManager.RacerFinished += OnRacerFinished;
                    OnRaceStateChanged(_subscribedRaceManager.CurrentState);
                }
            }

            if (_subscribedPlayerTracker != _playerTracker)
            {
                if (_subscribedPlayerTracker != null)
                {
                    _subscribedPlayerTracker.LapCompleted -= OnLapCompleted;
                    _subscribedPlayerTracker.CheckpointPassed -= OnCheckpointPassed;
                }

                _subscribedPlayerTracker = _playerTracker;

                if (_subscribedPlayerTracker != null)
                {
                    _subscribedPlayerTracker.LapCompleted += OnLapCompleted;
                    _subscribedPlayerTracker.CheckpointPassed += OnCheckpointPassed;
                }
            }

            if (_subscribedPositionManager != positionManager)
            {
                if (_subscribedPositionManager != null)
                {
                    _subscribedPositionManager.PositionsUpdated -= OnPositionsUpdated;
                }

                _subscribedPositionManager = positionManager;

                if (_subscribedPositionManager != null)
                {
                    _subscribedPositionManager.PositionsUpdated += OnPositionsUpdated;
                }
            }
        }

        private void UpdateTimerText()
        {
            if (_timerText == null)
            {
                return;
            }

            float elapsed;
            if (raceManager == null)
            {
                elapsed = Time.timeSinceLevelLoad;
            }
            else
            {
                elapsed = raceManager.CurrentState switch
                {
                    RaceState.Racing => raceManager.RaceElapsedTime,
                    RaceState.Finished => raceManager.RaceElapsedTime,
                    _ => Time.timeSinceLevelLoad
                };
            }

            var minutes = Mathf.FloorToInt(elapsed / 60f);
            var seconds = elapsed % 60f;
            _timerText.text = $"{minutes:0}:{seconds:00.00}";
        }

        private void UpdatePlayerHud()
        {
            if (_playerTracker == null)
            {
                return;
            }

            _totalRacers = Mathf.Max(1, FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID).Length);

            var position = positionManager != null ? positionManager.GetPosition(_playerTracker) : 0;
            var trackData = raceManager != null ? raceManager.TrackData : _playerTracker.TrackData;
            var totalLaps = trackData != null ? Mathf.Max(1, trackData.LapsToWin) : 1;
            var currentLap = Mathf.Clamp(_playerTracker.CompletedLaps + 1, 1, totalLaps);
            var lapsRemaining = Mathf.Max(0, totalLaps - _playerTracker.CompletedLaps);
            var coins = _playerKartController != null ? _playerKartController.CurrentCoins : 0;
            var powerUps = _playerPowerUpController != null ? _playerPowerUpController.AvailablePowerUpPoints : 0;

            if (_coinsText != null)
            {
                _coinsText.text = $"Coins: {coins}";
            }

            if (_powerUpPointsText != null)
            {
                _powerUpPointsText.text = $"Power Ups: {powerUps}";
            }

            if (_lapText != null)
            {
                _lapText.text = $"Lap {currentLap}/{totalLaps}\nLeft {lapsRemaining}";
            }

            if (_positionText != null)
            {
                _positionText.text = $"{position}/{_totalRacers}\n{GetPositionLabel(position)}";
            }
        }

        private void SetHudVisible(bool visible)
        {
            if (_coinsText != null)
            {
                _coinsText.gameObject.SetActive(visible);
            }

            if (_powerUpPointsText != null)
            {
                _powerUpPointsText.gameObject.SetActive(visible);
            }

            if (_lapText != null)
            {
                _lapText.gameObject.SetActive(visible);
            }

            if (_positionText != null)
            {
                _positionText.gameObject.SetActive(visible);
            }

            if (_timerText != null)
            {
                _timerText.gameObject.SetActive(visible);
            }
        }

        private static string GetPositionLabel(int position)
        {
            return position switch
            {
                1 => "Primero",
                2 => "Segundo",
                3 => "Tercero",
                4 => "Cuarto",
                5 => "Quinto",
                6 => "Sexto",
                7 => "Septimo",
                8 => "Octavo",
                9 => "Noveno",
                10 => "Decimo",
                _ => $"{position}th"
            };
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

        private Text FindTextInHierarchy(string objectName)
        {
            var transformMatch = FindTransformByName(transform, objectName);
            return transformMatch != null ? transformMatch.GetComponent<Text>() : null;
        }

        private RawImage FindRawImageInHierarchy(string objectName)
        {
            var transformMatch = FindTransformByName(transform, objectName);
            return transformMatch != null ? transformMatch.GetComponent<RawImage>() : null;
        }

        private GameObject FindGameObjectInHierarchy(string objectName)
        {
            var transformMatch = FindTransformByName(transform, objectName);
            return transformMatch != null ? transformMatch.gameObject : null;
        }

        private static Transform FindTransformByName(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindTransformByName(root.GetChild(index), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
