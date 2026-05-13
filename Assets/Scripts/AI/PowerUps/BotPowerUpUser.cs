using System.Collections;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.AI.PowerUps
{
    /*
     * Script: BotPowerUpUser.cs
     * Purpose: Makes AI bots automatically use stored power-ups at appropriate moments.
     * Attach To: AI kart root (any bot using AIKartInput or KartAgent).
     * Required Components: PowerUpInventory, KartController, CheckpointTracker.
     */
    [RequireComponent(typeof(PowerUpInventory))]
    public class BotPowerUpUser : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PowerUpInventory inventory;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;

        [Header("Usage Timing")]
        [SerializeField] private float minUseDelay = 0.5f;
        [SerializeField] private float maxUseDelay = 2.5f;
        [SerializeField] private float situationCheckTimeout = 3f;

        [Header("Usage Conditions")]
        [SerializeField] private float mushroomAlignmentThreshold = 0.65f;

        private PowerUpType _lastObservedPowerUp = PowerUpType.None;
        private Coroutine _useRoutine;

        private void Awake()
        {
            inventory ??= GetComponent<PowerUpInventory>();
            kartController ??= GetComponent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
        }

        private void Update()
        {
            if (inventory == null) return;

            var current = inventory.StoredPowerUp;

            if (current != PowerUpType.None && _lastObservedPowerUp == PowerUpType.None && _useRoutine == null)
                _useRoutine = StartCoroutine(DelayedUse());

            if (current == PowerUpType.None && _useRoutine != null)
            {
                StopCoroutine(_useRoutine);
                _useRoutine = null;
            }

            _lastObservedPowerUp = current;
        }

        private IEnumerator DelayedUse()
        {
            yield return new WaitForSeconds(Random.Range(minUseDelay, maxUseDelay));

            var timeout = situationCheckTimeout;
            while (timeout > 0f && !IsGoodMomentToUse(inventory.StoredPowerUp))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            inventory.UseStoredPowerUp();
            _useRoutine = null;
        }

        private bool IsGoodMomentToUse(PowerUpType powerUp)
        {
            switch (powerUp)
            {
                case PowerUpType.MushroomBoost:
                    if (checkpointTracker == null || checkpointTracker.NextCheckpoint == null) return true;
                    var toCheckpoint = (checkpointTracker.NextCheckpoint.position - transform.position).normalized;
                    return Vector3.Dot(transform.forward, toCheckpoint) >= mushroomAlignmentThreshold;
                case PowerUpType.Star:
                    return true;
                default:
                    return false;
            }
        }
    }
}
