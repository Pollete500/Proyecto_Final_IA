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
        [SerializeField] private float stuckSpeedThreshold = 0.9f;
        [SerializeField] private float secondsBeforeAutoRespawn = 4f;
        [SerializeField] private float respawnLift = 0.35f;

        private float _stuckTimer;
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
        }

        private void Update()
        {
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
            _lastRecoveryReference = value;
        }

        public void SetPlayerFlag(bool value)
        {
            isPlayer = value;

            if (isPlayer)
            {
                allowPlayerAutoRespawn = false;
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
            _lastRecoveryReference ??= trackData != null ? trackData.GetSpawnPoint(0) : null;
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
            Respawned?.Invoke(this);
        }

        private float GetDistanceToNextCheckpoint()
        {
            var nextCheckpoint = NextCheckpoint;
            return nextCheckpoint == null ? float.MaxValue : Vector3.Distance(transform.position, nextCheckpoint.position);
        }
    }
}
