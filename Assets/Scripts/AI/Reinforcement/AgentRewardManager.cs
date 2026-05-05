using UnityEngine;

namespace KartGame.AI.Reinforcement
{
    /*
     * Script: AgentRewardManager.cs
     * Purpose: Stores and evaluates the reward shaping used by the kart reinforcement learning agent.
     * Attach To: The same GameObject as KartAgent or a dedicated training root object.
     * Required Components: None.
     * Dependencies: KartAgent.
     * Inspector Setup: Tune checkpoint, lap, progress and collision penalties based on how aggressively or safely you want the agent to learn.
     */
    public class AgentRewardManager : MonoBehaviour
    {
        [Header("Positive Rewards")]
        [SerializeField] private float checkpointReward = 1f;
        [SerializeField] private float lapReward = 5f;
        [SerializeField] private float forwardProgressRewardScale = 0.03f;

        [Header("Negative Rewards")]
        [SerializeField] private float backwardProgressPenaltyScale = 0.02f;
        [SerializeField] private float idlePenaltyPerSecond = 0.01f;
        [SerializeField] private float wrongDirectionPenaltyPerSecond = 0.0125f;
        [SerializeField] private float wrongCheckpointPenalty = 0.15f;
        [SerializeField] private float wallCollisionPenalty = 0.35f;
        [SerializeField] private float wallContactPenaltyPerSecond = 0.02f;
        [SerializeField] private float offTrackPenalty = 0.5f;
        [SerializeField] private float outOfBoundsPenalty = 1f;
        [SerializeField] private float stepPenaltyPerDecision = 0.0005f;
        [SerializeField] private float episodeTimeoutPenalty = 0.25f;

        public float CheckpointReward => checkpointReward;
        public float LapReward => lapReward;
        public float WrongCheckpointPenalty => wrongCheckpointPenalty;
        public float WallCollisionPenalty => wallCollisionPenalty;
        public float OffTrackPenalty => offTrackPenalty;
        public float OutOfBoundsPenalty => outOfBoundsPenalty;
        public float EpisodeTimeoutPenalty => episodeTimeoutPenalty;

        public float EvaluateProgressReward(float previousDistance, float currentDistance, float normalizationDistance)
        {
            if (normalizationDistance <= 0.001f || float.IsInfinity(previousDistance) || float.IsInfinity(currentDistance))
            {
                return 0f;
            }

            var normalizedDelta = Mathf.Clamp((previousDistance - currentDistance) / normalizationDistance, -1f, 1f);
            return normalizedDelta >= 0f
                ? normalizedDelta * forwardProgressRewardScale
                : normalizedDelta * backwardProgressPenaltyScale;
        }

        public float GetIdlePenalty(float currentSpeed, float idleSpeedThreshold, float deltaTime)
        {
            return currentSpeed <= idleSpeedThreshold ? -idlePenaltyPerSecond * deltaTime : 0f;
        }

        public float GetWrongDirectionPenalty(float alignmentToCheckpoint, float deltaTime)
        {
            if (alignmentToCheckpoint >= 0f)
            {
                return 0f;
            }

            return alignmentToCheckpoint * wrongDirectionPenaltyPerSecond * deltaTime;
        }

        public float GetWallContactPenalty(float deltaTime)
        {
            return -wallContactPenaltyPerSecond * deltaTime;
        }

        public float GetStepPenalty()
        {
            return -stepPenaltyPerDecision;
        }
    }
}
