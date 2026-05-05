using System.Collections.Generic;
using KartGame.Core;
using UnityEngine;

namespace KartGame.AI.Reinforcement
{
    /*
     * Script: TrainingSceneManager.cs
     * Purpose: Provides shared training-scene references, randomized spawn poses and agent registration for ML-Agents kart training.
     * Attach To: TrainingSceneManager GameObject in the dedicated training scene.
     * Required Components: None.
     * Dependencies: TrackData, KartAgent.
     * Inspector Setup: Assign TrackData, then tune spawn randomization and optional deterministic spawn cycling for the training layout.
     */
    public class TrainingSceneManager : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private bool autoDiscoverAgents = true;
        [SerializeField] private bool randomizeSpawnPoint = true;
        [SerializeField] private float spawnPositionJitter = 0.75f;
        [SerializeField] private float spawnYawJitter = 8f;
        [SerializeField] private float spawnLift = 0.35f;
        [SerializeField] private List<KartAgent> registeredAgents = new List<KartAgent>();

        public TrackData TrackData => trackData;

        public void SetTrackData(TrackData value)
        {
            trackData = value;
        }

        private void Awake()
        {
            trackData ??= FindFirstObjectByType<TrackData>();

            if (autoDiscoverAgents)
            {
                RefreshAgents();
            }
        }

        [ContextMenu("Refresh Agents")]
        public void RefreshAgents()
        {
            registeredAgents.Clear();
            var agents = FindObjectsByType<KartAgent>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < agents.Length; index++)
            {
                RegisterAgent(agents[index]);
            }
        }

        public void RegisterAgent(KartAgent agent)
        {
            if (agent != null && !registeredAgents.Contains(agent))
            {
                registeredAgents.Add(agent);
            }
        }

        public void UnregisterAgent(KartAgent agent)
        {
            registeredAgents.Remove(agent);
        }

        public bool TryGetSpawnPose(KartAgent agent, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (trackData == null || trackData.SpawnPointCount == 0)
            {
                return false;
            }

            var spawnIndex = GetSpawnIndex(agent);
            var spawnPoint = trackData.GetSpawnPoint(spawnIndex);
            if (spawnPoint == null)
            {
                return false;
            }

            position = spawnPoint.position + Vector3.up * spawnLift;
            rotation = spawnPoint.rotation;

            if (spawnPositionJitter > 0f)
            {
                var planarJitter = Random.insideUnitCircle * spawnPositionJitter;
                position += spawnPoint.right * planarJitter.x + spawnPoint.forward * planarJitter.y;
            }

            if (spawnYawJitter > 0f)
            {
                rotation *= Quaternion.Euler(0f, Random.Range(-spawnYawJitter, spawnYawJitter), 0f);
            }

            return true;
        }

        private int GetSpawnIndex(KartAgent agent)
        {
            if (trackData == null || trackData.SpawnPointCount <= 1)
            {
                return 0;
            }

            if (randomizeSpawnPoint)
            {
                return Random.Range(0, trackData.SpawnPointCount);
            }

            var agentIndex = registeredAgents.IndexOf(agent);
            return Mathf.Clamp(agentIndex, 0, trackData.SpawnPointCount - 1);
        }
    }
}
