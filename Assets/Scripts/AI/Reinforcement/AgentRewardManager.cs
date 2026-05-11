using UnityEngine;

namespace KartGame.AI.Reinforcement
{
    /*
     * Script: AgentRewardManager.cs
     * Purpose: Stores and evaluates the reward shaping used by the kart reinforcement learning agent.
     * Attach To: The same GameObject as KartAgent or a dedicated training root object.
     * Required Components: None.
     * Dependencies: KartAgent.
     * Inspector Setup: Tune checkpoint, progress and collision penalties based on how aggressively or safely you want the agent to learn.
     */
    public class AgentRewardManager : MonoBehaviour
    {
        [Header("Positive Rewards")]
        [SerializeField] private float checkpointReward = 1f; //1
        [SerializeField] private float forwardProgressRewardScale = 0f; //0,03

        [Header("Negative Rewards")]
        [SerializeField] private float backwardProgressPenaltyScale = 0f; //0,02
        [SerializeField] private float idlePenaltyPerSecond = 0f; //0,01
        [SerializeField] private float wrongDirectionPenaltyPerSecond = 0f; //0.0125
        [SerializeField] private float wrongCheckpointPenalty = 0f; //0,15
        [SerializeField] private float wallCollisionPenalty = 0f; //0,035
        [SerializeField] private float wallContactPenaltyPerSecond = 0f; //0.02
        [SerializeField] private float offTrackPenalty = 0f; //0.05
        [SerializeField] private float outOfBoundsPenalty = 1f; //1
        [SerializeField] private float stepPenaltyPerDecision = 0f; //0.005
        [SerializeField] private float episodeTimeoutPenalty = 0f; //0.25

        public float CheckpointReward => checkpointReward;
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
