using System.Collections.Generic;
using KartGame.PowerUps;
using UnityEngine;

namespace KartGame.Kart
{
    /*
     * Script: KartController.cs
     * Purpose: Controls arcade kart movement using Rigidbody physics, including acceleration, steering, braking, boost, coins, slowdown and reset support.
     * Attach To: Player and AI kart root GameObjects.
     * Required Components: Rigidbody, Collider.
     * Dependencies: PlayerKartInput or AIKartInput, CheckpointTracker.
     * Inspector Setup: Configure acceleration, top speed, steering and grip values, then tune the ground probe to match the collider height.
     */
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class KartController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float accelerationForce = 22f;
        [SerializeField] private float reverseAccelerationForce = 10f;
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float steeringStrength = 110f;
        [SerializeField] private float brakingForce = 18f;
        [SerializeField] private float lateralGrip = 0.88f;
        [SerializeField] private float coastingDrag = 0.35f;

        [Header("Coins")]
        [SerializeField, Min(1)] private int maxCoins = 10;
        [SerializeField, Min(1f)] private float coinSpeedMultiplierAtMax = 2f;
        [SerializeField, Range(0.05f, 1f)] private float hazardSlowMultiplier = 0.2f;
        [SerializeField, Min(0f)] private float coinDropLifetime = 6f;
        [SerializeField, Min(0f)] private float coinDropImpulse = 2.5f;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 groundProbeOffset = new Vector3(0f, 0.35f, 0f);
        [SerializeField] private float groundProbeDistance = 0.55f;
        [SerializeField] private float downforce = 25f;
        [SerializeField] private float airSteeringMultiplier = 0.35f;

        [Header("Physics")]
        [SerializeField] private float mass = 140f;
        [SerializeField] private float angularDrag = 3f;
        [SerializeField] private bool ignoreCollisionsWithOtherKarts = true;

        private static readonly List<KartController> ActiveKarts = new List<KartController>();
        private Rigidbody _rigidbody;
        private Collider[] _kartColliders;
        private float _accelerationInput;
        private float _steeringInput;
        private float _brakeInput;
        private float _boostTimer;
        private float _boostMultiplier = 1f;
        private float _stunTimer;
        private float _hazardSlowTimer;
        private float _hazardSlowMultiplier = 1f;
        private float _invincibilityTimer;
        private int _currentCoins;
        private bool _isGrounded;
        private bool _controlEnabled = true;

        public float MaxSpeed => maxSpeed;
        public int CurrentCoins => _currentCoins;
        public int MaxCoins => Mathf.Max(1, maxCoins);
        public float CoinSpeedMultiplier => Mathf.Lerp(1f, Mathf.Max(1f, coinSpeedMultiplierAtMax), Mathf.Clamp01((float)_currentCoins / MaxCoins));
        public float HazardSlowMultiplier => _hazardSlowTimer > 0f ? _hazardSlowMultiplier : 1f;
        public bool IsGrounded => _isGrounded;
        public bool IsInvincible => _invincibilityTimer > 0f;
        public bool IsControlEnabled => _controlEnabled;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _kartColliders = GetComponentsInChildren<Collider>(true);
            _rigidbody.mass = mass;
            _rigidbody.angularDamping = angularDrag;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void OnValidate()
        {
            maxCoins = Mathf.Max(1, maxCoins);
            coinSpeedMultiplierAtMax = Mathf.Max(1f, coinSpeedMultiplierAtMax);
            hazardSlowMultiplier = Mathf.Clamp(hazardSlowMultiplier, 0.05f, 1f);
            coinDropLifetime = Mathf.Max(0.1f, coinDropLifetime);
            coinDropImpulse = Mathf.Max(0f, coinDropImpulse);
        }

        private void OnEnable()
        {
            RegisterKartCollisionIgnores();
        }

        private void OnDisable()
        {
            UnregisterKartCollisionIgnores();
        }

        private void FixedUpdate()
        {
            TickStatusEffects(Time.fixedDeltaTime);
            UpdateGroundedState();
            ApplyMotorForce();
            ApplySteering();
            ApplyLateralGrip();
            ApplyDownforce();
            LimitTopSpeed();
        }

        public void SetInput(float acceleration, float steering, float brake)
        {
            _accelerationInput = Mathf.Clamp(acceleration, -1f, 1f);
            _steeringInput = Mathf.Clamp(steering, -1f, 1f);
            _brakeInput = Mathf.Clamp01(brake);
        }

        public float GetCurrentSpeed()
        {
            return _rigidbody == null ? 0f : _rigidbody.linearVelocity.magnitude;
        }

        public void SetControlEnabled(bool isEnabled, bool freezePhysics = true)
        {
            _controlEnabled = isEnabled;
            SetInput(0f, 0f, 0f);

            if (_rigidbody == null) return;

            if (!isEnabled && freezePhysics)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }
            else if (isEnabled)
            {
                _rigidbody.isKinematic = false;
            }
        }

        public void ApplyBoost(float multiplier, float duration)
        {
            _boostMultiplier = Mathf.Max(_boostMultiplier, Mathf.Max(1f, multiplier));
            _boostTimer = Mathf.Max(_boostTimer, duration);
        }

        public void ApplyStun(float duration)
        {
            ApplyHazardSlow(duration, hazardSlowMultiplier);
        }

        public void ApplyHazardSlow(float duration, float multiplier = -1f)
        {
            var appliedMultiplier = multiplier > 0f ? multiplier : hazardSlowMultiplier;
            _hazardSlowMultiplier = Mathf.Min(_hazardSlowMultiplier, Mathf.Clamp(appliedMultiplier, 0.05f, 1f));
            _hazardSlowTimer = Mathf.Max(_hazardSlowTimer, duration);
        }

        public int AddCoins(int amount = 1)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var previousCoins = _currentCoins;
            _currentCoins = Mathf.Clamp(_currentCoins + amount, 0, MaxCoins);
            return _currentCoins - previousCoins;
        }

        public bool TryLoseCoinAndDrop(Vector3 dropPosition, Vector3 launchDirection)
        {
            if (_currentCoins <= 0)
            {
                return false;
            }

            _currentCoins = Mathf.Max(0, _currentCoins - 1);

            var normalizedDirection = launchDirection.sqrMagnitude > 0.001f
                ? launchDirection.normalized
                : (-transform.forward + Vector3.up * 0.35f).normalized;

            CoinPickup.CreateDroppedCoin(
                dropPosition,
                Quaternion.identity,
                1,
                coinDropLifetime,
                normalizedDirection,
                coinDropImpulse);

            return true;
        }

        public void ApplyInvincibility(float duration)
        {
            _invincibilityTimer = Mathf.Max(_invincibilityTimer, duration);
        }

        public void ResetKart(Vector3 position, Quaternion rotation)
        {
            if (_rigidbody == null)
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _boostTimer = 0f;
            _boostMultiplier = 1f;
            _stunTimer = 0f;
            _hazardSlowTimer = 0f;
            _hazardSlowMultiplier = 1f;
            _invincibilityTimer = 0f;
            _currentCoins = 0;
            SetInput(0f, 0f, 0f);
            Physics.SyncTransforms();
        }

        private void TickStatusEffects(float deltaTime)
        {
            if (_boostTimer > 0f)
            {
                _boostTimer -= deltaTime;
                if (_boostTimer <= 0f)
                {
                    _boostMultiplier = 1f;
                }
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= deltaTime;
            }

            if (_hazardSlowTimer > 0f)
            {
                _hazardSlowTimer -= deltaTime;
                if (_hazardSlowTimer <= 0f)
                {
                    _hazardSlowTimer = 0f;
                    _hazardSlowMultiplier = 1f;
                }
            }

            if (_invincibilityTimer > 0f)
            {
                _invincibilityTimer -= deltaTime;
            }
        }

        private void UpdateGroundedState()
        {
            var probeOrigin = transform.position + groundProbeOffset;
            _isGrounded = Physics.Raycast(probeOrigin, Vector3.down, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
        }

        private void ApplyMotorForce()
        {
            var canDrive = _controlEnabled;
            var forwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            var desiredAcceleration = canDrive ? _accelerationInput : 0f;
            var driveForce = desiredAcceleration >= 0f
                ? desiredAcceleration * accelerationForce
                : desiredAcceleration * reverseAccelerationForce;

            var movementSpeedMultiplier = GetMovementSpeedMultiplier();
            var airControlFactor = _isGrounded ? 1f : 0.45f;

            _rigidbody.AddForce(transform.forward * driveForce * movementSpeedMultiplier * airControlFactor, ForceMode.Acceleration);

            if (_brakeInput > 0f && _controlEnabled)
            {
                var brakeVector = -transform.forward * forwardSpeed * brakingForce * _brakeInput;
                _rigidbody.AddForce(brakeVector, ForceMode.Acceleration);
            }

            if (Mathf.Abs(desiredAcceleration) < 0.05f)
            {
                _rigidbody.AddForce(-_rigidbody.linearVelocity * coastingDrag, ForceMode.Acceleration);
            }
        }

        private void ApplySteering()
        {
            var canSteer = _controlEnabled;
            if (!canSteer)
            {
                return;
            }

            var signedSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            var speedFactor = Mathf.Clamp01(Mathf.Abs(signedSpeed) / Mathf.Max(0.01f, maxSpeed));
            var steeringMultiplier = _isGrounded ? 1f : airSteeringMultiplier;
            var reverseSteerMultiplier = signedSpeed >= -0.1f ? 1f : -0.7f;
            var turnDegrees = _steeringInput * steeringStrength * Mathf.Lerp(0.35f, 1f, speedFactor) * steeringMultiplier * reverseSteerMultiplier;

            _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, turnDegrees * Time.fixedDeltaTime, 0f));
        }

        private void ApplyLateralGrip()
        {
            var forwardVelocity = transform.forward * Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            var lateralVelocity = transform.right * Vector3.Dot(_rigidbody.linearVelocity, transform.right);
            var verticalVelocity = Vector3.Project(_rigidbody.linearVelocity, Vector3.up);
            var gripFactor = _isGrounded ? lateralGrip : Mathf.Lerp(lateralGrip, 1f, 0.55f);

            _rigidbody.linearVelocity = forwardVelocity + lateralVelocity * gripFactor + verticalVelocity;
        }

        private void ApplyDownforce()
        {
            if (_isGrounded)
            {
                _rigidbody.AddForce(Vector3.down * downforce, ForceMode.Acceleration);
            }
        }

        private void LimitTopSpeed()
        {
            var planarVelocity = new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);
            var currentTopSpeed = maxSpeed * GetMovementSpeedMultiplier();

            if (planarVelocity.magnitude <= currentTopSpeed)
            {
                return;
            }

            var limitedPlanarVelocity = planarVelocity.normalized * currentTopSpeed;
            _rigidbody.linearVelocity = new Vector3(limitedPlanarVelocity.x, _rigidbody.linearVelocity.y, limitedPlanarVelocity.z);
        }

        private float GetMovementSpeedMultiplier()
        {
            var boostFactor = _boostTimer > 0f ? _boostMultiplier : 1f;
            var coinFactor = CoinSpeedMultiplier;
            var hazardFactor = HazardSlowMultiplier;
            return boostFactor * coinFactor * hazardFactor;
        }

        private void RegisterKartCollisionIgnores()
        {
            if (ActiveKarts.Contains(this))
            {
                return;
            }

            for (var index = 0; index < ActiveKarts.Count; index++)
            {
                var otherKart = ActiveKarts[index];
                if (otherKart == null)
                {
                    continue;
                }

                SyncKartCollisionIgnore(otherKart, true);
            }

            ActiveKarts.Add(this);
        }

        private void UnregisterKartCollisionIgnores()
        {
            ActiveKarts.Remove(this);

            for (var index = 0; index < ActiveKarts.Count; index++)
            {
                var otherKart = ActiveKarts[index];
                if (otherKart == null)
                {
                    continue;
                }

                SyncKartCollisionIgnore(otherKart, false);
            }
        }

        private void SyncKartCollisionIgnore(KartController otherKart, bool ignore)
        {
            if (otherKart == null || otherKart == this)
            {
                return;
            }

            if (!ignoreCollisionsWithOtherKarts || !otherKart.ignoreCollisionsWithOtherKarts)
            {
                return;
            }

            _kartColliders ??= GetComponentsInChildren<Collider>(true);
            otherKart._kartColliders ??= otherKart.GetComponentsInChildren<Collider>(true);

            for (var thisIndex = 0; thisIndex < _kartColliders.Length; thisIndex++)
            {
                var thisCollider = _kartColliders[thisIndex];
                if (thisCollider == null)
                {
                    continue;
                }

                for (var otherIndex = 0; otherIndex < otherKart._kartColliders.Length; otherIndex++)
                {
                    var otherCollider = otherKart._kartColliders[otherIndex];
                    if (otherCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(thisCollider, otherCollider, ignore);
                }
            }
        }
    }
}
