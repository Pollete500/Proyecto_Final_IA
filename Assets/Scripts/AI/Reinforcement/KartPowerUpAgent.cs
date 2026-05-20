using KartGame.Core;
using KartGame.Kart;
using KartGame.PowerUps;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KartGame.AI.Reinforcement
{
    [RequireComponent(typeof(KartPowerUpController))]
    public class KartPowerUpAgent : Agent
    {
        [Header("References")]
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private CheckpointAwareRayPerceptionSensorComponent3D wallSensor;

        [Header("Episode Rules")]
        [SerializeField] private bool endEpisodeOnLapCompletion = true;
        [SerializeField] private bool endEpisodeOnRespawn;
        [SerializeField] private float episodeTimeoutPenalty = 0.1f;

        [Header("Context Sensing")]
        [SerializeField] private float nearbyEnemyDistance = 14f;
        [SerializeField] private float aheadSenseHalfAngle = 70f;
        [SerializeField] private float behindSenseHalfAngle = 70f;
        [SerializeField] private float nearbyHazardRadius = 8f;
        [SerializeField] private int nearbyHazardsForStar = 2;
        [SerializeField] private float straightSectionMaxTargetAngle = 12f;
        [SerializeField] private int maxWallHitsForStraightSection = 1;

        [Header("Rewards")]
        [SerializeField] private float correctPowerUpChoiceReward = 0.2f;
        [SerializeField] private float wrongPowerUpChoicePenalty = 0.15f;
        [SerializeField] private float missedOpportunityPenalty = 0.05f;
        [SerializeField] private float unnecessaryUsePenalty = 0.05f;
        [SerializeField] private float useWithoutPointsPenalty = 0.1f;
        [SerializeField] private float failedUsePenalty = 0.1f;
        [SerializeField] private float shellHitReward = 1f;
        [SerializeField] private float bananaHitReward = 1f;
        [SerializeField] private float decisionStepPenalty = 0.0005f;

        [Header("Runtime Debug")]
        public int debugEnemiesAheadClose;
        public int debugEnemiesBehindClose;
        public int debugNearbyHazards;
        public int debugNearbyWallHits;
        public bool debugStraightSection;
        public bool debugHasSuggestedPowerUp;
        public PowerUpType debugSuggestedPowerUp;

        private float _nearestAheadDistanceNormalized = 1f;
        private float _nearestBehindDistanceNormalized = 1f;
        private float _nextCheckpointAngleNormalized;
        private float _wallHitRatio;

        private void Awake()
        {
            CacheReferences();
            ConfigureWallSensorDebug();
        }

        private void OnValidate()
        {
            CacheReferences();
            ConfigureWallSensorDebug();
        }

        protected override void OnEnable()
        {
            CacheReferences();
            ConfigureWallSensorDebug();
            base.OnEnable();

            if (powerUpController != null)
            {
                powerUpController.PowerUpHit += HandlePowerUpHit;
            }

            if (checkpointTracker != null)
            {
                checkpointTracker.LapCompleted += HandleLapCompleted;
                checkpointTracker.Respawned += HandleRespawned;
            }
        }

        protected override void OnDisable()
        {
            if (powerUpController != null)
            {
                powerUpController.PowerUpHit -= HandlePowerUpHit;
            }

            if (checkpointTracker != null)
            {
                checkpointTracker.LapCompleted -= HandleLapCompleted;
                checkpointTracker.Respawned -= HandleRespawned;
            }

            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            CacheReferences();
            ResetRuntimeContext();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            CacheReferences();
            RefreshContext();

            sensor.AddObservation(Mathf.Clamp01(powerUpController != null ? powerUpController.AvailablePowerUpPoints / 3f : 0f));
            sensor.AddObservation(kartController != null ? Mathf.Clamp01(kartController.GetCurrentSpeed() / Mathf.Max(0.01f, kartController.MaxSpeed)) : 0f);
            sensor.AddObservation(kartController != null && kartController.IsInvincible);
            sensor.AddObservation(Mathf.Clamp01(debugEnemiesAheadClose / 4f));
            sensor.AddObservation(Mathf.Clamp01(debugEnemiesBehindClose / 4f));
            sensor.AddObservation(_nearestAheadDistanceNormalized);
            sensor.AddObservation(_nearestBehindDistanceNormalized);
            sensor.AddObservation(Mathf.Clamp01(debugNearbyHazards / 4f));
            sensor.AddObservation(_wallHitRatio);
            sensor.AddObservation(debugStraightSection);
            sensor.AddObservation(_nextCheckpointAngleNormalized);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            CacheReferences();
            RefreshContext();

            ApplyReward(-decisionStepPenalty);

            if (MaxStep > 0 && StepCount >= MaxStep - 1)
            {
                ApplyReward(-episodeTimeoutPenalty);
                EpisodeInterrupted();
                return;
            }

            var action = actions.DiscreteActions.Length > 0 ? actions.DiscreteActions[0] : 0;
            var chosenPowerUp = DecodeAction(action);
            var suggestedPowerUp = GetSuggestedPowerUp();

            debugHasSuggestedPowerUp = suggestedPowerUp.HasValue;
            if (suggestedPowerUp.HasValue)
            {
                debugSuggestedPowerUp = suggestedPowerUp.Value;
            }

            if (!chosenPowerUp.HasValue)
            {
                if (powerUpController != null && powerUpController.AvailablePowerUpPoints > 0 && suggestedPowerUp.HasValue)
                {
                    ApplyReward(-missedOpportunityPenalty);
                }

                return;
            }

            if (powerUpController == null || powerUpController.AvailablePowerUpPoints <= 0)
            {
                ApplyReward(-useWithoutPointsPenalty);
                return;
            }

            if (!suggestedPowerUp.HasValue)
            {
                ApplyReward(-unnecessaryUsePenalty);
            }
            else if (suggestedPowerUp.Value == chosenPowerUp.Value)
            {
                ApplyReward(correctPowerUpChoiceReward);
            }
            else
            {
                ApplyReward(-wrongPowerUpChoicePenalty);
            }

            var used = chosenPowerUp.Value == PowerUpType.Shell
                ? powerUpController.UsePowerUp(chosenPowerUp.Value, GetBestAheadTarget())
                : powerUpController.UsePowerUp(chosenPowerUp.Value);

            if (!used)
            {
                ApplyReward(-failedUsePenalty);
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (Keyboard.current == null || actionsOut.DiscreteActions.Length == 0)
            {
                return;
            }

            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0;

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                discreteActions[0] = 1;
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                discreteActions[0] = 2;
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                discreteActions[0] = 3;
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                discreteActions[0] = 4;
            }
        }

        public void NotifyDrivingEpisodeReset()
        {
            ResetRuntimeContext();

            if (StepCount > 0)
            {
                EndEpisode();
            }
        }

        private void HandlePowerUpHit(PowerUpType powerUpType, KartController _)
        {
            switch (powerUpType)
            {
                case PowerUpType.Shell:
                    ApplyReward(shellHitReward);
                    break;
                case PowerUpType.Banana:
                    ApplyReward(bananaHitReward);
                    break;
            }
        }

        private void HandleLapCompleted(CheckpointTracker tracker, int _)
        {
            if (tracker == checkpointTracker && endEpisodeOnLapCompletion)
            {
                EndEpisode();
            }
        }

        private void HandleRespawned(CheckpointTracker tracker)
        {
            if (tracker == checkpointTracker && endEpisodeOnRespawn)
            {
                EndEpisode();
            }
        }

        private void CacheReferences()
        {
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            wallSensor ??= GetComponent<CheckpointAwareRayPerceptionSensorComponent3D>();
            wallSensor ??= GetComponentInChildren<CheckpointAwareRayPerceptionSensorComponent3D>(true);
            wallSensor ??= GetComponentInParent<CheckpointAwareRayPerceptionSensorComponent3D>();
        }

        private void ConfigureWallSensorDebug()
        {
            if (wallSensor == null)
            {
                return;
            }

            wallSensor.ConfigureDebugGizmos(
                new Color(0.15f, 0.85f, 1f, 1f),
                new Color(0.55f, 0.65f, 0.75f, 1f),
                false);
        }

        private void RefreshContext()
        {
            debugEnemiesAheadClose = 0;
            debugEnemiesBehindClose = 0;
            debugNearbyHazards = CountNearbyHazards();
            debugNearbyWallHits = CountWallHits();
            debugStraightSection = IsStraightSection(debugNearbyWallHits);
            _nearestAheadDistanceNormalized = 1f;
            _nearestBehindDistanceNormalized = 1f;
            _nextCheckpointAngleNormalized = 0f;
            _wallHitRatio = GetWallHitRatio(debugNearbyWallHits);

            var bestAheadDistance = float.MaxValue;
            var bestBehindDistance = float.MaxValue;
            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var referenceTransform = kartController != null ? kartController.transform : transform;

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

                if (localTarget.z > 0f)
                {
                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= aheadSenseHalfAngle)
                    {
                        debugEnemiesAheadClose++;
                        if (planarDistance < bestAheadDistance)
                        {
                            bestAheadDistance = planarDistance;
                        }
                    }

                    continue;
                }

                var behindAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg);
                if (behindAngle <= behindSenseHalfAngle)
                {
                    debugEnemiesBehindClose++;
                    if (planarDistance < bestBehindDistance)
                    {
                        bestBehindDistance = planarDistance;
                    }
                }
            }

            if (bestAheadDistance < float.MaxValue)
            {
                _nearestAheadDistanceNormalized = Mathf.Clamp01(bestAheadDistance / Mathf.Max(0.01f, nearbyEnemyDistance));
            }

            if (bestBehindDistance < float.MaxValue)
            {
                _nearestBehindDistanceNormalized = Mathf.Clamp01(bestBehindDistance / Mathf.Max(0.01f, nearbyEnemyDistance));
            }
        }

        private PowerUpType? DecodeAction(int action)
        {
            return action switch
            {
                1 => PowerUpType.Banana,
                2 => PowerUpType.Shell,
                3 => PowerUpType.Mushroom,
                4 => PowerUpType.Star,
                _ => null
            };
        }

        private PowerUpType? GetSuggestedPowerUp()
        {
            if (debugNearbyHazards >= nearbyHazardsForStar)
            {
                return PowerUpType.Star;
            }

            if (debugEnemiesAheadClose > 0)
            {
                return PowerUpType.Shell;
            }

            if (debugEnemiesBehindClose > 0)
            {
                return PowerUpType.Banana;
            }

            if (debugStraightSection)
            {
                return PowerUpType.Mushroom;
            }

            return null;
        }

        private CheckpointTracker GetBestAheadTarget()
        {
            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var referenceTransform = kartController != null ? kartController.transform : transform;
            CheckpointTracker bestTarget = null;
            var bestDistance = float.MaxValue;

            for (var index = 0; index < trackers.Length; index++)
            {
                var tracker = trackers[index];
                if (tracker == null || tracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = referenceTransform.InverseTransformPoint(tracker.transform.position);
                if (localTarget.z <= 0f)
                {
                    continue;
                }

                var planarDistance = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (planarDistance > nearbyEnemyDistance)
                {
                    continue;
                }

                var angle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                if (angle > aheadSenseHalfAngle)
                {
                    continue;
                }

                if (planarDistance < bestDistance)
                {
                    bestDistance = planarDistance;
                    bestTarget = tracker;
                }
            }

            return bestTarget;
        }

        private int CountNearbyHazards()
        {
            var hits = Physics.OverlapSphere(transform.position, nearbyHazardRadius, ~0, QueryTriggerInteraction.Collide);
            var hazards = new HashSet<PowerUpHazardBase>();

            for (var index = 0; index < hits.Length; index++)
            {
                var hazard = hits[index] != null ? hits[index].GetComponentInParent<PowerUpHazardBase>() : null;
                if (hazard == null || hazard.OwnerKart == kartController)
                {
                    continue;
                }

                hazards.Add(hazard);
            }

            return hazards.Count;
        }

        private int CountWallHits()
        {
            var rayOutputs = wallSensor != null ? wallSensor.GetCurrentRayOutputs() : null;
            if (rayOutputs == null || rayOutputs.Length == 0)
            {
                return 0;
            }

            var hitCount = 0;
            for (var rayIndex = 0; rayIndex < rayOutputs.Length; rayIndex++)
            {
                if (rayOutputs[rayIndex].HasHit)
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        private float GetWallHitRatio(int wallHits)
        {
            var totalRays = wallSensor != null
                ? Mathf.Max(1, wallSensor.GetCurrentRayOutputs()?.Length ?? 0)
                : 1;
            return Mathf.Clamp01((float)wallHits / totalRays);
        }

        private bool IsStraightSection(int wallHitCount)
        {
            var nextCheckpoint = checkpointTracker != null ? checkpointTracker.NextCheckpoint : null;
            var referenceTransform = kartController != null ? kartController.transform : transform;
            if (nextCheckpoint == null)
            {
                return false;
            }

            var localTarget = referenceTransform.InverseTransformPoint(nextCheckpoint.position);
            var targetAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
            _nextCheckpointAngleNormalized = Mathf.Clamp01(targetAngle / 90f);
            return targetAngle <= straightSectionMaxTargetAngle && wallHitCount <= maxWallHitsForStraightSection;
        }

        private void ApplyReward(float rewardDelta)
        {
            if (Mathf.Approximately(rewardDelta, 0f))
            {
                return;
            }

            AddReward(rewardDelta);
        }

        private void ResetRuntimeContext()
        {
            debugEnemiesAheadClose = 0;
            debugEnemiesBehindClose = 0;
            debugNearbyHazards = 0;
            debugNearbyWallHits = 0;
            debugStraightSection = false;
            debugHasSuggestedPowerUp = false;
            _nearestAheadDistanceNormalized = 1f;
            _nearestBehindDistanceNormalized = 1f;
            _nextCheckpointAngleNormalized = 0f;
            _wallHitRatio = 0f;
        }
    }
}
