using System;
using System.Collections.Generic;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: PositionManager.cs
     * Purpose: Calculates live race positions using completed laps, last checkpoint and distance to the next checkpoint.
     * Attach To: RaceSystems GameObject.
     * Required Components: None.
     * Dependencies: TrackData, CheckpointTracker, LapManager.
     * Inspector Setup: Assign TrackData if you want to override RaceManager auto-wiring and tune refresh rate for HUD responsiveness.
     */
    public class PositionManager : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private float refreshInterval = 0.1f;
        [SerializeField] private List<CheckpointTracker> registeredRacers = new List<CheckpointTracker>();

        private readonly Dictionary<CheckpointTracker, int> _currentPositions = new Dictionary<CheckpointTracker, int>();
        private float _nextRefreshTime;

        public int RacerCount => registeredRacers.Count;

        public event Action PositionsUpdated;

        public void SetTrackData(TrackData value)
        {
            trackData = value;
        }

        public void ResetRacers(IReadOnlyList<CheckpointTracker> racers)
        {
            registeredRacers.Clear();
            _currentPositions.Clear();

            if (racers == null)
            {
                return;
            }

            for (var index = 0; index < racers.Count; index++)
            {
                var racer = racers[index];
                if (racer != null)
                {
                    registeredRacers.Add(racer);
                }
            }

            RefreshPositions();
        }

        public int GetPosition(CheckpointTracker tracker)
        {
            return tracker != null && _currentPositions.TryGetValue(tracker, out var position)
                ? position
                : 0;
        }

        public CheckpointTracker GetRacerAtPosition(int position)
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var racer = registeredRacers[index];
                if (racer != null && GetPosition(racer) == position)
                {
                    return racer;
                }
            }

            return null;
        }

        public void RefreshPositions()
        {
            registeredRacers.RemoveAll(racer => racer == null);
            registeredRacers.Sort(CompareRacers);

            for (var index = 0; index < registeredRacers.Count; index++)
            {
                _currentPositions[registeredRacers[index]] = index + 1;
            }

            PositionsUpdated?.Invoke();
        }

        private void Update()
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);
            RefreshPositions();
        }

        private int CompareRacers(CheckpointTracker left, CheckpointTracker right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            if (left.HasFinishedRace || right.HasFinishedRace)
            {
                if (left.HasFinishedRace && right.HasFinishedRace)
                {
                    return left.FinishPlacement.CompareTo(right.FinishPlacement);
                }

                return left.HasFinishedRace ? -1 : 1;
            }

            var lapCompare = right.CompletedLaps.CompareTo(left.CompletedLaps);
            if (lapCompare != 0)
            {
                return lapCompare;
            }

            var checkpointCompare = right.LastPassedCheckpointIndex.CompareTo(left.LastPassedCheckpointIndex);
            if (checkpointCompare != 0)
            {
                return checkpointCompare;
            }

            var distanceCompare = left.DistanceToNextCheckpoint.CompareTo(right.DistanceToNextCheckpoint);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            if (trackData != null && trackData.CheckpointCount == 0)
            {
                return 0;
            }

            return string.CompareOrdinal(left.name, right.name);
        }
    }
}
