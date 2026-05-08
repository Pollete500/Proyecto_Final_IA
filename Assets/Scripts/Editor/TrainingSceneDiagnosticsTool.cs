#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KartGame.AI.Reinforcement;
using KartGame.Core;
using KartGame.Kart;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartGame.EditorTools
{
    /*
     * Script: TrainingSceneDiagnosticsTool.cs
     * Purpose: Exports a plain-text diagnostics report for the active scene so ML-Agents training setup can be reviewed outside Unity.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: TrackData, TrainingSceneManager, KartAgent, BehaviorParameters, DecisionRequester, RayPerceptionSensorComponent3D.
     * Inspector Setup: Use the Tools/Kart Racing/ML-Agents menu in the Unity Editor. The tool scans the active scene and writes a report under Assets/Documentation/Diagnostics/.
     */
    public static class TrainingSceneDiagnosticsTool
    {
        private const string ReportDirectory = "Assets/Documentation/Diagnostics";
        private const string WallTag = "Wall";
        private const string CheckpointTag = "Checkpoint";
        private const string OffTrackTag = "OffTrack";

        [MenuItem("Tools/Kart Racing/ML-Agents/Export Training Scene Report")]
        public static void ExportTrainingSceneReport()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Training Scene Report", "No loaded active scene was found.", "OK");
                return;
            }

            var blockers = new List<string>();
            var warnings = new List<string>();
            var notes = new List<string>();
            var report = BuildReport(scene, blockers, warnings, notes);

            Directory.CreateDirectory(Path.GetFullPath(ReportDirectory));

            var fileName = $"{SanitizeFileName(scene.name)}_TrainingSceneReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var assetPath = $"{ReportDirectory}/{fileName}";
            var absolutePath = Path.GetFullPath(assetPath);

            File.WriteAllText(absolutePath, report, Encoding.UTF8);
            AssetDatabase.Refresh();

            var reportAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (reportAsset != null)
            {
                Selection.activeObject = reportAsset;
                EditorGUIUtility.PingObject(reportAsset);
            }

            var summary = $"Report exported to:\n{assetPath}\n\nBlockers: {blockers.Count}\nWarnings: {warnings.Count}\nNotes: {notes.Count}";
            EditorUtility.DisplayDialog("Training Scene Report", summary, "OK");
            Debug.Log($"Training scene diagnostics report exported: {assetPath}");
        }

        private static string BuildReport(Scene scene, List<string> blockers, List<string> warnings, List<string> notes)
        {
            var builder = new StringBuilder(8192);

            var trackDataObjects = GetSceneComponents<TrackData>(scene);
            var trainingManagers = GetSceneComponents<TrainingSceneManager>(scene);
            var kartAgents = GetSceneComponents<KartAgent>(scene);
            var checkpointTrackers = GetSceneComponents<CheckpointTracker>(scene);
            var colliders = GetSceneComponents<Collider>(scene);

            AppendHeader(builder, scene);
            AppendSceneOverview(builder, scene, trackDataObjects, trainingManagers, kartAgents, checkpointTrackers, colliders);
            AnalyzeTrackData(builder, trackDataObjects, blockers, warnings, notes);
            AnalyzeTrainingManagers(builder, trainingManagers, blockers, warnings, notes);
            AnalyzeAgents(builder, kartAgents, blockers, warnings, notes);
            AppendSummary(builder, blockers, warnings, notes);

            return builder.ToString();
        }

        private static void AppendHeader(StringBuilder builder, Scene scene)
        {
            builder.AppendLine("Kart Racing ML-Agents Training Scene Report");
            builder.AppendLine("==========================================");
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Scene: {scene.name}");
            builder.AppendLine($"Scene Path: {scene.path}");
            builder.AppendLine();
        }

        private static void AppendSceneOverview(
            StringBuilder builder,
            Scene scene,
            IReadOnlyList<TrackData> trackDataObjects,
            IReadOnlyList<TrainingSceneManager> trainingManagers,
            IReadOnlyList<KartAgent> kartAgents,
            IReadOnlyList<CheckpointTracker> checkpointTrackers,
            IReadOnlyList<Collider> colliders)
        {
            var wallCount = 0;
            var checkpointTagCount = 0;
            var offTrackCount = 0;

            for (var index = 0; index < colliders.Count; index++)
            {
                var collider = colliders[index];
                if (collider == null)
                {
                    continue;
                }

                var tag = collider.gameObject.tag;
                if (tag == WallTag)
                {
                    wallCount++;
                }
                else if (tag == CheckpointTag)
                {
                    checkpointTagCount++;
                }
                else if (tag == OffTrackTag)
                {
                    offTrackCount++;
                }
            }

            builder.AppendLine("Scene Overview");
            builder.AppendLine("--------------");
            builder.AppendLine($"Root Objects: {scene.rootCount}");
            builder.AppendLine($"TrackData Count: {trackDataObjects.Count}");
            builder.AppendLine($"TrainingSceneManager Count: {trainingManagers.Count}");
            builder.AppendLine($"KartAgent Count: {kartAgents.Count}");
            builder.AppendLine($"CheckpointTracker Count: {checkpointTrackers.Count}");
            builder.AppendLine($"Wall Collider Count (tagged '{WallTag}'): {wallCount}");
            builder.AppendLine($"Checkpoint Trigger Count (tagged '{CheckpointTag}'): {checkpointTagCount}");
            builder.AppendLine($"OffTrack Trigger Count (tagged '{OffTrackTag}'): {offTrackCount}");
            builder.AppendLine();
        }

        private static void AnalyzeTrackData(
            StringBuilder builder,
            IReadOnlyList<TrackData> trackDataObjects,
            List<string> blockers,
            List<string> warnings,
            List<string> notes)
        {
            builder.AppendLine("TrackData Analysis");
            builder.AppendLine("------------------");

            if (trackDataObjects.Count == 0)
            {
                blockers.Add("No TrackData was found in the active scene.");
                builder.AppendLine("No TrackData components found.");
                builder.AppendLine();
                return;
            }

            if (trackDataObjects.Count > 1)
            {
                warnings.Add($"Found {trackDataObjects.Count} TrackData components. Training should usually use exactly one.");
            }

            for (var trackIndex = 0; trackIndex < trackDataObjects.Count; trackIndex++)
            {
                var trackData = trackDataObjects[trackIndex];
                builder.AppendLine($"TrackData [{trackIndex}] : {GetHierarchyPath(trackData.transform)}");
                builder.AppendLine($"  Laps To Win: {trackData.LapsToWin}");
                builder.AppendLine($"  Checkpoints: {trackData.CheckpointCount}");
                builder.AppendLine($"  Spawn Points: {trackData.SpawnPointCount}");
                builder.AppendLine($"  Respawn Points: {trackData.RespawnPoints.Length}");
                builder.AppendLine($"  PowerUp Boxes: {trackData.PowerUpBoxes.Length}");

                if (trackData.CheckpointCount < 2)
                {
                    blockers.Add($"TrackData '{trackData.name}' has fewer than 2 checkpoints.");
                }

                AnalyzeTrackArray(builder, trackData, "Checkpoints", trackData.Checkpoints, true, blockers, warnings);
                AnalyzeTrackArray(builder, trackData, "SpawnPoints", trackData.SpawnPoints, false, blockers, warnings);
                AnalyzeTrackArray(builder, trackData, "RespawnPoints", trackData.RespawnPoints, false, blockers, warnings);
                AnalyzeTrackArray(builder, trackData, "PowerUpBoxes", trackData.PowerUpBoxes, false, blockers, warnings);

                var checkpointsRoot = trackData.transform.Find("Checkpoints");
                var spawnRoot = trackData.transform.Find("SpawnPoints");
                var respawnRoot = trackData.transform.Find("RespawnPoints");
                var trackBoundsRoot = trackData.transform.Find("TrackBounds");
                var offTrackRoot = trackData.transform.Find("OffTrackZones");

                if (checkpointsRoot != null && checkpointsRoot.childCount != trackData.CheckpointCount)
                {
                    warnings.Add($"TrackData '{trackData.name}' has {trackData.CheckpointCount} checkpoint refs but {checkpointsRoot.childCount} direct children under Checkpoints.");
                }

                if (spawnRoot == null || spawnRoot.childCount == 0)
                {
                    blockers.Add($"TrackData '{trackData.name}' has no spawn points under SpawnPoints.");
                }

                if (respawnRoot == null || respawnRoot.childCount == 0)
                {
                    warnings.Add($"TrackData '{trackData.name}' has no respawn points under RespawnPoints.");
                }

                if (trackBoundsRoot == null || trackBoundsRoot.childCount == 0)
                {
                    warnings.Add($"TrackData '{trackData.name}' has no children under TrackBounds. Wall perception may be incomplete.");
                }

                if (offTrackRoot == null || offTrackRoot.childCount == 0)
                {
                    notes.Add($"TrackData '{trackData.name}' has no OffTrackZones children. endEpisodeOnOffTrack will never trigger unless other off-track triggers exist elsewhere.");
                }

                builder.AppendLine();
            }
        }

        private static void AnalyzeTrackArray(
            StringBuilder builder,
            TrackData trackData,
            string label,
            Transform[] items,
            bool inspectCheckpointComponents,
            List<string> blockers,
            List<string> warnings)
        {
            if (items == null)
            {
                builder.AppendLine($"  {label}: null array");
                blockers.Add($"TrackData '{trackData.name}' has a null {label} array.");
                return;
            }

            var duplicatePaths = new HashSet<string>();

            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                if (item == null)
                {
                    builder.AppendLine($"  {label}[{index}] : MISSING");
                    blockers.Add($"TrackData '{trackData.name}' has a missing reference in {label}[{index}].");
                    continue;
                }

                var path = GetHierarchyPath(item);
                builder.AppendLine($"  {label}[{index}] : {path}");

                if (!duplicatePaths.Add(path))
                {
                    warnings.Add($"TrackData '{trackData.name}' contains a duplicate reference in {label}: {path}");
                }

                if (!inspectCheckpointComponents)
                {
                    continue;
                }

                var checkpoint = item.GetComponent<Checkpoint>();
                if (checkpoint == null)
                {
                    blockers.Add($"Checkpoint '{path}' is missing the Checkpoint component.");
                    continue;
                }

                if (checkpoint.TrackData != trackData)
                {
                    warnings.Add($"Checkpoint '{path}' points to a different TrackData than '{trackData.name}'.");
                }

                if (checkpoint.CheckpointIndex != index)
                {
                    warnings.Add($"Checkpoint '{path}' has CheckpointIndex {checkpoint.CheckpointIndex} but is at TrackData index {index}.");
                }

                var collider = item.GetComponent<Collider>();
                if (collider == null)
                {
                    blockers.Add($"Checkpoint '{path}' is missing a Collider.");
                }
                else if (!collider.isTrigger)
                {
                    warnings.Add($"Checkpoint '{path}' collider is not marked as trigger.");
                }

                if (item.gameObject.tag != CheckpointTag)
                {
                    warnings.Add($"Checkpoint '{path}' is tagged '{item.gameObject.tag}' instead of '{CheckpointTag}'.");
                }
            }
        }

        private static void AnalyzeTrainingManagers(
            StringBuilder builder,
            IReadOnlyList<TrainingSceneManager> trainingManagers,
            List<string> blockers,
            List<string> warnings,
            List<string> notes)
        {
            builder.AppendLine("TrainingSceneManager Analysis");
            builder.AppendLine("-----------------------------");

            if (trainingManagers.Count == 0)
            {
                blockers.Add("No TrainingSceneManager was found in the active scene.");
                builder.AppendLine("No TrainingSceneManager components found.");
                builder.AppendLine();
                return;
            }

            if (trainingManagers.Count > 1)
            {
                warnings.Add($"Found {trainingManagers.Count} TrainingSceneManager components. Training should usually use exactly one.");
            }

            for (var index = 0; index < trainingManagers.Count; index++)
            {
                var manager = trainingManagers[index];
                var serializedManager = new SerializedObject(manager);

                var randomizeSpawnPoint = serializedManager.FindProperty("randomizeSpawnPoint")?.boolValue ?? false;
                var spawnPositionJitter = serializedManager.FindProperty("spawnPositionJitter")?.floatValue ?? 0f;
                var spawnYawJitter = serializedManager.FindProperty("spawnYawJitter")?.floatValue ?? 0f;
                var spawnLift = serializedManager.FindProperty("spawnLift")?.floatValue ?? 0f;
                var registeredAgents = serializedManager.FindProperty("registeredAgents");
                var autoDiscoverAgents = serializedManager.FindProperty("autoDiscoverAgents")?.boolValue ?? false;

                builder.AppendLine($"TrainingSceneManager [{index}] : {GetHierarchyPath(manager.transform)}");
                builder.AppendLine($"  TrackData Assigned: {(manager.TrackData != null ? manager.TrackData.name : "None")}");
                builder.AppendLine($"  Auto Discover Agents: {autoDiscoverAgents}");
                builder.AppendLine($"  Randomize Spawn Point: {randomizeSpawnPoint}");
                builder.AppendLine($"  Spawn Position Jitter: {spawnPositionJitter:0.###}");
                builder.AppendLine($"  Spawn Yaw Jitter: {spawnYawJitter:0.###}");
                builder.AppendLine($"  Spawn Lift: {spawnLift:0.###}");
                builder.AppendLine($"  Registered Agents: {(registeredAgents != null ? registeredAgents.arraySize : 0)}");

                if (manager.TrackData == null)
                {
                    blockers.Add($"TrainingSceneManager '{manager.name}' does not have a TrackData reference.");
                }

                if (randomizeSpawnPoint || spawnPositionJitter > 0f || spawnYawJitter > 0f)
                {
                    notes.Add(
                        $"TrainingSceneManager '{manager.name}' uses spawn randomization or jitter. " +
                        "This is normal for training but causes visible teleport-like spawn changes between episodes.");
                }

                if (spawnLift <= 0f)
                {
                    warnings.Add($"TrainingSceneManager '{manager.name}' has Spawn Lift <= 0. The kart may spawn intersecting the track.");
                }

                builder.AppendLine();
            }
        }

        private static void AnalyzeAgents(
            StringBuilder builder,
            IReadOnlyList<KartAgent> kartAgents,
            List<string> blockers,
            List<string> warnings,
            List<string> notes)
        {
            builder.AppendLine("KartAgent Analysis");
            builder.AppendLine("------------------");

            if (kartAgents.Count == 0)
            {
                blockers.Add("No KartAgent was found in the active scene.");
                builder.AppendLine("No KartAgent components found.");
                builder.AppendLine();
                return;
            }

            for (var index = 0; index < kartAgents.Count; index++)
            {
                var agent = kartAgents[index];
                var behaviorParameters = agent.GetComponent<BehaviorParameters>();
                var decisionRequester = agent.GetComponent<DecisionRequester>();
                var raySensor = agent.GetComponent<RayPerceptionSensorComponent3D>();
                var rigidbody = agent.GetComponent<Rigidbody>();
                var kartController = agent.GetComponent<KartController>();
                var checkpointTracker = agent.GetComponent<CheckpointTracker>();
                var rewardManager = agent.GetComponent<AgentRewardManager>();

                builder.AppendLine($"KartAgent [{index}] : {GetHierarchyPath(agent.transform)}");
                builder.AppendLine($"  Rigidbody: {(rigidbody != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  KartController: {(kartController != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  CheckpointTracker: {(checkpointTracker != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  AgentRewardManager: {(rewardManager != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  BehaviorParameters: {(behaviorParameters != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  DecisionRequester: {(decisionRequester != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  RayPerceptionSensorComponent3D: {(raySensor != null ? "OK" : "MISSING")}");
                builder.AppendLine($"  MaxStep: {agent.MaxStep}");

                if (rigidbody == null) blockers.Add($"KartAgent '{agent.name}' is missing Rigidbody.");
                if (kartController == null) blockers.Add($"KartAgent '{agent.name}' is missing KartController.");
                if (checkpointTracker == null) blockers.Add($"KartAgent '{agent.name}' is missing CheckpointTracker.");
                if (rewardManager == null) blockers.Add($"KartAgent '{agent.name}' is missing AgentRewardManager.");
                if (behaviorParameters == null) blockers.Add($"KartAgent '{agent.name}' is missing BehaviorParameters.");
                if (decisionRequester == null) blockers.Add($"KartAgent '{agent.name}' is missing DecisionRequester.");
                if (raySensor == null) blockers.Add($"KartAgent '{agent.name}' is missing RayPerceptionSensorComponent3D.");

                if (checkpointTracker != null)
                {
                    builder.AppendLine($"  Tracker IsPlayer: {checkpointTracker.IsPlayer}");
                    builder.AppendLine($"  Tracker TrackData: {(checkpointTracker.TrackData != null ? checkpointTracker.TrackData.name : "None")}");

                    var trackerSerialized = new SerializedObject(checkpointTracker);
                    var autoRespawn = trackerSerialized.FindProperty("autoRespawnIfStuck")?.boolValue ?? false;
                    var stuckSeconds = trackerSerialized.FindProperty("secondsBeforeAutoRespawn")?.floatValue ?? 0f;
                    var allowPlayerAutoRespawn = trackerSerialized.FindProperty("allowPlayerAutoRespawn")?.boolValue ?? false;

                    builder.AppendLine($"  Auto Respawn If Stuck: {autoRespawn}");
                    builder.AppendLine($"  Seconds Before Auto Respawn: {stuckSeconds:0.###}");
                    builder.AppendLine($"  Allow Player Auto Respawn: {allowPlayerAutoRespawn}");

                    if (checkpointTracker.IsPlayer)
                    {
                        warnings.Add($"KartAgent '{agent.name}' has CheckpointTracker marked as player. Training agents are usually non-player.");
                    }

                    if (autoRespawn)
                    {
                        notes.Add($"KartAgent '{agent.name}' has CheckpointTracker auto-respawn enabled. This can look like short teleport jumps during training.");
                    }
                }

                if (behaviorParameters != null)
                {
                    var actionSpec = behaviorParameters.BrainParameters.ActionSpec;
                    builder.AppendLine($"  Behavior Name: {behaviorParameters.BehaviorName}");
                    builder.AppendLine($"  Behavior Type: {behaviorParameters.BehaviorType}");
                    builder.AppendLine($"  Model Assigned: {(behaviorParameters.Model != null ? behaviorParameters.Model.name : "None")}");
                    builder.AppendLine($"  Vector Observation Size: {behaviorParameters.BrainParameters.VectorObservationSize}");
                    builder.AppendLine($"  Stacked Vectors: {behaviorParameters.BrainParameters.NumStackedVectorObservations}");
                    builder.AppendLine($"  Continuous Actions: {actionSpec.NumContinuousActions}");
                    builder.AppendLine($"  Discrete Branches: {actionSpec.NumDiscreteActions}");
                    builder.AppendLine($"  Branch Sizes: {FormatBranchSizes(actionSpec.BranchSizes)}");
                    builder.AppendLine($"  Team ID: {behaviorParameters.TeamId}");

                    if (!string.Equals(behaviorParameters.BehaviorName, "KartAgent", StringComparison.Ordinal))
                    {
                        warnings.Add($"KartAgent '{agent.name}' uses Behavior Name '{behaviorParameters.BehaviorName}' instead of 'KartAgent'.");
                    }

                    if (behaviorParameters.BehaviorType == BehaviorType.InferenceOnly && behaviorParameters.Model == null)
                    {
                        blockers.Add($"KartAgent '{agent.name}' is InferenceOnly but has no model assigned.");
                    }

                    if (behaviorParameters.BrainParameters.VectorObservationSize != 13)
                    {
                        warnings.Add($"KartAgent '{agent.name}' uses Vector Observation Size {behaviorParameters.BrainParameters.VectorObservationSize}. The current agent code expects 13.");
                    }
                }

                if (decisionRequester != null)
                {
                    builder.AppendLine($"  Decision Period: {decisionRequester.DecisionPeriod}");
                    builder.AppendLine($"  Decision Step: {decisionRequester.DecisionStep}");
                    builder.AppendLine($"  Take Actions Between Decisions: {decisionRequester.TakeActionsBetweenDecisions}");

                    if (decisionRequester.DecisionPeriod > 1)
                    {
                        notes.Add($"KartAgent '{agent.name}' uses Decision Period {decisionRequester.DecisionPeriod}. Fast karts usually train best with 1.");
                    }
                }

                if (raySensor != null)
                {
                    builder.AppendLine($"  Ray Sensor Name: {raySensor.SensorName}");
                    builder.AppendLine($"  Rays Per Direction: {raySensor.RaysPerDirection}");
                    builder.AppendLine($"  Max Ray Degrees: {raySensor.MaxRayDegrees}");
                    builder.AppendLine($"  Sphere Cast Radius: {raySensor.SphereCastRadius}");
                    builder.AppendLine($"  Ray Length: {raySensor.RayLength}");
                    builder.AppendLine($"  Start Vertical Offset: {raySensor.StartVerticalOffset}");
                    builder.AppendLine($"  End Vertical Offset: {raySensor.EndVerticalOffset}");
                    builder.AppendLine($"  Detectable Tags: {FormatStringList(raySensor.DetectableTags)}");
                    builder.AppendLine($"  Ray Layer Mask: {LayerMaskToString(raySensor.RayLayerMask)}");
                }

                var agentSerialized = new SerializedObject(agent);
                var endEpisodeOnLapCompletion = agentSerialized.FindProperty("endEpisodeOnLapCompletion")?.boolValue ?? false;
                var endEpisodeOnOffTrack = agentSerialized.FindProperty("endEpisodeOnOffTrack")?.boolValue ?? false;
                var endEpisodeOnStrongWallCollision = agentSerialized.FindProperty("endEpisodeOnStrongWallCollision")?.boolValue ?? false;
                var normalizedDistanceReference = agentSerialized.FindProperty("normalizedDistanceReference")?.floatValue ?? 0f;
                var outOfBoundsHeight = agentSerialized.FindProperty("outOfBoundsHeight")?.floatValue ?? 0f;

                builder.AppendLine($"  End Episode On Lap Completion: {endEpisodeOnLapCompletion}");
                builder.AppendLine($"  End Episode On Off Track: {endEpisodeOnOffTrack}");
                builder.AppendLine($"  End Episode On Strong Wall Collision: {endEpisodeOnStrongWallCollision}");
                builder.AppendLine($"  Normalized Distance Reference: {normalizedDistanceReference:0.###}");
                builder.AppendLine($"  Out Of Bounds Height: {outOfBoundsHeight:0.###}");

                if (endEpisodeOnLapCompletion)
                {
                    notes.Add($"KartAgent '{agent.name}' ends episodes on lap completion. This is fine, but it creates visible reset teleports after a completed loop.");
                }

                if (!endEpisodeOnOffTrack)
                {
                    notes.Add($"KartAgent '{agent.name}' does not end on off-track. This reduces resets but may allow the agent to explore bad areas longer.");
                }

                builder.AppendLine();
            }
        }

        private static void AppendSummary(
            StringBuilder builder,
            IReadOnlyList<string> blockers,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> notes)
        {
            builder.AppendLine("Summary");
            builder.AppendLine("-------");
            builder.AppendLine($"Blockers: {blockers.Count}");
            for (var index = 0; index < blockers.Count; index++)
            {
                builder.AppendLine($"  - {blockers[index]}");
            }

            builder.AppendLine($"Warnings: {warnings.Count}");
            for (var index = 0; index < warnings.Count; index++)
            {
                builder.AppendLine($"  - {warnings[index]}");
            }

            builder.AppendLine($"Notes: {notes.Count}");
            for (var index = 0; index < notes.Count; index++)
            {
                builder.AppendLine($"  - {notes[index]}");
            }

            builder.AppendLine();
            builder.AppendLine("Recommended Status");
            builder.AppendLine("------------------");

            if (blockers.Count > 0)
            {
                builder.AppendLine("NOT READY");
                builder.AppendLine("There are blocking setup problems that should be fixed before training.");
            }
            else if (warnings.Count > 0)
            {
                builder.AppendLine("PARTIALLY READY");
                builder.AppendLine("Training can start, but the warnings above should be reviewed.");
            }
            else
            {
                builder.AppendLine("READY");
                builder.AppendLine("No obvious blockers or warnings were detected in the static scene analysis.");
            }
        }

        private static List<T> GetSceneComponents<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            var objects = Resources.FindObjectsOfTypeAll<T>();
            for (var index = 0; index < objects.Length; index++)
            {
                var component = objects[index];
                if (component == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(component) || component.hideFlags != HideFlags.None)
                {
                    continue;
                }

                if (component.gameObject.scene != scene)
                {
                    continue;
                }

                results.Add(component);
            }

            return results;
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "None";
            }

            var builder = new StringBuilder(target.name);
            var current = target.parent;
            while (current != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, current.name);
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string FormatBranchSizes(int[] branchSizes)
        {
            if (branchSizes == null || branchSizes.Length == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");
            for (var index = 0; index < branchSizes.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(branchSizes[index]);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatStringList(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(values[index]);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string LayerMaskToString(LayerMask mask)
        {
            if (mask.value == 0)
            {
                return "Nothing";
            }

            var names = new List<string>();
            for (var layer = 0; layer < 32; layer++)
            {
                if ((mask.value & (1 << layer)) == 0)
                {
                    continue;
                }

                var name = LayerMask.LayerToName(layer);
                names.Add(string.IsNullOrWhiteSpace(name) ? $"Layer{layer}" : name);
            }

            return names.Count > 0 ? string.Join(", ", names) : mask.value.ToString();
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                builder.Append(Array.IndexOf(invalid, current) >= 0 ? '_' : current);
            }

            return builder.ToString();
        }
    }
}
#endif
