using KartGame.Core;
using KartGame.Kart;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KartGame.AI.Reinforcement
{
    /*
     * Script: KartAgent.cs
     * Purpose: Trains or runs a kart policy with ML-Agents using checkpoint progress, ray sensors and shaped rewards centered on checkpoint following.
     * Attach To: A training kart root GameObject.
     * Required Components: KartController, CheckpointTracker, Rigidbody, Behavior Parameters.
     * Dependencies: AgentRewardManager, TrainingSceneManager, TrackData, DecisionRequester, optional RayPerceptionSensorComponent3D.
     * Inspector Setup: Pair this with Behavior Parameters, DecisionRequester and ray sensors. Set vector observation size to 13 and use either discrete [3,3] or continuous size 3 actions.
     */
    [RequireComponent(typeof(AgentRewardManager))]
    [RequireComponent(typeof(KartController))]
    [RequireComponent(typeof(CheckpointTracker))]
    [RequireComponent(typeof(Rigidbody))]
    public class KartAgent : Agent
    {
        [Header("References")]
        [SerializeField] private TrainingSceneManager trainingSceneManager;
        [SerializeField] private AgentRewardManager rewardManager;
        [SerializeField] private TrackData trackData;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private Rigidbody kartRigidbody;
        [SerializeField] private PlayerKartInput playerKartInputToDisable;
        [SerializeField] private AIKartInput aiKartInputToDisable;

        [Header("Training Rules")]
        [SerializeField] private bool endEpisodeOnLapCompletion = true;
        [SerializeField] private bool endEpisodeOnOffTrack = true;
        [SerializeField] private bool endEpisodeOnStrongWallCollision;
        [SerializeField] private bool disableCheckpointAutoRespawnDuringTraining = true;
        [SerializeField] private float normalizedDistanceReference = 25f;
        [SerializeField] private float idleSpeedThreshold = 1f;
        [SerializeField] private float reverseActivationSpeedThreshold = 1.5f;
        [SerializeField] private float outOfBoundsHeight = -2f;
        [SerializeField] private float hardCollisionSpeedThreshold = 12f;
        [SerializeField] private string wallTag = "Wall";
        [SerializeField] private string checkpointTag = "Checkpoint";
        [SerializeField] private string offTrackTag = "OffTrack";
        [SerializeField] private bool ignoreCheckpointsInRaySensor;
        [SerializeField, Min(0)] private int visibleCheckpointsAheadInRaySensor = 3;

        [Header("Reward Feedback")]
        [SerializeField] private bool showRewardFlash = true;
        [SerializeField] private bool logRewardEvents = true;
        [SerializeField] private bool logEpisodeResets = true;
        [SerializeField] private Renderer[] rewardFlashRenderers;
        [SerializeField] private Color positiveRewardColor = new Color(0.22f, 0.9f, 0.52f);
        [SerializeField] private Color negativeRewardColor = new Color(0.95f, 0.2f, 0.2f);
        [SerializeField] private float rewardFlashDuration = 0.5f;
        [SerializeField] private float positiveRewardFlashMinimumMagnitude = 0.00005f;
        [SerializeField] private float negativeRewardFlashMinimumMagnitude = 0.01f;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _rewardFlashPropertyBlock;
        private float _lastDistanceToCheckpoint = float.PositiveInfinity;
        private float _rewardFlashTimeRemaining;
        private bool _episodeRunning;
        private bool _isOffTrack;

        private void Awake()
        {
            EnsureRewardFlashResources();
            CacheReferences();
            ApplyRuntimeSetup();
        }

        private void Update()
        {
            UpdateRewardFlash(Time.deltaTime);
        }

        protected override void OnEnable()
        {
            CacheReferences();
            ApplyRuntimeSetup();
            base.OnEnable();

            if (checkpointTracker != null)
            {
                checkpointTracker.CheckpointPassed += HandleCheckpointPassed;
                checkpointTracker.LapCompleted += HandleLapCompleted;
            }

            if (trainingSceneManager != null)
            {
                trainingSceneManager.RegisterAgent(this);
            }
        }

        protected override void OnDisable()
        {
            if (checkpointTracker != null)
            {
                checkpointTracker.CheckpointPassed -= HandleCheckpointPassed;
                checkpointTracker.LapCompleted -= HandleLapCompleted;
            }

            if (trainingSceneManager != null)
            {
                trainingSceneManager.UnregisterAgent(this);
            }

            base.OnDisable();
        }

        private void FixedUpdate()
        {
            CacheReferences();
            if (!_episodeRunning)
            {
                return;
            }

            if (rewardManager == null)
            {
                return;
            }

            if (transform.position.y <= outOfBoundsHeight)
            {
                ApplyAgentReward(-rewardManager.OutOfBoundsPenalty);
                EndEpisode();
            }
        }

        public override void OnEpisodeBegin()
        {
            CacheReferences();

            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            trackData = trainingSceneManager != null && trainingSceneManager.TrackData != null
                ? trainingSceneManager.TrackData
                : trackData != null
                    ? trackData
                    : FindFirstObjectByType<TrackData>();

            checkpointTracker.SetTrackData(trackData);
            checkpointTracker.InitializeForRace(trackData);
            kartController.SetControlEnabled(true);

            var spawnPosition = transform.position + Vector3.up * 0.35f;
            var spawnRotation = transform.rotation;

            if (trainingSceneManager != null)
            {
                trainingSceneManager.TryGetSpawnPose(this, out spawnPosition, out spawnRotation);
            }
            else if (trackData != null)
            {
                var fallbackSpawn = trackData.GetSpawnPoint(0);
                if (fallbackSpawn != null)
                {
                    spawnPosition = fallbackSpawn.position + Vector3.up * 0.35f;
                    spawnRotation = fallbackSpawn.rotation;
                }
            }

            checkpointTracker.SetRecoveryReference(trackData != null ? trackData.GetSpawnPoint(0) : null);
            checkpointTracker.SetInitialSpawnPose(spawnPosition, spawnRotation);
            kartController.ResetKart(spawnPosition, spawnRotation);

            _isOffTrack = false;
            _episodeRunning = true;
            _lastDistanceToCheckpoint = checkpointTracker.DistanceToNextCheckpoint;

            if (logEpisodeResets)
            {
                Debug.Log($"EPISODIO REINICIADO: step={StepCount}, spawn=({spawnPosition.x:0.##}, {spawnPosition.y:0.##}, {spawnPosition.z:0.##})", this);
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            CacheReferences();
            if (!HasRequiredReferences())
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(false);
                sensor.AddObservation(false);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
                return;
            }

            var localVelocity = transform.InverseTransformDirection(kartRigidbody.linearVelocity);
            var maxSpeed = Mathf.Max(0.01f, kartController.MaxSpeed);
            sensor.AddObservation(Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(kartController.GetCurrentSpeed() / maxSpeed, 0f, 2f));

            var nextCheckpoint = checkpointTracker.NextCheckpoint;
            if (nextCheckpoint != null)
            {
                var localTarget = transform.InverseTransformPoint(nextCheckpoint.position);
                var planarTarget = new Vector2(localTarget.x, localTarget.z);
                var planarDirection = planarTarget.sqrMagnitude > 0.001f ? planarTarget.normalized : Vector2.zero;
                sensor.AddObservation(planarDirection.x);
                sensor.AddObservation(planarDirection.y);
                sensor.AddObservation(Mathf.Clamp(planarTarget.magnitude / Mathf.Max(0.01f, normalizedDistanceReference), 0f, 4f));

                var forwardAlignment = Vector3.Dot(transform.forward, nextCheckpoint.forward);
                var targetAlignment = Vector3.Dot(transform.forward, (nextCheckpoint.position - transform.position).normalized);
                sensor.AddObservation(forwardAlignment);
                sensor.AddObservation(targetAlignment);
            }
            else
            {
                sensor.AddObservation(Vector2.zero);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }

            sensor.AddObservation(kartController.IsGrounded);
            sensor.AddObservation(_isOffTrack);
            sensor.AddObservation(trackData != null && trackData.CheckpointCount > 0
                ? (float)checkpointTracker.LastPassedCheckpointIndex / trackData.CheckpointCount
                : 0f);
            sensor.AddObservation(trackData != null ? Mathf.Clamp01((float)checkpointTracker.CompletedLaps / Mathf.Max(1, trackData.LapsToWin)) : 0f);
            sensor.AddObservation(Mathf.Clamp(transform.up.y, -1f, 1f));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            CacheReferences();
            if (!HasRequiredReferences())
            {
                return;
            }

            var acceleration = 0f;
            var steering = 0f;
            var brake = 0f;

            if (actions.DiscreteActions.Length >= 2)
            {
                var moveAction = actions.DiscreteActions[0];
                var steeringAction = actions.DiscreteActions[1];

                switch (moveAction)
                {
                    case 1:
                        acceleration = 1f;
                        break;
                    case 2:
                        if (kartController.GetCurrentSpeed() > reverseActivationSpeedThreshold)
                        {
                            brake = 1f;
                        }
                        else
                        {
                            acceleration = -0.5f;
                        }
                        break;
                }

                switch (steeringAction)
                {
                    case 1:
                        steering = -1f;
                        break;
                    case 2:
                        steering = 1f;
                        break;
                }
            }
            else if (actions.ContinuousActions.Length >= 3)
            {
                steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
                acceleration = Mathf.Clamp01((actions.ContinuousActions[1] + 1f) * 0.5f);
                brake = Mathf.Clamp01((actions.ContinuousActions[2] + 1f) * 0.5f);
            }

            kartController.SetInput(acceleration, steering, brake);

            var currentDistance = checkpointTracker.DistanceToNextCheckpoint;
            ApplyAgentReward(rewardManager.EvaluateProgressReward(_lastDistanceToCheckpoint, currentDistance, normalizedDistanceReference));
            ApplyAgentReward(rewardManager.GetIdlePenalty(kartController.GetCurrentSpeed(), idleSpeedThreshold, Time.fixedDeltaTime));
            ApplyAgentReward(rewardManager.GetStepPenalty());

            var nextCheckpoint = checkpointTracker.NextCheckpoint;
            if (nextCheckpoint != null)
            {
                var directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
                var alignment = Vector3.Dot(transform.forward, directionToCheckpoint);
                ApplyAgentReward(rewardManager.GetWrongDirectionPenalty(alignment, Time.fixedDeltaTime));
            }

            _lastDistanceToCheckpoint = currentDistance;

            if (MaxStep > 0 && StepCount >= MaxStep - 1 && rewardManager != null)
            {
                ApplyAgentReward(-rewardManager.EpisodeTimeoutPenalty);
                EpisodeInterrupted();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (Keyboard.current == null)
            {
                return;
            }

            var acceleratePressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            var brakePressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            var steerLeftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            var steerRightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            if (actionsOut.DiscreteActions.Length >= 2)
            {
                var discreteActions = actionsOut.DiscreteActions;
                discreteActions[0] = acceleratePressed ? 1 : brakePressed ? 2 : 0;
                discreteActions[1] = steerLeftPressed ? 1 : steerRightPressed ? 2 : 0;
                return;
            }

            if (actionsOut.ContinuousActions.Length >= 3)
            {
                var continuousActions = actionsOut.ContinuousActions;
                continuousActions[0] = steerLeftPressed ? -1f : steerRightPressed ? 1f : 0f;
                continuousActions[1] = acceleratePressed ? 1f : -1f;
                continuousActions[2] = brakePressed ? 1f : -1f;
            }
        }

        public void AutoAssignReferences(TrainingSceneManager manager, TrackData assignedTrackData)
        {
            trainingSceneManager = manager != null ? manager : FindFirstObjectByType<TrainingSceneManager>();
            trackData = assignedTrackData != null ? assignedTrackData : FindFirstObjectByType<TrackData>();
            CacheReferences();
            ApplyRuntimeSetup();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_episodeRunning)
            {
                return;
            }

            var checkpoint = other.GetComponent<Checkpoint>();
            if (checkpoint != null)
            {
                if (other.CompareTag(checkpointTag) && checkpoint.CheckpointIndex != checkpointTracker.NextCheckpointIndex)
                {
                    ApplyAgentReward(-rewardManager.WrongCheckpointPenalty);
                }

                return;
            }

            if (other.CompareTag(offTrackTag))
            {
                _isOffTrack = true;
                ApplyAgentReward(-rewardManager.OffTrackPenalty);

                if (endEpisodeOnOffTrack)
                {
                    EndEpisode();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(offTrackTag))
            {
                _isOffTrack = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_episodeRunning || !IsWallCollision(collision.collider))
            {
                return;
            }

            ApplyAgentReward(-rewardManager.WallCollisionPenalty);

            if (endEpisodeOnStrongWallCollision && collision.relativeVelocity.magnitude >= hardCollisionSpeedThreshold)
            {
                EndEpisode();
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!_episodeRunning || !IsWallCollision(collision.collider))
            {
                return;
            }

            ApplyAgentReward(rewardManager.GetWallContactPenalty(Time.fixedDeltaTime));
        }

        private void HandleCheckpointPassed(CheckpointTracker tracker, Checkpoint checkpoint)
        {
            if (tracker != checkpointTracker || rewardManager == null)
            {
                return;
            }

            ApplyAgentReward(rewardManager.CheckpointReward);
            _lastDistanceToCheckpoint = checkpointTracker.DistanceToNextCheckpoint;
        }

        private void HandleLapCompleted(CheckpointTracker tracker, int completedLaps)
        {
            if (tracker != checkpointTracker)
            {
                return;
            }

            if (endEpisodeOnLapCompletion)
            {
                EndEpisode();
            }
        }

        private bool IsWallCollision(Collider other)
        {
            return other != null && other.CompareTag(wallTag);
        }

        private void CacheReferences()
        {
            if (trainingSceneManager == null)
            {
                trainingSceneManager = FindFirstObjectByType<TrainingSceneManager>();
            }

            if (rewardManager == null)
            {
                rewardManager = GetComponent<AgentRewardManager>();
            }

            if (trackData == null)
            {
                trackData = FindFirstObjectByType<TrackData>();
            }

            if (kartController == null)
            {
                kartController = GetComponent<KartController>();
            }

            if (checkpointTracker == null)
            {
                checkpointTracker = GetComponent<CheckpointTracker>();
            }

            if (kartRigidbody == null)
            {
                kartRigidbody = GetComponent<Rigidbody>();
            }

            if (playerKartInputToDisable == null)
            {
                playerKartInputToDisable = GetComponent<PlayerKartInput>();
            }

            if (aiKartInputToDisable == null)
            {
                aiKartInputToDisable = GetComponent<AIKartInput>();
            }

            if (rewardFlashRenderers == null || rewardFlashRenderers.Length == 0)
            {
                rewardFlashRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void ApplyRuntimeSetup()
        {
            if (playerKartInputToDisable != null)
            {
                playerKartInputToDisable.enabled = false;
            }

            if (aiKartInputToDisable != null)
            {
                aiKartInputToDisable.enabled = false;
            }

            if (checkpointTracker != null)
            {
                checkpointTracker.SetPlayerFlag(false);
                checkpointTracker.SetTrackData(trackData);
                checkpointTracker.SetAutoRespawnIfStuck(!disableCheckpointAutoRespawnDuringTraining);
            }

            ConfigureRaySensors();
        }

        private bool HasRequiredReferences()
        {
            if (rewardManager != null && kartController != null && checkpointTracker != null && kartRigidbody != null)
            {
                return true;
            }

            Debug.LogError($"KartAgent on '{name}' is missing required references. RewardManager: {rewardManager != null}, KartController: {kartController != null}, CheckpointTracker: {checkpointTracker != null}, Rigidbody: {kartRigidbody != null}", this);
            return false;
        }

        private void ConfigureRaySensors()
        {
            var raySensors = GetComponentsInChildren<CheckpointAwareRayPerceptionSensorComponent3D>(true);
            foreach (var raySensor in raySensors)
            {
                if (raySensor == null)
                {
                    continue;
                }

                raySensor.CheckpointTracker = checkpointTracker;
                raySensor.IgnorePassedCheckpoints = ignoreCheckpointsInRaySensor;
                raySensor.LimitCheckpointDetectionWindow = ignoreCheckpointsInRaySensor;
                raySensor.AdditionalVisibleCheckpointsAhead = visibleCheckpointsAheadInRaySensor;
            }
        }

        private void ApplyAgentReward(float rewardDelta)
        {
            if (Mathf.Approximately(rewardDelta, 0f))
            {
                return;
            }

            if (logRewardEvents)
            {
                Debug.Log($"RECOMPENSA: {rewardDelta:+0.###;-0.###;0}", this);
            }

            AddReward(rewardDelta);
            TriggerRewardFlash(rewardDelta);
        }

        private void TriggerRewardFlash(float rewardDelta)
        {
            if (!showRewardFlash || rewardFlashDuration <= 0f)
            {
                return;
            }

            var magnitude = Mathf.Abs(rewardDelta);
            if (rewardDelta > 0f)
            {
                if (magnitude < positiveRewardFlashMinimumMagnitude)
                {
                    return;
                }

                ApplyRewardFlashColor(positiveRewardColor);
                _rewardFlashTimeRemaining = rewardFlashDuration;
            }
            else if (rewardDelta < 0f)
            {
                if (magnitude < negativeRewardFlashMinimumMagnitude)
                {
                    return;
                }

                ApplyRewardFlashColor(negativeRewardColor);
                _rewardFlashTimeRemaining = rewardFlashDuration;
            }
        }

        private void UpdateRewardFlash(float deltaTime)
        {
            if (_rewardFlashTimeRemaining <= 0f)
            {
                return;
            }

            _rewardFlashTimeRemaining -= deltaTime;
            if (_rewardFlashTimeRemaining > 0f)
            {
                return;
            }

            ClearRewardFlash();
        }

        private void ApplyRewardFlashColor(Color color)
        {
            EnsureRewardFlashResources();
            if (rewardFlashRenderers == null || rewardFlashRenderers.Length == 0)
            {
                rewardFlashRenderers = GetComponentsInChildren<Renderer>(true);
            }

            foreach (var rewardRenderer in rewardFlashRenderers)
            {
                if (rewardRenderer == null)
                {
                    continue;
                }

                var sharedMaterial = rewardRenderer.sharedMaterial;
                if (sharedMaterial == null)
                {
                    continue;
                }

                _rewardFlashPropertyBlock.Clear();

                if (sharedMaterial.HasProperty(BaseColorPropertyId))
                {
                    _rewardFlashPropertyBlock.SetColor(BaseColorPropertyId, color);
                }
                else if (sharedMaterial.HasProperty(ColorPropertyId))
                {
                    _rewardFlashPropertyBlock.SetColor(ColorPropertyId, color);
                }
                else
                {
                    continue;
                }

                rewardRenderer.SetPropertyBlock(_rewardFlashPropertyBlock);
            }
        }

        private void ClearRewardFlash()
        {
            _rewardFlashTimeRemaining = 0f;

            if (rewardFlashRenderers == null)
            {
                return;
            }

            foreach (var rewardRenderer in rewardFlashRenderers)
            {
                if (rewardRenderer == null)
                {
                    continue;
                }

                rewardRenderer.SetPropertyBlock(null);
            }
        }

        private void EnsureRewardFlashResources()
        {
            if (_rewardFlashPropertyBlock == null)
            {
                _rewardFlashPropertyBlock = new MaterialPropertyBlock();
            }
        }
    }
}
