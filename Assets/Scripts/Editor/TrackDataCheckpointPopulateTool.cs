#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using KartGame.Core;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    /*
     * Script: TrackDataCheckpointPopulateTool.cs
     * Purpose: Provides an editor window that fills a TrackData checkpoint array from checkpoint objects found under any source hierarchy.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: TrackData, Checkpoint.
     * Inspector Setup: Open the tool from Tools/Kart Racing/Track Data, assign a GameObject with TrackData, then assign the source root that contains checkpoint children.
     */
    public class TrackDataCheckpointPopulateTool : EditorWindow
    {
        private const string DefaultCheckpointNameToken = "Checkpoint";

        [SerializeField] private GameObject trackDataObject;
        [SerializeField] private Transform checkpointsSourceRoot;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool autoConfigureCheckpointComponents = true;
        [SerializeField] private bool matchCheckpointNames = true;
        [SerializeField] private string checkpointNameToken = DefaultCheckpointNameToken;

        [MenuItem("Tools/Kart Racing/Track Data/Populate Checkpoints From Source")]
        public static void OpenWindow()
        {
            var window = GetWindow<TrackDataCheckpointPopulateTool>("TrackData Checkpoints");
            window.minSize = new Vector2(440f, 260f);
            window.InitializeFromSelection();
        }

        private void OnEnable()
        {
            InitializeFromSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Populate TrackData Checkpoints", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select the GameObject that has TrackData, then choose the source root that contains your checkpoint objects. " +
                "The tool searches recursively and fills the TrackData checkpoint array in hierarchy order.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                trackDataObject = (GameObject)EditorGUILayout.ObjectField("TrackData Object", trackDataObject, typeof(GameObject), true);
                checkpointsSourceRoot = (Transform)EditorGUILayout.ObjectField("Checkpoints Source", checkpointsSourceRoot, typeof(Transform), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected As TrackData"))
                {
                    trackDataObject = Selection.activeGameObject;
                }

                if (GUILayout.Button("Use Selected As Source"))
                {
                    checkpointsSourceRoot = Selection.activeTransform;
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
                autoConfigureCheckpointComponents = EditorGUILayout.Toggle("Auto Configure Checkpoints", autoConfigureCheckpointComponents);
                matchCheckpointNames = EditorGUILayout.Toggle("Match Names", matchCheckpointNames);

                using (new EditorGUI.DisabledScope(!matchCheckpointNames))
                {
                    checkpointNameToken = EditorGUILayout.TextField("Name Contains", checkpointNameToken);
                }
            }

            var targetTrackData = ResolveTrackData();
            var foundCount = targetTrackData != null && checkpointsSourceRoot != null
                ? FindCheckpointTransforms(checkpointsSourceRoot, includeInactive, matchCheckpointNames, checkpointNameToken).Count
                : 0;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Found Checkpoints", foundCount.ToString());

            using (new EditorGUI.DisabledScope(targetTrackData == null || checkpointsSourceRoot == null))
            {
                if (GUILayout.Button("Populate TrackData Checkpoints", GUILayout.Height(32f)))
                {
                    PopulateTrackDataCheckpoints(targetTrackData, checkpointsSourceRoot);
                }
            }

            if (trackDataObject != null && targetTrackData == null)
            {
                EditorGUILayout.HelpBox("The selected TrackData Object does not contain a TrackData component.", MessageType.Warning);
            }
        }

        private void InitializeFromSelection()
        {
            if (Selection.activeGameObject == null)
            {
                return;
            }

            if (trackDataObject == null && Selection.activeGameObject.GetComponentInParent<TrackData>() != null)
            {
                trackDataObject = Selection.activeGameObject.GetComponentInParent<TrackData>().gameObject;
            }

            checkpointsSourceRoot ??= Selection.activeTransform;
        }

        private TrackData ResolveTrackData()
        {
            return trackDataObject != null ? trackDataObject.GetComponent<TrackData>() : null;
        }

        private void PopulateTrackDataCheckpoints(TrackData targetTrackData, Transform sourceRoot)
        {
            var checkpointTransforms = FindCheckpointTransforms(sourceRoot, includeInactive, matchCheckpointNames, checkpointNameToken);
            if (checkpointTransforms.Count == 0)
            {
                EditorUtility.DisplayDialog("TrackData Checkpoints", "No checkpoint candidates were found under the selected source root.", "OK");
                return;
            }

            Undo.RecordObject(targetTrackData, "Populate TrackData Checkpoints");

            var serializedObject = new SerializedObject(targetTrackData);
            var checkpointsProperty = serializedObject.FindProperty("checkpoints");
            checkpointsProperty.arraySize = checkpointTransforms.Count;

            for (var index = 0; index < checkpointTransforms.Count; index++)
            {
                checkpointsProperty.GetArrayElementAtIndex(index).objectReferenceValue = checkpointTransforms[index];
            }

            serializedObject.ApplyModifiedProperties();

            if (autoConfigureCheckpointComponents)
            {
                for (var index = 0; index < checkpointTransforms.Count; index++)
                {
                    ConfigureCheckpoint(targetTrackData, checkpointTransforms[index], index);
                }
            }

            EditorUtility.SetDirty(targetTrackData);
            Selection.activeGameObject = targetTrackData.gameObject;
            EditorUtility.DisplayDialog(
                "TrackData Checkpoints",
                $"Assigned {checkpointTransforms.Count} checkpoints to '{targetTrackData.name}'.",
                "OK");
        }

        private static List<Transform> FindCheckpointTransforms(Transform sourceRoot, bool includeInactiveChildren, bool allowNameMatches, string checkpointToken)
        {
            var results = new List<Transform>();
            CollectCheckpointTransformsRecursive(sourceRoot, includeInactiveChildren, allowNameMatches, checkpointToken, results);
            return results;
        }

        private static void CollectCheckpointTransformsRecursive(
            Transform current,
            bool includeInactiveChildren,
            bool allowNameMatches,
            string checkpointToken,
            List<Transform> results)
        {
            if (current == null)
            {
                return;
            }

            if ((includeInactiveChildren || current.gameObject.activeInHierarchy) &&
                IsCheckpointCandidate(current, allowNameMatches, checkpointToken))
            {
                results.Add(current);
            }

            for (var index = 0; index < current.childCount; index++)
            {
                CollectCheckpointTransformsRecursive(
                    current.GetChild(index),
                    includeInactiveChildren,
                    allowNameMatches,
                    checkpointToken,
                    results);
            }
        }

        private static bool IsCheckpointCandidate(Transform transformCandidate, bool allowNameMatches, string checkpointToken)
        {
            if (transformCandidate == null)
            {
                return false;
            }

            if (transformCandidate.GetComponent<Checkpoint>() != null)
            {
                return true;
            }

            return allowNameMatches &&
                   !string.IsNullOrWhiteSpace(checkpointToken) &&
                   transformCandidate.name.IndexOf(checkpointToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ConfigureCheckpoint(TrackData trackData, Transform checkpointTransform, int checkpointIndex)
        {
            if (trackData == null || checkpointTransform == null)
            {
                return;
            }

            var checkpoint = checkpointTransform.GetComponent<Checkpoint>() ??
                             Undo.AddComponent<Checkpoint>(checkpointTransform.gameObject);
            checkpoint.Configure(trackData, checkpointIndex);

            var collider = checkpointTransform.GetComponent<Collider>() ??
                           Undo.AddComponent<BoxCollider>(checkpointTransform.gameObject);
            collider.isTrigger = true;

            EditorUtility.SetDirty(checkpoint);
            EditorUtility.SetDirty(checkpointTransform.gameObject);
        }
    }
}
#endif
