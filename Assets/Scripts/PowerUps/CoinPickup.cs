using System.Collections.Generic;
using System.Collections;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(Collider))]
    public class CoinPickup : MonoBehaviour
    {
        private const string CoinLayerName = "Coin";
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private int coinAmount = 1;
        [SerializeField] private bool coinTrain = false;
        [SerializeField] private bool respawnAfterPickup = false;
        [SerializeField] private float respawnDelay = 6f;
        [SerializeField] private float droppedCoinLifetime = 8f;
        [SerializeField] private float droppedCoinLaunchForce = 2.5f;
        [SerializeField] private bool rotateVisual = true;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Renderer[] visualRenderers;
        [SerializeField] private Collider[] collidersToToggle;
        [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.15f, 1f);

        private bool _isAvailable = true;
        private Coroutine _respawnRoutine;
        private readonly HashSet<int> _ignoredKartIds = new HashSet<int>();

        public static event System.Action<CoinPickup, KartController, int> AnyCoinCollected;
        public static event System.Action<CoinPickup, KartController, int> AnyTrainingCoinTouched;

        private void Awake()
        {
            ApplyLayerFromName(gameObject, CoinLayerName);
            CacheReferences();
            EnsureTriggerColliders();
            ApplyCoinVisuals();
        }

        private void OnValidate()
        {
            coinAmount = Mathf.Max(1, coinAmount);
            respawnDelay = Mathf.Max(0f, respawnDelay);
            droppedCoinLifetime = Mathf.Max(0.1f, droppedCoinLifetime);
            droppedCoinLaunchForce = Mathf.Max(0f, droppedCoinLaunchForce);
            ApplyLayerFromName(gameObject, CoinLayerName);
            CacheReferences();
            EnsureTriggerColliders();
            ApplyCoinVisuals();
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

            var kartController = other != null ? other.GetComponentInParent<KartController>() : null;
            if (kartController == null)
            {
                return;
            }

            if (coinTrain && IsBotKart(kartController) && IsIgnoredFor(kartController))
            {
                return;
            }

            if (coinTrain && IsBotKart(kartController))
            {
                AnyTrainingCoinTouched?.Invoke(this, kartController, coinAmount);
                IgnoreFor(kartController);
                return;
            }

            var addedCoins = kartController.AddCoins(coinAmount);
            if (addedCoins > 0)
            {
                AnyCoinCollected?.Invoke(this, kartController, addedCoins);
            }

            ConsumePickup();
        }

        public void InitializeAsDroppedCoin(int amount, float lifetime, Vector3 launchDirection, float launchForce)
        {
            coinTrain = false;
            respawnAfterPickup = false;
            coinAmount = Mathf.Max(1, amount);
            droppedCoinLifetime = Mathf.Max(0.1f, lifetime);
            droppedCoinLaunchForce = Mathf.Max(0f, launchForce);
            _isAvailable = true;

            CacheReferences();
            EnsureTriggerColliders();
            ApplyCoinVisuals();
            SetAvailable(true);

            if (TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody.isKinematic = false;
                rigidbody.useGravity = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;

                var impulseDirection = launchDirection.sqrMagnitude > 0.001f
                    ? launchDirection.normalized
                    : (Vector3.up + transform.forward * 0.5f).normalized;
                rigidbody.AddForce(impulseDirection * droppedCoinLaunchForce, ForceMode.Impulse);
                rigidbody.AddTorque(Random.onUnitSphere * droppedCoinLaunchForce, ForceMode.Impulse);
            }

            Destroy(gameObject, droppedCoinLifetime);
        }

        public static CoinPickup CreateDroppedCoin(
            Vector3 position,
            Quaternion rotation,
            int amount,
            float lifetime,
            Vector3 launchDirection,
            float launchForce)
        {
            var coinObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinObject.name = "DroppedCoin";
            coinObject.transform.SetPositionAndRotation(position, rotation);
            coinObject.transform.localScale = new Vector3(0.38f, 0.08f, 0.38f);
            ApplyLayerFromName(coinObject, CoinLayerName);

            var rigidbody = coinObject.AddComponent<Rigidbody>();
            rigidbody.mass = 0.15f;
            rigidbody.linearDamping = 0.35f;
            rigidbody.angularDamping = 0.1f;
            rigidbody.useGravity = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var pickup = coinObject.AddComponent<CoinPickup>();
            pickup.InitializeAsDroppedCoin(amount, lifetime, launchDirection, launchForce);
            return pickup;
        }

        private void ConsumePickup()
        {
            if (respawnAfterPickup)
            {
                SetAvailable(false);

                if (_respawnRoutine != null)
                {
                    StopCoroutine(_respawnRoutine);
                }

                if (respawnDelay > 0f)
                {
                    _respawnRoutine = StartCoroutine(RespawnRoutine());
                }
                else
                {
                    SetAvailable(true);
                }

                return;
            }

            Destroy(gameObject);
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

        public bool IsIgnoredFor(KartController kartController)
        {
            return kartController != null && _ignoredKartIds.Contains(kartController.GetInstanceID());
        }

        public void IgnoreFor(KartController kartController)
        {
            if (kartController == null)
            {
                return;
            }

            if (!_ignoredKartIds.Add(kartController.GetInstanceID()))
            {
                return;
            }

            var pickupColliders = collidersToToggle != null && collidersToToggle.Length > 0
                ? collidersToToggle
                : GetComponentsInChildren<Collider>(true);
            var kartColliders = kartController.GetComponentsInChildren<Collider>(true);

            for (var pickupIndex = 0; pickupIndex < pickupColliders.Length; pickupIndex++)
            {
                var pickupCollider = pickupColliders[pickupIndex];
                if (pickupCollider == null)
                {
                    continue;
                }

                for (var kartIndex = 0; kartIndex < kartColliders.Length; kartIndex++)
                {
                    var kartCollider = kartColliders[kartIndex];
                    if (kartCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(pickupCollider, kartCollider, true);
                }
            }
        }

        private void ApplyCoinVisuals()
        {
            if (visualRenderers == null)
            {
                return;
            }

            var propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < visualRenderers.Length; index++)
            {
                if (visualRenderers[index] == null)
                {
                    continue;
                }

                visualRenderers[index].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorPropertyId, coinColor);
                propertyBlock.SetColor(ColorPropertyId, coinColor);
                visualRenderers[index].SetPropertyBlock(propertyBlock);
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

        private static bool IsBotKart(KartController kartController)
        {
            return kartController != null && kartController.GetComponentInParent<PlayerKartInput>() == null;
        }

        private static void ApplyLayerFromName(GameObject targetObject, string layerName)
        {
            if (targetObject == null)
            {
                return;
            }

            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            ApplyLayerRecursively(targetObject.transform, layer);
        }

        private static void ApplyLayerRecursively(Transform targetTransform, int layer)
        {
            if (targetTransform == null)
            {
                return;
            }

            targetTransform.gameObject.layer = layer;

            for (var childIndex = 0; childIndex < targetTransform.childCount; childIndex++)
            {
                ApplyLayerRecursively(targetTransform.GetChild(childIndex), layer);
            }
        }
    }
}
