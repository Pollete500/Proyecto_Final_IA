using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(SphereCollider))]
    public class BananaHazard : PowerUpHazardBase
    {
        private const string BananaLayerName = "Banana";
        [SerializeField, Min(0f)] private float bananaStunDuration = 2.5f;
        [SerializeField] private bool bananaTrain = false;

        public static event System.Action<BananaHazard, KartController> AnyTrainingBananaTouched;

        protected override bool ShouldAutoDespawn => !bananaTrain;

        protected override void Awake()
        {
            base.Awake();

            ApplyLayerFromName(gameObject, BananaLayerName);

            var sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsOwnerCollider(other))
            {
                return;
            }

            var targetKart = other.GetComponentInParent<KartController>();
            if (targetKart == null)
            {
                return;
            }

            if (targetKart.IsInvincible)
            {
                return;
            }

            if (bananaTrain && IsBotKart(targetKart))
            {
                AnyTrainingBananaTouched?.Invoke(this, targetKart);
                targetKart.ApplyStun(bananaStunDuration);
                IgnoreFor(targetKart);
                return;
            }

            targetKart.ApplyStun(bananaStunDuration);
            OwnerPowerUpController?.NotifyPowerUpHit(PowerUpType, targetKart);
            Destroy(gameObject);
        }

        public void SetTrainingMode(bool trainingMode)
        {
            bananaTrain = trainingMode;
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
