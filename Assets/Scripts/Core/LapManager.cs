using System;
using System.Collections.Generic;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: LapManager.cs
     * Purpose: Tracks lap completion and finish order for all racers in the current race.
     * Attach To: RaceSystems GameObject.
     * Required Components: None.
     * Dependencies: TrackData, CheckpointTracker, RaceManager.
     * Inspector Setup: Assign TrackData if you do not want RaceManager to auto-wire it at runtime.
     */
    public class LapManager : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private List<CheckpointTracker> registeredRacers = new List<CheckpointTracker>();

        private readonly Dictionary<CheckpointTracker, int> _finishPlacements = new Dictionary<CheckpointTracker, int>();

        public event Action<CheckpointTracker, int> RacerFinished;

        public void SetTrackData(TrackData value)
        {
            trackData = value;
        }

        public void ResetRacers(IReadOnlyList<CheckpointTracker> racers)
        {
            UnsubscribeFromRacers();
            registeredRacers.Clear();
            _finishPlacements.Clear();

            if (racers == null)
            {
                return;
            }

            for (var index = 0; index < racers.Count; index++)
            {
                var racer = racers[index];
                if (racer == null)
                {
                    continue;
                }

                registeredRacers.Add(racer);
                racer.LapCompleted += HandleLapCompleted;
            }
        }

        public int GetFinishPlacement(CheckpointTracker tracker)
        {
            return tracker != null && _finishPlacements.TryGetValue(tracker, out var placement)
                ? placement
                : 0;
        }

        private void OnDestroy()
        {
            UnsubscribeFromRacers();
        }

        private void HandleLapCompleted(CheckpointTracker tracker, int completedLaps)
        {
            if (tracker == null || trackData == null || _finishPlacements.ContainsKey(tracker))
            {
                return;
            }

            if (completedLaps < trackData.LapsToWin)
            {
                return;
            }

            var finishPlacement = _finishPlacements.Count + 1;
            _finishPlacements[tracker] = finishPlacement;
            tracker.MarkFinished(finishPlacement);
            RacerFinished?.Invoke(tracker, finishPlacement);
        }

        private void UnsubscribeFromRacers()
        {
            for (var index = 0; index < registeredRacers.Count; index++)
            {
                var racer = registeredRacers[index];
                if (racer == null)
                {
                    continue;
                }

                racer.LapCompleted -= HandleLapCompleted;
            }
        }
    }
}
