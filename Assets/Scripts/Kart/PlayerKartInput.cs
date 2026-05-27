using KartGame.Core;
using KartGame.PowerUps;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KartGame.Kart
{
    /*
     * Script: PlayerKartInput.cs
     * Purpose: Reads keyboard input and forwards acceleration, steering, braking, reset and power-up usage requests to the player kart.
     * Attach To: Player kart root GameObject.
     * Required Components: KartController.
     * Dependencies: CheckpointTracker, optional future PowerUpInventory.
     * Inspector Setup: Keep this on the same GameObject as KartController and ensure the project uses the Input System package or Both input backends.
     */
    [RequireComponent(typeof(KartController))]
    public class PlayerKartInput : MonoBehaviour
    {
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private float reverseSpeedThreshold = 1.25f;
        [SerializeField] private bool forceEnableControlOnInput = true;

        private float _nextDisabledControlWarningTime;

        private void Awake()
        {
            kartController ??= GetComponent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInChildren<KartPowerUpController>(true);
        }

        private void Update()
        {
            if (kartController == null || Keyboard.current == null)
            {
                return;
            }

            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInChildren<KartPowerUpController>(true);

            var acceleratePressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            var brakePressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            var steerLeftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            var steerRightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            var steering = 0f;
            if (steerLeftPressed)
            {
                steering -= 1f;
            }

            if (steerRightPressed)
            {
                steering += 1f;
            }

            var acceleration = acceleratePressed ? 1f : 0f;
            var brake = 0f;

            if (brakePressed)
            {
                if (kartController.GetCurrentSpeed() > reverseSpeedThreshold)
                {
                    brake = 1f;
                }
                else
                {
                    acceleration = -1f;
                }
            }

            var hasMovementInput = !Mathf.Approximately(acceleration, 0f) || !Mathf.Approximately(steering, 0f) || brake > 0f;
            if (hasMovementInput && !kartController.IsControlEnabled)
            {
                if (forceEnableControlOnInput)
                {
                    var raceManager = RaceManager.Instance;
                    var isRacing = raceManager != null && raceManager.CurrentState == RaceState.Racing;
                    var playerFinished = checkpointTracker != null && checkpointTracker.HasFinishedRace;
                    if (isRacing && !playerFinished)
                    {
                        kartController.SetControlEnabled(true);
                    }
                }
                else if (Time.unscaledTime >= _nextDisabledControlWarningTime)
                {
                    Debug.LogWarning("Player input is being received, but KartController control is disabled.", this);
                    _nextDisabledControlWarningTime = Time.unscaledTime + 1f;
                }
            }

            kartController.SetInput(acceleration, steering, brake);

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                checkpointTracker?.RespawnToRecoveryPoint();
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (powerUpController != null)
                {
                    powerUpController.UseStoredPowerUp();
                }
                else
                {
                    BroadcastMessage("UseStoredPowerUp", SendMessageOptions.DontRequireReceiver);
                }
            }

            if (WasPowerUpKeyPressed(Keyboard.current.digit1Key, Keyboard.current.numpad1Key))
            {
                TryUseSpecificPowerUp(PowerUpType.Banana);
            }
            else if (WasPowerUpKeyPressed(Keyboard.current.digit2Key, Keyboard.current.numpad2Key))
            {
                TryUseSpecificPowerUp(PowerUpType.Shell);
            }
            else if (WasPowerUpKeyPressed(Keyboard.current.digit3Key, Keyboard.current.numpad3Key))
            {
                TryUseSpecificPowerUp(PowerUpType.Mushroom);
            }
            else if (WasPowerUpKeyPressed(Keyboard.current.digit4Key, Keyboard.current.numpad4Key))
            {
                TryUseSpecificPowerUp(PowerUpType.Star);
            }
        }

        private static bool WasPowerUpKeyPressed(ButtonControl mainKey, ButtonControl alternativeKey)
        {
            return (mainKey != null && mainKey.wasPressedThisFrame)
                || (alternativeKey != null && alternativeKey.wasPressedThisFrame);
        }

        private void TryUseSpecificPowerUp(PowerUpType powerUpType)
        {
            if (powerUpController != null)
            {
                powerUpController.UsePowerUp(powerUpType);
                return;
            }

            Debug.LogWarning($"No KartPowerUpController was found for player power-up input '{powerUpType}'.", this);
        }
    }
}
