using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class HomingShellProjectile : PowerUpHazardBase
    {
        [SerializeField] private string wallTag = "Wall";

        private CheckpointTracker _targetTracker;
        private float _speed = 14f;
        private float _turnRateDegrees = 220f;

        protected override void Awake()
        {
            base.Awake();

            var shellRigidbody = GetComponent<Rigidbody>();
            shellRigidbody.useGravity = false;
            shellRigidbody.isKinematic = true;
            shellRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
        }

        protected override void Update()
        {
            base.Update();

            var desiredDirection = transform.forward;
            if (_targetTracker != null)
            {
                var targetDirection = _targetTracker.transform.position - transform.position;
                targetDirection.y = 0f;
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    desiredDirection = targetDirection.normalized;
                }
            }

            var maxRadiansDelta = _turnRateDegrees * Mathf.Deg2Rad * Time.deltaTime;
            var currentForward = Vector3.RotateTowards(transform.forward, desiredDirection, maxRadiansDelta, 0f);
            if (currentForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(currentForward, Vector3.up);
            }

            transform.position += transform.forward * _speed * Time.deltaTime;
        }

        public void Initialize(
            KartPowerUpController ownerPowerUpController,
            KartController ownerKart,
            PowerUpType powerUpType,
            CheckpointTracker targetTracker,
            float stunDuration,
            float lifetime,
            float speed,
            float turnRateDegrees,
            float hitRadius)
        {
            base.Initialize(ownerPowerUpController, ownerKart, powerUpType, stunDuration, lifetime);
            _targetTracker = targetTracker;
            _speed = Mathf.Max(0.1f, speed);
            _turnRateDegrees = Mathf.Max(0f, turnRateDegrees);

            var sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.radius = Mathf.Max(0.1f, hitRadius);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsOwnerCollider(other))
            {
                return;
            }

            var targetKart = other.GetComponentInParent<KartController>();
            if (targetKart != null)
            {
                if (targetKart.IsInvincible)
                {
                    Destroy(gameObject);
                    return;
                }

                if (TryAffect(targetKart))
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (other.CompareTag(wallTag))
            {
                Destroy(gameObject);
            }
        }
    }
}
