using System.Collections;
using KartGame.PowerUps;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(Collider))]
    public class PowerUpPickupBox : MonoBehaviour
    {
        [SerializeField] private int pointsGranted = 1;
        [SerializeField] private float respawnDelay = 6f;
        [SerializeField] private bool rotateVisual = true;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Renderer[] visualRenderers;
        [SerializeField] private Collider[] collidersToToggle;

        private bool _isAvailable = true;
        private Coroutine _respawnRoutine;

        private void Awake()
        {
            CacheReferences();
            EnsureTriggerColliders();
        }

        private void OnValidate()
        {
            CacheReferences();
            EnsureTriggerColliders();
        }

        private void Update()
        {
            if (_isAvailable && rotateVisual && visualRoot != null)
            {
                visualRoot.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isAvailable)
            {
                return;
            }

            var powerUpController = ResolvePowerUpController(other);
            if (powerUpController == null)
            {
                return;
            }

            powerUpController.AddPowerUpPoint(pointsGranted);
            SetAvailable(false);

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
            }

            if (respawnDelay > 0f)
            {
                _respawnRoutine = StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            SetAvailable(true);
            _respawnRoutine = null;
        }

        private void SetAvailable(bool isAvailable)
        {
            _isAvailable = isAvailable;

            if (visualRenderers != null)
            {
                for (var index = 0; index < visualRenderers.Length; index++)
                {
                    if (visualRenderers[index] != null)
                    {
                        visualRenderers[index].enabled = isAvailable;
                    }
                }
            }

            if (collidersToToggle != null)
            {
                for (var index = 0; index < collidersToToggle.Length; index++)
                {
                    if (collidersToToggle[index] != null)
                    {
                        collidersToToggle[index].enabled = isAvailable;
                    }
                }
            }
        }

        private void CacheReferences()
        {
            visualRoot ??= gameObject;

            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                visualRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (collidersToToggle == null || collidersToToggle.Length == 0)
            {
                collidersToToggle = GetComponents<Collider>();
            }
        }

        private void EnsureTriggerColliders()
        {
            var colliders = GetComponents<Collider>();
            for (var index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].isTrigger = true;
                }
            }
        }

        private static KartPowerUpController ResolvePowerUpController(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            var controller = other.GetComponentInParent<KartPowerUpController>();
            if (controller != null)
            {
                return controller;
            }

            var kartController = other.GetComponentInParent<Kart.KartController>();
            return kartController != null ? kartController.GetComponentInChildren<KartPowerUpController>(true) : null;
        }
    }
}
