#if UNITY_EDITOR
using KartGame.Core;
using KartGame.Kart;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    /*
     * Script: KartRacingSetupTools.cs
     * Purpose: Adds Unity editor menu commands that scaffold the prototype race scene, karts and track hierarchy.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: TrackData, RaceManager, KartController, CheckpointTracker and related runtime scripts.
     * Inspector Setup: Use the Tools/Kart Racing/MVP Setup menu inside the Unity Editor.
     */
    public static class KartRacingSetupTools
    {
        private const int PrototypeCheckpointCount = 8;
        private const int PrototypeBotCount = 6;

        [MenuItem("Tools/Kart Racing/MVP Setup/Create Track Root")]
        public static void CreateTrackRootMenu()
        {
            var trackData = EnsureTrackRoot();
            EnsurePrototypeTrackLayout(trackData, false);
            AutoConfigureTrack(trackData);
            Selection.activeGameObject = trackData.gameObject;
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Auto Configure Selected Track")]
        public static void AutoConfigureSelectedTrackMenu()
        {
            var trackData = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<TrackData>()
                : null;

            if (trackData == null)
            {
                trackData = Object.FindFirstObjectByType<TrackData>();
            }

            if (trackData == null)
            {
                EditorUtility.DisplayDialog("Kart Racing Setup", "No TrackData found in the current scene.", "OK");
                return;
            }

            AutoConfigureTrack(trackData);
            Selection.activeGameObject = trackData.gameObject;
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Create Race Systems")]
        public static void CreateRaceSystemsMenu()
        {
            var trackData = EnsureTrackRoot();
            var raceManager = EnsureRaceSystems(trackData);
            Selection.activeGameObject = raceManager.gameObject;
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Create Player Kart")]
        public static void CreatePlayerKartMenu()
        {
            var trackData = EnsureTrackRoot();
            EnsurePrototypeTrackLayout(trackData, false);
            AutoConfigureTrack(trackData);

            var spawnPoint = trackData.GetSpawnPoint(0);
            var tracker = CreateKart("PlayerKart", true, spawnPoint, trackData);
            EnsureFollowCamera(tracker.transform);
            Selection.activeGameObject = tracker.gameObject;
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Create AI Kart")]
        public static void CreateAiKartMenu()
        {
            var trackData = EnsureTrackRoot();
            EnsurePrototypeTrackLayout(trackData, false);
            AutoConfigureTrack(trackData);

            var nextBotIndex = GetExistingBotCount() + 1;
            var spawnPoint = trackData.GetSpawnPoint(GetBotSpawnIndex(trackData, nextBotIndex));
            var tracker = CreateKart($"AIKart_{nextBotIndex:00}", false, spawnPoint, trackData);
            Selection.activeGameObject = tracker.gameObject;
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Create Follow Camera")]
        public static void CreateFollowCameraMenu()
        {
            EnsureFollowCamera(FindPlayerTarget());
        }

        [MenuItem("Tools/Kart Racing/MVP Setup/Build Minimal Prototype Scene")]
        public static void BuildMinimalPrototypeSceneMenu()
        {
            var trackData = EnsureTrackRoot();
            EnsurePrototypeTrackLayout(trackData, true);
            AutoConfigureTrack(trackData);

            var playerTracker = EnsurePlayerKart(trackData);
            EnsureAiKarts(trackData, PrototypeBotCount);
            EnsureFollowCamera(playerTracker.transform);

            var raceManager = EnsureRaceSystems(trackData);
            var allTrackers = Object.FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            raceManager.SetRegisteredRacers(allTrackers);

            EditorUtility.SetDirty(trackData);
            EditorUtility.SetDirty(raceManager.gameObject);
            Selection.activeGameObject = raceManager.gameObject;
        }

        private static TrackData EnsureTrackRoot()
        {
            var trackData = Object.FindFirstObjectByType<TrackData>();
            if (trackData != null)
            {
                EnsureChildContainer(trackData.transform, "Checkpoints");
                EnsureChildContainer(trackData.transform, "SpawnPoints");
                EnsureChildContainer(trackData.transform, "PowerUpBoxes");
                EnsureChildContainer(trackData.transform, "RespawnPoints");
                EnsureChildContainer(trackData.transform, "TrackBounds");
                EnsureChildContainer(trackData.transform, "OffTrackZones");
                return trackData;
            }

            var trackRoot = new GameObject("TrackRoot");
            Undo.RegisterCreatedObjectUndo(trackRoot, "Create TrackRoot");
            trackData = Undo.AddComponent<TrackData>(trackRoot);

            EnsureChildContainer(trackRoot.transform, "Checkpoints");
            EnsureChildContainer(trackRoot.transform, "SpawnPoints");
            EnsureChildContainer(trackRoot.transform, "PowerUpBoxes");
            EnsureChildContainer(trackRoot.transform, "RespawnPoints");
            EnsureChildContainer(trackRoot.transform, "TrackBounds");
            EnsureChildContainer(trackRoot.transform, "OffTrackZones");

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
            return trackData;
        }

        private static RaceManager EnsureRaceSystems(TrackData trackData)
        {
            var systemsRoot = GameObject.Find("RaceSystems");
            if (systemsRoot == null)
            {
                systemsRoot = new GameObject("RaceSystems");
                Undo.RegisterCreatedObjectUndo(systemsRoot, "Create RaceSystems");
            }

            var lapManager = systemsRoot.GetComponent<LapManager>() ?? Undo.AddComponent<LapManager>(systemsRoot);
            var positionManager = systemsRoot.GetComponent<PositionManager>() ?? Undo.AddComponent<PositionManager>(systemsRoot);
            var raceManager = systemsRoot.GetComponent<RaceManager>() ?? Undo.AddComponent<RaceManager>(systemsRoot);

            raceManager.SetTrackData(trackData);
            raceManager.SetLapManager(lapManager);
            raceManager.SetPositionManager(positionManager);
            lapManager.SetTrackData(trackData);
            positionManager.SetTrackData(trackData);

            EditorUtility.SetDirty(systemsRoot);
            return raceManager;
        }

        private static void EnsurePrototypeTrackLayout(TrackData trackData, bool createGroundPlane)
        {
            if (trackData == null)
            {
                return;
            }

            if (createGroundPlane)
            {
                EnsurePrototypeGround();
            }

            var checkpointsRoot = EnsureChildContainer(trackData.transform, "Checkpoints");
            var spawnPointsRoot = EnsureChildContainer(trackData.transform, "SpawnPoints");
            var powerUpRoot = EnsureChildContainer(trackData.transform, "PowerUpBoxes");
            var respawnRoot = EnsureChildContainer(trackData.transform, "RespawnPoints");

            if (checkpointsRoot.childCount == 0)
            {
                const float xRadius = 32f;
                const float zRadius = 20f;

                for (var index = 0; index < PrototypeCheckpointCount; index++)
                {
                    var checkpointObject = new GameObject($"Checkpoint_{index:00}");
                    Undo.RegisterCreatedObjectUndo(checkpointObject, "Create Prototype Checkpoint");
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
                    Undo.RegisterCreatedObjectUndo(respawnObject, "Create Prototype Respawn");
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
                    new Vector3(-2f, 0f, -14f),
                    new Vector3(2f, 0f, -14f),
                    new Vector3(-2f, 0f, -17f),
                    new Vector3(2f, 0f, -17f)
                };

                for (var index = 0; index < spawnOffsets.Length; index++)
                {
                    var spawnObject = new GameObject($"Spawn_{index:00}");
                    Undo.RegisterCreatedObjectUndo(spawnObject, "Create Prototype Spawn");
                    spawnObject.transform.SetParent(spawnPointsRoot, false);
                    spawnObject.transform.position = startReference.TransformPoint(spawnOffsets[index]);
                    spawnObject.transform.rotation = startReference.rotation;
                }
            }

            if (powerUpRoot.childCount == 0 && checkpointsRoot.childCount >= 4)
            {
                for (var index = 1; index < checkpointsRoot.childCount; index += 2)
                {
                    var checkpointTransform = checkpointsRoot.GetChild(index);
                    var powerUpPoint = new GameObject($"PowerUpBox_{index:00}");
                    Undo.RegisterCreatedObjectUndo(powerUpPoint, "Create Prototype PowerUp Point");
                    powerUpPoint.transform.SetParent(powerUpRoot, false);
                    powerUpPoint.transform.position = checkpointTransform.position - checkpointTransform.right * 3f + Vector3.up * 0.75f;
                    powerUpPoint.transform.rotation = checkpointTransform.rotation;
                }
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
        }

        private static void AutoConfigureTrack(TrackData trackData)
        {
            if (trackData == null)
            {
                return;
            }

            var checkpointsRoot = trackData.transform.Find("Checkpoints");
            if (checkpointsRoot == null)
            {
                return;
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
            }

            trackData.SyncChildCollections();
            EditorUtility.SetDirty(trackData);
        }

        private static CheckpointTracker EnsurePlayerKart(TrackData trackData)
        {
            var trackers = Object.FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < trackers.Length; index++)
            {
                if (trackers[index] != null && trackers[index].IsPlayer)
                {
                    trackers[index].SetTrackData(trackData);
                    return trackers[index];
                }
            }

            return CreateKart("PlayerKart", true, trackData.GetSpawnPoint(0), trackData);
        }

        private static void EnsureAiKarts(TrackData trackData, int botCount)
        {
            var existingBots = GetExistingBotCount();
            for (var index = existingBots; index < botCount; index++)
            {
                var spawnPoint = trackData.GetSpawnPoint(GetBotSpawnIndex(trackData, index + 1));
                CreateKart($"AIKart_{index + 1:00}", false, spawnPoint, trackData);
            }
        }

        private static int GetExistingBotCount()
        {
            var trackers = Object.FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var botCount = 0;

            for (var index = 0; index < trackers.Length; index++)
            {
                if (trackers[index] != null && !trackers[index].IsPlayer)
                {
                    botCount++;
                }
            }

            return botCount;
        }

        private static CheckpointTracker CreateKart(string kartName, bool isPlayer, Transform spawnPoint, TrackData trackData)
        {
            var kartObject = new GameObject(kartName);
            Undo.RegisterCreatedObjectUndo(kartObject, $"Create {kartName}");

            var spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            var spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            kartObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var rigidbody = Undo.AddComponent<Rigidbody>(kartObject);
            rigidbody.mass = 140f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var collider = Undo.AddComponent<BoxCollider>(kartObject);
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.4f, 0.9f, 2.4f);

            var kartController = Undo.AddComponent<KartController>(kartObject);
            var checkpointTracker = Undo.AddComponent<CheckpointTracker>(kartObject);
            checkpointTracker.SetTrackData(trackData);
            checkpointTracker.SetPlayerFlag(isPlayer);
            checkpointTracker.SetRecoveryReference(spawnPoint);

            if (isPlayer)
            {
                Undo.AddComponent<PlayerKartInput>(kartObject);
            }
            else
            {
                Undo.AddComponent<AIKartInput>(kartObject);
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(visual, $"Create {kartName} Visual");
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
                        color = isPlayer ? new Color(0.16f, 0.63f, 1f) : new Color(1f, 0.4f, 0.18f)
                    };

                    renderer.sharedMaterial = material;
                }
            }

            EditorUtility.SetDirty(kartController);
            return checkpointTracker;
        }

        private static void EnsurePrototypeGround()
        {
            if (GameObject.Find("PrototypeGround") != null)
            {
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Create Prototype Ground");
            ground.name = "PrototypeGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(15f, 1f, 15f);
        }

        private static void EnsureFollowCamera(Transform target)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
                camera = Undo.AddComponent<Camera>(cameraObject);
                camera.tag = "MainCamera";
                Undo.AddComponent<AudioListener>(cameraObject);
            }

            var cameraFollow = camera.GetComponent<CameraFollow>() ?? Undo.AddComponent<CameraFollow>(camera.gameObject);
            if (target != null)
            {
                cameraFollow.SetTarget(target);
            }

            EditorUtility.SetDirty(camera.gameObject);
            Selection.activeGameObject = camera.gameObject;
        }

        private static Transform FindPlayerTarget()
        {
            var trackers = Object.FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            for (var index = 0; index < trackers.Length; index++)
            {
                if (trackers[index] != null && trackers[index].IsPlayer)
                {
                    return trackers[index].transform;
                }
            }

            return Selection.activeTransform;
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

        private static int GetBotSpawnIndex(TrackData trackData, int botNumber)
        {
            if (trackData == null || trackData.SpawnPointCount <= 1)
            {
                return 0;
            }

            return Mathf.Clamp(botNumber, 1, trackData.SpawnPointCount - 1);
        }
    }
}
#endif
