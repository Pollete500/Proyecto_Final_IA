using System;
using System.Collections;
using System.Collections.Generic;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.Core
{
    public enum RaceState
    {
        Setup,
        Countdown,
        Racing,
        Finished
    }

    /*
     * Script: RaceManager.cs
     * Purpose: Boots the race, places racers on the grid, runs the countdown and ends the race when the player or every racer finishes.
     * Attach To: RaceSystems GameObject.
     * Required Components: None.
     * Dependencies: TrackData, LapManager, PositionManager, CheckpointTracker, KartController.
     * Inspector Setup: Assign TrackData, LapManager and PositionManager or leave auto-registration enabled so the script finds them at runtime.
     */
    public class RaceManager : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private LapManager lapManager;
        [SerializeField] private PositionManager positionManager;
        [SerializeField] private List<CheckpointTracker> registeredRacers = new List<CheckpointTracker>();
        [SerializeField] private float countdownDuration = 3f;
        [SerializeField] private bool enableStartCountdown = true;
        [SerializeField] private bool autoRegisterSceneRacers = true;
        [SerializeField] private bool autoPlaceRacersOnSpawnPoints = true;
        [SerializeField] private bool finishRaceWhenPlayerFinishes = true;
        [SerializeField] private float postPlayerFinishTimeout = 90f;

        private Coroutine _raceFlowRoutine;
        private Coroutine _postPlayerFinishRoutine;
        private float _countdownRemaining;
        private float _raceStartTime;
        private float _raceEndTime;
        private readonly List<CheckpointTracker> _directLapSubscriptions = new List<CheckpointTracker>();

        public static RaceManager Instance { get; private set; }
        public TrackData TrackData => trackData;
        public RaceState CurrentState { get; private set; } = RaceState.Setup;
        public float CountdownRemaining => _countdownRemaining;
        public float RaceElapsedTime =>
            CurrentState == RaceState.Finished
                ? Mathf.Max(0f, _raceEndTime - _raceStartTime)
                : CurrentState == RaceState.Racing
                    ? Mathf.Max(0f, Time.time - _raceStartTime)
                    : 0f;

        public event Action<RaceState> RaceStateChanged;
        public event Action<CheckpointTracker> RacerFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (_raceFlowRoutine == null)
            {
                _raceFlowRoutine = StartCoroutine(BootstrapRaceRoutine());
            }
        }

        private void OnDestroy()
        {
            UnsubscribeDirectLapEvents();

            if (lapManager != null)
            {
                lapManager.RacerFinished -= HandleRacerFinished;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetTrackData(TrackData value)
        {
            trackData = value;
        }

        public void SetLapManager(LapManager value)
        {
            if (lapManager != null)
            {
                lapManager.RacerFinished -= HandleRacerFinished;
            }

            lapManager = value;

            if (lapManager != null)
            {
                lapManager.RacerFinished += HandleRacerFinished;
            }
        }

        public void SetPositionManager(PositionManager value)
        {
            positionManager = value;
        }

        public void SetRegisteredRacers(IReadOnlyList<CheckpointTracker> racers)
        {
            registeredRacers.Clear();

            if (racers == null)
            {
                return;
            }

            for (var index = 0; index < racers.Count; index++)
            {
                if (racers[index] != null)
                {
                    registeredRacers.Add(racers[index]);
                }
            }
        }

        public bool IsRaceActive()
        {
            return CurrentState == RaceState.Racing;
        }

        public void RestartRaceFlow()
        {
            if (_raceFlowRoutine != null)
            {
                StopCoroutine(_raceFlowRoutine);
            }

            _raceFlowRoutine = StartCoroutine(BootstrapRaceRoutine());
        }

        private IEnumerator BootstrapRaceRoutine()
        {
            RefreshSceneReferences();
            WireSystems();
            PrepareRacersForRace();

            if (registeredRacers.Count == 0)
            {
                Debug.LogWarning("RaceManager could not find racers. Add CheckpointTracker to player and bot karts.");
                yield break;
            }

            if (enableStartCountdown)
            {
                SetRaceState(RaceState.Countdown);
                SetKartControlEnabled(false);

                _countdownRemaining = Mathf.Max(0f, countdownDuration);
                while (_countdownRemaining > 0f)
                {
                    _countdownRemaining -= Time.deltaTime;
                    yield return null;
                }

                _countdownRemaining = 0f;
            }

            _raceStartTime = Time.time;
            SetKartControlEnabled(true);
            SetRaceState(RaceState.Racing);
        }

        private void RefreshSceneReferences()
        {
            trackData ??= FindFirstObjectByType<TrackData>();
            if (trackData != null && PlayerPrefs.HasKey("LapsToWin"))
                trackData.SetLapsToWin(PlayerPrefs.GetInt("LapsToWin"));
            lapManager ??= FindFirstObjectByType<LapManager>();
            positionManager ??= FindFirstObjectByType<PositionManager>();

            if (lapManager != null)
            {
                lapManager.RacerFinished -= HandleRacerFinished;
                lapManager.RacerFinished += HandleRacerFinished;
            }

            if (!autoRegisterSceneRacers)
            {
                return;
            }

            var foundTrackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var orderedTrackers = new List<CheckpointTracker>(foundTrackers.Length);

            for (var index = 0; index < foundTrackers.Length; index++)
            {
                var tracker = foundTrackers[index];
                if (tracker != null)
                {
                    orderedTrackers.Add(tracker);
                }
            }

            orderedTrackers.Sort((left, right) =>
            {
                if (left.IsPlayer != right.IsPlayer)
                {
                    return left.IsPlayer ? -1 : 1;
                }

                return string.CompareOrdinal(left.name, right.name);
            });

            registeredRacers = orderedTrackers;
        }

        private void WireSystems()
        {
            UnsubscribeDirectLapEvents();

            if (trackData == null)
            {
                Debug.LogWarning("RaceManager requires a TrackData reference.");
                return;
            }

            if (lapManager != null)
            {
                lapManager.SetTrackData(trackData);
                lapManager.ResetRacers(registeredRacers);
            }

            if (positionManager != null)
            {
                positionManager.SetTrackData(trackData);
                positionManager.ResetRacers(registeredRacers);
            }

            SubscribeDirectLapEvents();
        }

        private void PrepareRacersForRace()
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var tracker = registeredRacers[index];
                if (tracker == null)
                {
                    continue;
                }

                tracker.InitializeForRace(trackData);

                if (!autoPlaceRacersOnSpawnPoints)
                {
                    continue;
                }

                var spawnPoint = trackData != null ? trackData.GetSpawnPoint(index) : null;
                if (spawnPoint == null)
                {
                    continue;
                }

                var controller = tracker.GetComponent<KartController>();
                if (controller != null)
                {
                    controller.ResetKart(spawnPoint.position, spawnPoint.rotation);
                }
                else
                {
                    tracker.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                }

                tracker.SetRecoveryReference(spawnPoint);
                tracker.SetInitialSpawnPose(spawnPoint.position, spawnPoint.rotation);
            }
        }

        private void HandleRacerFinished(CheckpointTracker tracker, int _)
        {
            if (tracker == null)
            {
                return;
            }

            RacerFinished?.Invoke(tracker);

            if (CurrentState == RaceState.Finished)
            {
                return;
            }

            if (AllRacersFinished())
            {
                if (_postPlayerFinishRoutine != null)
                {
                    StopCoroutine(_postPlayerFinishRoutine);
                    _postPlayerFinishRoutine = null;
                }
                FinishRace();
                return;
            }

            if (finishRaceWhenPlayerFinishes && IsPlayerTracker(tracker))
            {
                var playerController = tracker.GetComponent<KartController>();
                if (playerController != null)
                {
                    playerController.SetControlEnabled(false, freezePhysics: false);
                }

                FinishRace();
            }
        }

        private IEnumerator PostPlayerFinishRoutine()
        {
            float elapsed = 0f;
            while (elapsed < postPlayerFinishTimeout)
            {
                if (AllRacersFinished()) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            _postPlayerFinishRoutine = null;
            if (CurrentState != RaceState.Finished)
                FinishRace();
        }

        private bool AllRacersFinished()
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var tracker = registeredRacers[index];
                if (tracker != null && !tracker.HasFinishedRace)
                {
                    return false;
                }
            }

            return registeredRacers.Count > 0;
        }

        private void FinishRace()
        {
            if (CurrentState == RaceState.Finished)
            {
                return;
            }

            _raceEndTime = Time.time;
            SetKartControlEnabled(false);
            SetRaceState(RaceState.Finished);
        }

        private void SetKartControlEnabled(bool isEnabled)
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var tracker = registeredRacers[index];
                if (tracker == null)
                {
                    continue;
                }

                var controller = tracker.GetComponent<KartController>();
                if (controller != null)
                {
                    controller.SetControlEnabled(isEnabled);
                }
            }
        }

        private void SetRaceState(RaceState newState)
        {
            CurrentState = newState;
            RaceStateChanged?.Invoke(newState);
        }

        private void SubscribeDirectLapEvents()
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                SubscribeDirectLapEvent(registeredRacers[index]);
            }

            var sceneTrackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < sceneTrackers.Length; index++)
            {
                SubscribeDirectLapEvent(sceneTrackers[index]);
            }
        }

        private void SubscribeDirectLapEvent(CheckpointTracker tracker)
        {
            if (tracker == null || _directLapSubscriptions.Contains(tracker))
            {
                return;
            }

            tracker.LapCompleted += HandleTrackerLapCompleted;
            _directLapSubscriptions.Add(tracker);
        }

        private void UnsubscribeDirectLapEvents()
        {
            for (var index = 0; index < _directLapSubscriptions.Count; index++)
            {
                var tracker = _directLapSubscriptions[index];
                if (tracker != null)
                {
                    tracker.LapCompleted -= HandleTrackerLapCompleted;
                }
            }

            _directLapSubscriptions.Clear();
        }

        private void HandleTrackerLapCompleted(CheckpointTracker tracker, int completedLaps)
        {
            if (tracker == null || trackData == null || tracker.HasFinishedRace)
            {
                return;
            }

            if (completedLaps < trackData.LapsToWin)
            {
                return;
            }

            var placement = lapManager != null ? lapManager.GetFinishPlacement(tracker) : 0;
            if (placement <= 0)
            {
                placement = GetNextFallbackFinishPlacement();
                tracker.MarkFinished(placement);
                RacerFinished?.Invoke(tracker);
            }

            if (finishRaceWhenPlayerFinishes && IsPlayerTracker(tracker))
            {
                var playerController = tracker.GetComponent<KartController>() ?? tracker.GetComponentInChildren<KartController>(true);
                if (playerController != null)
                {
                    playerController.SetControlEnabled(false, freezePhysics: false);
                }

                FinishRace();
            }
        }

        private int GetNextFallbackFinishPlacement()
        {
            var maxPlacement = 0;
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var tracker = registeredRacers[index];
                if (tracker != null && tracker.FinishPlacement > maxPlacement)
                {
                    maxPlacement = tracker.FinishPlacement;
                }
            }

            return maxPlacement + 1;
        }

        private static bool IsPlayerTracker(CheckpointTracker tracker)
        {
            return tracker != null &&
                   (tracker.IsPlayer || tracker.GetComponent<PlayerKartInput>() != null || tracker.GetComponentInChildren<PlayerKartInput>(true) != null);
        }
    }
}
