using KartGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private float reverseSpeedThreshold = 1.25f;
        [SerializeField] private bool forceEnableControlOnInput = true;

        private float _nextDisabledControlWarningTime;

        private void Awake()
        {
            kartController ??= GetComponent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
        }

        private void Update()
        {
            if (kartController == null || Keyboard.current == null)
            {
                return;
            }

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
                    if (raceManager == null || raceManager.CurrentState != RaceState.Finished)
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
                SendMessage("UseStoredPowerUp", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
