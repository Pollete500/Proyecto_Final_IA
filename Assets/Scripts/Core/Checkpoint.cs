using KartGame.Kart;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: Checkpoint.cs
     * Purpose: Defines a trigger checkpoint that advances kart lap progress when the correct kart enters it.
     * Attach To: Each checkpoint trigger GameObject.
     * Required Components: Collider set as trigger.
     * Dependencies: TrackData, CheckpointTracker.
     * Inspector Setup: Assign the owning TrackData, keep checkpoints ordered by sibling index and ensure the collider covers the track width.
     */
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private TrackData trackData;
        [SerializeField] private int checkpointIndex;
        [SerializeField] private bool drawTriggerGizmoAlways = true;
        [SerializeField] private Color triggerGizmoColor = new Color(0.35f, 1f, 0.45f, 0.18f);
        [SerializeField] private Color triggerWireGizmoColor = new Color(0.35f, 1f, 0.45f, 0.9f);

        public TrackData TrackData => trackData;
        public int CheckpointIndex => checkpointIndex;

        public void Configure(TrackData ownerTrack, int index)
        {
            trackData = ownerTrack;
            checkpointIndex = Mathf.Max(0, index);
        }

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();

            if (trackData == null)
            {
                trackData = GetComponentInParent<TrackData>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var tracker = other.GetComponentInParent<CheckpointTracker>();
            if (tracker == null)
            {
                return;
            }

            tracker.ProcessCheckpoint(this);
        }

        private void OnDrawGizmos()
        {
            if (!drawTriggerGizmoAlways)
            {
                return;
            }

            DrawTriggerGizmo();
        }

        private void EnsureTriggerCollider()
        {
            var checkpointCollider = GetComponent<Collider>();
            if (checkpointCollider != null)
            {
                checkpointCollider.isTrigger = true;
            }
        }

        private void DrawTriggerGizmo()
        {
            var checkpointCollider = GetComponent<Collider>();
            if (checkpointCollider == null || !checkpointCollider.enabled || !checkpointCollider.isTrigger)
            {
                return;
            }

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;

            switch (checkpointCollider)
            {
                case BoxCollider boxCollider:
                    Gizmos.color = triggerGizmoColor;
                    Gizmos.DrawCube(boxCollider.center, boxCollider.size);
                    Gizmos.color = triggerWireGizmoColor;
                    Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                    break;

                case SphereCollider sphereCollider:
                    Gizmos.color = triggerGizmoColor;
                    Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
                    Gizmos.color = triggerWireGizmoColor;
                    Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
                    break;

                default:
                    Gizmos.matrix = previousMatrix;
                    Gizmos.color = triggerGizmoColor;
                    Gizmos.DrawCube(checkpointCollider.bounds.center, checkpointCollider.bounds.size);
                    Gizmos.color = triggerWireGizmoColor;
                    Gizmos.DrawWireCube(checkpointCollider.bounds.center, checkpointCollider.bounds.size);
                    break;
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
