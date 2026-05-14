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

        // Tuned training circuit: straight → wide right bend → sweep → hairpin → S bend → start.
        // Order must be: 00 → 01 → 02 → ... → 11 → 00.
        // X = left/right in top view, Z = forward/back in top view.
        private static readonly Vector3[] ImprovedCheckpointPositions =
        {
            new Vector3(-32f, 1.2f, -12f),  // 00 Start / finish line
            new Vector3(-32f, 1.2f,   8f),  // 01 Main straight
            new Vector3(-32f, 1.2f,  28f),  // 02 End of straight, before right bend
            new Vector3(-14f, 1.2f,  46f),  // 03 Right bend entry
            new Vector3( 12f, 1.2f,  48f),  // 04 Wide right bend apex
            new Vector3( 36f, 1.2f,  36f),  // 05 Right bend exit
            new Vector3( 44f, 1.2f,  14f),  // 06 Long sweeping section
            new Vector3( 42f, 1.2f,  -4f),  // 07 Sweep exit
            new Vector3( 30f, 1.2f, -20f),  // 08 Hairpin entry
            new Vector3( 10f, 1.2f, -26f),  // 09 Hairpin apex
            new Vector3( -8f, 1.2f, -18f),  // 10 Hairpin exit / S bend entry
            new Vector3(-20f, 1.2f, -12f),  // 11 S bend exit, return to start
        };

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

        [MenuItem("Tools/Kart Racing/ML-Agents/Build Improved Training Track In Current Scene")]
        public static void BuildImprovedTrainingTrackInCurrentScene()
        {
            EnsureTagsAndLayers();

            var trackData = Object.FindFirstObjectByType<TrackData>();
            if (trackData == null)
            {
                var trackRoot = new GameObject("TrackRoot");
                Undo.RegisterCreatedObjectUndo(trackRoot, "Create TrackRoot");
                trackData = Undo.AddComponent<TrackData>(trackRoot);
            }

            EnsureChildContainer(trackData.transform, "Checkpoints");
            EnsureChildContainer(trackData.transform, "SpawnPoints");
            EnsureChildContainer(trackData.transform, "PowerUpBoxes");
            EnsureChildContainer(trackData.transform, "RespawnPoints");
            EnsureChildContainer(trackData.transform, "TrackBounds");
            EnsureChildContainer(trackData.transform, "OffTrackZones");

            EnsurePrototypeGround();

            ClearChildContainer(trackData.transform, "Checkpoints");
            ClearChildContainer(trackData.transform, "SpawnPoints");
            ClearChildContainer(trackData.transform, "RespawnPoints");
            ClearChildContainer(trackData.transform, "TrackBounds");

            trackData.SetLapsToWin(1);
            EnsureImprovedTrackLayout(trackData);
            AutoConfigureCheckpoints(trackData);
            EnsureImprovedTrainingWalls(trackData);

            var manager = EnsureTrainingSceneManager(trackData);
            var trainingKart = EnsureTrainingKart(trackData);
            ConfigureKartAgent(trainingKart, manager, trackData);

            // Always reset kart to spawn 0 after rebuilding
            var spawnPoint = trackData.GetSpawnPoint(0);
            if (spawnPoint != null)
            {
                trainingKart.transform.SetPositionAndRotation(
                    spawnPoint.position + Vector3.up * 0.35f,
                    spawnPoint.rotation);
                var rb = trainingKart.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                EditorUtility.SetDirty(trainingKart);
            }

            EditorUtility.SetDirty(trackData);
            Selection.activeGameObject = trainingKart;
            Debug.Log("[ML-Agents] Improved training track built: 12 checkpoints, front straight + right sweep + hairpin + S-chicane.");
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

        private static void ClearChildContainer(Transform parent, string containerName)
        {
            var container = parent.Find(containerName);
            if (container == null) return;
            for (var i = container.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
        }

        private static void EnsureImprovedTrackLayout(TrackData trackData)
        {
            var checkpointsRoot = EnsureChildContainer(trackData.transform, "Checkpoints");
            var spawnPointsRoot = EnsureChildContainer(trackData.transform, "SpawnPoints");
            var respawnRoot     = EnsureChildContainer(trackData.transform, "RespawnPoints");

            if (checkpointsRoot.childCount == 0)
            {
                for (var i = 0; i < ImprovedCheckpointPositions.Length; i++)
                {
                    var cpObj = new GameObject($"Checkpoint_{i:00}");
                    Undo.RegisterCreatedObjectUndo(cpObj, "Create Improved Checkpoint");
                    cpObj.transform.SetParent(checkpointsRoot, false);
                    cpObj.transform.position = ImprovedCheckpointPositions[i];

                    var nextPos = ImprovedCheckpointPositions[(i + 1) % ImprovedCheckpointPositions.Length];
                    cpObj.transform.rotation = Quaternion.LookRotation((nextPos - ImprovedCheckpointPositions[i]).normalized, Vector3.up);

                    var col = Undo.AddComponent<BoxCollider>(cpObj);
                    col.isTrigger = true;
                    col.size = new Vector3(13f, 3f, 2.5f);
                }
            }

            if (spawnPointsRoot.childCount == 0 && checkpointsRoot.childCount > 0)
            {
                var startRef = checkpointsRoot.GetChild(0);
                // Spawn slightly before the S/F line, facing checkpoint 01.
                // This avoids spawning after Checkpoint_00.
                // Spawn NORTH of CP0 (between CP0 and CP1) so NextCheckpointIndex starts at 1, not 0.
                // Kart faces the same direction as CP0 (toward CP1).
                var offsets = new[] { new Vector3(-2f, 0f, 4f), new Vector3(2f, 0f, 4f), new Vector3(-4f, 0f, 8f), new Vector3(4f, 0f, 8f) };
                for (var i = 0; i < offsets.Length; i++)
                {
                    var spawnObj = new GameObject($"Spawn_{i:00}");
                    Undo.RegisterCreatedObjectUndo(spawnObj, "Create Improved Spawn");
                    spawnObj.transform.SetParent(spawnPointsRoot, false);
                    spawnObj.transform.SetPositionAndRotation(startRef.TransformPoint(offsets[i]), startRef.rotation);
                }
            }

            if (respawnRoot.childCount == 0)
            {
                for (var i = 0; i < checkpointsRoot.childCount; i++)
                {
                    var cp = checkpointsRoot.GetChild(i);
                    var respawnObj = new GameObject($"Respawn_{i:00}");
                    Undo.RegisterCreatedObjectUndo(respawnObj, "Create Improved Respawn");
                    respawnObj.transform.SetParent(respawnRoot, false);
                    respawnObj.transform.SetPositionAndRotation(cp.position - cp.forward * 3f, cp.rotation);
                }
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
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

            const float trackHalfWidth = 7.5f;
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

        private static void EnsureImprovedTrainingWalls(TrackData trackData)
        {
            if (trackData == null || trackData.CheckpointCount < 2) return;

            var wallsRoot = EnsureChildContainer(trackData.transform, "TrackBounds");

            const float trackHalfWidth = 7.5f;
            const float wallThickness  = 0.75f;
            const float outerHeight    = 2.5f;
            const float innerHeight    = 0.4f;  // curb-height — keeps track visually open

            var outerColor = new Color(1f, 0.55f, 0.1f);          // orange outer barrier
            var innerColorA = new Color(0.9f, 0.15f, 0.15f);       // red curb
            var innerColorB = new Color(0.95f, 0.95f, 0.95f);      // white curb (alternates)

            for (var i = 0; i < trackData.CheckpointCount; i++)
            {
                var current = trackData.GetCheckpoint(i);
                var next    = trackData.GetCheckpoint((i + 1) % trackData.CheckpointCount);
                if (current == null || next == null) continue;

                var segment = next.position - current.position;
                var segLen  = segment.magnitude;
                if (segLen <= 0.01f) continue;

                var dir   = segment / segLen;
                var right = Vector3.Cross(Vector3.up, dir).normalized;

                // Outer wall — tall orange barrier
                CreateWallSegment(wallsRoot, i, "Left",
                    current.position + right * trackHalfWidth,
                    next.position    + right * trackHalfWidth,
                    wallThickness, outerHeight, outerColor);

                // Inner wall — short curb, alternating red/white
                var curbColor = (i % 2 == 0) ? innerColorA : innerColorB;
                CreateWallSegment(wallsRoot, i, "Right",
                    current.position - right * trackHalfWidth,
                    next.position    - right * trackHalfWidth,
                    wallThickness, innerHeight, curbColor);
            }

            AssignWallMetadata(wallsRoot);
        }

        private static void CreateWallSegment(Transform parent, int index, string side, Vector3 start, Vector3 end, float thickness, float height, Color? colorOverride = null)
        {
            var wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wallObject, "Create Training Wall");
            wallObject.name = $"Wall_{side}_{index:00}";
            wallObject.transform.SetParent(parent, false);

            // Flatten Y so walls always sit on the ground regardless of checkpoint height
            var s = new Vector3(start.x, 0f, start.z);
            var e = new Vector3(end.x,   0f, end.z);
            var segment  = e - s;
            var midpoint = (s + e) * 0.5f + Vector3.up * (height * 0.5f);
            wallObject.transform.position = midpoint;
            wallObject.transform.rotation = Quaternion.LookRotation(segment.normalized, Vector3.up);
            wallObject.transform.localScale = new Vector3(thickness, height, segment.magnitude);

            var renderer = wallObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var wallColor = colorOverride ?? (side == "Left"
                        ? new Color(1f, 0.55f, 0.1f)    // orange outer
                        : new Color(0.95f, 0.95f, 0.95f)); // white inner
                    renderer.sharedMaterial = new Material(shader) { color = wallColor };
                }
            }
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
            var existing = GameObject.Find("TrainingGround");
            if (existing != null)
            {
                // Re-center and resize for improved track bounds
                existing.transform.position = new Vector3(20f, 0f, 26f);
                existing.transform.localScale = new Vector3(18f, 1f, 18f);
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Create Training Ground");
            ground.name = "TrainingGround";
            ground.transform.position = new Vector3(20f, 0f, 26f);
            ground.transform.localScale = new Vector3(18f, 1f, 18f);

            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                    renderer.sharedMaterial = new Material(shader) { color = new Color(0.22f, 0.22f, 0.22f) };
            }
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
