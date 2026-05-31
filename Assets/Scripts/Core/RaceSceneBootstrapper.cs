using KartGame.Kart;
using KartGame.UI;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: RaceSceneBootstrapper.cs
     * Purpose: Ensures race scenes have RaceSystems and RaceUI even if the scene was not set up manually.
     * Attach To: Not required. Runs automatically after scene load when a TrackData exists.
     */
    public static class RaceSceneBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapLoadedScene()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var trackData = FindBestTrackData();
            if (trackData == null)
            {
                return;
            }

            EnsureRaceSystems(trackData);

            if (Object.FindFirstObjectByType<PlayerKartInput>() != null)
            {
                EnsureRaceUI();
            }
        }

        private static TrackData FindBestTrackData()
        {
            var trackDatas = Object.FindObjectsByType<TrackData>(FindObjectsSortMode.InstanceID);
            TrackData best = null;
            var bestScore = int.MinValue;

            for (var index = 0; index < trackDatas.Length; index++)
            {
                var candidate = trackDatas[index];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var score = candidate.CheckpointCount * 100 + candidate.SpawnPointCount;
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static void EnsureRaceSystems(TrackData trackData)
        {
            var raceManager = Object.FindFirstObjectByType<RaceManager>();
            var lapManager = Object.FindFirstObjectByType<LapManager>();
            var positionManager = Object.FindFirstObjectByType<PositionManager>();

            var systemsRoot = raceManager != null
                ? raceManager.gameObject
                : lapManager != null
                    ? lapManager.gameObject
                    : positionManager != null
                        ? positionManager.gameObject
                        : GameObject.Find("RaceSystems");

            if (systemsRoot == null)
            {
                systemsRoot = new GameObject("RaceSystems");
            }

            lapManager ??= systemsRoot.GetComponent<LapManager>() ?? systemsRoot.AddComponent<LapManager>();
            positionManager ??= systemsRoot.GetComponent<PositionManager>() ?? systemsRoot.AddComponent<PositionManager>();
            raceManager ??= systemsRoot.GetComponent<RaceManager>() ?? systemsRoot.AddComponent<RaceManager>();

            raceManager.SetTrackData(trackData);
            raceManager.SetLapManager(lapManager);
            raceManager.SetPositionManager(positionManager);
            lapManager.SetTrackData(trackData);
            positionManager.SetTrackData(trackData);
        }

        private static void EnsureRaceUI()
        {
            if (Object.FindFirstObjectByType<RaceUI>() != null)
            {
                return;
            }

            var uiRoot = new GameObject("RaceUI");
            uiRoot.AddComponent<RaceUI>();
        }
    }
}
