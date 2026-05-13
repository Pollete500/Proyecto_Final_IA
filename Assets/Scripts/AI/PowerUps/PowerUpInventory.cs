using KartGame.Kart;
using UnityEngine;

namespace KartGame.AI.PowerUps
{
    [RequireComponent(typeof(KartController))]
    public class PowerUpInventory : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KartController kartController;

        [Header("Stored Item")]
        [SerializeField] private PowerUpType storedPowerUp = PowerUpType.None;

        [Header("Item Tuning")]
        [SerializeField] private float mushroomBoostMultiplier = 1.5f;
        [SerializeField] private float mushroomBoostDuration = 2.0f;
        [SerializeField] private float starDuration = 4.0f;

        public PowerUpType StoredPowerUp => storedPowerUp;
        public bool HasStoredPowerUp => storedPowerUp != PowerUpType.None;

        private void Awake()
        {
            kartController ??= GetComponent<KartController>();
        }

        public bool SetStoredPowerUp(PowerUpType powerUpType)
        {
            if (powerUpType == PowerUpType.None)
            {
                return false;
            }

            storedPowerUp = powerUpType;
            return true;
        }

        public bool ClearStoredPowerUp()
        {
            if (storedPowerUp == PowerUpType.None)
            {
                return false;
            }

            storedPowerUp = PowerUpType.None;
            return true;
        }

        public bool UseStoredPowerUp()
        {
            if (kartController == null || storedPowerUp == PowerUpType.None)
            {
                return false;
            }

            var consumed = ApplyStoredPowerUp();
            if (consumed)
            {
                storedPowerUp = PowerUpType.None;
            }

            return consumed;
        }

        public bool TryAssignByRacePosition(int racePosition, int racerCount)
        {
            return PowerUpManager.TryAssignForPosition(this, racePosition, racerCount);
        }

        private bool ApplyStoredPowerUp()
        {
            switch (storedPowerUp)
            {
                case PowerUpType.MushroomBoost:
                    kartController.ApplyBoost(mushroomBoostMultiplier, mushroomBoostDuration);
                    return true;
                case PowerUpType.Star:
                    kartController.ApplyInvincibility(starDuration);
                    return true;
                case PowerUpType.Banana:
                case PowerUpType.Shell:
                    Debug.LogWarning($"Power-up '{storedPowerUp}' is reserved for the future trap/projectile system.", this);
                    return false;
                default:
                    return false;
            }
        }
    }
}
