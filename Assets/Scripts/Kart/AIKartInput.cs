using KartGame.Core;
using UnityEngine;

namespace KartGame.Kart
{
    /*
     * Script: AIKartInput.cs
     * Purpose: Provides a simple checkpoint-following bot controller that keeps karts moving around the track until ML-Agents replaces it.
     * Attach To: AI kart root GameObject.
     * Required Components: KartController, CheckpointTracker.
     * Dependencies: TrackData and checkpoint transforms configured through CheckpointTracker.
     * Inspector Setup: Tune corner speed and obstacle avoidance distances based on track width and kart top speed.
     */
    [RequireComponent(typeof(KartController))]
    [RequireComponent(typeof(CheckpointTracker))]
    public class AIKartInput : MonoBehaviour
    {
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private float steeringSensitivity = 1.8f;
        [SerializeField] private float maxTargetAngle = 55f;
        [SerializeField] private float cornerAcceleration = 0.6f;
        [SerializeField] private float brakeAngle = 70f;
        [SerializeField] private float lookAheadDistance = 5f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float obstacleRayDistance = 5f;
        [SerializeField] private float obstacleAvoidanceStrength = 0.75f;

        private void Awake()
        {
            kartController ??= GetComponent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
        }

        private void Update()
        {
            if (kartController == null || checkpointTracker == null)
            {
                return;
            }

            var nextCheckpoint = checkpointTracker.NextCheckpoint;
            if (nextCheckpoint == null)
            {
                kartController.SetInput(0f, 0f, 1f);
                return;
            }

            var targetPoint = nextCheckpoint.position + nextCheckpoint.forward * lookAheadDistance;
            var localTarget = transform.InverseTransformPoint(targetPoint);
            var targetAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            var steering = Mathf.Clamp(targetAngle / Mathf.Max(1f, maxTargetAngle), -1f, 1f) * steeringSensitivity;

            var obstacleAvoidance = GetObstacleAvoidanceSteering();
            steering = Mathf.Clamp(steering + obstacleAvoidance, -1f, 1f);

            var absoluteAngle = Mathf.Abs(targetAngle);
            var acceleration = absoluteAngle > brakeAngle ? cornerAcceleration : 1f;
            var brake = absoluteAngle > brakeAngle ? 0.85f : 0f;

            if (Mathf.Abs(obstacleAvoidance) > 0.1f)
            {
                acceleration = Mathf.Min(acceleration, 0.7f);
                brake = Mathf.Max(brake, 0.25f);
            }

            kartController.SetInput(acceleration, steering, brake);
        }

        private float GetObstacleAvoidanceSteering()
        {
            var origin = transform.position + Vector3.up * 0.45f;
            var leftOrigin = origin - transform.right * 0.55f;
            var rightOrigin = origin + transform.right * 0.55f;

            var leftBlocked = Physics.Raycast(leftOrigin, transform.forward, obstacleRayDistance, obstacleMask, QueryTriggerInteraction.Ignore);
            var rightBlocked = Physics.Raycast(rightOrigin, transform.forward, obstacleRayDistance, obstacleMask, QueryTriggerInteraction.Ignore);

            if (leftBlocked == rightBlocked)
            {
                return 0f;
            }

            return leftBlocked ? obstacleAvoidanceStrength : -obstacleAvoidanceStrength;
        }
    }
}
