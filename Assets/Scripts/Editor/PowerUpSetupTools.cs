#if UNITY_EDITOR
using KartGame.Kart;
using KartGame.PowerUps;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using KartGame.AI.Reinforcement;

namespace KartGame.EditorTools
{
    public static class PowerUpSetupTools
    {
        // Editor helpers for power-up controller/brain setup.
        [MenuItem("Tools/Kart Racing/Power-Ups/Create Player PowerUp Controller Child")]
        public static void CreatePlayerPowerUpControllerChild()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "Selecciona primero el kart del jugador.", "OK");
                return;
            }

            var kartRoot = Selection.activeGameObject;
            var kartController = kartRoot.GetComponent<KartController>();
            var checkpointTracker = kartRoot.GetComponent<CheckpointTracker>();

            if (kartController == null || checkpointTracker == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "El objeto seleccionado debe tener KartController y CheckpointTracker.", "OK");
                return;
            }

            var playerKartInput = kartRoot.GetComponent<PlayerKartInput>();
            if (playerKartInput == null)
            {
                playerKartInput = Undo.AddComponent<PlayerKartInput>(kartRoot);
            }

            var controllerRoot = FindOrCreateChild(kartRoot.transform, "PowerUpController");
            var forwardAnchor = FindOrCreateChild(controllerRoot.transform, "ForwardLaunchPoint");
            var rearAnchor = FindOrCreateChild(controllerRoot.transform, "RearDropPoint");

            ConfigureAnchors(forwardAnchor.transform, rearAnchor.transform);

            var powerUpController = controllerRoot.GetComponent<KartPowerUpController>();
            if (powerUpController == null)
            {
                powerUpController = Undo.AddComponent<KartPowerUpController>(controllerRoot);
            }

            var indicatorVisualizer = controllerRoot.GetComponent<PowerUpContextIndicatorVisualizer>();
            if (indicatorVisualizer == null)
            {
                indicatorVisualizer = Undo.AddComponent<PowerUpContextIndicatorVisualizer>(controllerRoot);
            }

            RemoveComponentIfExists<KartPowerUpBotBrain>(controllerRoot);
            RemoveComponentIfExists<KartPowerUpAgent>(controllerRoot);
            RemoveComponentIfExists<BehaviorParameters>(controllerRoot);
            RemoveComponentIfExists<DecisionRequester>(controllerRoot);
            RemoveComponentIfExists<CheckpointAwareRayPerceptionSensorComponent3D>(controllerRoot);

            AssignControllerReferences(powerUpController, kartController, checkpointTracker, forwardAnchor.transform, rearAnchor.transform);
            ConfigurePlayerController(powerUpController);
            AssignPlayerInputReferences(playerKartInput, kartController, checkpointTracker);
            AssignIndicatorVisualizerReferences(indicatorVisualizer, powerUpController, null, kartController, checkpointTracker);
            indicatorVisualizer.EnsureIndicatorObjects();

            Selection.activeGameObject = controllerRoot;
            EditorUtility.SetDirty(kartRoot);
            EditorUtility.SetDirty(controllerRoot);
            EditorUtility.DisplayDialog("Power-Ups", "Hijo PowerUpController del jugador creado y configurado.", "OK");
        }

        [MenuItem("Tools/Kart Racing/Power-Ups/Create Bot PowerUp Controller Child")]
        public static void CreateBotPowerUpControllerChild()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "Selecciona primero el kart bot.", "OK");
                return;
            }

            var kartRoot = Selection.activeGameObject;
            var kartController = kartRoot.GetComponent<KartController>();
            var checkpointTracker = kartRoot.GetComponent<CheckpointTracker>();

            if (kartController == null || checkpointTracker == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "El objeto seleccionado debe tener KartController y CheckpointTracker.", "OK");
                return;
            }

            var controllerRoot = FindOrCreateChild(kartRoot.transform, "PowerUpController");
            var forwardAnchor = FindOrCreateChild(controllerRoot.transform, "ForwardLaunchPoint");
            var rearAnchor = FindOrCreateChild(controllerRoot.transform, "RearDropPoint");
            ConfigureAnchors(forwardAnchor.transform, rearAnchor.transform);

            var powerUpController = controllerRoot.GetComponent<KartPowerUpController>();
            if (powerUpController == null)
            {
                powerUpController = Undo.AddComponent<KartPowerUpController>(controllerRoot);
            }

            var indicatorVisualizer = controllerRoot.GetComponent<PowerUpContextIndicatorVisualizer>();
            if (indicatorVisualizer == null)
            {
                indicatorVisualizer = Undo.AddComponent<PowerUpContextIndicatorVisualizer>(controllerRoot);
            }

            var legacyBotBrain = controllerRoot.GetComponent<KartPowerUpBotBrain>();
            if (legacyBotBrain != null)
            {
                Undo.DestroyObjectImmediate(legacyBotBrain);
            }

            var powerUpAgent = controllerRoot.GetComponent<KartPowerUpAgent>();
            if (powerUpAgent == null)
            {
                powerUpAgent = Undo.AddComponent<KartPowerUpAgent>(controllerRoot);
            }

            var behaviorParameters = controllerRoot.GetComponent<BehaviorParameters>();
            if (behaviorParameters == null)
            {
                behaviorParameters = Undo.AddComponent<BehaviorParameters>(controllerRoot);
            }

            var decisionRequester = controllerRoot.GetComponent<DecisionRequester>();
            if (decisionRequester == null)
            {
                decisionRequester = Undo.AddComponent<DecisionRequester>(controllerRoot);
            }

            var wallSensor = controllerRoot.GetComponent<CheckpointAwareRayPerceptionSensorComponent3D>();
            if (wallSensor == null)
            {
                wallSensor = Undo.AddComponent<CheckpointAwareRayPerceptionSensorComponent3D>(controllerRoot);
            }

            AssignControllerReferences(powerUpController, kartController, checkpointTracker, forwardAnchor.transform, rearAnchor.transform);
            AssignAgentReferences(powerUpAgent, powerUpController, kartController, checkpointTracker);
            ConfigureBehaviorParameters(behaviorParameters);
            ConfigureDecisionRequester(decisionRequester);
            ConfigureWallSensor(wallSensor, checkpointTracker);
            AssignIndicatorVisualizerReferences(indicatorVisualizer, powerUpController, powerUpAgent, kartController, checkpointTracker);
            indicatorVisualizer.EnsureIndicatorObjects();

            Selection.activeGameObject = controllerRoot;
            EditorUtility.SetDirty(controllerRoot);
            EditorUtility.DisplayDialog("Power-Ups", "Hijo PowerUpController ML-Agents creado y configurado.", "OK");
        }

        [MenuItem("Tools/Kart Racing/Power-Ups/Create Bot Random Forest PowerUp Brain")]
        public static void CreateBotRandomForestPowerUpBrain()
        {
            CreateBotRandomForestPowerUpBrainInternal(
                "Assets/Data/classifier_rules/powerups_random_forest.json",
                "GameObject PowerUpBrain con Random Forest base creado y configurado.");
        }

        [MenuItem("Tools/Kart Racing/Power-Ups/Create Bot Aggressive Random Forest PowerUp Brain")]
        public static void CreateBotAggressiveRandomForestPowerUpBrain()
        {
            CreateBotRandomForestPowerUpBrainInternal(
                "Assets/Data/classifier_rules/powerups_random_forest_agresivo.json",
                "GameObject PowerUpBrain agresivo creado y configurado.");
        }

        [MenuItem("Tools/Kart Racing/Power-Ups/Create Bot Peaceful Random Forest PowerUp Brain")]
        public static void CreateBotPeacefulRandomForestPowerUpBrain()
        {
            CreateBotRandomForestPowerUpBrainInternal(
                "Assets/Data/classifier_rules/powerups_random_forest_pacifico.json",
                "GameObject PowerUpBrain pacifico creado y configurado.");
        }

        private static void CreateBotRandomForestPowerUpBrainInternal(string modelJsonPath, string successMessage)
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "Selecciona primero el kart bot.", "OK");
                return;
            }

            var kartRoot = Selection.activeGameObject;
            var kartController = kartRoot.GetComponent<KartController>();
            var checkpointTracker = kartRoot.GetComponent<CheckpointTracker>();

            if (kartController == null || checkpointTracker == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "El objeto seleccionado debe tener KartController y CheckpointTracker.", "OK");
                return;
            }

            var controllerRoot = FindOrCreateChild(kartRoot.transform, "PowerUpController");
            var forwardAnchor = FindOrCreateChild(controllerRoot.transform, "ForwardLaunchPoint");
            var rearAnchor = FindOrCreateChild(controllerRoot.transform, "RearDropPoint");
            var brainRoot = FindOrCreateChild(controllerRoot.transform, "PowerUpBrain");

            ConfigureAnchors(forwardAnchor.transform, rearAnchor.transform);

            var powerUpController = controllerRoot.GetComponent<KartPowerUpController>();
            if (powerUpController == null)
            {
                powerUpController = Undo.AddComponent<KartPowerUpController>(controllerRoot);
            }

            var indicatorVisualizer = controllerRoot.GetComponent<PowerUpContextIndicatorVisualizer>();
            if (indicatorVisualizer == null)
            {
                indicatorVisualizer = Undo.AddComponent<PowerUpContextIndicatorVisualizer>(controllerRoot);
            }

            RemoveComponentIfExists<KartPowerUpBotBrain>(controllerRoot);
            RemoveComponentIfExists<KartPowerUpAgent>(controllerRoot);
            RemoveComponentIfExists<BehaviorParameters>(controllerRoot);
            RemoveComponentIfExists<DecisionRequester>(controllerRoot);
            RemoveComponentIfExists<CheckpointAwareRayPerceptionSensorComponent3D>(controllerRoot);
            RemoveComponentIfExists<KartPowerUpBotBrain>(brainRoot);
            RemoveComponentIfExists<KartPowerUpAgent>(brainRoot);
            RemoveComponentIfExists<BehaviorParameters>(brainRoot);
            RemoveComponentIfExists<DecisionRequester>(brainRoot);
            RemoveComponentIfExists<CheckpointAwareRayPerceptionSensorComponent3D>(brainRoot);

            var randomForestBrain = brainRoot.GetComponent<RandomForestPowerUpBrain>();
            if (randomForestBrain == null)
            {
                randomForestBrain = Undo.AddComponent<RandomForestPowerUpBrain>(brainRoot);
            }

            AssignControllerReferences(powerUpController, kartController, checkpointTracker, forwardAnchor.transform, rearAnchor.transform);
            ConfigureBotController(powerUpController);
            AssignRandomForestBrainReferences(
                randomForestBrain,
                powerUpController,
                kartController,
                checkpointTracker,
                AssetDatabase.LoadAssetAtPath<TextAsset>(modelJsonPath));
            AssignIndicatorVisualizerReferences(indicatorVisualizer, powerUpController, null, kartController, checkpointTracker);
            indicatorVisualizer.EnsureIndicatorObjects();

            Selection.activeGameObject = brainRoot;
            EditorUtility.SetDirty(controllerRoot);
            EditorUtility.SetDirty(brainRoot);
            EditorUtility.DisplayDialog("Power-Ups", successMessage, "OK");
        }

        [MenuItem("Tools/Kart Racing/Power-Ups/Create Context Indicators")]
        public static void CreateContextIndicators()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "Selecciona primero el kart o el PowerUpController.", "OK");
                return;
            }

            var selectedObject = Selection.activeGameObject;
            var controllerRoot = ResolveControllerRoot(selectedObject);
            if (controllerRoot == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "No se ha encontrado un PowerUpController en el objeto seleccionado ni en sus hijos.", "OK");
                return;
            }

            var powerUpController = controllerRoot.GetComponent<KartPowerUpController>();
            if (powerUpController == null)
            {
                EditorUtility.DisplayDialog("Power-Ups", "El PowerUpController no tiene KartPowerUpController.", "OK");
                return;
            }

            var kartRoot = controllerRoot.transform.parent != null ? controllerRoot.transform.parent.gameObject : selectedObject;
            var kartController = kartRoot.GetComponent<KartController>();
            var checkpointTracker = kartRoot.GetComponent<CheckpointTracker>();
            var powerUpAgent = controllerRoot.GetComponent<KartPowerUpAgent>();

            var indicatorVisualizer = controllerRoot.GetComponent<PowerUpContextIndicatorVisualizer>();
            if (indicatorVisualizer == null)
            {
                indicatorVisualizer = Undo.AddComponent<PowerUpContextIndicatorVisualizer>(controllerRoot);
            }

            AssignIndicatorVisualizerReferences(indicatorVisualizer, powerUpController, powerUpAgent, kartController, checkpointTracker);
            indicatorVisualizer.EnsureIndicatorObjects();

            Selection.activeGameObject = controllerRoot;
            EditorUtility.SetDirty(controllerRoot);
            EditorUtility.DisplayDialog("Power-Ups", "Indicadores de contexto creados/configurados.", "OK");
        }

        private static void ConfigureAnchors(Transform forwardAnchor, Transform rearAnchor)
        {
            forwardAnchor.localPosition = new Vector3(0f, 0.35f, 1.5f);
            forwardAnchor.localRotation = Quaternion.identity;

            rearAnchor.localPosition = new Vector3(0f, 0.35f, -1.35f);
            rearAnchor.localRotation = Quaternion.identity;
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject ResolveControllerRoot(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                return null;
            }

            if (selectedObject.GetComponent<KartPowerUpController>() != null)
            {
                return selectedObject;
            }

            var controllerInChildren = selectedObject.GetComponentInChildren<KartPowerUpController>(true);
            return controllerInChildren != null ? controllerInChildren.gameObject : null;
        }

        private static void AssignControllerReferences(
            KartPowerUpController powerUpController,
            KartController kartController,
            CheckpointTracker checkpointTracker,
            Transform forwardAnchor,
            Transform rearAnchor)
        {
            var serializedObject = new SerializedObject(powerUpController);
            serializedObject.FindProperty("kartController").objectReferenceValue = kartController;
            serializedObject.FindProperty("checkpointTracker").objectReferenceValue = checkpointTracker;
            serializedObject.FindProperty("forwardLaunchPoint").objectReferenceValue = forwardAnchor;
            serializedObject.FindProperty("rearDropPoint").objectReferenceValue = rearAnchor;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(powerUpController);
        }

        private static void ConfigurePlayerController(KartPowerUpController powerUpController)
        {
            var serializedObject = new SerializedObject(powerUpController);
            serializedObject.FindProperty("awardPointEveryXCheckpointsForBots").boolValue = false;
            serializedObject.FindProperty("logCheckpointPointGain").boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(powerUpController);
        }

        private static void ConfigureBotController(KartPowerUpController powerUpController)
        {
            var serializedObject = new SerializedObject(powerUpController);
            serializedObject.FindProperty("awardPointEveryXCheckpointsForBots").boolValue = true;
            serializedObject.FindProperty("logCheckpointPointGain").boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(powerUpController);
        }

        private static void AssignAgentReferences(
            KartPowerUpAgent powerUpAgent,
            KartPowerUpController powerUpController,
            KartController kartController,
            CheckpointTracker checkpointTracker)
        {
            var serializedObject = new SerializedObject(powerUpAgent);
            serializedObject.FindProperty("powerUpController").objectReferenceValue = powerUpController;
            serializedObject.FindProperty("kartController").objectReferenceValue = kartController;
            serializedObject.FindProperty("checkpointTracker").objectReferenceValue = checkpointTracker;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(powerUpAgent);
        }

        private static void AssignIndicatorVisualizerReferences(
            PowerUpContextIndicatorVisualizer indicatorVisualizer,
            KartPowerUpController powerUpController,
            KartPowerUpAgent powerUpAgent,
            KartController kartController,
            CheckpointTracker checkpointTracker)
        {
            var serializedObject = new SerializedObject(indicatorVisualizer);
            serializedObject.FindProperty("powerUpController").objectReferenceValue = powerUpController;
            serializedObject.FindProperty("powerUpAgent").objectReferenceValue = powerUpAgent;
            serializedObject.FindProperty("kartController").objectReferenceValue = kartController;
            serializedObject.FindProperty("checkpointTracker").objectReferenceValue = checkpointTracker;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(indicatorVisualizer);
        }

        private static void AssignRandomForestBrainReferences(
            RandomForestPowerUpBrain randomForestBrain,
            KartPowerUpController powerUpController,
            KartController kartController,
            CheckpointTracker checkpointTracker,
            TextAsset modelJson)
        {
            var serializedObject = new SerializedObject(randomForestBrain);
            serializedObject.FindProperty("powerUpController").objectReferenceValue = powerUpController;
            serializedObject.FindProperty("kartController").objectReferenceValue = kartController;
            serializedObject.FindProperty("checkpointTracker").objectReferenceValue = checkpointTracker;
            serializedObject.FindProperty("modelJson").objectReferenceValue = modelJson;
            serializedObject.FindProperty("wallSenseMask").intValue = LayerMask.GetMask("KartWall");
            serializedObject.FindProperty("requireRaceToBeActive").boolValue = true;
            serializedObject.FindProperty("logPredictedPowerUps").boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(randomForestBrain);
        }

        private static void AssignPlayerInputReferences(
            PlayerKartInput playerKartInput,
            KartController kartController,
            CheckpointTracker checkpointTracker)
        {
            var serializedObject = new SerializedObject(playerKartInput);
            serializedObject.FindProperty("kartController").objectReferenceValue = kartController;
            serializedObject.FindProperty("checkpointTracker").objectReferenceValue = checkpointTracker;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerKartInput);
        }

        private static void ConfigureBehaviorParameters(BehaviorParameters behaviorParameters)
        {
            behaviorParameters.BehaviorName = "PowerUpAgent";
            behaviorParameters.BehaviorType = BehaviorType.Default;
            behaviorParameters.BrainParameters.VectorObservationSize = 13;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5);
            EditorUtility.SetDirty(behaviorParameters);
        }

        private static void ConfigureDecisionRequester(DecisionRequester decisionRequester)
        {
            decisionRequester.DecisionPeriod = 5;
            decisionRequester.TakeActionsBetweenDecisions = true;
            EditorUtility.SetDirty(decisionRequester);
        }

        private static void ConfigureWallSensor(CheckpointAwareRayPerceptionSensorComponent3D wallSensor, CheckpointTracker checkpointTracker)
        {
            wallSensor.SensorName = "PowerUpWallSensor";
            wallSensor.RaysPerDirection = 2;
            wallSensor.MaxRayDegrees = 45f;
            wallSensor.SphereCastRadius = 0.25f;
            wallSensor.RayLength = 8f;
            wallSensor.StartVerticalOffset = 0.75f;
            wallSensor.EndVerticalOffset = 0f;
            wallSensor.DetectableTags = new System.Collections.Generic.List<string> { "Wall" };
            wallSensor.RayLayerMask = LayerMask.GetMask("KartWall");
            wallSensor.CheckpointTracker = checkpointTracker;
            wallSensor.IgnorePassedCheckpoints = false;
            wallSensor.LimitCheckpointDetectionWindow = false;
            wallSensor.AdditionalVisibleCheckpointsAhead = 0;
            wallSensor.ConfigureDebugGizmos(
                new Color(0.15f, 0.85f, 1f, 1f),
                new Color(0.55f, 0.65f, 0.75f, 1f),
                false);
            EditorUtility.SetDirty(wallSensor);
        }

        private static void RemoveComponentIfExists<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }
    }
}
#endif
