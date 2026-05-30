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
        private const string RoadVisualRootName = "RoadVisual";
        private const string WallVisualRootName = "TrackWalls";
        private const string RoadMaterialPath = "Assets/Generated/UnitySplineRoadBlack.mat";

        [MenuItem("Tools/Kart Racing/Track Data/Create Configured Track Root With Unity Spline")]
        public static void CreateConfiguredTrackRootWithUnitySplineMenu()
        {
            EnsureTagsAndLayers();

            var trackData = ResolveTrackData();
            if (trackData == null)
            {
                var trackRoot = new GameObject("TrackRoot");
                Undo.RegisterCreatedObjectUndo(trackRoot, "Create TrackRoot");
                trackData = Undo.AddComponent<TrackData>(trackRoot);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Configured Track Root With Unity Spline");

            EnsureDefaultTrackHierarchy(trackData);
            ConfigureTrackDataDefaults(trackData);

            var splineContainer = CreateOrReuseSplineContainer(trackData);
            EnsureCheckpointSettings(splineContainer);
            ConfigureDefaultSpline(splineContainer, trackData);
            GenerateRoadVisual(splineContainer, trackData);
            GenerateWallVisual(splineContainer, trackData);
            GenerateCheckpoints(splineContainer, trackData);
            GenerateDefaultSpawnAndRespawnPoints(trackData);

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
            Selection.activeGameObject = trackData.gameObject;
            EditorGUIUtility.PingObject(trackData.gameObject);
        }

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
            EnsureCheckpointSettings(splineContainer);
            ConfigureDefaultSpline(splineContainer, trackData);
            GenerateRoadVisual(splineContainer, trackData);
            GenerateWallVisual(splineContainer, trackData);
            GenerateCheckpoints(splineContainer, trackData);
            GenerateDefaultSpawnAndRespawnPoints(trackData);

            Selection.activeGameObject = splineContainer.gameObject;
        }

        public static void GenerateCheckpointsFromSettings(UnitySplineCheckpointSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var splineContainer = settings.GetComponent<SplineContainer>();
            if (splineContainer == null)
            {
                splineContainer = settings.GetComponentInParent<SplineContainer>();
            }

            var trackData = settings.GetComponentInParent<TrackData>();
            if (splineContainer == null || trackData == null)
            {
                return;
            }

            GenerateCheckpoints(splineContainer, trackData);
        }

        public static void GenerateRoadVisualFromSettings(UnitySplineCheckpointSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var splineContainer = settings.GetComponent<SplineContainer>();
            if (splineContainer == null)
            {
                splineContainer = settings.GetComponentInParent<SplineContainer>();
            }

            var trackData = settings.GetComponentInParent<TrackData>();
            if (splineContainer == null || trackData == null)
            {
                return;
            }

            GenerateRoadVisual(splineContainer, trackData);
        }

        public static void GenerateWallVisualFromSettings(UnitySplineCheckpointSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var splineContainer = settings.GetComponent<SplineContainer>();
            if (splineContainer == null)
            {
                splineContainer = settings.GetComponentInParent<SplineContainer>();
            }

            var trackData = settings.GetComponentInParent<TrackData>();
            if (splineContainer == null || trackData == null)
            {
                return;
            }

            GenerateWallVisual(splineContainer, trackData);
        }

        [MenuItem("Tools/Kart Racing/Track Data/Generate Road Visual From Selected Unity Spline")]
        public static void GenerateRoadVisualFromSelectedSplineMenu()
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

            GenerateRoadVisual(splineContainer, trackData);
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

        [MenuItem("Tools/Kart Racing/Track Data/Generate Wall Visual From Selected Unity Spline")]
        public static void GenerateWallVisualFromSelectedSplineMenu()
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

            GenerateWallVisual(splineContainer, trackData);
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

        private static void EnsureDefaultTrackHierarchy(TrackData trackData)
        {
            if (trackData == null)
            {
                return;
            }

            EnsureChildContainer(trackData.transform, "Checkpoints");
            EnsureChildContainer(trackData.transform, "SpawnPoints");
            EnsureChildContainer(trackData.transform, "PowerUpBoxes");
            EnsureChildContainer(trackData.transform, "RespawnPoints");
            EnsureChildContainer(trackData.transform, "TrackBounds");
            EnsureChildContainer(trackData.transform, "OffTrackZones");
        }

        private static void ConfigureTrackDataDefaults(TrackData trackData)
        {
            if (trackData == null)
            {
                return;
            }

            trackData.SetLapsToWin(1);

            var serializedTrackData = new SerializedObject(trackData);
            SetSerializedBool(serializedTrackData, "drawGizmos", true);
            SetSerializedBool(serializedTrackData, "closeCheckpointLoopGizmo", true);
            SetSerializedColor(serializedTrackData, "checkpointGizmoColor", new Color(1f, 0.78f, 0.2f, 0.9f));
            SetSerializedBool(serializedTrackData, "generatePowerUpUsageReport", true);
            SetSerializedBool(serializedTrackData, "generatePowerUpLearningReport", true);
            serializedTrackData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(trackData);
        }

        private static void GenerateDefaultSpawnAndRespawnPoints(TrackData trackData)
        {
            if (trackData == null)
            {
                return;
            }

            var checkpointsRoot = trackData.transform.Find("Checkpoints");
            var spawnPointsRoot = EnsureChildContainer(trackData.transform, "SpawnPoints");
            var respawnPointsRoot = EnsureChildContainer(trackData.transform, "RespawnPoints");
            var powerUpBoxesRoot = EnsureChildContainer(trackData.transform, "PowerUpBoxes");

            if (checkpointsRoot == null || checkpointsRoot.childCount == 0)
            {
                return;
            }

            if (spawnPointsRoot.childCount == 0)
            {
                var startReference = checkpointsRoot.GetChild(0);
                var spawnOffsets = new[]
                {
                    new Vector3(0f, 0f, -8f),
                    new Vector3(-2f, 0f, -11f),
                    new Vector3(2f, 0f, -11f),
                    new Vector3(-2f, 0f, -14f),
                    new Vector3(2f, 0f, -14f),
                    new Vector3(-2f, 0f, -17f),
                    new Vector3(2f, 0f, -17f)
                };

                for (var index = 0; index < spawnOffsets.Length; index++)
                {
                    var spawnObject = new GameObject($"Spawn_{index:00}");
                    Undo.RegisterCreatedObjectUndo(spawnObject, "Create Track Spawn");
                    spawnObject.transform.SetParent(spawnPointsRoot, false);
                    spawnObject.transform.position = startReference.TransformPoint(spawnOffsets[index]);
                    spawnObject.transform.rotation = startReference.rotation;
                }
            }

            if (respawnPointsRoot.childCount == 0)
            {
                for (var index = 0; index < checkpointsRoot.childCount; index++)
                {
                    var checkpointTransform = checkpointsRoot.GetChild(index);
                    var respawnObject = new GameObject($"Respawn_{index:00}");
                    Undo.RegisterCreatedObjectUndo(respawnObject, "Create Track Respawn");
                    respawnObject.transform.SetParent(respawnPointsRoot, false);
                    respawnObject.transform.position = checkpointTransform.position - checkpointTransform.forward * 2.5f;
                    respawnObject.transform.rotation = checkpointTransform.rotation;
                }
            }

            if (powerUpBoxesRoot.childCount == 0 && checkpointsRoot.childCount >= 4)
            {
                for (var index = 1; index < checkpointsRoot.childCount; index += 2)
                {
                    var checkpointTransform = checkpointsRoot.GetChild(index);
                    var powerUpObject = new GameObject($"PowerUpBox_{index:00}");
                    Undo.RegisterCreatedObjectUndo(powerUpObject, "Create Track PowerUp Box");
                    powerUpObject.transform.SetParent(powerUpBoxesRoot, false);
                    powerUpObject.transform.position = checkpointTransform.position - checkpointTransform.right * 3f + Vector3.up * 0.75f;
                    powerUpObject.transform.rotation = checkpointTransform.rotation;
                }
            }
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

            var settings = ResolveCheckpointSettings(splineContainer, trackData);
            var checkpointVerticalOffset = settings != null ? settings.CheckpointVerticalOffset : 1.2f;
            var checkpointsRoot = CreateFreshCheckpointsRoot(trackData.transform);
            var spacing = settings != null ? settings.CheckpointSpacing : 8f;
            var width = settings != null ? settings.CheckpointWidth : 14f;
            var height = settings != null ? settings.CheckpointHeight : 3f;
            var depth = settings != null ? settings.CheckpointDepth : 2.5f;
            var poses = SampleCheckpointPoses(splineContainer, spacing);
            if (poses.Count == 0)
            {
                EditorUtility.DisplayDialog("Unity Spline", "Could not sample the spline to generate checkpoints.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Checkpoints From Unity Spline");

            for (var index = 0; index < poses.Count; index++)
            {
                var pose = poses[index];
                var checkpointObject = new GameObject($"Checkpoint_{index:00}");
                Undo.RegisterCreatedObjectUndo(checkpointObject, "Create Spline Checkpoint");
                checkpointObject.transform.SetParent(checkpointsRoot, false);
                TryAssignTag(checkpointObject, "Checkpoint");
                TryAssignLayer(checkpointObject, "KartCheckpoint");
                checkpointObject.transform.localScale = new Vector3(1f, 1f, 0.12f);

                checkpointObject.transform.SetPositionAndRotation(
                    pose.Position + Vector3.up * checkpointVerticalOffset,
                    Quaternion.LookRotation(pose.Forward, Vector3.up));

                var collider = Undo.AddComponent<BoxCollider>(checkpointObject);
                collider.isTrigger = true;
                collider.center = Vector3.zero;
                collider.size = new Vector3(width, height, depth);
                EditorUtility.SetDirty(collider);

                var checkpoint = Undo.AddComponent<Checkpoint>(checkpointObject);
                if (checkpoint != null)
                {
                    checkpoint.Configure(trackData, index);
                    EditorUtility.SetDirty(checkpoint);
                }
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
            EditorUtility.SetDirty(trackData.gameObject);
            Debug.Log($"Generated {poses.Count} checkpoints from Unity spline under {trackData.name}.");
            Selection.activeGameObject = checkpointsRoot.gameObject;
            EditorGUIUtility.PingObject(checkpointsRoot.gameObject);
        }

        private static void GenerateRoadVisual(SplineContainer splineContainer, TrackData trackData)
        {
            if (splineContainer == null || trackData == null)
            {
                return;
            }

            var settings = ResolveCheckpointSettings(splineContainer, trackData);
            if (settings != null && !settings.GenerateRoadVisual)
            {
                return;
            }

            if (splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return;
            }

            var roadRoot = CreateFreshRoadRoot(trackData.transform);
            var roadWidth = settings != null ? settings.RoadWidth : 12f;
            var roadHeightOffset = settings != null ? settings.RoadHeightOffset : 0.05f;
            var poses = SampleRoadPoses(splineContainer, settings != null ? settings.RoadSampleSpacing : 1.5f);
            if (poses.Count < 2)
            {
                return;
            }

            var roadObject = roadRoot.gameObject;
            var mesh = BuildRoadMesh(roadObject.transform, splineContainer, poses, roadWidth, roadHeightOffset);
            if (mesh == null)
            {
                return;
            }

            var meshFilter = roadObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = Undo.AddComponent<MeshFilter>(roadObject);
            }

            var meshRenderer = roadObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = Undo.AddComponent<MeshRenderer>(roadObject);
            }

            meshFilter.sharedMesh = mesh;

            var meshCollider = roadObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(roadObject);
            }

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;

            meshRenderer.sharedMaterial = LoadOrCreateRoadMaterial();
            meshRenderer.receiveShadows = true;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(meshCollider);
            EditorUtility.SetDirty(meshRenderer);
            EditorUtility.SetDirty(roadObject);
        }

        private static void GenerateWallVisual(SplineContainer splineContainer, TrackData trackData)
        {
            if (splineContainer == null || trackData == null)
            {
                return;
            }

            var settings = ResolveCheckpointSettings(splineContainer, trackData);
            if (settings != null && !settings.GenerateWallVisual)
            {
                return;
            }

            if (splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return;
            }

            var wallRoot = CreateFreshWallRoot(trackData.transform);
            var roadWidth = settings != null ? settings.RoadWidth : 12f;
            var wallHeight = settings != null ? settings.WallHeight : 2f;
            var wallThickness = settings != null ? settings.WallThickness : 0.25f;
            var wallOffset = settings != null ? settings.WallOffset : 0.5f;
            var wallSpacing = settings != null ? settings.WallSampleSpacing : 2f;
            var wallOverlap = settings != null ? settings.WallSegmentOverlap : 0.2f;
            var poses = SampleRoadPoses(splineContainer, wallSpacing);
            if (poses.Count < 2)
            {
                return;
            }

            var closed = splineContainer.Spline.Closed;
            var segmentCount = closed ? poses.Count : poses.Count - 1;
            var wallMaterial = LoadOrCreateRoadMaterial();

            ClearChildren(wallRoot);

            CreateWallSide(wallRoot, poses, segmentCount, roadWidth, wallHeight, wallThickness, wallOffset, wallOverlap, -1f, wallMaterial, "LeftWall");
            CreateWallSide(wallRoot, poses, segmentCount, roadWidth, wallHeight, wallThickness, wallOffset, wallOverlap, 1f, wallMaterial, "RightWall");

            EditorUtility.SetDirty(wallRoot.gameObject);
        }

        private static void CreateWallSide(
            Transform wallRoot,
            List<SplineCheckpointPose> poses,
            int segmentCount,
            float roadWidth,
            float wallHeight,
            float wallThickness,
            float wallOffset,
            float wallOverlap,
            float sideSign,
            Material wallMaterial,
            string sideName)
        {
            if (wallRoot == null || poses == null || poses.Count < 2)
            {
                return;
            }

            var sideRoot = new GameObject(sideName);
            Undo.RegisterCreatedObjectUndo(sideRoot, "Create Wall Side");
            sideRoot.transform.SetParent(wallRoot, false);

            for (var index = 0; index < segmentCount; index++)
            {
                var nextIndex = index + 1;
                if (nextIndex >= poses.Count)
                {
                    nextIndex = 0;
                }

                var current = poses[index];
                var next = poses[nextIndex];
                var segmentVector = next.Position - current.Position;
                var segmentLength = segmentVector.magnitude;
                if (segmentLength <= 0.001f)
                {
                    continue;
                }

                var segmentDir = segmentVector / segmentLength;
                var right = Vector3.Cross(Vector3.up, segmentDir).normalized;
                if (right.sqrMagnitude < 0.0001f)
                {
                    right = Vector3.right;
                }

                var edgeOffset = roadWidth * 0.5f + wallOffset;
                var baseMid = (current.Position + next.Position) * 0.5f + Vector3.up * (wallHeight * 0.5f);
                var outwardOffset = right * sideSign * edgeOffset;

                var wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(wallObject, "Create Wall Segment");
                wallObject.name = $"{sideName}_{index:00}";
                wallObject.transform.SetParent(sideRoot.transform, false);
                TryAssignTag(wallObject, "Wall");
                TryAssignLayer(wallObject, "KartWall");
                wallObject.transform.position = baseMid + outwardOffset;
                wallObject.transform.rotation = Quaternion.LookRotation(segmentDir, Vector3.up);
                wallObject.transform.localScale = new Vector3(wallThickness, wallHeight, segmentLength + wallOverlap);

                var renderer = wallObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = wallMaterial;
                    renderer.receiveShadows = true;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                var collider = wallObject.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.isTrigger = false;
                }
            }
        }

        private static List<SplineCheckpointPose> SampleRoadPoses(SplineContainer splineContainer, float spacing)
        {
            var sampledPoses = new List<SplineCheckpointPose>();
            if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return sampledPoses;
            }

            var length = Mathf.Max(0.01f, CalculateSplineLengthSafe(splineContainer));
            var denseStep = Mathf.Clamp(spacing * 0.5f, 0.05f, 1f);
            var denseSegments = Mathf.Max(32, Mathf.CeilToInt(length / denseStep));

            for (var sampleIndex = 0; sampleIndex <= denseSegments; sampleIndex++)
            {
                var t = sampleIndex / (float)denseSegments;
                sampledPoses.Add(CreateSamplePose(splineContainer, t));
            }

            return sampledPoses;
        }

        private static List<SplineCheckpointPose> SampleCheckpointPoses(SplineContainer splineContainer, float spacing)
        {
            var sampledPoses = new List<SplineCheckpointPose>();
            if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return sampledPoses;
            }

            var length = Mathf.Max(0.01f, CalculateSplineLengthSafe(splineContainer));
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

        private static float CalculateSplineLengthSafe(SplineContainer splineContainer)
        {
            if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                return 0.01f;
            }

            try
            {
                return splineContainer.CalculateLength();
            }
            catch
            {
                return 0.01f;
            }
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
                EnsureCheckpointSettings(existing);
                return existing;
            }

            var splineObject = new GameObject(SplineRootName);
            Undo.RegisterCreatedObjectUndo(splineObject, "Create Unity Spline");
            splineObject.transform.SetParent(trackData.transform, false);

            var splineContainer = Undo.AddComponent<SplineContainer>(splineObject);
            splineContainer.Spline = new Spline();
            splineContainer.Spline.Closed = true;
            EnsureCheckpointSettings(splineContainer);

            return splineContainer;
        }

        private static UnitySplineCheckpointSettings EnsureCheckpointSettings(SplineContainer splineContainer)
        {
            if (splineContainer == null)
            {
                return null;
            }

            var settings = splineContainer.GetComponent<UnitySplineCheckpointSettings>();
            if (settings != null)
            {
                return settings;
            }

            settings = Undo.AddComponent<UnitySplineCheckpointSettings>(splineContainer.gameObject);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static UnitySplineCheckpointSettings ResolveCheckpointSettings(SplineContainer splineContainer, TrackData trackData)
        {
            if (splineContainer != null)
            {
                var splineSettings = splineContainer.GetComponent<UnitySplineCheckpointSettings>();
                if (splineSettings != null)
                {
                    return splineSettings;
                }
            }

            if (trackData != null)
            {
                var trackSettings = trackData.GetComponent<UnitySplineCheckpointSettings>();
                if (trackSettings != null)
                {
                    return trackSettings;
                }
            }

            return null;
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

        private static void TryAssignLayer(GameObject target, string layerName)
        {
            if (target == null || string.IsNullOrWhiteSpace(layerName))
            {
                return;
            }

            var layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                target.layer = layerIndex;
            }
        }

        private static void TryAssignTag(GameObject target, string tagName)
        {
            if (target == null || string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            try
            {
                if (target.CompareTag(tagName))
                {
                    return;
                }
            }
            catch
            {
                // Tag may not exist yet.
            }

            try
            {
                target.tag = tagName;
            }
            catch
            {
                // Ignore if the tag is not defined in the project.
            }
        }

        private static Transform CreateFreshCheckpointsRoot(Transform trackRoot)
        {
            if (trackRoot == null)
            {
                return null;
            }

            var existing = trackRoot.Find(CheckpointsRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var checkpointsRoot = new GameObject(CheckpointsRootName);
            Undo.RegisterCreatedObjectUndo(checkpointsRoot, "Create Checkpoints Root");
            checkpointsRoot.transform.SetParent(trackRoot, false);
            return checkpointsRoot.transform;
        }

        private static Transform CreateFreshRoadRoot(Transform trackRoot)
        {
            if (trackRoot == null)
            {
                return null;
            }

            var existing = trackRoot.Find(RoadVisualRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var roadObject = new GameObject(RoadVisualRootName);
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Road Visual Root");
            roadObject.transform.SetParent(trackRoot, false);
            return roadObject.transform;
        }

        private static Transform CreateFreshWallRoot(Transform trackRoot)
        {
            if (trackRoot == null)
            {
                return null;
            }

            var existing = trackRoot.Find(WallVisualRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var wallObject = new GameObject(WallVisualRootName);
            Undo.RegisterCreatedObjectUndo(wallObject, "Create Wall Visual Root");
            wallObject.transform.SetParent(trackRoot, false);
            return wallObject.transform;
        }

        private static Mesh BuildRoadMesh(Transform roadTransform, SplineContainer splineContainer, List<SplineCheckpointPose> poses, float roadWidth, float roadHeightOffset)
        {
            if (roadTransform == null || splineContainer == null || poses == null || poses.Count < 2)
            {
                return null;
            }

            var vertexCount = poses.Count * 2;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var closed = splineContainer.Spline != null && splineContainer.Spline.Closed;
            var segmentCount = closed ? poses.Count : poses.Count - 1;
            var triangles = new int[segmentCount * 6];

            var halfWidth = Mathf.Max(0.5f, roadWidth * 0.5f);
            var totalLength = 0f;
            var segmentLengths = new float[poses.Count];
            segmentLengths[0] = 0f;
            for (var index = 1; index < poses.Count; index++)
            {
                totalLength += Vector3.Distance(poses[index - 1].Position, poses[index].Position);
                segmentLengths[index] = totalLength;
            }

            for (var index = 0; index < poses.Count; index++)
            {
                var current = poses[index];
                var forward = current.Forward;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }

                var right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.0001f)
                {
                    right = Vector3.right;
                }

                var center = current.Position + Vector3.up * roadHeightOffset;
                var left = center - right * halfWidth;
                var rightPoint = center + right * halfWidth;

                var vertexIndex = index * 2;
                vertices[vertexIndex] = roadTransform.InverseTransformPoint(left);
                vertices[vertexIndex + 1] = roadTransform.InverseTransformPoint(rightPoint);
                normals[vertexIndex] = Vector3.up;
                normals[vertexIndex + 1] = Vector3.up;

                var u = totalLength > 0.01f ? segmentLengths[index] / totalLength : index / (float)(poses.Count - 1);
                uvs[vertexIndex] = new Vector2(0f, u);
                uvs[vertexIndex + 1] = new Vector2(1f, u);
            }

            var triangleIndex = 0;
            for (var index = 0; index < segmentCount; index++)
            {
                var vertexIndex = index * 2;
                var nextIndex = index + 1;
                if (nextIndex >= poses.Count)
                {
                    nextIndex = 0;
                }

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = nextIndex * 2;
                triangles[triangleIndex++] = vertexIndex + 1;

                triangles[triangleIndex++] = vertexIndex + 1;
                triangles[triangleIndex++] = nextIndex * 2;
                triangles[triangleIndex++] = nextIndex * 2 + 1;
            }

            var mesh = new Mesh
            {
                name = "UnitySplineRoadMesh"
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material LoadOrCreateRoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = "UnitySplineRoadBlack"
            };
            material.color = Color.black;
            material.SetColor("_BaseColor", Color.black);

            var folder = System.IO.Path.GetDirectoryName(RoadMaterialPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            AssetDatabase.CreateAsset(material, RoadMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
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

        private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            if (serializedObject == null)
            {
                return;
            }

            var property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
            }
        }

        private static void SetSerializedColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            if (serializedObject == null)
            {
                return;
            }

            var property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Color)
            {
                property.colorValue = value;
            }
        }

        private static void EnsureTagsAndLayers()
        {
            EnsureTagExists("Wall");
            EnsureTagExists("Checkpoint");
            EnsureTagExists("OffTrack");
            EnsureLayerExists("KartWall");
            EnsureLayerExists("KartCheckpoint");
            EnsureLayerExists("OffTrack");
        }

        private static void EnsureTagExists(string tagName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            for (var index = 0; index < tagsProp.arraySize; index++)
            {
                if (tagsProp.GetArrayElementAtIndex(index).stringValue == tagName)
                {
                    return;
                }
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
        }

        private static int EnsureLayerExists(string layerName)
        {
            var existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer != -1)
            {
                return existingLayer;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (var index = 8; index < layersProp.arraySize; index++)
            {
                var layerProp = layersProp.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return index;
                }

                if (layerProp.stringValue == layerName)
                {
                    return index;
                }
            }

            Debug.LogWarning($"Could not create layer '{layerName}'. All user layer slots are already occupied.");
            return -1;
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
