#if UNITY_EDITOR
using System.Collections.Generic;
using KartGame.AI.Reinforcement;
using KartGame.Core;
using KartGame.Kart;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    /*
     * Script: KartMLAgentsSetupTools.cs
     * Purpose: Adds Unity editor menu commands to scaffold a dedicated kart training scene for ML-Agents.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: TrackData, KartController, CheckpointTracker, KartAgent, TrainingSceneManager and ML-Agents package components.
     * Inspector Setup: Use the Tools/Kart Racing/ML-Agents menu in the Unity Editor after the ML-Agents package resolves.
     */
    public static class KartMLAgentsSetupTools
    {
        private const int PrototypeCheckpointCount = 8;
        private const string WallTag = "Wall";
        private const string CheckpointTag = "Checkpoint";
        private const string OffTrackTag = "OffTrack";
        private const string WallLayerName = "KartWall";
        private const string CheckpointLayerName = "KartCheckpoint";
        private const string OffTrackLayerName = "OffTrack";

        [MenuItem("Tools/Kart Racing/ML-Agents/Build Training Prototype In Current Scene")]
        public static void BuildTrainingPrototypeInCurrentScene()
        {
            EnsureTagsAndLayers();

            var trackData = EnsureTrainingTrack();
            AutoConfigureCheckpoints(trackData);
            EnsureTrainingWalls(trackData);

            var manager = EnsureTrainingSceneManager(trackData);
            var trainingKart = EnsureTrainingKart(trackData);
            ConfigureKartAgent(trainingKart, manager, trackData);

            Selection.activeGameObject = trainingKart;
        }

        [MenuItem("Tools/Kart Racing/ML-Agents/Configure Selected Kart As Agent")]
        public static void ConfigureSelectedKartAsAgent()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Kart ML-Agents", "Select a kart GameObject first.", "OK");
                return;
            }

            EnsureTagsAndLayers();

            var kartObject = Selection.activeGameObject;
            var trackData = Object.FindFirstObjectByType<TrackData>();
            if (trackData == null)
            {
                EditorUtility.DisplayDialog("Kart ML-Agents", "No TrackData found. Create a training track first.", "OK");
                return;
            }

            AutoConfigureCheckpoints(trackData);
            EnsureTrainingWalls(trackData);

            var manager = EnsureTrainingSceneManager(trackData);
            ConfigureKartAgent(kartObject, manager, trackData);
            Selection.activeGameObject = kartObject;
        }

        private static TrackData EnsureTrainingTrack()
        {
            var trackData = Object.FindFirstObjectByType<TrackData>();
            if (trackData == null)
            {
                var trackRoot = new GameObject("TrackRoot");
                Undo.RegisterCreatedObjectUndo(trackRoot, "Create Training TrackRoot");
                trackData = Undo.AddComponent<TrackData>(trackRoot);
            }

            EnsureChildContainer(trackData.transform, "Checkpoints");
            EnsureChildContainer(trackData.transform, "SpawnPoints");
            EnsureChildContainer(trackData.transform, "PowerUpBoxes");
            EnsureChildContainer(trackData.transform, "RespawnPoints");
            EnsureChildContainer(trackData.transform, "TrackBounds");
            EnsureChildContainer(trackData.transform, "OffTrackZones");

            EnsurePrototypeGround();
            EnsurePrototypeTrackLayout(trackData);
            trackData.SetLapsToWin(1);
            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
            return trackData;
        }

        private static void EnsurePrototypeTrackLayout(TrackData trackData)
        {
            var checkpointsRoot = EnsureChildContainer(trackData.transform, "Checkpoints");
            var spawnPointsRoot = EnsureChildContainer(trackData.transform, "SpawnPoints");
            var respawnRoot = EnsureChildContainer(trackData.transform, "RespawnPoints");

            if (checkpointsRoot.childCount == 0)
            {
                const float xRadius = 32f;
                const float zRadius = 20f;

                for (var index = 0; index < PrototypeCheckpointCount; index++)
                {
                    var checkpointObject = new GameObject($"Checkpoint_{index:00}");
                    Undo.RegisterCreatedObjectUndo(checkpointObject, "Create Training Checkpoint");
                    checkpointObject.transform.SetParent(checkpointsRoot, false);

                    var currentAngle = index / (float)PrototypeCheckpointCount * Mathf.PI * 2f;
                    var nextAngle = (index + 1) / (float)PrototypeCheckpointCount * Mathf.PI * 2f;
                    var currentPosition = new Vector3(Mathf.Cos(currentAngle) * xRadius, 1.2f, Mathf.Sin(currentAngle) * zRadius);
                    var nextPosition = new Vector3(Mathf.Cos(nextAngle) * xRadius, 1.2f, Mathf.Sin(nextAngle) * zRadius);
                    var forward = (nextPosition - currentPosition).normalized;

                    checkpointObject.transform.position = currentPosition;
                    checkpointObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

                    var collider = Undo.AddComponent<BoxCollider>(checkpointObject);
                    collider.isTrigger = true;
                    collider.size = new Vector3(10f, 3f, 2.5f);
                }
            }

            if (respawnRoot.childCount == 0)
            {
                for (var index = 0; index < checkpointsRoot.childCount; index++)
                {
                    var checkpointTransform = checkpointsRoot.GetChild(index);
                    var respawnObject = new GameObject($"Respawn_{index:00}");
                    Undo.RegisterCreatedObjectUndo(respawnObject, "Create Training Respawn");
                    respawnObject.transform.SetParent(respawnRoot, false);
                    respawnObject.transform.position = checkpointTransform.position - checkpointTransform.forward * 2.5f;
                    respawnObject.transform.rotation = checkpointTransform.rotation;
                }
            }

            if (spawnPointsRoot.childCount == 0 && checkpointsRoot.childCount > 0)
            {
                var startReference = checkpointsRoot.GetChild(0);
                var spawnOffsets = new[]
                {
                    new Vector3(0f, 0f, -8f),
                    new Vector3(-2f, 0f, -11f),
                    new Vector3(2f, 0f, -11f),
                    new Vector3(-2f, 0f, -14f)
                };

                for (var index = 0; index < spawnOffsets.Length; index++)
                {
                    var spawnObject = new GameObject($"Spawn_{index:00}");
                    Undo.RegisterCreatedObjectUndo(spawnObject, "Create Training Spawn");
                    spawnObject.transform.SetParent(spawnPointsRoot, false);
                    spawnObject.transform.position = startReference.TransformPoint(spawnOffsets[index]);
                    spawnObject.transform.rotation = startReference.rotation;
                }
            }
        }

        private static void AutoConfigureCheckpoints(TrackData trackData)
        {
            var checkpointsRoot = trackData.transform.Find("Checkpoints");
            if (checkpointsRoot == null)
            {
                return;
            }

            var checkpointLayer = EnsureLayerExists(CheckpointLayerName);
            if (checkpointLayer < 0)
            {
                checkpointLayer = 0;
            }

            for (var index = 0; index < checkpointsRoot.childCount; index++)
            {
                var checkpointTransform = checkpointsRoot.GetChild(index);
                var checkpoint = checkpointTransform.GetComponent<Checkpoint>() ?? Undo.AddComponent<Checkpoint>(checkpointTransform.gameObject);
                checkpoint.Configure(trackData, index);

                var collider = checkpointTransform.GetComponent<Collider>() ?? Undo.AddComponent<BoxCollider>(checkpointTransform.gameObject);
                collider.isTrigger = true;

                if (collider is BoxCollider boxCollider)
                {
                    boxCollider.size = new Vector3(10f, 3f, 2.5f);
                }

                checkpointTransform.gameObject.tag = CheckpointTag;
                checkpointTransform.gameObject.layer = checkpointLayer;
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
        }

        private static void EnsureTrainingWalls(TrackData trackData)
        {
            if (trackData == null || trackData.CheckpointCount < 2)
            {
                return;
            }

            var wallsRoot = EnsureChildContainer(trackData.transform, "TrackBounds");
            if (wallsRoot.childCount > 0)
            {
                AssignWallMetadata(wallsRoot);
                return;
            }

            const float trackHalfWidth = 6.5f;
            const float wallThickness = 0.75f;
            const float wallHeight = 2.5f;

            for (var index = 0; index < trackData.CheckpointCount; index++)
            {
                var current = trackData.GetCheckpoint(index);
                var next = trackData.GetCheckpoint((index + 1) % trackData.CheckpointCount);
                if (current == null || next == null)
                {
                    continue;
                }

                var segment = next.position - current.position;
                var segmentLength = segment.magnitude;
                if (segmentLength <= 0.01f)
                {
                    continue;
                }

                var direction = segment / segmentLength;
                var right = Vector3.Cross(Vector3.up, direction).normalized;
                CreateWallSegment(wallsRoot, index, "Left", current.position + right * trackHalfWidth, next.position + right * trackHalfWidth, wallThickness, wallHeight);
                CreateWallSegment(wallsRoot, index, "Right", current.position - right * trackHalfWidth, next.position - right * trackHalfWidth, wallThickness, wallHeight);
            }

            AssignWallMetadata(wallsRoot);
        }

        private static void CreateWallSegment(Transform parent, int index, string side, Vector3 start, Vector3 end, float thickness, float height)
        {
            var wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wallObject, "Create Training Wall");
            wallObject.name = $"Wall_{side}_{index:00}";
            wallObject.transform.SetParent(parent, false);

            var segment = end - start;
            var midpoint = (start + end) * 0.5f + Vector3.up * (height * 0.5f);
            wallObject.transform.position = midpoint;
            wallObject.transform.rotation = Quaternion.LookRotation(segment.normalized, Vector3.up);
            wallObject.transform.localScale = new Vector3(thickness, height, segment.magnitude);
        }

        private static void AssignWallMetadata(Transform wallsRoot)
        {
            var wallLayer = EnsureLayerExists(WallLayerName);
            if (wallLayer < 0)
            {
                wallLayer = 0;
            }
            for (var index = 0; index < wallsRoot.childCount; index++)
            {
                var wall = wallsRoot.GetChild(index).gameObject;
                wall.tag = WallTag;
                wall.layer = wallLayer;
            }
        }

        private static TrainingSceneManager EnsureTrainingSceneManager(TrackData trackData)
        {
            var managerObject = GameObject.Find("TrainingSceneManager");
            if (managerObject == null)
            {
                managerObject = new GameObject("TrainingSceneManager");
                Undo.RegisterCreatedObjectUndo(managerObject, "Create TrainingSceneManager");
            }

            var manager = managerObject.GetComponent<TrainingSceneManager>() ?? Undo.AddComponent<TrainingSceneManager>(managerObject);
            manager.SetTrackData(trackData);
            EditorUtility.SetDirty(managerObject);
            return manager;
        }

        private static GameObject EnsureTrainingKart(TrackData trackData)
        {
            var existing = GameObject.Find("TrainingKartAgent");
            if (existing != null)
            {
                return existing;
            }

            var spawnPoint = trackData != null ? trackData.GetSpawnPoint(0) : null;
            var kartObject = new GameObject("TrainingKartAgent");
            Undo.RegisterCreatedObjectUndo(kartObject, "Create TrainingKartAgent");

            var spawnPosition = spawnPoint != null ? spawnPoint.position + Vector3.up * 0.35f : Vector3.up * 0.35f;
            var spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            kartObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var rigidbody = Undo.AddComponent<Rigidbody>(kartObject);
            rigidbody.mass = 140f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var collider = Undo.AddComponent<BoxCollider>(kartObject);
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.4f, 0.9f, 2.4f);

            Undo.AddComponent<KartController>(kartObject);
            var checkpointTracker = Undo.AddComponent<CheckpointTracker>(kartObject);
            checkpointTracker.SetTrackData(trackData);
            checkpointTracker.SetPlayerFlag(false);
            checkpointTracker.SetRecoveryReference(spawnPoint);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(visual, "Create TrainingKartAgent Visual");
            visual.name = "Visual";
            visual.transform.SetParent(kartObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            visual.transform.localScale = new Vector3(1.35f, 0.6f, 2.2f);

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Undo.DestroyObjectImmediate(visualCollider);
            }

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var material = new Material(shader)
                    {
                        color = new Color(0.22f, 0.9f, 0.52f)
                    };

                    renderer.sharedMaterial = material;
                }
            }

            return kartObject;
        }

        private static void ConfigureKartAgent(GameObject kartObject, TrainingSceneManager trainingSceneManager, TrackData trackData)
        {
            var kartController = kartObject.GetComponent<KartController>() ?? Undo.AddComponent<KartController>(kartObject);
            var checkpointTracker = kartObject.GetComponent<CheckpointTracker>() ?? Undo.AddComponent<CheckpointTracker>(kartObject);
            checkpointTracker.SetTrackData(trackData);
            checkpointTracker.SetPlayerFlag(false);

            var playerInput = kartObject.GetComponent<PlayerKartInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            var aiInput = kartObject.GetComponent<AIKartInput>();
            if (aiInput != null)
            {
                aiInput.enabled = false;
            }

            kartController.SetControlEnabled(true);

            var rewardManager = kartObject.GetComponent<AgentRewardManager>() ?? Undo.AddComponent<AgentRewardManager>(kartObject);
            var kartAgent = kartObject.GetComponent<KartAgent>() ?? Undo.AddComponent<KartAgent>(kartObject);
            var behaviorParameters = kartObject.GetComponent<BehaviorParameters>() ?? Undo.AddComponent<BehaviorParameters>(kartObject);
            var decisionRequester = kartObject.GetComponent<DecisionRequester>() ?? Undo.AddComponent<DecisionRequester>(kartObject);
            var raySensor = kartObject.GetComponent<RayPerceptionSensorComponent3D>() ?? Undo.AddComponent<RayPerceptionSensorComponent3D>(kartObject);

            trainingSceneManager.RegisterAgent(kartAgent);
            kartAgent.MaxStep = 5000;
            kartAgent.AutoAssignReferences(trainingSceneManager, trackData);

            behaviorParameters.BehaviorName = "KartAgent";
            behaviorParameters.BehaviorType = BehaviorType.Default;
            behaviorParameters.BrainParameters.VectorObservationSize = 13;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3, 3);

            decisionRequester.DecisionPeriod = 1;
            decisionRequester.TakeActionsBetweenDecisions = true;

            raySensor.SensorName = "KartRaySensor";
            raySensor.RaysPerDirection = 3;
            raySensor.MaxRayDegrees = 70f;
            raySensor.SphereCastRadius = 0.45f;
            raySensor.RayLength = 14f;
            raySensor.StartVerticalOffset = 0.75f;
            raySensor.EndVerticalOffset = 0f;
            raySensor.DetectableTags = new List<string> { WallTag, CheckpointTag };
            raySensor.RayLayerMask = LayerMask.GetMask(WallLayerName, CheckpointLayerName);

            EditorUtility.SetDirty(rewardManager);
            EditorUtility.SetDirty(kartAgent);
            EditorUtility.SetDirty(behaviorParameters);
            EditorUtility.SetDirty(decisionRequester);
            EditorUtility.SetDirty(raySensor);
        }

        private static void EnsurePrototypeGround()
        {
            if (GameObject.Find("TrainingGround") != null)
            {
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Create Training Ground");
            ground.name = "TrainingGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(15f, 1f, 15f);
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

        private static void EnsureTagsAndLayers()
        {
            EnsureTagExists(WallTag);
            EnsureTagExists(CheckpointTag);
            EnsureTagExists(OffTrackTag);
            EnsureLayerExists(WallLayerName);
            EnsureLayerExists(CheckpointLayerName);
            EnsureLayerExists(OffTrackLayerName);
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
    }
}
#endif
