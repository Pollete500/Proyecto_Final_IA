using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: UnitySplineCheckpointSettings.cs
     * Purpose: Stores checkpoint generation parameters for Unity Spline-based tracks.
     * Attach To: The RoadSpline GameObject or the TrackRoot that owns the spline.
     * Required Components: None.
     * Inspector Setup: Tune checkpoint spacing and collider width before running the spline checkpoint generator.
     */
    [DisallowMultipleComponent]
    public class UnitySplineCheckpointSettings : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float checkpointSpacing = 8f;
        [SerializeField, Min(0f)] private float checkpointVerticalOffset = 1.2f;
        [SerializeField, Min(1f)] private float checkpointWidth = 14f;
        [SerializeField, Min(0.5f)] private float checkpointHeight = 3f;
        [SerializeField, Min(0.5f)] private float checkpointDepth = 2.5f;
        [SerializeField] private bool autoRegenerateCheckpoints = true;

        public float CheckpointSpacing => checkpointSpacing;
        public float CheckpointVerticalOffset => checkpointVerticalOffset;
        public float CheckpointWidth => checkpointWidth;
        public float CheckpointHeight => checkpointHeight;
        public float CheckpointDepth => checkpointDepth;
        public bool AutoRegenerateCheckpoints => autoRegenerateCheckpoints;

        [ContextMenu("Reset To Default Checkpoint Size")]
        private void ResetToDefaultCheckpointSize()
        {
            checkpointSpacing = 8f;
            checkpointVerticalOffset = 1.2f;
            checkpointWidth = 14f;
            checkpointHeight = 3f;
            checkpointDepth = 2.5f;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || !autoRegenerateCheckpoints)
            {
                return;
            }

            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
