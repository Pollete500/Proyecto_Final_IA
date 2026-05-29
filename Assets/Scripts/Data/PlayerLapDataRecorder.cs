using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using KartGame.Core;
using KartGame.PowerUps;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartGame.Kart
{
    /*
     * Script: PlayerLapDataRecorder.cs
     * Purpose: Saves one CSV row with player metrics every time the player completes a lap.
     * Attach To: Player kart root GameObject.
     * Required Components: KartController, CheckpointTracker.
     * Dependencies: RaceManager, PositionManager, KartPowerUpController.
     * Inspector Setup: PlayerKartInput auto-adds this component at runtime. Keep map tags/layers aligned with track walls/off-track zones.
     */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartController))]
    [RequireComponent(typeof(CheckpointTracker))]
    public class PlayerLapDataRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private KartController kartController;
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private PositionManager positionManager;
        [SerializeField] private RaceManager raceManager;

        [Header("Recording")]
        [SerializeField] private bool recordOnlyPlayer = true;
        [SerializeField] private string dataFolderName = "data";
        [SerializeField] private string playerDataFolderName = "playerdata";
        [SerializeField] private bool logCsvPathOnCreate = true;

        [Header("Map Collision Filters")]
        [SerializeField] private string[] mapCollisionTags = { "Wall", "OffTrack" };
        [SerializeField] private string[] mapCollisionLayerNames = { "KartWall", "OffTrack" };
        [SerializeField] private bool countStaticUntaggedCollisionsAsMap;

        [Header("Runtime Debug")]
        public string debugCsvPath;
        public int debugCurrentLapIndex;
        public float debugCurrentLapSeconds;
        public int debugMapCollisions;
        public int debugBananaCollisions;
        public int debugShellCollisions;
        public int debugBananasThrown;
        public int debugShellsThrown;
        public int debugMushroomsUsed;
        public int debugStarsUsed;

        private string _csvPath;
        private bool _isRaceRecording;
        private bool _subscribedToPowerUpEvents;
        private CheckpointTracker _subscribedCheckpointTracker;
        private RaceManager _subscribedRaceManager;
        private float _lapStartTime;
        private int _mapCollisions;
        private int _bananaCollisions;
        private int _shellCollisions;
        private int _bananasThrown;
        private int _shellsThrown;
        private int _mushroomsUsed;
        private int _starsUsed;

        private void Awake()
        {
            CacheReferences();
            SubscribeToEvents();
            SyncDebugState();
        }

        private void Start()
        {
            CacheReferences();
            SubscribeToEvents();

            if (raceManager != null && raceManager.CurrentState == RaceState.Racing)
            {
                BeginRaceRecording();
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            SubscribeToEvents();
            SyncDebugState();
        }

        private void Update()
        {
            if (raceManager == null || positionManager == null)
            {
                CacheReferences();
                SubscribeToEvents();
            }

            if (_isRaceRecording)
            {
                debugCurrentLapSeconds = Mathf.Max(0f, Time.time - _lapStartTime);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            _isRaceRecording = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!ShouldRecordRuntimeEvent() || collision == null || collision.collider == null)
            {
                return;
            }

            if (IsMapCollision(collision.collider))
            {
                _mapCollisions++;
                SyncDebugState();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!ShouldRecordRuntimeEvent() || other == null)
            {
                return;
            }

            if (IsMapCollision(other))
            {
                _mapCollisions++;
                SyncDebugState();
            }
        }

        private void HandleRaceStateChanged(RaceState newState)
        {
            if (newState == RaceState.Racing)
            {
                BeginRaceRecording();
                return;
            }

            if (newState == RaceState.Setup || newState == RaceState.Countdown || newState == RaceState.Finished)
            {
                _isRaceRecording = false;
            }
        }

        private void HandleLapCompleted(CheckpointTracker tracker, int completedLap)
        {
            if (tracker != checkpointTracker || !ShouldRecordRuntimeEvent())
            {
                return;
            }

            debugCurrentLapIndex = completedLap;
            WriteCurrentLapRow();
            ResetLapCounters();
            _lapStartTime = Time.time;
            SyncDebugState();
        }

        private void HandleAnyPowerUpUsed(KartPowerUpController sourceController, PowerUpType powerUpType)
        {
            if (!ShouldRecordRuntimeEvent() || !IsPlayerPowerUpController(sourceController))
            {
                return;
            }

            switch (powerUpType)
            {
                case PowerUpType.Banana:
                    _bananasThrown++;
                    break;
                case PowerUpType.Shell:
                    _shellsThrown++;
                    break;
                case PowerUpType.Mushroom:
                    _mushroomsUsed++;
                    break;
                case PowerUpType.Star:
                    _starsUsed++;
                    break;
            }

            SyncDebugState();
        }

        private void HandleAnyPowerUpHit(KartPowerUpController sourceController, PowerUpType powerUpType, KartController targetKart)
        {
            if (!ShouldRecordRuntimeEvent() || targetKart != kartController)
            {
                return;
            }

            switch (powerUpType)
            {
                case PowerUpType.Banana:
                    _bananaCollisions++;
                    break;
                case PowerUpType.Shell:
                    _shellCollisions++;
                    break;
            }

            SyncDebugState();
        }

        private void BeginRaceRecording()
        {
            if (!ShouldRecordThisKart())
            {
                return;
            }

            EnsureCsvCreated();
            ResetLapCounters();
            _lapStartTime = Time.time;
            _isRaceRecording = true;
            debugCurrentLapIndex = 0;
            SyncDebugState();
        }

        private void WriteCurrentLapRow()
        {
            EnsureCsvCreated();

            var lapTime = Mathf.Max(0f, Time.time - _lapStartTime);
            var position = GetCurrentPosition();
            var builder = new StringBuilder();
            builder.Append(lapTime.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(position.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_mapCollisions.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_bananaCollisions.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_shellCollisions.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_bananasThrown.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_shellsThrown.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_mushroomsUsed.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(_starsUsed.ToString(CultureInfo.InvariantCulture))
                .AppendLine();

            WriteTextWithRetry(_csvPath, builder.ToString(), append: true);
        }

        private void EnsureCsvCreated()
        {
            if (!string.IsNullOrWhiteSpace(_csvPath))
            {
                return;
            }

            var outputDirectory = Path.Combine(Application.dataPath, dataFolderName, playerDataFolderName);
            Directory.CreateDirectory(outputDirectory);

            var sceneName = SanitizeFileName(SceneManager.GetActiveScene().name);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            _csvPath = Path.Combine(outputDirectory, $"player_laps_{sceneName}_{timestamp}.csv");
            debugCsvPath = _csvPath;

            const string header = "lapTime_delta,position_delta,mapCollisions_total,bananaHitsRecived_total,shellHitsRecived_Total,bananasUsed_total,shellsUsed_total,mushroomsUsed_total,starsUsed_total";
            WriteTextWithRetry(_csvPath, header + Environment.NewLine, append: false);

            if (logCsvPathOnCreate)
            {
                Debug.Log($"Player lap data CSV: {_csvPath}", this);
            }
        }

        private int GetCurrentPosition()
        {
            if (checkpointTracker != null && checkpointTracker.HasFinishedRace && checkpointTracker.FinishPlacement > 0)
            {
                return checkpointTracker.FinishPlacement;
            }

            positionManager ??= FindFirstObjectByType<PositionManager>();
            if (positionManager == null || checkpointTracker == null)
            {
                return 0;
            }

            positionManager.RefreshPositions();
            return positionManager.GetPosition(checkpointTracker);
        }

        private bool IsMapCollision(Collider other)
        {
            if (other.GetComponentInParent<KartController>() != null)
            {
                return false;
            }

            if (other.GetComponentInParent<PowerUpHazardBase>() != null)
            {
                return false;
            }

            if (HasConfiguredMapTag(other.transform) || HasConfiguredMapLayer(other.transform))
            {
                return true;
            }

            return countStaticUntaggedCollisionsAsMap && other.attachedRigidbody == null && !other.isTrigger;
        }

        private bool HasConfiguredMapTag(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                for (var index = 0; index < mapCollisionTags.Length; index++)
                {
                    var mapTag = mapCollisionTags[index];
                    if (!string.IsNullOrWhiteSpace(mapTag) && current.CompareTag(mapTag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasConfiguredMapLayer(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                var layerName = LayerMask.LayerToName(current.gameObject.layer);
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    continue;
                }

                for (var index = 0; index < mapCollisionLayerNames.Length; index++)
                {
                    if (string.Equals(layerName, mapCollisionLayerNames[index], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPlayerPowerUpController(KartPowerUpController sourceController)
        {
            if (sourceController == null)
            {
                return false;
            }

            if (sourceController == powerUpController)
            {
                return true;
            }

            if (sourceController.KartController != null)
            {
                return sourceController.KartController == kartController;
            }

            if (sourceController.CheckpointTracker != null)
            {
                return sourceController.CheckpointTracker == checkpointTracker;
            }

            var sourceKart = sourceController.GetComponentInParent<KartController>();
            return sourceKart != null && sourceKart == kartController;
        }

        private bool ShouldRecordRuntimeEvent()
        {
            return _isRaceRecording && ShouldRecordThisKart();
        }

        private bool ShouldRecordThisKart()
        {
            return checkpointTracker != null
                && kartController != null
                && (!recordOnlyPlayer || checkpointTracker.IsPlayer);
        }

        private void ResetLapCounters()
        {
            _mapCollisions = 0;
            _bananaCollisions = 0;
            _shellCollisions = 0;
            _bananasThrown = 0;
            _shellsThrown = 0;
            _mushroomsUsed = 0;
            _starsUsed = 0;
        }

        private void CacheReferences()
        {
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInChildren<KartPowerUpController>(true);
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            raceManager ??= RaceManager.Instance;
            raceManager ??= FindFirstObjectByType<RaceManager>();
            positionManager ??= FindFirstObjectByType<PositionManager>();
        }

        private void SubscribeToEvents()
        {
            if (!_subscribedToPowerUpEvents)
            {
                KartPowerUpController.AnyPowerUpUsed += HandleAnyPowerUpUsed;
                KartPowerUpController.AnyPowerUpHit += HandleAnyPowerUpHit;
                _subscribedToPowerUpEvents = true;
            }

            if (_subscribedCheckpointTracker != checkpointTracker)
            {
                if (_subscribedCheckpointTracker != null)
                {
                    _subscribedCheckpointTracker.LapCompleted -= HandleLapCompleted;
                }

                _subscribedCheckpointTracker = checkpointTracker;

                if (_subscribedCheckpointTracker != null)
                {
                    _subscribedCheckpointTracker.LapCompleted += HandleLapCompleted;
                }
            }

            if (_subscribedRaceManager != raceManager)
            {
                if (_subscribedRaceManager != null)
                {
                    _subscribedRaceManager.RaceStateChanged -= HandleRaceStateChanged;
                }

                _subscribedRaceManager = raceManager;

                if (_subscribedRaceManager != null)
                {
                    _subscribedRaceManager.RaceStateChanged += HandleRaceStateChanged;
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_subscribedToPowerUpEvents)
            {
                KartPowerUpController.AnyPowerUpUsed -= HandleAnyPowerUpUsed;
                KartPowerUpController.AnyPowerUpHit -= HandleAnyPowerUpHit;
                _subscribedToPowerUpEvents = false;
            }

            if (_subscribedCheckpointTracker != null)
            {
                _subscribedCheckpointTracker.LapCompleted -= HandleLapCompleted;
                _subscribedCheckpointTracker = null;
            }

            if (_subscribedRaceManager != null)
            {
                _subscribedRaceManager.RaceStateChanged -= HandleRaceStateChanged;
                _subscribedRaceManager = null;
            }
        }

        private void SyncDebugState()
        {
            debugCsvPath = _csvPath;
            debugCurrentLapSeconds = _isRaceRecording ? Mathf.Max(0f, Time.time - _lapStartTime) : 0f;
            debugMapCollisions = _mapCollisions;
            debugBananaCollisions = _bananaCollisions;
            debugShellCollisions = _shellCollisions;
            debugBananasThrown = _bananasThrown;
            debugShellsThrown = _shellsThrown;
            debugMushroomsUsed = _mushroomsUsed;
            debugStarsUsed = _starsUsed;
        }


        private static bool WriteTextWithRetry(string path, string contents, bool append)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var attempts = 5;
            var delayMilliseconds = 25;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    using var fileStream = new FileStream(
                        path,
                        append ? FileMode.OpenOrCreate : FileMode.Create,
                        FileAccess.Write,
                        FileShare.ReadWrite);

                    if (append)
                    {
                        fileStream.Seek(0, SeekOrigin.End);
                    }

                    using var writer = new StreamWriter(fileStream);
                    writer.Write(contents);
                    writer.Flush();
                    return true;
                }
                catch (IOException) when (attempt < attempts)
                {
                    Thread.Sleep(delayMilliseconds);
                    delayMilliseconds *= 2;
                }
                catch (UnauthorizedAccessException) when (attempt < attempts)
                {
                    Thread.Sleep(delayMilliseconds);
                    delayMilliseconds *= 2;
                }
            }

            Debug.LogWarning($"No se pudo escribir el CSV de vueltas del jugador: {path}");
            return false;
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            for (var index = 0; index < invalidChars.Length; index++)
            {
                value = value.Replace(invalidChars[index], '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "Scene" : value;
        }
    }
}
