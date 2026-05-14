using System;
using KartGame.Core;
using UnityEngine;

namespace KartGame.Kart
{
    /*
     * Script: CheckpointTracker.cs
     * Purpose: Stores per-kart progress through checkpoints, laps and respawn state for positioning and race flow.
     * Attach To: Player and AI kart root GameObjects.
     * Required Components: None, but KartController is strongly recommended.
     * Dependencies: TrackData, Checkpoint, KartController, LapManager, PositionManager.
     * Inspector Setup: Assign TrackData directly or let RaceManager inject it, then enable auto respawn if you want stuck karts to recover automatically.
     */
    public class CheckpointTracker : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private KartController kartController;
        [SerializeField] private bool isPlayer;
        [SerializeField] private bool autoRespawnIfStuck = true;
        [SerializeField] private bool allowPlayerAutoRespawn;
        [SerializeField] private bool respawnAtInitialSpawnPoint = true;
        [SerializeField] private bool resetProgressOnInitialSpawnRespawn = true;
        [SerializeField] private bool logAutoRespawns = true;
        [SerializeField] private float stuckSpeedThreshold = 0.9f;
        [SerializeField] private float secondsBeforeAutoRespawn = 4f;
        [SerializeField] private float respawnLift = 0.35f;

        [Header("Runtime Debug")]
        public int debugNextCheckpointIndex;
        public int debugLastPassedCheckpointIndex = -1;
        public int debugCompletedLaps;

        private float _stuckTimer;
        private bool _hasInitialSpawnPose;
        private Vector3 _initialSpawnPosition;
        private Quaternion _initialSpawnRotation;
        private Transform _initialSpawnReference;
        private Transform _lastRecoveryReference;

        public int CompletedLaps { get; private set; }
        public int LastPassedCheckpointIndex { get; private set; } = -1;
        public int NextCheckpointIndex { get; private set; }
        public bool HasFinishedRace { get; private set; }
        public int FinishPlacement { get; private set; }
        public bool IsPlayer => isPlayer;
        public TrackData TrackData => trackData;
        public Transform NextCheckpoint => trackData != null && trackData.CheckpointCount > 0 ? trackData.GetCheckpoint(NextCheckpointIndex) : null;
        public float DistanceToNextCheckpoint => GetDistanceToNextCheckpoint();

        public event Action<CheckpointTracker, Checkpoint> CheckpointPassed;
        public event Action<CheckpointTracker, int> LapCompleted;
        public event Action<CheckpointTracker> Respawned;

        private void Awake()
        {
            kartController ??= GetComponent<KartController>();
            SyncDebugState();
        }

        private void Update()
        {
            SyncDebugState();

            if (!autoRespawnIfStuck || kartController == null || !kartController.IsControlEnabled || HasFinishedRace)
            {
                _stuckTimer = 0f;
                return;
            }

            if (isPlayer && !allowPlayerAutoRespawn)
            {
                _stuckTimer = 0f;
                return;
            }

            if (GetDistanceToNextCheckpoint() < 4f)
            {
                _stuckTimer = 0f;
                return;
            }

            if (kartController.GetCurrentSpeed() <= stuckSpeedThreshold)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= secondsBeforeAutoRespawn)
                {
                    RespawnToRecoveryPoint();
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        public void SetTrackData(TrackData value)
        {
            trackData = value;
        }

        public void SetRecoveryReference(Transform value)
        {
            _initialSpawnReference = value;
            _lastRecoveryReference = value;

            if (value != null)
            {
                _initialSpawnPosition = value.position + Vector3.up * respawnLift;
                _initialSpawnRotation = value.rotation;
                _hasInitialSpawnPose = true;
            }
        }

        public void SetInitialSpawnPose(Vector3 position, Quaternion rotation)
        {
            _initialSpawnPosition = position;
            _initialSpawnRotation = rotation;
            _hasInitialSpawnPose = true;
        }

        public void SetPlayerFlag(bool value)
        {
            isPlayer = value;

            if (isPlayer)
            {
                allowPlayerAutoRespawn = false;
            }
        }

        public void SetAutoRespawnIfStuck(bool value)
        {
            autoRespawnIfStuck = value;
            if (!autoRespawnIfStuck)
            {
                _stuckTimer = 0f;
            }
        }

        public void InitializeForRace(TrackData assignedTrackData)
        {
            if (assignedTrackData != null)
            {
                trackData = assignedTrackData;
            }

            CompletedLaps = 0;
            LastPassedCheckpointIndex = -1;
            NextCheckpointIndex = 0;
            HasFinishedRace = false;
            FinishPlacement = 0;
            _stuckTimer = 0f;
            _initialSpawnReference ??= trackData != null ? trackData.GetSpawnPoint(0) : null;
            _lastRecoveryReference = _initialSpawnReference;

            if (!_hasInitialSpawnPose && _initialSpawnReference != null)
            {
                _initialSpawnPosition = _initialSpawnReference.position + Vector3.up * respawnLift;
                _initialSpawnRotation = _initialSpawnReference.rotation;
                _hasInitialSpawnPose = true;
            }

            SyncDebugState();
        }

        public bool ProcessCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || HasFinishedRace)
            {
                return false;
            }

            if (trackData == null)
            {
                trackData = checkpoint.TrackData;
            }

            if (trackData == null)
            {
                return false;
            }

            if (checkpoint.TrackData != null && checkpoint.TrackData != trackData)
            {
                return false;
            }

            if (checkpoint.CheckpointIndex != NextCheckpointIndex)
            {
                return false;
            }

            LastPassedCheckpointIndex = checkpoint.CheckpointIndex;
            NextCheckpointIndex = (checkpoint.CheckpointIndex + 1) % Mathf.Max(1, trackData.CheckpointCount);
            _lastRecoveryReference = checkpoint.transform;
            _stuckTimer = 0f;

            if (trackData.CheckpointCount > 0 && checkpoint.CheckpointIndex == trackData.CheckpointCount - 1)
            {
                CompletedLaps++;
                LapCompleted?.Invoke(this, CompletedLaps);
            }

            SyncDebugState();
            CheckpointPassed?.Invoke(this, checkpoint);
            return true;
        }

        public void MarkFinished(int finishPlacement)
        {
            HasFinishedRace = true;
            FinishPlacement = Mathf.Max(1, finishPlacement);
        }

        public void RespawnToRecoveryPoint()
        {
            if (kartController == null)
            {
                return;
            }

            Vector3 position;
            Quaternion rotation;

            if (respawnAtInitialSpawnPoint && _hasInitialSpawnPose)
            {
                if (resetProgressOnInitialSpawnRespawn)
                {
                    CompletedLaps = 0;
                    LastPassedCheckpointIndex = -1;
                    NextCheckpointIndex = 0;
                    HasFinishedRace = false;
                    FinishPlacement = 0;
                    SyncDebugState();
                }

                kartController.ResetKart(_initialSpawnPosition, _initialSpawnRotation);
                _stuckTimer = 0f;
                if (logAutoRespawns)
                {
                    Debug.Log($"AUTO RESPAWN: initial-spawn=({_initialSpawnPosition.x:0.##}, {_initialSpawnPosition.y:0.##}, {_initialSpawnPosition.z:0.##})", this);
                }
                Respawned?.Invoke(this);
                return;
            }

            if (_lastRecoveryReference != null)
            {
                position = _lastRecoveryReference.position;
                rotation = _lastRecoveryReference.rotation;
            }
            else if (trackData != null && trackData.TryGetRecoveryPose(transform.position, out position, out rotation))
            {
            }
            else
            {
                position = transform.position;
                rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            }

            kartController.ResetKart(position + Vector3.up * respawnLift, rotation);
            _stuckTimer = 0f;
            if (logAutoRespawns)
            {
                var respawnPosition = position + Vector3.up * respawnLift;
                Debug.Log($"AUTO RESPAWN: recovery=({respawnPosition.x:0.##}, {respawnPosition.y:0.##}, {respawnPosition.z:0.##})", this);
            }
            Respawned?.Invoke(this);
        }

        private float GetDistanceToNextCheckpoint()
        {
            var nextCheckpoint = NextCheckpoint;
            return nextCheckpoint == null ? float.MaxValue : Vector3.Distance(transform.position, nextCheckpoint.position);
        }

        private void SyncDebugState()
        {
            debugNextCheckpointIndex = NextCheckpointIndex;
            debugLastPassedCheckpointIndex = LastPassedCheckpointIndex;
            debugCompletedLaps = CompletedLaps;
        }
    }
}
