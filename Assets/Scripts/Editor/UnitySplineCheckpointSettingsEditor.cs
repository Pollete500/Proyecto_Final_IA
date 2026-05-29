#if UNITY_EDITOR
using KartGame.Core;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    [CustomEditor(typeof(UnitySplineCheckpointSettings))]
    public class UnitySplineCheckpointSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Regenerate Road Visual Now", GUILayout.Height(30f)))
                {
                    var settings = (UnitySplineCheckpointSettings)target;
                    UnitySplineCheckpointTools.GenerateRoadVisualFromSettings(settings);
                }

                if (GUILayout.Button("Regenerate Checkpoints Now", GUILayout.Height(30f)))
                {
                    var settings = (UnitySplineCheckpointSettings)target;
                    UnitySplineCheckpointTools.GenerateCheckpointsFromSettings(settings);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
