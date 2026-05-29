#if UNITY_EDITOR
using System.Collections.Generic;
using KartGame.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace KartGame.EditorTools
{
    public static class UnitySplineCheckpointTools
    {
        private const string SplineRootName = "RoadSpline";
        private const string CheckpointsRootName = "Checkpoints";
        private const float DefaultCheckpointSpacing = 8f;
        private const float DefaultCheckpointHeight = 1.2f;
        private static readonly Vector3 DefaultCheckpointColliderSize = new Vector3(10f, 3f, 2.5f);

        [MenuItem("Tools/Kart Racing/Track Data/Create Unity Spline And Checkpoints")]
        public static void CreateUnitySplineAndCheckpointsMenu()
        {
            var trackData = ResolveTrackData();
            if (trackData == null)
            {
                EditorUtility.DisplayDialog("Unity Spline", "No TrackData found in the current scene.", "OK");
                return;
            }

            var splineContainer = CreateOrReuseSplineContainer(trackData);
            ConfigureDefaultSpline(splineContainer, trackData);
            GenerateCheckpoints(splineContainer, trackData);

            Selection.activeGameObject = splineContainer.gameObject;
        }

        [MenuItem("Tools/Kart Racing/Track Data/Generate Checkpoints From Selected Unity Spline")]
        public static void GenerateCheckpointsFromSelectedSplineMenu()
        {
            var splineContainer = ResolveSplineContainer();
            if (splineContainer == null)
            {
                EditorUtility.DisplayDialog("Unity Spline", "Select a SplineContainer in the Hierarchy first.", "OK");
                return;
            }

            var trackData = splineContainer.GetComponentInParent<TrackData>();
            if (trackData == null)
            {
                EditorUtility.DisplayDialog("Unity Spline", "The selected spline must be under a TrackData object.", "OK");
                return;
            }

            GenerateCheckpoints(splineContainer, trackData);
        }

        [MenuItem("Tools/Kart Racing/Track Data/Generate Checkpoints From Selected Unity Spline", true)]
        private static bool CanGenerateCheckpointsFromSelectedSplineMenu()
        {
            return ResolveSplineContainer() != null;
        }

        private static void ConfigureDefaultSpline(SplineContainer splineContainer, TrackData trackData)
        {
            if (splineContainer == null)
            {
                return;
            }

            var spline = new Spline();
            spline.Closed = true;

            var bounds = CalculateTrackBounds(trackData);
            var center = bounds.center;
            var extents = bounds.extents;
            var horizontalRadius = Mathf.Max(10f, extents.x + 6f);
            var depthRadius = Mathf.Max(10f, extents.z + 6f);
            var height = center.y + 1.2f;

            var worldKnotPositions = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0.88f, 0f, 0.45f),
                new Vector3(0.95f, 0f, -0.3f),
                new Vector3(0f, 0f, -1f),
                new Vector3(-0.95f, 0f, -0.3f),
                new Vector3(-0.88f, 0f, 0.45f)
            };

            var localKnotPositions = new List<float3>(worldKnotPositions.Length);
            for (var index = 0; index < worldKnotPositions.Length; index++)
            {
                var worldPosition = new Vector3(
                    center.x + worldKnotPositions[index].x * horizontalRadius,
                    height,
                    center.z + worldKnotPositions[index].z * depthRadius);

                var localPosition = splineContainer.transform.InverseTransformPoint(worldPosition);
                localKnotPositions.Add(new float3(localPosition.x, localPosition.y, localPosition.z));
            }

            spline.AddRange(localKnotPositions, TangentMode.AutoSmooth);
            splineContainer.Spline = spline;

            EditorUtility.SetDirty(splineContainer);
            EditorUtility.SetDirty(splineContainer.gameObject);
        }

        private static void GenerateCheckpoints(SplineContainer splineContainer, TrackData trackData)
        {
            if (splineContainer == null)
            {
                return;
            }

            if (trackData == null)
            {
                EditorUtility.DisplayDialog("Unity Spline", "The spline must be placed under a TrackData object.", "OK");
                return;
            }

            if (splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                EditorUtility.DisplayDialog("Unity Spline", "Add at least two knots to the spline before generating checkpoints.", "OK");
                return;
            }

            var checkpointsRoot = EnsureChildContainer(trackData.transform, CheckpointsRootName);
            var poses = SampleCheckpointPoses(splineContainer, DefaultCheckpointSpacing);
            if (poses.Count == 0)
            {
                EditorUtility.DisplayDialog("Unity Spline", "Could not sample the spline to generate checkpoints.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Checkpoints From Unity Spline");

            ClearChildren(checkpointsRoot);

            for (var index = 0; index < poses.Count; index++)
            {
                var pose = poses[index];
                var checkpointObject = new GameObject($"Checkpoint_{index:00}");
                Undo.RegisterCreatedObjectUndo(checkpointObject, "Create Spline Checkpoint");
                checkpointObject.transform.SetParent(checkpointsRoot, false);
                checkpointObject.transform.SetPositionAndRotation(
                    pose.Position + Vector3.up * DefaultCheckpointHeight,
                    Quaternion.LookRotation(pose.Forward, Vector3.up));

                var checkpoint = Undo.AddComponent<Checkpoint>(checkpointObject);
                checkpoint.Configure(trackData, index);

                var collider = Undo.AddComponent<BoxCollider>(checkpointObject);
                collider.isTrigger = true;
                collider.size = DefaultCheckpointColliderSize;
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
            EditorUtility.SetDirty(trackData.gameObject);
            Selection.activeGameObject = splineContainer.gameObject;
        }

        private static List<SplineCheckpointPose> SampleCheckpointPoses(SplineContainer splineContainer, float spacing)
        {
            var sampledPoses = new List<SplineCheckpointPose>();
            if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return sampledPoses;
            }

            var length = Mathf.Max(0.01f, splineContainer.CalculateLength());
            var denseStep = Mathf.Clamp(spacing * 0.25f, 0.25f, 2f);
            var denseSegments = Mathf.Max(16, Mathf.CeilToInt(length / denseStep));

            var denseSamples = new List<SplineCheckpointPose>(denseSegments + 1);
            for (var sampleIndex = 0; sampleIndex <= denseSegments; sampleIndex++)
            {
                var t = sampleIndex / (float)denseSegments;
                denseSamples.Add(CreateSamplePose(splineContainer, t));
            }

            var previous = denseSamples[0];
            sampledPoses.Add(previous);

            var travelled = 0f;
            var nextSampleDistance = Mathf.Max(0.5f, spacing);

            for (var sampleIndex = 1; sampleIndex < denseSamples.Count; sampleIndex++)
            {
                var current = denseSamples[sampleIndex];
                var segmentDistance = Vector3.Distance(previous.Position, current.Position);
                if (segmentDistance <= Mathf.Epsilon)
                {
                    previous = current;
                    continue;
                }

                while (travelled + segmentDistance >= nextSampleDistance)
                {
                    var t = Mathf.Clamp01((nextSampleDistance - travelled) / segmentDistance);
                    sampledPoses.Add(LerpPose(previous, current, t));
                    nextSampleDistance += spacing;
                }

                travelled += segmentDistance;
                previous = current;
            }

            if (splineContainer.Spline.Closed && sampledPoses.Count > 1)
            {
                var first = sampledPoses[0];
                var last = sampledPoses[sampledPoses.Count - 1];
                if (Vector3.Distance(first.Position, last.Position) < Mathf.Max(0.5f, spacing * 0.35f))
                {
                    sampledPoses.RemoveAt(sampledPoses.Count - 1);
                }
            }

            return sampledPoses;
        }

        private static SplineCheckpointPose CreateSamplePose(SplineContainer splineContainer, float t)
        {
            var position = (Vector3)splineContainer.EvaluatePosition(t);
            var tangent = (Vector3)splineContainer.EvaluateTangent(t);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                var offsetT = Mathf.Clamp01(t + 0.01f);
                var offsetPosition = (Vector3)splineContainer.EvaluatePosition(offsetT);
                tangent = offsetPosition - position;
            }

            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.forward;
            }

            return new SplineCheckpointPose
            {
                Position = position,
                Forward = tangent.normalized
            };
        }

        private static SplineCheckpointPose LerpPose(SplineCheckpointPose left, SplineCheckpointPose right, float t)
        {
            var forward = Vector3.Slerp(left.Forward, right.Forward, t);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = right.Position - left.Position;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return new SplineCheckpointPose
            {
                Position = Vector3.Lerp(left.Position, right.Position, t),
                Forward = forward.normalized
            };
        }

        private static SplineContainer CreateOrReuseSplineContainer(TrackData trackData)
        {
            var existing = trackData.GetComponentInChildren<SplineContainer>(true);
            if (existing != null)
            {
                return existing;
            }

            var splineObject = new GameObject(SplineRootName);
            Undo.RegisterCreatedObjectUndo(splineObject, "Create Unity Spline");
            splineObject.transform.SetParent(trackData.transform, false);

            var splineContainer = Undo.AddComponent<SplineContainer>(splineObject);
            splineContainer.Spline = new Spline();
            splineContainer.Spline.Closed = true;

            return splineContainer;
        }

        private static SplineContainer ResolveSplineContainer()
        {
            if (Selection.activeGameObject != null)
            {
                var selectedSpline = Selection.activeGameObject.GetComponentInParent<SplineContainer>();
                if (selectedSpline != null)
                {
                    return selectedSpline;
                }
            }

            return Object.FindFirstObjectByType<SplineContainer>();
        }

        private static TrackData ResolveTrackData()
        {
            if (Selection.activeGameObject != null)
            {
                var selectedTrackData = Selection.activeGameObject.GetComponentInParent<TrackData>();
                if (selectedTrackData != null)
                {
                    return selectedTrackData;
                }
            }

            return Object.FindFirstObjectByType<TrackData>();
        }

        private static Transform EnsureChildContainer(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index);
                if (child != null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static Bounds CalculateTrackBounds(TrackData trackData)
        {
            var bounds = new Bounds(trackData != null ? trackData.transform.position : Vector3.zero, Vector3.one * 20f);
            var hasAnyPoint = false;

            if (trackData == null)
            {
                return bounds;
            }

            hasAnyPoint |= EncapsulateTransforms(trackData.Checkpoints, ref bounds);
            hasAnyPoint |= EncapsulateTransforms(trackData.SpawnPoints, ref bounds);
            hasAnyPoint |= EncapsulateTransforms(trackData.RespawnPoints, ref bounds);

            if (!hasAnyPoint)
            {
                bounds = new Bounds(trackData.transform.position, Vector3.one * 20f);
            }

            return bounds;
        }

        private static bool EncapsulateTransforms(Transform[] transforms, ref Bounds outputBounds)
        {
            var hasAny = false;
            if (transforms == null)
            {
                return false;
            }

            for (var index = 0; index < transforms.Length; index++)
            {
                var tr = transforms[index];
                if (tr == null)
                {
                    continue;
                }

                if (!hasAny)
                {
                    outputBounds = new Bounds(tr.position, Vector3.zero);
                    hasAny = true;
                }
                else
                {
                    outputBounds.Encapsulate(tr.position);
                }
            }

            return hasAny;
        }

        private struct SplineCheckpointPose
        {
            public Vector3 Position;
            public Vector3 Forward;
        }
    }
}
#endif
