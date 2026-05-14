using System;
using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Serialization;

namespace KartGame.AI.Reinforcement
{
    [AddComponentMenu("ML Agents/Checkpoint Aware Ray Perception Sensor 3D")]
    public class CheckpointAwareRayPerceptionSensorComponent3D : SensorComponent
    {
        [SerializeField, FormerlySerializedAs("sensorName")]
        private string m_SensorName = "CheckpointAwareRayPerceptionSensor";

        [SerializeField, FormerlySerializedAs("detectableTags")]
        private List<string> m_DetectableTags = new List<string>();

        [SerializeField, FormerlySerializedAs("raysPerDirection")]
        private int m_RaysPerDirection = 3;

        [SerializeField, FormerlySerializedAs("maxRayDegrees")]
        private float m_MaxRayDegrees = 70f;

        [SerializeField, FormerlySerializedAs("sphereCastRadius")]
        private float m_SphereCastRadius = 0.5f;

        [SerializeField, FormerlySerializedAs("rayLength")]
        private float m_RayLength = 20f;

        [SerializeField, FormerlySerializedAs("rayLayerMask")]
        private LayerMask m_RayLayerMask = -5;

        [SerializeField, FormerlySerializedAs("observationStacks")]
        private int m_ObservationStacks = 1;

        [SerializeField]
        private bool m_AlternatingRayOrder = true;

        [SerializeField]
        private bool m_UseBatchedRaycasts;

        [SerializeField, FormerlySerializedAs("startVerticalOffset")]
        private float m_StartVerticalOffset = 0.75f;

        [SerializeField, FormerlySerializedAs("endVerticalOffset")]
        private float m_EndVerticalOffset;

        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private bool ignorePassedCheckpoints = true;
        [SerializeField] private bool limitCheckpointDetectionWindow = true;
        [SerializeField] private int additionalVisibleCheckpointsAhead = 3;

        [Header("Debug Gizmos")]
        [SerializeField] private Color debugRayHitColor = Color.red;
        [SerializeField] private Color debugRayMissColor = Color.white;

        private CheckpointAwareRayPerceptionSensor3D _customSensor;

        public string SensorName
        {
            get => m_SensorName;
            set
            {
                m_SensorName = value;
                UpdateSensor();
            }
        }

        public List<string> DetectableTags
        {
            get => m_DetectableTags;
            set
            {
                m_DetectableTags = value ?? new List<string>();
                UpdateSensor();
            }
        }

        public int RaysPerDirection
        {
            get => m_RaysPerDirection;
            set
            {
                m_RaysPerDirection = Mathf.Max(0, value);
                UpdateSensor();
            }
        }

        public float MaxRayDegrees
        {
            get => m_MaxRayDegrees;
            set
            {
                m_MaxRayDegrees = Mathf.Clamp(value, 0f, 180f);
                UpdateSensor();
            }
        }

        public float SphereCastRadius
        {
            get => m_SphereCastRadius;
            set
            {
                m_SphereCastRadius = Mathf.Max(0f, value);
                UpdateSensor();
            }
        }

        public float RayLength
        {
            get => m_RayLength;
            set
            {
                m_RayLength = Mathf.Max(0.01f, value);
                UpdateSensor();
            }
        }

        public LayerMask RayLayerMask
        {
            get => m_RayLayerMask;
            set
            {
                m_RayLayerMask = value;
                UpdateSensor();
            }
        }

        public int ObservationStacks
        {
            get => m_ObservationStacks;
            set => m_ObservationStacks = Mathf.Max(1, value);
        }

        public bool AlternatingRayOrder
        {
            get => m_AlternatingRayOrder;
            set
            {
                m_AlternatingRayOrder = value;
                UpdateSensor();
            }
        }

        public bool UseBatchedRaycasts
        {
            get => m_UseBatchedRaycasts;
            set
            {
                m_UseBatchedRaycasts = value;
                UpdateSensor();
            }
        }

        public float StartVerticalOffset
        {
            get => m_StartVerticalOffset;
            set
            {
                m_StartVerticalOffset = value;
                UpdateSensor();
            }
        }

        public float EndVerticalOffset
        {
            get => m_EndVerticalOffset;
            set
            {
                m_EndVerticalOffset = value;
                UpdateSensor();
            }
        }

        public CheckpointTracker CheckpointTracker
        {
            get => checkpointTracker;
            set
            {
                checkpointTracker = value;
                _customSensor?.SetCheckpointTracker(checkpointTracker);
            }
        }

        public bool IgnorePassedCheckpoints
        {
            get => ignorePassedCheckpoints;
            set
            {
                ignorePassedCheckpoints = value;
                _customSensor?.SetIgnorePassedCheckpoints(ignorePassedCheckpoints);
            }
        }

        public bool LimitCheckpointDetectionWindow
        {
            get => limitCheckpointDetectionWindow;
            set
            {
                limitCheckpointDetectionWindow = value;
                _customSensor?.SetLimitCheckpointDetectionWindow(limitCheckpointDetectionWindow);
            }
        }

        public int AdditionalVisibleCheckpointsAhead
        {
            get => additionalVisibleCheckpointsAhead;
            set
            {
                additionalVisibleCheckpointsAhead = Mathf.Max(0, value);
                _customSensor?.SetAdditionalVisibleCheckpointsAhead(additionalVisibleCheckpointsAhead);
            }
        }

        private void Awake()
        {
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
        }

        private void OnValidate()
        {
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            UpdateSensor();
        }

        public override ISensor[] CreateSensors()
        {
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();

            _customSensor = new CheckpointAwareRayPerceptionSensor3D(
                m_SensorName,
                GetRayPerceptionInput(),
                checkpointTracker,
                ignorePassedCheckpoints,
                limitCheckpointDetectionWindow,
                additionalVisibleCheckpointsAhead);

            if (m_ObservationStacks > 1)
            {
                var stackingSensor = new StackingSensor(_customSensor, m_ObservationStacks);
                return new ISensor[] { stackingSensor };
            }

            return new ISensor[] { _customSensor };
        }

        private void OnDrawGizmosSelected()
        {
            var rayOutputs = _customSensor?.RayPerceptionOutput?.RayOutputs;
            if (rayOutputs == null || rayOutputs.Length == 0)
            {
                rayOutputs = SimulateRayOutputs();
            }

            if (rayOutputs == null)
            {
                return;
            }

            foreach (var rayOutput in rayOutputs)
            {
                DrawRaycastGizmo(rayOutput);
            }
        }

        public RayPerceptionInput GetRayPerceptionInput()
        {
            var rayAngles = m_AlternatingRayOrder
                ? GetRayAnglesAlternating(m_RaysPerDirection, m_MaxRayDegrees)
                : GetRayAngles(m_RaysPerDirection, m_MaxRayDegrees);

            return new RayPerceptionInput
            {
                RayLength = m_RayLength,
                DetectableTags = m_DetectableTags,
                Angles = rayAngles,
                StartOffset = m_StartVerticalOffset,
                EndOffset = m_EndVerticalOffset,
                CastRadius = m_SphereCastRadius,
                Transform = transform,
                CastType = RayPerceptionCastType.Cast3D,
                LayerMask = m_RayLayerMask,
                UseBatchedRaycasts = false
            };
        }

        private void UpdateSensor()
        {
            _customSensor?.SetRayPerceptionInput(GetRayPerceptionInput());
            _customSensor?.SetCheckpointTracker(checkpointTracker);
            _customSensor?.SetIgnorePassedCheckpoints(ignorePassedCheckpoints);
            _customSensor?.SetLimitCheckpointDetectionWindow(limitCheckpointDetectionWindow);
            _customSensor?.SetAdditionalVisibleCheckpointsAhead(additionalVisibleCheckpointsAhead);
        }

        private RayPerceptionOutput.RayOutput[] SimulateRayOutputs()
        {
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            var input = GetRayPerceptionInput();
            var output = new RayPerceptionOutput.RayOutput[input.Angles.Count];

            for (var rayIndex = 0; rayIndex < input.Angles.Count; rayIndex++)
            {
                output[rayIndex] = CheckpointAwareRayPerceptionSensor3D.PerceiveSingleRay(
                    input,
                    rayIndex,
                    checkpointTracker,
                    ignorePassedCheckpoints,
                    limitCheckpointDetectionWindow,
                    additionalVisibleCheckpointsAhead);
            }

            return output;
        }

        private void DrawRaycastGizmo(RayPerceptionOutput.RayOutput rayOutput)
        {
            var startPositionWorld = rayOutput.StartPositionWorld;
            var endPositionWorld = rayOutput.EndPositionWorld;
            var rayDirection = (endPositionWorld - startPositionWorld) * rayOutput.HitFraction;

            var lerpT = rayOutput.HitFraction * rayOutput.HitFraction;
            Gizmos.color = Color.Lerp(debugRayHitColor, debugRayMissColor, lerpT);
            Gizmos.DrawRay(startPositionWorld, rayDirection);

            if (rayOutput.HasHit)
            {
                Gizmos.DrawWireSphere(startPositionWorld + rayDirection, Mathf.Max(rayOutput.ScaledCastRadius, 0.05f));
            }
        }

        private static float[] GetRayAnglesAlternating(int raysPerDirection, float maxRayDegrees)
        {
            var anglesOut = new float[2 * raysPerDirection + 1];
            if (raysPerDirection <= 0)
            {
                anglesOut[0] = 90f;
                return anglesOut;
            }

            var delta = maxRayDegrees / raysPerDirection;
            anglesOut[0] = 90f;
            for (var index = 0; index < raysPerDirection; index++)
            {
                anglesOut[2 * index + 1] = 90f - (index + 1) * delta;
                anglesOut[2 * index + 2] = 90f + (index + 1) * delta;
            }

            return anglesOut;
        }

        private static float[] GetRayAngles(int raysPerDirection, float maxRayDegrees)
        {
            var anglesOut = new float[2 * raysPerDirection + 1];
            if (raysPerDirection <= 0)
            {
                anglesOut[0] = 90f;
                return anglesOut;
            }

            var delta = maxRayDegrees / raysPerDirection;
            for (var index = 0; index < anglesOut.Length; index++)
            {
                anglesOut[index] = 90f + (index - raysPerDirection) * delta;
            }

            return anglesOut;
        }

        private class CheckpointAwareRayPerceptionSensor3D : ISensor
        {
            private readonly string _name;
            private readonly RayPerceptionOutput _rayPerceptionOutput = new RayPerceptionOutput();
            private CheckpointTracker _checkpointTracker;
            private bool _ignorePassedCheckpoints;
            private bool _limitCheckpointDetectionWindow;
            private int _additionalVisibleCheckpointsAhead;
            private RayPerceptionInput _rayPerceptionInput;
            private float[] _observations;
            private ObservationSpec _observationSpec;

            public RayPerceptionOutput RayPerceptionOutput => _rayPerceptionOutput;

            public CheckpointAwareRayPerceptionSensor3D(
                string name,
                RayPerceptionInput rayPerceptionInput,
                CheckpointTracker checkpointTracker,
                bool ignorePassedCheckpoints,
                bool limitCheckpointDetectionWindow,
                int additionalVisibleCheckpointsAhead)
            {
                _name = name;
                _rayPerceptionInput = rayPerceptionInput;
                _checkpointTracker = checkpointTracker;
                _ignorePassedCheckpoints = ignorePassedCheckpoints;
                _limitCheckpointDetectionWindow = limitCheckpointDetectionWindow;
                _additionalVisibleCheckpointsAhead = Mathf.Max(0, additionalVisibleCheckpointsAhead);
                ResizeObservationBuffer();
            }

            public void SetCheckpointTracker(CheckpointTracker checkpointTracker)
            {
                _checkpointTracker = checkpointTracker;
            }

            public void SetIgnorePassedCheckpoints(bool ignorePassedCheckpoints)
            {
                _ignorePassedCheckpoints = ignorePassedCheckpoints;
            }

            public void SetLimitCheckpointDetectionWindow(bool limitCheckpointDetectionWindow)
            {
                _limitCheckpointDetectionWindow = limitCheckpointDetectionWindow;
            }

            public void SetAdditionalVisibleCheckpointsAhead(int additionalVisibleCheckpointsAhead)
            {
                _additionalVisibleCheckpointsAhead = Mathf.Max(0, additionalVisibleCheckpointsAhead);
            }

            public void SetRayPerceptionInput(RayPerceptionInput rayPerceptionInput)
            {
                _rayPerceptionInput = rayPerceptionInput;
                ResizeObservationBuffer();
            }

            public int Write(ObservationWriter writer)
            {
                Array.Clear(_observations, 0, _observations.Length);

                var numRays = _rayPerceptionInput.Angles.Count;
                var numDetectableTags = _rayPerceptionInput.DetectableTags.Count;
                for (var rayIndex = 0; rayIndex < numRays; rayIndex++)
                {
                    _rayPerceptionOutput.RayOutputs[rayIndex].ToFloatArray(numDetectableTags, rayIndex, _observations);
                }

                writer.AddList(_observations);
                return _observations.Length;
            }

            public void Update()
            {
                var numRays = _rayPerceptionInput.Angles.Count;
                if (_rayPerceptionOutput.RayOutputs == null || _rayPerceptionOutput.RayOutputs.Length != numRays)
                {
                    _rayPerceptionOutput.RayOutputs = new RayPerceptionOutput.RayOutput[numRays];
                }

                for (var rayIndex = 0; rayIndex < numRays; rayIndex++)
                {
                    _rayPerceptionOutput.RayOutputs[rayIndex] = PerceiveSingleRay(
                        _rayPerceptionInput,
                        rayIndex,
                        _checkpointTracker,
                        _ignorePassedCheckpoints,
                        _limitCheckpointDetectionWindow,
                        _additionalVisibleCheckpointsAhead);
                }
            }

            public void Reset()
            {
            }

            public ObservationSpec GetObservationSpec()
            {
                return _observationSpec;
            }

            public string GetName()
            {
                return _name;
            }

            public byte[] GetCompressedObservation()
            {
                return null;
            }

            public CompressionSpec GetCompressionSpec()
            {
                return CompressionSpec.Default();
            }

            private void ResizeObservationBuffer()
            {
                var size = _rayPerceptionInput.OutputSize();
                _observations = new float[size];
                _observationSpec = ObservationSpec.Vector(size);
            }

            public static RayPerceptionOutput.RayOutput PerceiveSingleRay(
                RayPerceptionInput input,
                int rayIndex,
                CheckpointTracker checkpointTracker,
                bool ignorePassedCheckpoints,
                bool limitCheckpointDetectionWindow,
                int additionalVisibleCheckpointsAhead)
            {
                var unscaledRayLength = input.RayLength;
                var unscaledCastRadius = input.CastRadius;

                var extents = input.RayExtents(rayIndex);
                var startPositionWorld = extents.StartPositionWorld;
                var endPositionWorld = extents.EndPositionWorld;

                var rayDirection = endPositionWorld - startPositionWorld;
                var scaledRayLength = rayDirection.magnitude;
                var scaledCastRadius = unscaledRayLength > 0f
                    ? unscaledCastRadius * scaledRayLength / unscaledRayLength
                    : unscaledCastRadius;

                var rayOutput = new RayPerceptionOutput.RayOutput
                {
                    HasHit = false,
                    HitFraction = 1f,
                    HitTaggedObject = false,
                    HitTagIndex = -1,
                    HitGameObject = null,
                    StartPositionWorld = startPositionWorld,
                    EndPositionWorld = endPositionWorld,
                    ScaledCastRadius = scaledCastRadius
                };

                if (scaledRayLength <= 0f)
                {
                    return rayOutput;
                }

                var rayDirectionNormalized = rayDirection.normalized;
                RaycastHit[] hits;

                if (scaledCastRadius > 0f)
                {
                    hits = Physics.SphereCastAll(
                        startPositionWorld,
                        scaledCastRadius,
                        rayDirectionNormalized,
                        scaledRayLength,
                        input.LayerMask,
                        QueryTriggerInteraction.Collide);
                }
                else
                {
                    hits = Physics.RaycastAll(
                        startPositionWorld,
                        rayDirectionNormalized,
                        scaledRayLength,
                        input.LayerMask,
                        QueryTriggerInteraction.Collide);
                }

                if (hits == null || hits.Length == 0)
                {
                    return rayOutput;
                }

                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

                for (var hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    var hit = hits[hitIndex];
                    var hitObject = hit.collider != null ? hit.collider.gameObject : null;
                    if (hitObject == null)
                    {
                        continue;
                    }

                    if (IsSelfHit(hit.collider, input.Transform))
                    {
                        continue;
                    }

                    if (ShouldIgnoreHit(
                        hit.collider,
                        checkpointTracker,
                        ignorePassedCheckpoints,
                        limitCheckpointDetectionWindow,
                        additionalVisibleCheckpointsAhead))
                    {
                        continue;
                    }

                    rayOutput.HasHit = true;
                    rayOutput.HitFraction = scaledRayLength > 0f ? hit.distance / scaledRayLength : 0f;
                    rayOutput.HitGameObject = hitObject;

                    var numTags = input.DetectableTags?.Count ?? 0;
                    for (var tagIndex = 0; tagIndex < numTags; tagIndex++)
                    {
                        var tag = input.DetectableTags[tagIndex];
                        if (!string.IsNullOrEmpty(tag) && hitObject.CompareTag(tag))
                        {
                            rayOutput.HitTaggedObject = true;
                            rayOutput.HitTagIndex = tagIndex;
                            break;
                        }
                    }

                    return rayOutput;
                }

                return rayOutput;
            }

            private static bool IsSelfHit(Collider hitCollider, Transform sensorTransform)
            {
                if (hitCollider == null || sensorTransform == null)
                {
                    return false;
                }

                var sensorRoot = sensorTransform.root;
                return hitCollider.transform == sensorRoot || hitCollider.transform.IsChildOf(sensorRoot);
            }

            private static bool ShouldIgnoreHit(
                Collider hitCollider,
                CheckpointTracker checkpointTracker,
                bool ignorePassedCheckpoints,
                bool limitCheckpointDetectionWindow,
                int additionalVisibleCheckpointsAhead)
            {
                if (hitCollider == null || checkpointTracker == null)
                {
                    return false;
                }

                var checkpoint = hitCollider.GetComponent<Checkpoint>();
                if (checkpoint == null)
                {
                    return false;
                }

                if (checkpointTracker.TrackData != null && checkpoint.TrackData != null && checkpoint.TrackData != checkpointTracker.TrackData)
                {
                    return false;
                }

                var checkpointCount = checkpointTracker.TrackData != null
                    ? checkpointTracker.TrackData.CheckpointCount
                    : 0;

                if (limitCheckpointDetectionWindow && checkpointCount > 0)
                {
                    var targetCheckpointIndex = checkpointTracker.NextCheckpointIndex;
                    var relativeDistance = (checkpoint.CheckpointIndex - targetCheckpointIndex + checkpointCount) % checkpointCount;
                    return relativeDistance > Mathf.Max(0, additionalVisibleCheckpointsAhead);
                }

                if (!ignorePassedCheckpoints)
                {
                    return false;
                }

                if (checkpoint.CheckpointIndex == checkpointTracker.LastPassedCheckpointIndex)
                {
                    return true;
                }

                var nextCheckpointIndex = checkpointTracker.NextCheckpointIndex;
                if (nextCheckpointIndex <= 0)
                {
                    return false;
                }

                return checkpoint.CheckpointIndex < nextCheckpointIndex;
            }
        }
    }
}
