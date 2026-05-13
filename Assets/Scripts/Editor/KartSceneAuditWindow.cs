#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using KartGame.AI.Reinforcement;
using KartGame.Core;
using KartGame.Kart;
using KartGame.UI;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    /*
     * Script: KartSceneAuditWindow.cs
     * Purpose: Editor window that scans the active scene and reports the status of every required component.
     * Open via: Tools > Kart Racing > Scene Audit  (Ctrl+Shift+A)
     */
    public class KartSceneAuditWindow : EditorWindow
    {
        private enum Status { Header, Ok, Warn, Error, Info }

        private struct Entry
        {
            public Status status;
            public string message;
            public Object pingTarget;
        }

        private List<Entry> _entries = new List<Entry>();
        private Vector2 _scroll;
        private GUIStyle _headerStyle, _okStyle, _warnStyle, _errorStyle, _infoStyle;
        private bool _stylesReady;

        [MenuItem("Tools/Kart Racing/Scene Audit %#a")]
        public static void OpenWindow()
        {
            var window = GetWindow<KartSceneAuditWindow>("Scene Audit");
            window.minSize = new Vector2(400, 300);
            window.RunAudit();
            window.Show();
        }

        private void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Kart Scene Audit", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) RunAudit();
            if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                GUIUtility.systemCopyBuffer = BuildTextReport();
                Debug.Log("[Scene Audit] Report copied to clipboard.");
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _entries)
            {
                EditorGUILayout.BeginHorizontal();
                switch (entry.status)
                {
                    case Status.Header: EditorGUILayout.LabelField(entry.message, _headerStyle); break;
                    case Status.Ok:     EditorGUILayout.LabelField("  ✓  " + entry.message, _okStyle); break;
                    case Status.Warn:   EditorGUILayout.LabelField("  ⚠  " + entry.message, _warnStyle); break;
                    case Status.Error:  EditorGUILayout.LabelField("  ✗  " + entry.message, _errorStyle); break;
                    case Status.Info:   EditorGUILayout.LabelField("       " + entry.message, _infoStyle); break;
                }
                if (entry.pingTarget != null && GUILayout.Button("→", GUILayout.Width(24)))
                {
                    EditorGUIUtility.PingObject(entry.pingTarget);
                    Selection.activeObject = entry.pingTarget;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Configure Checkpoints")) { KartRacingSetupTools.AutoConfigureSelectedTrackMenu(); RunAudit(); }
            if (GUILayout.Button("Create Race Systems"))        { KartRacingSetupTools.CreateRaceSystemsMenu(); RunAudit(); }
            EditorGUILayout.EndHorizontal();
        }

        private void RunAudit()
        {
            _entries.Clear();
            AuditTrack();
            AuditRaceSystems();
            AuditKarts();
            AuditCamera();
            AuditUI();
            AuditMLAgents();
            Repaint();
        }

        // ── Track ─────────────────────────────────────────────────────────────────

        private void AuditTrack()
        {
            AddHeader("TRACK");
            var trackData = Object.FindFirstObjectByType<TrackData>();
            if (trackData == null) { AddError("No TrackData found — create a TrackRoot."); return; }
            AddOk($"TrackData on '{trackData.name}'", trackData);

            var checkpointsRoot = trackData.transform.Find("Checkpoints");
            if (checkpointsRoot == null || checkpointsRoot.childCount == 0)
                AddError("Checkpoints container empty — run Auto Configure Checkpoints.");
            else
                AuditCheckpoints(trackData, checkpointsRoot);

            AuditChildContainer(trackData, "SpawnPoints", required: 7, label: "spawn points (need 7 for 1P+6 bots)");
            AuditChildContainer(trackData, "RespawnPoints", required: 0, label: "respawn points");
            AuditChildContainer(trackData, "PowerUpBoxes", required: 0, label: "power-up spawn points");
            AddInfo($"Laps to win: {trackData.LapsToWin}");
        }

        private void AuditChildContainer(TrackData trackData, string containerName, int required, string label)
        {
            var root = trackData.transform.Find(containerName);
            var count = root != null ? root.childCount : 0;
            if (count == 0 && required > 0)
                AddError($"{containerName}: 0 {label}.");
            else if (count < required)
                AddWarn($"{containerName}: {count} {label}.");
            else if (count == 0)
                AddWarn($"{containerName}: 0 {label}.");
            else
                AddOk($"{containerName}: {count}");
        }

        private void AuditCheckpoints(TrackData trackData, Transform checkpointsRoot)
        {
            var count = checkpointsRoot.childCount;
            var allOk = true;
            for (var i = 0; i < count; i++)
            {
                var child = checkpointsRoot.GetChild(i);
                var cp = child.GetComponent<Checkpoint>();
                var col = child.GetComponent<Collider>();
                if (cp == null)           { AddError($"Checkpoint_{i:00}: missing Checkpoint component.", child.gameObject); allOk = false; }
                else if (cp.TrackData == null) { AddWarn($"Checkpoint_{i:00}: TrackData not wired — run Auto Configure.", child.gameObject); allOk = false; }
                else if (cp.CheckpointIndex != i) { AddError($"Checkpoint_{i:00}: index={cp.CheckpointIndex}, expected {i}.", child.gameObject); allOk = false; }
                if (col == null || !col.isTrigger) { AddError($"Checkpoint_{i:00}: collider missing or not trigger.", child.gameObject); allOk = false; }
            }
            if (allOk) AddOk($"Checkpoints: {count} — all OK (0 → {count - 1})");
            else        AddWarn($"Checkpoints: {count} — issues found above");
        }

        // ── Race Systems ──────────────────────────────────────────────────────────

        private void AuditRaceSystems()
        {
            AddHeader("RACE SYSTEMS");
            var rm = Object.FindFirstObjectByType<RaceManager>();
            if (rm == null) { AddError("RaceManager not found."); return; }
            AddOk("RaceManager", rm);
            if (Object.FindFirstObjectByType<LapManager>() == null) AddError("LapManager missing.");
            else AddOk("LapManager");
            if (Object.FindFirstObjectByType<PositionManager>() == null) AddError("PositionManager missing.");
            else AddOk("PositionManager");
            if (rm.TrackData == null) AddWarn("RaceManager.TrackData not assigned (will auto-find at runtime).");
            else AddOk($"TrackData → '{rm.TrackData.name}'");
        }

        // ── Karts ─────────────────────────────────────────────────────────────────

        private void AuditKarts()
        {
            AddHeader("KARTS");
            var trackers = Object.FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var players = new List<CheckpointTracker>();
            var bots = new List<CheckpointTracker>();
            foreach (var t in trackers) { if (t.IsPlayer) players.Add(t); else bots.Add(t); }

            if (players.Count == 0)     AddError("No player kart (no CheckpointTracker with IsPlayer = true).");
            else if (players.Count > 1) AddWarn($"{players.Count} player karts found — only one should be player.");
            else { AddOk($"Player: '{players[0].name}'", players[0]); AuditSingleKart(players[0], true); }

            if (bots.Count == 0) AddError("No AI bots found.");
            else { AddOk($"AI bots: {bots.Count}"); foreach (var b in bots) AuditSingleKart(b, false); }
        }

        private void AuditSingleKart(CheckpointTracker tracker, bool isPlayer)
        {
            var go = tracker.gameObject;
            var prefix = isPlayer ? "  Player" : $"  {go.name}";
            if (go.GetComponent<KartController>() == null) AddError($"{prefix}: missing KartController.", go);
            if (tracker.TrackData == null) AddWarn($"{prefix}: TrackData not assigned (injected at runtime).", go);
            if (isPlayer) { if (go.GetComponent<PlayerKartInput>() == null) AddError($"{prefix}: missing PlayerKartInput.", go); }
            else
            {
                var hasAI = go.GetComponent<AIKartInput>() != null;
                var hasAgent = go.GetComponent<KartAgent>() != null;
                if (!hasAI && !hasAgent) AddError($"{prefix}: no input controller.", go);
                else if (hasAgent)
                {
                    var bp = go.GetComponent<BehaviorParameters>();
                    var hasModel = bp != null && bp.Model != null;
                    AddOk($"{prefix}: KartAgent ({(hasModel ? "model ✓" : "no model — heuristic fallback")})", go);
                }
                else AddInfo($"{prefix}: AIKartInput (heuristic)");
            }
        }

        // ── Camera ────────────────────────────────────────────────────────────────

        private void AuditCamera()
        {
            AddHeader("CAMERA");
            var cam = Camera.main;
            if (cam == null) { AddError("No Main Camera."); return; }
            AddOk("Main Camera", cam);
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) AddWarn("CameraFollow not on Main Camera.");
            else AddOk("CameraFollow", follow);
        }

        // ── UI ────────────────────────────────────────────────────────────────────

        private void AuditUI()
        {
            AddHeader("UI");
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { AddError("No Canvas — HUD will not show."); return; }
            AddOk($"Canvas: '{canvas.name}'", canvas);
            AuditUIComponent<CountdownUI>("CountdownUI");
            AuditUIComponent<RaceHUD>("RaceHUD");
            AuditUIComponent<ResultsUI>("ResultsUI");
        }

        private void AuditUIComponent<T>(string label) where T : Object
        {
            var c = Object.FindFirstObjectByType<T>();
            if (c == null) AddWarn($"{label} not in scene — add it to the Canvas.");
            else AddOk(label, c);
        }

        // ── ML-Agents ─────────────────────────────────────────────────────────────

        private void AuditMLAgents()
        {
            AddHeader("ML-AGENTS TRAINING");
            var manager = Object.FindFirstObjectByType<TrainingSceneManager>();
            if (manager == null) { AddInfo("No TrainingSceneManager (normal for race scene)."); AddInfo("Open Assets/Scenes/TrainingScene.unity for training."); return; }
            AddOk($"TrainingSceneManager: '{manager.name}'", manager);

            var agent = Object.FindFirstObjectByType<KartAgent>();
            if (agent == null) { AddError("No KartAgent — run ML-Agents > Build Training Prototype."); return; }
            AddOk($"KartAgent: '{agent.name}'", agent);

            var bp = agent.GetComponent<BehaviorParameters>();
            if (bp == null) { AddError("BehaviorParameters missing.", agent); return; }
            AddOk($"BehaviorParameters: '{bp.BehaviorName}', type={bp.BehaviorType}", bp);
            if (bp.Model == null) AddInfo("No model — set BehaviorType=Default for training.");
            else AddOk($"Model: '{bp.Model.name}'");

            var dr = agent.GetComponent<Unity.MLAgents.DecisionRequester>();
            if (dr == null) AddError("DecisionRequester missing.", agent);
            else AddOk($"DecisionRequester: period={dr.DecisionPeriod}");

            var ray = agent.GetComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent3D>();
            if (ray == null) AddWarn("RayPerceptionSensorComponent3D not found.", agent);
            else AddOk($"Ray sensor: {ray.RaysPerDirection * 2 + 1} rays, length={ray.RayLength}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void AddHeader(string t) => _entries.Add(new Entry { status = Status.Header, message = t });
        private void AddOk(string m, Object p = null)   => _entries.Add(new Entry { status = Status.Ok,    message = m, pingTarget = p });
        private void AddWarn(string m, Object p = null)  => _entries.Add(new Entry { status = Status.Warn,  message = m, pingTarget = p });
        private void AddError(string m, Object p = null) => _entries.Add(new Entry { status = Status.Error, message = m, pingTarget = p });
        private void AddInfo(string m) => _entries.Add(new Entry { status = Status.Info, message = m });

        private string BuildTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Kart Scene Audit ===");
            foreach (var e in _entries)
            {
                var prefix = e.status switch { Status.Header => "\n──", Status.Ok => "  OK", Status.Warn => "  ⚠ ", Status.Error => "  ✗ ", _ => "    " };
                sb.AppendLine(prefix + " " + e.message);
            }
            return sb.ToString();
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            _okStyle    = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.25f, 0.88f, 0.35f) } };
            _warnStyle  = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.82f, 0.1f) } };
            _errorStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
            _infoStyle  = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.55f, 0.75f, 1f) } };
            _stylesReady = true;
        }
    }
}
#endif
