#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using KartGame.Core;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    /*
     * Script: CheckpointReindexTool.cs
     * Purpose: Provides an editor window that reindexes checkpoint components found under any source hierarchy.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: Checkpoint, optional TrackData.
     * Inspector Setup: Open the tool from Tools/Kart Racing/Track Data, assign the checkpoint root and optionally a TrackData owner.
     */
    public class CheckpointReindexTool : EditorWindow
    {
        private const string DefaultCheckpointNameToken = "Checkpoint";

        [SerializeField] private Transform checkpointsRoot;
        [SerializeField] private GameObject trackDataObject;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool searchRecursively = true;
        [SerializeField] private bool autoAddCheckpointComponent = false;
        [SerializeField] private bool ensureTriggerCollider = true;
        [SerializeField] private bool matchCheckpointNames = true;
        [SerializeField] private string checkpointNameToken = DefaultCheckpointNameToken;

        [MenuItem("Tools/Kart Racing/Track Data/Reindex Checkpoints")]
        public static void OpenWindow()
        {
            var window = GetWindow<CheckpointReindexTool>("Reindex Checkpoints");
            window.minSize = new Vector2(430f, 280f);
            window.InitializeFromSelection();
        }

        private void OnEnable()
        {
            InitializeFromSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Reindex Checkpoints", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assign the GameObject that contains your checkpoints. The tool reindexes them in hierarchy order starting at 0.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                checkpointsRoot = (Transform)EditorGUILayout.ObjectField("Checkpoints Root", checkpointsRoot, typeof(Transform), true);
                trackDataObject = (GameObject)EditorGUILayout.ObjectField("TrackData Object", trackDataObject, typeof(GameObject), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected As Root"))
                {
                    checkpointsRoot = Selection.activeTransform;
                }

                if (GUILayout.Button("Use Selected TrackData"))
                {
                    trackDataObject = Selection.activeGameObject;
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
                searchRecursively = EditorGUILayout.Toggle("Search Recursively", searchRecursively);
                autoAddCheckpointComponent = EditorGUILayout.Toggle("Auto Add Checkpoint", autoAddCheckpointComponent);
                ensureTriggerCollider = EditorGUILayout.Toggle("Ensure Trigger Collider", ensureTriggerCollider);
                matchCheckpointNames = EditorGUILayout.Toggle("Match Names", matchCheckpointNames);

                using (new EditorGUI.DisabledScope(!matchCheckpointNames))
                {
                    checkpointNameToken = EditorGUILayout.TextField("Name Contains", checkpointNameToken);
                }
            }

            var ownerTrackData = ResolveTrackData();
            var foundCheckpoints = checkpointsRoot != null
                ? FindCheckpointTransforms(checkpointsRoot, includeInactive, searchRecursively, autoAddCheckpointComponent, matchCheckpointNames, checkpointNameToken)
                : new List<Transform>();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Found Checkpoints", foundCheckpoints.Count.ToString());

            using (new EditorGUI.DisabledScope(checkpointsRoot == null || foundCheckpoints.Count == 0))
            {
                if (GUILayout.Button("Reindex Checkpoints", GUILayout.Height(32f)))
                {
                    ReindexCheckpoints(foundCheckpoints, ownerTrackData);
                }
            }

            if (trackDataObject != null && ownerTrackData == null)
            {
                EditorGUILayout.HelpBox("The selected TrackData Object does not contain a TrackData component.", MessageType.Warning);
            }
        }

        private void InitializeFromSelection()
        {
            if (Selection.activeTransform == null)
            {
                return;
            }

            checkpointsRoot ??= Selection.activeTransform;

            if (trackDataObject == null)
            {
                var selectedTrackData = Selection.activeTransform.GetComponentInParent<TrackData>();
                if (selectedTrackData != null)
                {
                    trackDataObject = selectedTrackData.gameObject;
                }
            }
        }

        private TrackData ResolveTrackData()
        {
            return trackDataObject != null ? trackDataObject.GetComponent<TrackData>() : null;
        }

        private void ReindexCheckpoints(IReadOnlyList<Transform> checkpointTransforms, TrackData ownerTrackData)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Reindex Checkpoints");
            var undoGroup = Undo.GetCurrentGroup();

            for (var index = 0; index < checkpointTransforms.Count; index++)
            {
                ConfigureCheckpoint(checkpointTransforms[index], ownerTrackData, index);
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (ownerTrackData != null)
            {
                EditorUtility.SetDirty(ownerTrackData);
                Selection.activeGameObject = ownerTrackData.gameObject;
            }

            EditorUtility.DisplayDialog(
                "Reindex Checkpoints",
                $"Reindexed {checkpointTransforms.Count} checkpoints starting at 0.",
                "OK");
        }

        private void ConfigureCheckpoint(Transform checkpointTransform, TrackData ownerTrackData, int checkpointIndex)
        {
            if (checkpointTransform == null)
            {
                return;
            }

            var checkpoint = checkpointTransform.GetComponent<Checkpoint>();
            if (checkpoint == null)
            {
                if (!autoAddCheckpointComponent)
                {
                    return;
                }

                checkpoint = Undo.AddComponent<Checkpoint>(checkpointTransform.gameObject);
            }

            Undo.RecordObject(checkpoint, "Reindex Checkpoint");
            checkpoint.Configure(ownerTrackData != null ? ownerTrackData : checkpoint.TrackData, checkpointIndex);
            EditorUtility.SetDirty(checkpoint);

            if (!ensureTriggerCollider)
            {
                return;
            }

            var collider = checkpointTransform.GetComponent<Collider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(checkpointTransform.gameObject);
            }

            Undo.RecordObject(collider, "Configure Checkpoint Collider");
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        private static List<Transform> FindCheckpointTransforms(
            Transform root,
            bool includeInactiveChildren,
            bool recursive,
            bool allowMissingCheckpointComponent,
            bool allowNameMatches,
            string checkpointToken)
        {
            var results = new List<Transform>();

            if (recursive)
            {
                CollectCheckpointTransformsRecursive(root, includeInactiveChildren, allowMissingCheckpointComponent, allowNameMatches, checkpointToken, results, includeRoot: false);
            }
            else
            {
                for (var index = 0; index < root.childCount; index++)
                {
                    var child = root.GetChild(index);
                    if ((includeInactiveChildren || child.gameObject.activeInHierarchy) &&
                        IsCheckpointCandidate(child, allowMissingCheckpointComponent, allowNameMatches, checkpointToken))
                    {
                        results.Add(child);
                    }
                }
            }

            return results;
        }

        private static void CollectCheckpointTransformsRecursive(
            Transform current,
            bool includeInactiveChildren,
            bool allowMissingCheckpointComponent,
            bool allowNameMatches,
            string checkpointToken,
            List<Transform> results,
            bool includeRoot)
        {
            if (current == null)
            {
                return;
            }

            if (includeRoot &&
                (includeInactiveChildren || current.gameObject.activeInHierarchy) &&
                IsCheckpointCandidate(current, allowMissingCheckpointComponent, allowNameMatches, checkpointToken))
            {
                results.Add(current);
            }

            for (var index = 0; index < current.childCount; index++)
            {
                CollectCheckpointTransformsRecursive(
                    current.GetChild(index),
                    includeInactiveChildren,
                    allowMissingCheckpointComponent,
                    allowNameMatches,
                    checkpointToken,
                    results,
                    includeRoot: true);
            }
        }

        private static bool IsCheckpointCandidate(
            Transform transformCandidate,
            bool allowMissingCheckpointComponent,
            bool allowNameMatches,
            string checkpointToken)
        {
            if (transformCandidate == null)
            {
                return false;
            }

            if (transformCandidate.GetComponent<Checkpoint>() != null)
            {
                return true;
            }

            return allowMissingCheckpointComponent &&
                   allowNameMatches &&
                   !string.IsNullOrWhiteSpace(checkpointToken) &&
                   transformCandidate.name.IndexOf(checkpointToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
