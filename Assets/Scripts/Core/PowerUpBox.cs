using System.Collections;
using KartGame.AI.PowerUps;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.Core
{
    /*
     * Script: PowerUpBox.cs
     * Purpose: Physical item box on track that grants a random power-up when a kart drives through it.
     * Attach To: A GameObject with a Collider (set as trigger) placed on the track.
     * Required Components: Collider (trigger).
     * Dependencies: PowerUpInventory (on the kart), PositionManager, RaceManager.
     * Inspector Setup: Assign visualRoot to the child mesh object so it can be hidden on pickup.
     *                  Adjust respawnDelay for how quickly the box reappears.
     */
    [RequireComponent(typeof(Collider))]
    public class PowerUpBox : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float respawnDelay = 8f;

        [Header("References")]
        [SerializeField] private GameObject visualRoot;

        private PositionManager _positionManager;
        private bool _available = true;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Start()
        {
            _positionManager = FindFirstObjectByType<PositionManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_available) return;
            if (RaceManager.Instance == null || !RaceManager.Instance.IsRaceActive()) return;

            var inventory = other.GetComponentInParent<PowerUpInventory>();
            if (inventory == null || inventory.HasStoredPowerUp) return;

            var tracker = other.GetComponentInParent<CheckpointTracker>();
            var position   = (_positionManager != null && tracker != null) ? _positionManager.GetPosition(tracker) : 1;
            var racerCount = _positionManager != null ? _positionManager.RacerCount : 7;

            inventory.TryAssignByRacePosition(position, racerCount);

            _available = false;
            SetVisible(false);
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            _available = true;
            SetVisible(true);
        }

        private void SetVisible(bool show)
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(show);
            }
            else
            {
                var mesh = GetComponent<MeshRenderer>();
                if (mesh != null) mesh.enabled = show;
            }
        }
    }
}
