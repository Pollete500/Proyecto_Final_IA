using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(KartPowerUpController))]
    public class KartPowerUpBotBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;

        [Header("Decision Timing")]
        [SerializeField] private float decisionInterval = 0.25f;
        [SerializeField] private float minimumSecondsBetweenUses = 1.25f;

        [Header("Enemy Sensing")]
        [SerializeField] private float nearbyEnemyDistance = 14f;
        [SerializeField] private float aheadSenseHalfAngle = 70f;
        [SerializeField] private float behindSenseHalfAngle = 70f;

        [Header("Hazard Sensing")]
        [SerializeField] private float nearbyHazardRadius = 8f;
        [SerializeField] private int nearbyHazardsForStar = 2;

        [Header("Wall Rays")]
        [SerializeField] private LayerMask wallSenseMask = ~0;
        [SerializeField] private float wallSenseStartHeight = 0.75f;
        [SerializeField] private float wallSenseDistance = 8f;
        [SerializeField] private int wallSenseRaysPerDirection = 2;
        [SerializeField] private float wallSenseMaxDegrees = 45f;

        [Header("Straight Section Detection")]
        [SerializeField] private float straightSectionMaxTargetAngle = 12f;
        [SerializeField] private int maxWallHitsForStraightSection = 1;

        [Header("Runtime Debug")]
        public int debugEnemiesAheadClose;
        public int debugEnemiesBehindClose;
        public int debugNearbyHazards;
        public int debugNearbyWallHits;
        public bool debugStraightSection;
        public bool debugHasSuggestedPowerUp;
        public PowerUpType debugSuggestedPowerUp;

        private float _nextDecisionTime;
        private float _nextAllowedUseTime;
        private CheckpointTracker _bestAheadTarget;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void Update()
        {
            CacheReferences();
            if (powerUpController == null || kartController == null || checkpointTracker == null)
            {
                return;
            }

            if (Time.time < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = Time.time + Mathf.Max(0.05f, decisionInterval);
            RefreshContext();

            if (powerUpController.AvailablePowerUpPoints <= 0 || Time.time < _nextAllowedUseTime)
            {
                debugHasSuggestedPowerUp = false;
                return;
            }

            if (RaceManager.Instance != null && !RaceManager.Instance.IsRaceActive())
            {
                return;
            }

            if (!kartController.IsControlEnabled)
            {
                return;
            }

            var suggestedPowerUp = ChoosePowerUp();
            debugHasSuggestedPowerUp = suggestedPowerUp.HasValue;
            if (suggestedPowerUp.HasValue)
            {
                debugSuggestedPowerUp = suggestedPowerUp.Value;
            }

            if (!suggestedPowerUp.HasValue)
            {
                return;
            }

            var used = suggestedPowerUp.Value == PowerUpType.Shell
                ? powerUpController.UsePowerUp(suggestedPowerUp.Value, _bestAheadTarget)
                : powerUpController.UsePowerUp(suggestedPowerUp.Value);

            if (used)
            {
                _nextAllowedUseTime = Time.time + Mathf.Max(0.1f, minimumSecondsBetweenUses);
            }
        }

        private void RefreshContext()
        {
            debugEnemiesAheadClose = 0;
            debugEnemiesBehindClose = 0;
            debugNearbyHazards = CountNearbyHazards();
            debugNearbyWallHits = CountWallHits();
            debugStraightSection = IsStraightSection(debugNearbyWallHits);
            _bestAheadTarget = null;

            var allTrackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var bestAheadDistance = float.MaxValue;

            for (var index = 0; index < allTrackers.Length; index++)
            {
                var otherTracker = allTrackers[index];
                if (otherTracker == null || otherTracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = transform.InverseTransformPoint(otherTracker.transform.position);
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
                            _bestAheadTarget = otherTracker;
                        }
                    }

                    continue;
                }

                var behindAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg);
                if (behindAngle <= behindSenseHalfAngle)
                {
                    debugEnemiesBehindClose++;
                }
            }
        }

        private PowerUpType? ChoosePowerUp()
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

        private int CountNearbyHazards()
        {
            var hits = Physics.OverlapSphere(transform.position, nearbyHazardRadius, ~0, QueryTriggerInteraction.Collide);
            var hazards = new HashSet<PowerUpHazardBase>();

            for (var index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                if (hit == null)
                {
                    continue;
                }

                var hazard = hit.GetComponentInParent<PowerUpHazardBase>();
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
            var hitCount = 0;
            var origin = transform.position + Vector3.up * wallSenseStartHeight;
            var rayCount = Mathf.Max(0, wallSenseRaysPerDirection);
            var stepAngle = rayCount > 0 ? wallSenseMaxDegrees / rayCount : 0f;

            for (var rayIndex = -rayCount; rayIndex <= rayCount; rayIndex++)
            {
                var yaw = stepAngle * rayIndex;
                var direction = Quaternion.Euler(0f, yaw, 0f) * transform.forward;
                if (Physics.Raycast(origin, direction, wallSenseDistance, wallSenseMask, QueryTriggerInteraction.Ignore))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        private bool IsStraightSection(int wallHitCount)
        {
            var nextCheckpoint = checkpointTracker.NextCheckpoint;
            if (nextCheckpoint == null)
            {
                return false;
            }

            var localTarget = transform.InverseTransformPoint(nextCheckpoint.position);
            var targetAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
            return targetAngle <= straightSectionMaxTargetAngle && wallHitCount <= maxWallHitsForStraightSection;
        }

        private void CacheReferences()
        {
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
        }
    }
}
