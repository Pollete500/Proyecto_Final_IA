using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    public abstract class PowerUpHazardBase : MonoBehaviour
    {
        [SerializeField] private float fallbackLifetime = 10f;
        [SerializeField] private float fallbackStunDuration = 2f;

        private Collider[] _ownColliders;
        private float _despawnAt;
        private bool _didNotifyDisposed;

        public KartPowerUpController OwnerPowerUpController { get; private set; }
        public KartController OwnerKart { get; private set; }
        public PowerUpType PowerUpType { get; private set; }
        public float StunDuration { get; private set; }
        public event System.Action<PowerUpHazardBase> Disposed;

        protected virtual void Awake()
        {
            _ownColliders = GetComponentsInChildren<Collider>(true);
            _despawnAt = Time.time + Mathf.Max(0.1f, fallbackLifetime);
        }

        protected virtual void Update()
        {
            if (Time.time >= _despawnAt)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_didNotifyDisposed)
            {
                return;
            }

            _didNotifyDisposed = true;
            Disposed?.Invoke(this);
        }

        public virtual void Initialize(
            KartPowerUpController ownerPowerUpController,
            KartController ownerKart,
            PowerUpType powerUpType,
            float stunDuration,
            float lifetime)
        {
            OwnerPowerUpController = ownerPowerUpController;
            OwnerKart = ownerKart;
            PowerUpType = powerUpType;
            StunDuration = stunDuration > 0f ? stunDuration : fallbackStunDuration;
            _despawnAt = Time.time + Mathf.Max(0.1f, lifetime > 0f ? lifetime : fallbackLifetime);
            IgnoreOwnerCollisions();
        }

        protected bool TryAffect(KartController targetKart)
        {
            if (!CanAffect(targetKart))
            {
                return false;
            }

            targetKart.ApplyStun(StunDuration);
            OwnerPowerUpController?.NotifyPowerUpHit(PowerUpType, targetKart);
            return true;
        }

        protected bool CanAffect(KartController targetKart)
        {
            if (targetKart == null || targetKart == OwnerKart)
            {
                return false;
            }

            if (OwnerKart != null && targetKart.transform.root == OwnerKart.transform.root)
            {
                return false;
            }

            return !targetKart.IsInvincible;
        }

        protected bool IsOwnerCollider(Collider other)
        {
            if (other == null || OwnerKart == null)
            {
                return false;
            }

            return other.transform.root == OwnerKart.transform.root;
        }

        protected void IgnoreOwnerCollisions()
        {
            if (OwnerKart == null)
            {
                return;
            }

            _ownColliders ??= GetComponentsInChildren<Collider>(true);
            var ownerColliders = OwnerKart.GetComponentsInChildren<Collider>(true);

            for (var ownIndex = 0; ownIndex < _ownColliders.Length; ownIndex++)
            {
                var ownCollider = _ownColliders[ownIndex];
                if (ownCollider == null)
                {
                    continue;
                }

                for (var ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
                {
                    var ownerCollider = ownerColliders[ownerIndex];
                    if (ownerCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, ownerCollider, true);
                }
            }
        }
    }
}
