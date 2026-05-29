using System;
using KartGame.Core;
using KartGame.PowerUps;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KartGame.Kart
{
    /*
     * Script: PlayerKartInput.cs
     * Purpose: Reads keyboard and mouse input and forwards acceleration, steering, braking, reset and power-up usage requests to the player kart.
     * Attach To: Player kart root GameObject.
     * Required Components: KartController.
     * Dependencies: CheckpointTracker, optional future PowerUpInventory.
     * Inspector Setup: Keep this on the same GameObject as KartController and ensure the project uses the Input System package or Both input backends.
     */
    [RequireComponent(typeof(KartController))]
    public class PlayerKartInput : MonoBehaviour
    {
        [Serializable]
        private struct PowerUpInputBinding
        {
            [SerializeField] public TriggerType triggerType;
            [SerializeField] public Key key;
        }

        private enum TriggerType
        {
            None = 0,
            Key = 1,
            MouseLeftButton = 2,
            MouseRightButton = 3,
            MouseMiddleButton = 4
        }

        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private float reverseSpeedThreshold = 1.25f;
        [SerializeField] private bool forceEnableControlOnInput = true;

        [Header("Power-Up Controls")]
        [SerializeField] private PowerUpInputBinding useStoredPowerUpBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.Space
        };
        [SerializeField] private PowerUpInputBinding bananaPowerUpBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.MouseRightButton,
            key = Key.None
        };
        [SerializeField] private PowerUpInputBinding bananaPowerUpAlternateBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.Digit1
        };
        [SerializeField] private PowerUpInputBinding shellPowerUpBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.MouseLeftButton,
            key = Key.None
        };
        [SerializeField] private PowerUpInputBinding shellPowerUpAlternateBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.Digit2
        };
        [SerializeField] private PowerUpInputBinding mushroomPowerUpBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.Digit3
        };
        [SerializeField] private PowerUpInputBinding starPowerUpBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.LeftShift
        };
        [SerializeField] private PowerUpInputBinding starPowerUpAlternateBinding = new PowerUpInputBinding
        {
            triggerType = TriggerType.Key,
            key = Key.Digit4
        };
        [SerializeField, HideInInspector] private int powerUpControlPresetVersion;

        private float _nextDisabledControlWarningTime;

        private void Awake()
        {
            ApplyDefaultPowerUpControlPresetIfNeeded();
            kartController ??= GetComponent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInChildren<KartPowerUpController>(true);
        }

        private void Update()
        {
            ApplyDefaultPowerUpControlPresetIfNeeded();

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
                if (checkpointTracker != null && !checkpointTracker.HasFinishedRace)
                {
                    checkpointTracker.RespawnToRecoveryPoint();
                }
            }

            if (WasBindingPressedThisFrame(useStoredPowerUpBinding))
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

            if (WasBindingPressedThisFrame(bananaPowerUpBinding) || WasBindingPressedThisFrame(bananaPowerUpAlternateBinding))
            {
                TryUseSpecificPowerUp(PowerUpType.Banana);
            }
            else if (WasBindingPressedThisFrame(shellPowerUpBinding) || WasBindingPressedThisFrame(shellPowerUpAlternateBinding))
            {
                TryUseSpecificPowerUp(PowerUpType.Shell);
            }
            else if (WasBindingPressedThisFrame(mushroomPowerUpBinding))
            {
                TryUseSpecificPowerUp(PowerUpType.Mushroom);
            }
            else if (WasBindingPressedThisFrame(starPowerUpBinding) || WasBindingPressedThisFrame(starPowerUpAlternateBinding))
            {
                TryUseSpecificPowerUp(PowerUpType.Star);
            }
        }

        private void OnValidate()
        {
            ApplyDefaultPowerUpControlPresetIfNeeded();
        }

        private void ApplyDefaultPowerUpControlPresetIfNeeded()
        {
            if (powerUpControlPresetVersion >= 1)
            {
                return;
            }

            useStoredPowerUpBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.Space
            };
            bananaPowerUpBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.MouseRightButton,
                key = Key.None
            };
            bananaPowerUpAlternateBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.Digit1
            };
            shellPowerUpBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.MouseLeftButton,
                key = Key.None
            };
            shellPowerUpAlternateBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.Digit2
            };
            mushroomPowerUpBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.Digit3
            };
            starPowerUpBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.LeftShift
            };
            starPowerUpAlternateBinding = new PowerUpInputBinding
            {
                triggerType = TriggerType.Key,
                key = Key.Digit4
            };

            powerUpControlPresetVersion = 1;
        }

        private static bool WasBindingPressedThisFrame(PowerUpInputBinding binding)
        {
            if (Keyboard.current == null && Mouse.current == null)
            {
                return false;
            }

            return binding.triggerType switch
            {
                TriggerType.Key => WasKeyPressedThisFrame(binding.key),
                TriggerType.MouseLeftButton => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame,
                TriggerType.MouseRightButton => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame,
                TriggerType.MouseMiddleButton => Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame,
                _ => false
            };
        }

        private static bool WasKeyPressedThisFrame(Key key)
        {
            if (key == Key.None || Keyboard.current == null)
            {
                return false;
            }

            var keyControl = Keyboard.current[key];
            return keyControl != null && keyControl.wasPressedThisFrame;
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
