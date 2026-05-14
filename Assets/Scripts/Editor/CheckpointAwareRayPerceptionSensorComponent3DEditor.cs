#if UNITY_EDITOR
using KartGame.AI.Reinforcement;
using UnityEditor;

namespace KartGame.EditorTools
{
    [CustomEditor(typeof(CheckpointAwareRayPerceptionSensorComponent3D))]
    [CanEditMultipleObjects]
    public class CheckpointAwareRayPerceptionSensorComponent3DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawProperty("m_SensorName");
            DrawProperty("m_DetectableTags");
            DrawProperty("m_RaysPerDirection");
            DrawProperty("m_MaxRayDegrees");
            DrawProperty("m_SphereCastRadius");
            DrawProperty("m_RayLength");
            DrawProperty("m_RayLayerMask");
            DrawProperty("m_ObservationStacks", "Stacked Raycasts");
            DrawProperty("m_StartVerticalOffset");
            DrawProperty("m_EndVerticalOffset");
            DrawProperty("m_AlternatingRayOrder");
            DrawProperty("m_UseBatchedRaycasts");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checkpoint Filtering", EditorStyles.boldLabel);
            DrawProperty("checkpointTracker");
            DrawProperty("ignorePassedCheckpoints");
            DrawProperty("limitCheckpointDetectionWindow");
            DrawProperty("additionalVisibleCheckpointsAhead");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Gizmos", EditorStyles.boldLabel);
            DrawProperty("debugRayHitColor");
            DrawProperty("debugRayMissColor");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName, string customLabel = null)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(customLabel))
            {
                EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, new UnityEngine.GUIContent(customLabel), true);
            }
        }
    }
}
#endif
