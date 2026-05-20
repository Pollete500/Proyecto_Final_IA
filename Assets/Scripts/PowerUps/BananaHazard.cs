using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class BananaHazard : PowerUpHazardBase
    {
        protected override void Awake()
        {
            base.Awake();

            var bananaRigidbody = GetComponent<Rigidbody>();
            bananaRigidbody.useGravity = false;
            bananaRigidbody.isKinematic = true;
            bananaRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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

            if (TryAffect(targetKart))
            {
                Destroy(gameObject);
            }
        }
    }
}
