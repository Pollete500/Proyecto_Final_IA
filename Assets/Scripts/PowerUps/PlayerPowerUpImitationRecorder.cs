using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using KartGame.Core;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [DisallowMultipleComponent]
    public class PlayerPowerUpImitationRecorder : MonoBehaviour
    {
        private struct ContextSnapshot
        {
            public float Timestamp;
            public bool Recta;
            public int EnemiesAhead;
            public int EnemiesBehind;
            public int BananasAhead;
            public int ShellsBehind;
        }

        private struct PowerUpRecordRow
        {
            public bool Recta;
            public int EnemiesAhead;
            public int EnemiesBehind;
            public int BananasAhead;
            public int ShellsBehind;
            public string PowerUpTirado;
        }

        [Header("References")]
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private KartController kartController;

        [Header("Recording")]
        [SerializeField] private bool recordOnlyPlayer = true;
        [SerializeField] private bool appendToSessionCsv = true;
        [SerializeField] private bool writeLapCsv = true;
        [SerializeField] private bool autoBuildPlayerModelOnLapCompleted = true;
        [SerializeField] private float sampleInterval = 0.15f;
        [SerializeField] private float reactionDelaySeconds = 0.35f;
        [SerializeField] private string outputFolderRelativePath = "Assets/Data/PlayerPowerUpInteractions";
        [SerializeField] private string sessionFileName = "player_powerup_interactions.csv";
        [SerializeField] private bool logRecording;

        [Header("Context Sensing")]
        [SerializeField] private float nearbyEnemyDistance = 14f;
        [SerializeField] private float aheadSenseHalfAngle = 70f;
        [SerializeField] private float behindSenseHalfAngle = 70f;
        [SerializeField] private float nearbyHazardDistance = 10f;
        [SerializeField] private LayerMask wallSenseMask = ~0;
        [SerializeField] private float wallSenseStartHeight = 0.75f;
        [SerializeField] private float wallSenseDistance = 8f;
        [SerializeField] private int wallSenseRaysPerDirection = 2;
        [SerializeField] private float wallSenseMaxDegrees = 45f;
        [SerializeField] private bool ignoreStraightSectionMaxTargetAngle;
        [SerializeField] private float straightSectionMaxTargetAngle = 12f;
        [SerializeField] private int maxWallHitsForStraightSection = 1;

        [Header("Runtime Debug")]
        public int debugBufferedSnapshots;
        public int debugRecordedRows;
        public int debugCurrentLapRows;
        public int debugCurrentLapIndex;
        public string debugLastRecordedPowerUp;

        private readonly List<ContextSnapshot> _snapshots = new List<ContextSnapshot>();
        private readonly List<PowerUpRecordRow> _currentLapRows = new List<PowerUpRecordRow>();
        private float _nextSampleTime;
        private bool _subscribedToPowerUpEvents;
        private CheckpointTracker _subscribedCheckpointTracker;
        private string _outputFolderPath;
        private string _lapOutputFolderPath;
        private string _sessionCsvPath;
        private string _lastLapCsvPath;
        private string _sessionRunId;
        private bool _initializedForCurrentPlaySession;

        private void Awake()
        {
            CacheReferences();
            EnsureOutputPaths();
            SubscribeToEvents();
            SyncDebugState();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureOutputPaths();
            SubscribeToEvents();
            SyncDebugState();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            _ = FlushLapRows(writeLapFile: false);
            _currentLapRows.Clear();
            _snapshots.Clear();
            _initializedForCurrentPlaySession = false;
            _sessionRunId = null;
            _lastLapCsvPath = null;
            SyncDebugState();
        }

        private void OnValidate()
        {
            CacheReferences();
            EnsureOutputPaths();
            sampleInterval = Mathf.Max(0.05f, sampleInterval);
            reactionDelaySeconds = Mathf.Max(0f, reactionDelaySeconds);
            nearbyEnemyDistance = Mathf.Max(0f, nearbyEnemyDistance);
            nearbyHazardDistance = Mathf.Max(0f, nearbyHazardDistance);
            wallSenseDistance = Mathf.Max(0f, wallSenseDistance);
            wallSenseRaysPerDirection = Mathf.Max(0, wallSenseRaysPerDirection);
            maxWallHitsForStraightSection = Mathf.Max(0, maxWallHitsForStraightSection);
        }

        private void Update()
        {
            CacheReferences();

            if (!ShouldSampleContext())
            {
                return;
            }

            if (Time.unscaledTime < _nextSampleTime)
            {
                return;
            }

            _nextSampleTime = Time.unscaledTime + Mathf.Max(0.05f, sampleInterval);
            _snapshots.Add(CaptureContextSnapshot(Time.unscaledTime));
            TrimSnapshotHistory();
            SyncDebugState();
        }

        private void CacheReferences()
        {
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            if (checkpointTracker == null && powerUpController != null)
            {
                checkpointTracker = powerUpController.CheckpointTracker;
            }

            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            if (kartController == null && powerUpController != null)
            {
                kartController = powerUpController.KartController;
            }

            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
        }

        private void EnsureOutputPaths()
        {
            var relativeFolder = string.IsNullOrWhiteSpace(outputFolderRelativePath)
                ? "Assets/Data/PlayerPowerUpInteractions"
                : outputFolderRelativePath;

            var normalizedRelativeFolder = relativeFolder
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            _outputFolderPath = Path.Combine(Application.dataPath, "..", normalizedRelativeFolder);
            _outputFolderPath = Path.GetFullPath(_outputFolderPath);

            if (Application.isPlaying && (!_initializedForCurrentPlaySession || string.IsNullOrWhiteSpace(_sessionRunId)))
            {
                _sessionRunId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                _initializedForCurrentPlaySession = true;
            }
            else if (string.IsNullOrWhiteSpace(_sessionRunId))
            {
                _sessionRunId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            }

            _sessionCsvPath = Path.Combine(_outputFolderPath, sessionFileName);
            _lapOutputFolderPath = Path.Combine(_outputFolderPath, "Laps", _sessionRunId);

            if (!Directory.Exists(_outputFolderPath))
            {
                Directory.CreateDirectory(_outputFolderPath);
            }

            if (!Directory.Exists(_lapOutputFolderPath))
            {
                Directory.CreateDirectory(_lapOutputFolderPath);
            }
        }

        private void SubscribeToEvents()
        {
            if (!_subscribedToPowerUpEvents)
            {
                KartPowerUpController.AnyPowerUpUsed += HandleAnyPowerUpUsed;
                _subscribedToPowerUpEvents = true;
            }

            if (_subscribedCheckpointTracker == checkpointTracker)
            {
                return;
            }

            if (_subscribedCheckpointTracker != null)
            {
                _subscribedCheckpointTracker.LapCompleted -= HandleLapCompleted;
                _subscribedCheckpointTracker = null;
            }

            if (checkpointTracker != null)
            {
                checkpointTracker.LapCompleted += HandleLapCompleted;
                _subscribedCheckpointTracker = checkpointTracker;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_subscribedToPowerUpEvents)
            {
                KartPowerUpController.AnyPowerUpUsed -= HandleAnyPowerUpUsed;
                _subscribedToPowerUpEvents = false;
            }

            if (_subscribedCheckpointTracker != null)
            {
                _subscribedCheckpointTracker.LapCompleted -= HandleLapCompleted;
                _subscribedCheckpointTracker = null;
            }
        }

        private void HandleAnyPowerUpUsed(KartPowerUpController sourceController, PowerUpType powerUpType)
        {
            if (!ShouldRecordSource(sourceController))
            {
                return;
            }

            var snapshot = SelectSnapshotForTime(Time.unscaledTime - reactionDelaySeconds);
            var row = new PowerUpRecordRow
            {
                Recta = snapshot.Recta,
                EnemiesAhead = snapshot.EnemiesAhead,
                EnemiesBehind = snapshot.EnemiesBehind,
                BananasAhead = snapshot.BananasAhead,
                ShellsBehind = snapshot.ShellsBehind,
                PowerUpTirado = powerUpType.ToString().ToLowerInvariant()
            };

            _currentLapRows.Add(row);
            debugRecordedRows++;
            debugCurrentLapRows = _currentLapRows.Count;
            debugLastRecordedPowerUp = row.PowerUpTirado;

            if (logRecording)
            {
                Debug.Log(
                    $"POWERUP RECORDED: lap={debugCurrentLapIndex} | {row.PowerUpTirado} | recta={row.Recta} | delante={row.EnemiesAhead} | atras={row.EnemiesBehind} | bananasDelante={row.BananasAhead} | conchasAtras={row.ShellsBehind}",
                    this);
            }

            SyncDebugState();
        }

        private void HandleLapCompleted(CheckpointTracker tracker, int completedLap)
        {
            if (tracker == null || tracker != checkpointTracker)
            {
                return;
            }

            debugCurrentLapIndex = completedLap;
            var wroteLapData = FlushLapRows(writeLapFile: writeLapCsv);
            if (writeLapCsv && wroteLapData)
            {
                TryBuildPlayerModelAfterLap(_lastLapCsvPath);
            }
            _currentLapRows.Clear();
            _snapshots.Clear();
            _nextSampleTime = Time.unscaledTime + Mathf.Max(0.05f, sampleInterval);
            SyncDebugState();
        }

        private bool ShouldSampleContext()
        {
            if (powerUpController == null || checkpointTracker == null || kartController == null)
            {
                return false;
            }

            if (recordOnlyPlayer && !checkpointTracker.IsPlayer)
            {
                return false;
            }

            if (checkpointTracker.HasFinishedRace)
            {
                return false;
            }

            if (RaceManager.Instance != null && !RaceManager.Instance.IsRaceActive())
            {
                return false;
            }

            return kartController.IsControlEnabled;
        }

        private bool ShouldRecordSource(KartPowerUpController sourceController)
        {
            if (sourceController == null)
            {
                return false;
            }

            var sourceTracker = sourceController.CheckpointTracker;
            if (sourceTracker == null || !sourceTracker.IsPlayer)
            {
                return false;
            }

            if (recordOnlyPlayer && checkpointTracker != null && sourceTracker != checkpointTracker)
            {
                return false;
            }

            return true;
        }

        private ContextSnapshot CaptureContextSnapshot(float timestamp)
        {
            return new ContextSnapshot
            {
                Timestamp = timestamp,
                Recta = IsStraightSection(),
                EnemiesAhead = CountEnemies(true),
                EnemiesBehind = CountEnemies(false),
                BananasAhead = CountHazards(true, PowerUpType.Banana),
                ShellsBehind = CountHazards(false, PowerUpType.Shell)
            };
        }

        private ContextSnapshot SelectSnapshotForTime(float targetTime)
        {
            if (_snapshots.Count == 0)
            {
                return CaptureContextSnapshot(targetTime);
            }

            for (var index = _snapshots.Count - 1; index >= 0; index--)
            {
                if (_snapshots[index].Timestamp <= targetTime)
                {
                    return _snapshots[index];
                }
            }

            return _snapshots[0];
        }

        private void TrimSnapshotHistory()
        {
            var minimumHistorySeconds = Mathf.Max(1.5f, reactionDelaySeconds + sampleInterval * 2f);
            var thresholdTime = Time.unscaledTime - minimumHistorySeconds;

            while (_snapshots.Count > 0 && _snapshots[0].Timestamp < thresholdTime)
            {
                _snapshots.RemoveAt(0);
            }
        }

        private int CountEnemies(bool ahead)
        {
            if (kartController == null)
            {
                return 0;
            }

            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var referenceTransform = kartController.transform;
            var enemyCount = 0;

            for (var index = 0; index < trackers.Length; index++)
            {
                var otherTracker = trackers[index];
                if (otherTracker == null || otherTracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = referenceTransform.InverseTransformPoint(otherTracker.transform.position);
                var planarDistance = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (planarDistance > nearbyEnemyDistance)
                {
                    continue;
                }

                if (ahead)
                {
                    if (localTarget.z <= 0f)
                    {
                        continue;
                    }

                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= aheadSenseHalfAngle)
                    {
                        enemyCount++;
                    }
                }
                else
                {
                    if (localTarget.z >= 0f)
                    {
                        continue;
                    }

                    var behindAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg);
                    if (behindAngle <= behindSenseHalfAngle)
                    {
                        enemyCount++;
                    }
                }
            }

            return enemyCount;
        }

        private int CountHazards(bool ahead, PowerUpType powerUpType)
        {
            if (kartController == null)
            {
                return 0;
            }

            var referenceTransform = kartController.transform;
            var hazardHits = Physics.OverlapSphere(referenceTransform.position, nearbyHazardDistance, ~0, QueryTriggerInteraction.Collide);
            var seenHazards = new HashSet<PowerUpHazardBase>();
            var count = 0;

            for (var index = 0; index < hazardHits.Length; index++)
            {
                var hit = hazardHits[index];
                var hazard = hit != null ? hit.GetComponentInParent<PowerUpHazardBase>() : null;
                if (hazard == null || hazard.OwnerKart == kartController || hazard.IsIgnoredFor(kartController) || !seenHazards.Add(hazard) || hazard.PowerUpType != powerUpType)
                {
                    continue;
                }

                var localHazard = referenceTransform.InverseTransformPoint(hazard.transform.position);
                if (ahead)
                {
                    if (localHazard.z <= 0f)
                    {
                        continue;
                    }

                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localHazard.x, localHazard.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= aheadSenseHalfAngle)
                    {
                        count++;
                    }
                }
                else
                {
                    if (localHazard.z >= 0f)
                    {
                        continue;
                    }

                    var behindAngle = Mathf.Abs(Mathf.Atan2(localHazard.x, -localHazard.z) * Mathf.Rad2Deg);
                    if (behindAngle <= behindSenseHalfAngle)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private bool IsStraightSection()
        {
            if (checkpointTracker == null || checkpointTracker.NextCheckpoint == null || kartController == null)
            {
                return false;
            }

            var localTarget = kartController.transform.InverseTransformPoint(checkpointTracker.NextCheckpoint.position);
            var targetAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
            var wallHits = CountWallHits();
            var angleMatches = ignoreStraightSectionMaxTargetAngle || targetAngle <= straightSectionMaxTargetAngle;
            return angleMatches && wallHits <= maxWallHitsForStraightSection;
        }

        private int CountWallHits()
        {
            if (kartController == null)
            {
                return 0;
            }

            var hitCount = 0;
            var referenceTransform = kartController.transform;
            var origin = referenceTransform.position + Vector3.up * wallSenseStartHeight;
            var raysPerDirection = Mathf.Max(0, wallSenseRaysPerDirection);
            var stepAngle = raysPerDirection > 0 ? wallSenseMaxDegrees / raysPerDirection : 0f;

            for (var rayIndex = -raysPerDirection; rayIndex <= raysPerDirection; rayIndex++)
            {
                var yaw = stepAngle * rayIndex;
                var direction = Quaternion.Euler(0f, yaw, 0f) * referenceTransform.forward;
                if (Physics.Raycast(origin, direction, wallSenseDistance, wallSenseMask, QueryTriggerInteraction.Ignore))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        private bool FlushLapRows(bool writeLapFile)
        {
            if (_currentLapRows.Count == 0)
            {
                return false;
            }

            EnsureOutputPaths();
            var wroteSomething = false;
            var allWritesSucceeded = true;

            if (appendToSessionCsv)
            {
                wroteSomething = true;
                allWritesSucceeded &= WriteRowsToCsv(_sessionCsvPath, _currentLapRows, append: true);
            }

            if (writeLapFile)
            {
                wroteSomething = true;
                _lastLapCsvPath = Path.Combine(
                    _lapOutputFolderPath,
                    $"player_powerup_interactions_lap_{Mathf.Max(0, debugCurrentLapIndex):000}_{_sessionRunId}.csv");
                allWritesSucceeded &= WriteRowsToCsv(_lastLapCsvPath, _currentLapRows, append: false);
            }
            else
            {
                _lastLapCsvPath = null;
            }

            return wroteSomething && allWritesSucceeded;
        }

        private static bool WriteRowsToCsv(string path, IReadOnlyList<PowerUpRecordRow> rows, bool append)
        {
            if (rows == null || rows.Count == 0)
            {
                return false;
            }

            var builder = new StringBuilder();
            var shouldWriteHeader = !append || !File.Exists(path);
            if (shouldWriteHeader)
            {
                builder.AppendLine("recta,enemigos_delante,enemigos_atras,platanos_delante,conchas_atras,power_up_tirado");
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                builder.Append(row.Recta ? "true" : "false")
                    .Append(',')
                    .Append(row.EnemiesAhead)
                    .Append(',')
                    .Append(row.EnemiesBehind)
                    .Append(',')
                    .Append(row.BananasAhead)
                    .Append(',')
                    .Append(row.ShellsBehind)
                    .Append(',')
                    .Append(row.PowerUpTirado)
                    .AppendLine();
            }

            return WriteTextWithRetry(path, builder.ToString(), append);
        }

        private static bool WriteTextWithRetry(string path, string contents, bool append)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

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

            Debug.LogWarning($"No se pudo escribir el CSV tras varios intentos: {path}");
            return false;
        }

        private void SyncDebugState()
        {
            debugBufferedSnapshots = _snapshots.Count;
            debugCurrentLapRows = _currentLapRows.Count;
        }

        private void TryBuildPlayerModelAfterLap(string lapCsvPath)
        {
            if (!autoBuildPlayerModelOnLapCompleted)
            {
                return;
            }

            if (!Application.isEditor)
            {
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var pythonPath = ResolvePythonExecutable(projectRoot);
            var buildScriptPath = Path.GetFullPath(Path.Combine(projectRoot, "Assets", "Python", "build_player_powerup_behavior_profile.py"));

            if (!File.Exists(buildScriptPath))
            {
                Debug.LogWarning($"No existe el script de entrenamiento del jugador: {buildScriptPath}", this);
                return;
            }

            if (!string.Equals(pythonPath, "python", StringComparison.OrdinalIgnoreCase) && !File.Exists(pythonPath))
            {
                Debug.LogWarning($"No existe el interprete de Python configurado para el entrenamiento: {pythonPath}", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(lapCsvPath) || !File.Exists(lapCsvPath))
            {
                Debug.LogWarning(
                    "No hay CSV de vuelta disponible para entrenar el random forest del jugador. " +
                    "Activa writeLapCsv o revisa que la escritura haya salido bien.",
                    this);
                return;
            }

            Debug.Log(
                $"Lanzando entrenamiento del random forest del jugador con el CSV de la vuelta {Path.GetFileName(lapCsvPath)}.",
                this);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{buildScriptPath}\" --input \"{lapCsvPath}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    Debug.LogWarning("No se pudo lanzar el entrenamiento del random forest del jugador.", this);
                    return;
                }

                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    ApplyLatestPlayerModelToBrains();
                    Debug.Log(
                        $"PLAYER RANDOM FOREST ACTUALIZADO con el dataset de la vuelta {Path.GetFileName(lapCsvPath)}.",
                        this);
                }
                else
                {
                    Debug.LogWarning(
                        $"El entrenamiento del random forest del jugador termino con codigo {process.ExitCode}.",
                        this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Fallo al construir el random forest del jugador: {exception.Message}", this);
            }
        }

        private void ApplyLatestPlayerModelToBrains()
        {
#if UNITY_EDITOR
            const string playerModelAssetPath = "Assets/Data/classifier_rules/ImitarPlayer/powerups_random_forest_player.json";

            UnityEditor.AssetDatabase.Refresh();
            var latestPlayerModel = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(playerModelAssetPath);
            if (latestPlayerModel == null)
            {
                Debug.LogWarning($"No se pudo cargar el modelo del jugador recien entrenado: {playerModelAssetPath}", this);
                return;
            }

            var brains = FindObjectsByType<RandomForestPowerUpBrain>(FindObjectsSortMode.None);
            var updatedBrains = 0;
            for (var index = 0; index < brains.Length; index++)
            {
                var brain = brains[index];
                if (brain == null)
                {
                    continue;
                }

                var rootName = brain.transform.root != null ? brain.transform.root.name : brain.gameObject.name;
                var shouldFollowLatestPlayerModel = brain.FollowLatestPlayerModel
                    || rootName.IndexOf("imitador", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!shouldFollowLatestPlayerModel)
                {
                    continue;
                }

                brain.SetModelReferences(latestPlayerModel, latestPlayerModel);
                UnityEditor.EditorUtility.SetDirty(brain);
                updatedBrains++;
            }

            if (updatedBrains == 0)
            {
                Debug.LogWarning(
                    "No se encontro ningun RandomForestPowerUpBrain de imitacion para actualizar al modelo del jugador.",
                    this);
            }
#endif
        }

        private static string ResolvePythonExecutable(string projectRoot)
        {
            var localVenvPython = Path.GetFullPath(Path.Combine(projectRoot, ".venv", "Scripts", "python.exe"));
            if (File.Exists(localVenvPython))
            {
                return localVenvPython;
            }

            return "python";
        }
    }
}
