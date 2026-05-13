using UnityEngine;

namespace KartGame.AI.PowerUps
{
    public static class PowerUpManager
    {
        public static PowerUpType GetPowerUpForPosition(int racePosition, int racerCount)
        {
            racePosition = Mathf.Max(1, racePosition);
            racerCount = Mathf.Max(1, racerCount);

            if (racerCount == 1)
            {
                return PowerUpType.MushroomBoost;
            }

            var normalizedPosition = Mathf.InverseLerp(1f, racerCount, racePosition);
            var roll = Random.value;

            if (normalizedPosition <= 0.34f)
            {
                return roll < 0.75f ? PowerUpType.Star : PowerUpType.MushroomBoost;
            }

            if (normalizedPosition <= 0.67f)
            {
                return roll < 0.55f ? PowerUpType.MushroomBoost : PowerUpType.Star;
            }

            return roll < 0.8f ? PowerUpType.MushroomBoost : PowerUpType.Star;
        }

        public static bool TryAssignForPosition(PowerUpInventory inventory, int racePosition, int racerCount)
        {
            if (inventory == null)
            {
                return false;
            }

            return inventory.SetStoredPowerUp(GetPowerUpForPosition(racePosition, racerCount));
        }
    }
}
