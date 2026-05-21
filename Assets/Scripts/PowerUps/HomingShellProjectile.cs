using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class HomingShellProjectile : PowerUpHazardBase
    {
        [SerializeField] private string wallTag = "Wall";
        [SerializeField] private float targetVerticalOffset = 0.35f;

        private CheckpointTracker _targetTracker;
        private KartController _targetKart;
        private Rigidbody _shellRigidbody;
        private float _speed = 14f;
        private float _turnRateDegrees = 220f;
        private bool _homeToTarget = true;

        protected override void Awake()
        {
            base.Awake();

            _shellRigidbody = GetComponent<Rigidbody>();
            _shellRigidbody.useGravity = false;
            _shellRigidbody.isKinematic = true;
            _shellRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
        }

        protected override void Update()
        {
            base.Update();
        }

        private void FixedUpdate()
        {
            var desiredDirection = transform.forward;
            var targetTransform = GetTargetTransform();
            if (_homeToTarget && targetTransform != null)
            {
                var targetAimPosition = targetTransform.position + Vector3.up * targetVerticalOffset;
                var targetDirection = targetAimPosition - transform.position;
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    desiredDirection = targetDirection.normalized;
                }
            }

            var maxRadiansDelta = _turnRateDegrees * Mathf.Deg2Rad * Time.deltaTime;
            var currentForward = Vector3.RotateTowards(transform.forward, desiredDirection, maxRadiansDelta, 0f);
            if (currentForward.sqrMagnitude > 0.001f)
            {
                _shellRigidbody.MoveRotation(Quaternion.LookRotation(currentForward, Vector3.up));
            }

            _shellRigidbody.MovePosition(_shellRigidbody.position + currentForward * _speed * Time.fixedDeltaTime);
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
            float hitRadius,
            bool homeToTarget)
        {
            base.Initialize(ownerPowerUpController, ownerKart, powerUpType, stunDuration, lifetime);
            _targetTracker = targetTracker;
            _targetKart = targetTracker != null
                ? targetTracker.GetComponent<KartController>() ?? targetTracker.GetComponentInParent<KartController>()
                : null;
            _speed = Mathf.Max(0.1f, speed);
            _turnRateDegrees = Mathf.Max(0f, turnRateDegrees);
            _homeToTarget = homeToTarget;

            var sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.radius = Mathf.Max(0.1f, hitRadius);
        }

        private Transform GetTargetTransform()
        {
            if (_targetKart != null)
            {
                return _targetKart.transform;
            }

            return _targetTracker != null ? _targetTracker.transform : null;
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
