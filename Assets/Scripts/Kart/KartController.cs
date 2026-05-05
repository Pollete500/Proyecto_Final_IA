using UnityEngine;

namespace KartGame.Kart
{
    /*
     * Script: KartController.cs
     * Purpose: Controls arcade kart movement using Rigidbody physics, including acceleration, steering, braking, boost, stun and reset support.
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

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 groundProbeOffset = new Vector3(0f, 0.35f, 0f);
        [SerializeField] private float groundProbeDistance = 0.55f;
        [SerializeField] private float downforce = 25f;
        [SerializeField] private float airSteeringMultiplier = 0.35f;

        [Header("Physics")]
        [SerializeField] private float mass = 140f;
        [SerializeField] private float angularDrag = 3f;

        private Rigidbody _rigidbody;
        private float _accelerationInput;
        private float _steeringInput;
        private float _brakeInput;
        private float _boostTimer;
        private float _boostMultiplier = 1f;
        private float _stunTimer;
        private float _invincibilityTimer;
        private bool _isGrounded;
        private bool _controlEnabled = true;

        public float MaxSpeed => maxSpeed;
        public bool IsGrounded => _isGrounded;
        public bool IsInvincible => _invincibilityTimer > 0f;
        public bool IsControlEnabled => _controlEnabled;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.mass = mass;
            _rigidbody.angularDamping = angularDrag;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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

        public void SetControlEnabled(bool isEnabled)
        {
            _controlEnabled = isEnabled;

            if (!isEnabled)
            {
                SetInput(0f, 0f, 1f);
            }
        }

        public void ApplyBoost(float multiplier, float duration)
        {
            _boostMultiplier = Mathf.Max(_boostMultiplier, Mathf.Max(1f, multiplier));
            _boostTimer = Mathf.Max(_boostTimer, duration);
        }

        public void ApplyStun(float duration)
        {
            _stunTimer = Mathf.Max(_stunTimer, duration);
            _rigidbody.linearVelocity *= 0.35f;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        public void ApplyInvincibility(float duration)
        {
            _invincibilityTimer = Mathf.Max(_invincibilityTimer, duration);
        }

        public void ResetKart(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _boostTimer = 0f;
            _boostMultiplier = 1f;
            _stunTimer = 0f;
            SetInput(0f, 0f, 0f);
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
            var canDrive = _controlEnabled && _stunTimer <= 0f;
            var forwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);
            var desiredAcceleration = canDrive ? _accelerationInput : 0f;
            var driveForce = desiredAcceleration >= 0f
                ? desiredAcceleration * accelerationForce
                : desiredAcceleration * reverseAccelerationForce;

            var boostFactor = _boostTimer > 0f ? _boostMultiplier : 1f;
            var airControlFactor = _isGrounded ? 1f : 0.45f;

            _rigidbody.AddForce(transform.forward * driveForce * boostFactor * airControlFactor, ForceMode.Acceleration);

            if (canDrive && _brakeInput > 0f)
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
            var canSteer = _controlEnabled && _stunTimer <= 0f;
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
            var currentTopSpeed = maxSpeed * (_boostTimer > 0f ? _boostMultiplier : 1f);

            if (planarVelocity.magnitude <= currentTopSpeed)
            {
                return;
            }

            var limitedPlanarVelocity = planarVelocity.normalized * currentTopSpeed;
            _rigidbody.linearVelocity = new Vector3(limitedPlanarVelocity.x, _rigidbody.linearVelocity.y, limitedPlanarVelocity.z);
        }
    }
}
