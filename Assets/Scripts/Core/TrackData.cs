using System;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: TrackData.cs
     * Purpose: Stores track references such as checkpoints, spawn points and recovery points for a race scene.
     * Attach To: TrackRoot GameObject.
     * Required Components: None.
     * Dependencies: Checkpoint, RaceManager, CheckpointTracker.
     * Inspector Setup: Place child containers named Checkpoints, SpawnPoints, PowerUpBoxes and RespawnPoints under TrackRoot, then run Sync Child Collections.
     */
    public class TrackData : MonoBehaviour
    {
        [SerializeField] private Transform[] checkpoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] powerUpBoxes = Array.Empty<Transform>();
        [SerializeField] private Transform[] respawnPoints = Array.Empty<Transform>();
        [SerializeField] private int lapsToWin = 3;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool closeCheckpointLoopGizmo = true;
        [SerializeField] private Color checkpointGizmoColor = new Color(1f, 0.78f, 0.2f, 0.9f);

        public int LapsToWin => Mathf.Max(1, lapsToWin);
        public int CheckpointCount => checkpoints?.Length ?? 0;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;
        public Transform[] Checkpoints => checkpoints;
        public Transform[] SpawnPoints => spawnPoints;
        public Transform[] PowerUpBoxes => powerUpBoxes;
        public Transform[] RespawnPoints => respawnPoints;

        [ContextMenu("Sync Child Collections")]
        public void SyncChildCollections()
        {
            checkpoints = CollectDirectChildren("Checkpoints");
            spawnPoints = CollectDirectChildren("SpawnPoints");
            powerUpBoxes = CollectDirectChildren("PowerUpBoxes");
            respawnPoints = CollectDirectChildren("RespawnPoints");
        }

        public void SetLapsToWin(int value)
        {
            lapsToWin = Mathf.Max(1, value);
        }

        public Transform GetCheckpoint(int index)
        {
            if (checkpoints == null || checkpoints.Length == 0)
            {
                return null;
            }

            var clampedIndex = Mathf.Clamp(index, 0, checkpoints.Length - 1);
            return checkpoints[clampedIndex];
        }

        public Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            var clampedIndex = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[clampedIndex];
        }

        public bool TryGetRecoveryPose(Vector3 worldPosition, out Vector3 recoveryPosition, out Quaternion recoveryRotation)
        {
            var candidates = respawnPoints != null && respawnPoints.Length > 0
                ? respawnPoints
                : checkpoints != null && checkpoints.Length > 0
                    ? checkpoints
                    : spawnPoints;

            if (candidates == null || candidates.Length == 0)
            {
                recoveryPosition = transform.position;
                recoveryRotation = transform.rotation;
                return false;
            }

            var closestDistance = float.MaxValue;
            Transform closest = candidates[0];

            foreach (var candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                var distance = (candidate.position - worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            recoveryPosition = closest.position;
            recoveryRotation = closest.rotation;
            return true;
        }

        private Transform[] CollectDirectChildren(string rootName)
        {
            var root = transform.Find(rootName);
            if (root == null)
            {
                return Array.Empty<Transform>();
            }

            var collection = new Transform[root.childCount];
            for (var index = 0; index < root.childCount; index++)
            {
                collection[index] = root.GetChild(index);
            }

            return collection;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || checkpoints == null || checkpoints.Length == 0)
            {
                return;
            }

            Gizmos.color = checkpointGizmoColor;

            for (var index = 0; index < checkpoints.Length; index++)
            {
                var current = checkpoints[index];
                if (current == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(current.position, 0.65f);

                Transform next = null;
                if (index < checkpoints.Length - 1)
                {
                    next = checkpoints[index + 1];
                }
                else if (closeCheckpointLoopGizmo && checkpoints.Length > 1)
                {
                    next = checkpoints[0];
                }

                if (next != null)
                {
                    Gizmos.DrawLine(current.position, next.position);
                }
            }
        }
    }
}
