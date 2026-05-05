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

        private void EnsureTriggerCollider()
        {
            var checkpointCollider = GetComponent<Collider>();
            if (checkpointCollider != null)
            {
                checkpointCollider.isTrigger = true;
            }
        }
    }
}
