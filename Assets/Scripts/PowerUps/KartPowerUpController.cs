using System;
using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    public class KartPowerUpController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;

        [Header("Inventory")]
        [SerializeField] private int availablePowerUpPoints;
        [SerializeField] private bool allowPowerUpPointAccumulation = true;
        [SerializeField] private int maxAccumulatedPowerUpPoints = 3;
        [SerializeField] private PowerUpType manualPowerUpType = PowerUpType.Mushroom;
        [SerializeField] private bool logPowerUpUsage = true;

        [Header("Bot Checkpoint Point Gain")]
        [SerializeField] private bool awardPointEveryXCheckpointsForBots;
        [SerializeField] private int checkpointsPerPowerUpPoint = 1;
        [SerializeField] private bool logCheckpointPointGain;

        [Header("Spawn Anchors")]
        [SerializeField] private Transform forwardLaunchPoint;
        [SerializeField] private Transform rearDropPoint;
        [SerializeField] private float forwardLaunchDistance = 1.5f;
        [SerializeField] private float rearDropDistance = 1.35f;
        [SerializeField] private float spawnHeightOffset = 0.35f;

        [Header("Use Limits")]
        [SerializeField] private bool onlyOneActiveDeployableAtATime = true;
        [SerializeField] private bool applySingleDeployableLimitToPlayer;
        [SerializeField] private bool applySingleDeployableLimitToBots = true;
        [SerializeField] private bool limitAnyPowerUpUseRate = true;
        [SerializeField] private float minimumSecondsBetweenAnyPowerUpUses = 0.2f;

        [Header("Projectile / Hazard Prefabs")]
        [SerializeField] private GameObject bananaPrefab;
        [SerializeField] private GameObject shellPrefab;

        [Header("Banana")]
        [SerializeField] private float bananaLifetime = 15f;
        [SerializeField] private float bananaStunDuration = 2.5f;

        [Header("Shell")]
        [SerializeField] private float shellLifetime = 8f;
        [SerializeField] private float shellSpeed = 14f;
        [SerializeField] private float shellTurnRateDegrees = 220f;
        [SerializeField] private float shellHitRadius = 0.45f;
        [SerializeField] private float shellStunDuration = 2.25f;
        [SerializeField] private float shellTargetDistance = 24f;
        [SerializeField] private float shellTargetMaxAngle = 70f;
        [SerializeField] private bool shellHomesToTargetAhead = true;

        [Header("Mushroom")]
        [SerializeField] private float mushroomBoostMultiplier = 1.6f;
        [SerializeField] private float mushroomBoostDuration = 1.4f;

        [Header("Star")]
        [SerializeField] private float starInvincibilityDuration = 4f;

        [Header("Visual Feedback")]
        [SerializeField] private bool colorKartByLastUsedPowerUp = true;
        [SerializeField] private Renderer[] powerUpColorRenderers;
        [SerializeField] private Color bananaPowerUpColor = new Color(1f, 0.9f, 0.15f, 1f);
        [SerializeField] private Color shellPowerUpColor = new Color(0.2f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color mushroomPowerUpColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color starPowerUpColor = Color.white;

        [Header("Runtime Debug")]
        public int debugAvailablePowerUpPoints;
        public int debugCheckpointsSinceLastPoint;
        public bool debugHasActiveDeployable;
        public float debugSecondsUntilNextAllowedUse;

        public int AvailablePowerUpPoints => availablePowerUpPoints;
        public static event System.Action<KartPowerUpController, PowerUpType> AnyPowerUpUsed;
        public static event System.Action<KartPowerUpController, PowerUpType, KartController> AnyPowerUpHit;
        public event System.Action<int> PowerUpPointsAdded;
        public event System.Action<KartPowerUpController, PowerUpType> PowerUpUsed;
        public event System.Action<PowerUpType> PowerUpUseFailed;
        public event System.Action<PowerUpType, KartController> PowerUpHit;

        private CheckpointTracker _subscribedCheckpointTracker;
        private int _checkpointsSinceLastPoint;
        private PowerUpHazardBase _activeDeployableHazard;
        private float _nextAllowedUseTime;
        private MaterialPropertyBlock _powerUpColorPropertyBlock;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            CacheReferences();
            SubscribeToCheckpointEvents();
            EnforcePowerUpUseCooldownFloor();
            SyncDebugState();
        }

        private void OnEnable()
        {
            CacheReferences();
            SubscribeToCheckpointEvents();
            EnforcePowerUpUseCooldownFloor();
            SyncDebugState();
        }

        private void OnDisable()
        {
            UnsubscribeFromCheckpointEvents();

            if (_activeDeployableHazard != null)
            {
                _activeDeployableHazard.Disposed -= HandleActiveDeployableDisposed;
            }
        }

        private void OnValidate()
        {
            CacheReferences();
            checkpointsPerPowerUpPoint = Mathf.Max(1, checkpointsPerPowerUpPoint);
            maxAccumulatedPowerUpPoints = Mathf.Max(1, maxAccumulatedPowerUpPoints);
            EnforcePowerUpUseCooldownFloor();
            availablePowerUpPoints = allowPowerUpPointAccumulation
                ? Mathf.Clamp(availablePowerUpPoints, 0, maxAccumulatedPowerUpPoints)
                : Mathf.Clamp(availablePowerUpPoints, 0, 1);
        }

        public void AddPowerUpPoint(int amount = 1)
        {
            var pointsToAdd = Mathf.Max(1, amount);
            var previousPoints = availablePowerUpPoints;

            if (allowPowerUpPointAccumulation)
            {
                availablePowerUpPoints = Mathf.Clamp(
                    availablePowerUpPoints + pointsToAdd,
                    0,
                    Mathf.Max(1, maxAccumulatedPowerUpPoints));
            }
            else
            {
                availablePowerUpPoints = availablePowerUpPoints > 0 ? 1 : Mathf.Min(1, pointsToAdd);
            }

            var grantedPoints = Mathf.Max(0, availablePowerUpPoints - previousPoints);
            SyncDebugState();
            if (grantedPoints > 0)
            {
                PowerUpPointsAdded?.Invoke(grantedPoints);
            }
        }

        public bool UseStoredPowerUp()
        {
            return UsePowerUp(manualPowerUpType);
        }

        public bool UseBananaPowerUp()
        {
            return UsePowerUp(PowerUpType.Banana);
        }

        public bool UseShellPowerUp()
        {
            return UsePowerUp(PowerUpType.Shell);
        }

        public bool UseMushroomPowerUp()
        {
            return UsePowerUp(PowerUpType.Mushroom);
        }

        public bool UseStarPowerUp()
        {
            return UsePowerUp(PowerUpType.Star);
        }

        public bool UsePowerUp(PowerUpType powerUpType)
        {
            return UsePowerUp(powerUpType, null);
        }

        public bool UsePowerUp(PowerUpType powerUpType, CheckpointTracker preferredTarget)
        {
            CacheReferences();
            if (kartController == null || availablePowerUpPoints <= 0)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning($"POWER UP FALLIDO: {powerUpType} | motivo=sin kart o sin puntos", this);
                }

                PowerUpUseFailed?.Invoke(powerUpType);
                return false;
            }

            if (limitAnyPowerUpUseRate && Time.time < _nextAllowedUseTime)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning($"POWER UP FALLIDO: {powerUpType} | motivo=cooldown {Mathf.Max(0f, _nextAllowedUseTime - Time.time):0.00}s", this);
                }

                PowerUpUseFailed?.Invoke(powerUpType);
                return false;
            }

            var used = powerUpType switch
            {
                PowerUpType.Banana => TryUseBanana(),
                PowerUpType.Shell => TryUseShell(preferredTarget),
                PowerUpType.Mushroom => TryUseMushroom(),
                PowerUpType.Star => TryUseStar(),
                _ => false
            };

            if (!used)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning($"POWER UP FALLIDO: {powerUpType} | motivo=no pudo ejecutarse", this);
                }

                PowerUpUseFailed?.Invoke(powerUpType);
                return false;
            }

            availablePowerUpPoints = Mathf.Max(0, availablePowerUpPoints - 1);
            _nextAllowedUseTime = limitAnyPowerUpUseRate
                ? Time.time + Mathf.Max(0.2f, minimumSecondsBetweenAnyPowerUpUses)
                : 0f;

            ApplyLastUsedPowerUpColor(powerUpType);
            SyncDebugState();

            if (logPowerUpUsage)
            {
                Debug.Log($"POWER UP USADO: {powerUpType} | puntos restantes={availablePowerUpPoints}", this);
            }

            PowerUpUsed?.Invoke(this, powerUpType);
            AnyPowerUpUsed?.Invoke(this, powerUpType);
            return true;
        }

        private bool TryUseBanana()
        {
            if (HasActiveDeployable())
            {
                if (logPowerUpUsage)
                {
                    var activeName = _activeDeployableHazard != null ? _activeDeployableHazard.name : "Unknown";
                    Debug.LogWarning($"POWER UP FALLIDO: Banana | motivo=deployable activo '{activeName}'", this);
                }

                return false;
            }

            var launchTransform = GetLaunchReferenceTransform();
            var spawnPosition = rearDropPoint != null
                ? rearDropPoint.position
                : launchTransform.position - launchTransform.forward * rearDropDistance + Vector3.up * spawnHeightOffset;

            var bananaObject = bananaPrefab != null
                ? Instantiate(bananaPrefab, spawnPosition, Quaternion.identity)
                : CreateFallbackBanana(spawnPosition);

            if (bananaObject == null)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning("POWER UP FALLIDO: Banana | motivo=no se pudo crear el objeto banana", this);
                }

                return false;
            }

            var bananaHazard = bananaObject.GetComponent<BananaHazard>();
            if (bananaHazard == null)
            {
                bananaHazard = bananaObject.AddComponent<BananaHazard>();
            }

            if (bananaHazard == null)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning("POWER UP FALLIDO: Banana | motivo=no se pudo obtener/crear BananaHazard", this);
                }

                Destroy(bananaObject);
                return false;
            }

            EnsureKinematicTriggerCollider(bananaObject, 0.6f);
            bananaHazard.Initialize(this, kartController, PowerUpType.Banana, bananaStunDuration, bananaLifetime);
            RegisterActiveDeployable(bananaHazard);
            ConfigureRuntimeHazardObject(bananaObject);
            SyncDebugState();
            return true;
        }

        private bool TryUseShell(CheckpointTracker preferredTarget)
        {
            if (HasActiveDeployable())
            {
                if (logPowerUpUsage)
                {
                    var activeName = _activeDeployableHazard != null ? _activeDeployableHazard.name : "Unknown";
                    Debug.LogWarning($"POWER UP FALLIDO: Shell | motivo=deployable activo '{activeName}'", this);
                }

                return false;
            }

            var launchTransform = GetLaunchReferenceTransform();
            var spawnPosition = forwardLaunchPoint != null
                ? forwardLaunchPoint.position
                : launchTransform.position + launchTransform.forward * forwardLaunchDistance + Vector3.up * spawnHeightOffset;
            var spawnRotation = Quaternion.LookRotation(launchTransform.forward, Vector3.up);

            var shellObject = shellPrefab != null
                ? Instantiate(shellPrefab, spawnPosition, spawnRotation)
                : CreateFallbackShell(spawnPosition, spawnRotation);

            if (shellObject == null)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning("POWER UP FALLIDO: Shell | motivo=no se pudo crear el objeto shell", this);
                }

                return false;
            }

            var shell = shellObject.GetComponent<HomingShellProjectile>();
            if (shell == null)
            {
                shell = shellObject.AddComponent<HomingShellProjectile>();
            }

            if (shell == null)
            {
                if (logPowerUpUsage)
                {
                    Debug.LogWarning("POWER UP FALLIDO: Shell | motivo=no se pudo obtener/crear HomingShellProjectile", this);
                }

                Destroy(shellObject);
                return false;
            }

            EnsureKinematicTriggerCollider(shellObject, shellHitRadius);
            shell.Initialize(
                this,
                kartController,
                PowerUpType.Shell,
                shellHomesToTargetAhead ? (preferredTarget != null ? preferredTarget : FindBestShellTarget()) : null,
                shellStunDuration,
                shellLifetime,
                shellSpeed,
                shellTurnRateDegrees,
                shellHitRadius,
                shellHomesToTargetAhead);
            RegisterActiveDeployable(shell);
            ConfigureRuntimeHazardObject(shellObject);
            SyncDebugState();
            return true;
        }

        private bool TryUseMushroom()
        {
            kartController.ApplyBoost(mushroomBoostMultiplier, mushroomBoostDuration);
            return true;
        }

        private bool TryUseStar()
        {
            kartController.ApplyInvincibility(starInvincibilityDuration);
            return true;
        }

        private CheckpointTracker FindBestShellTarget()
        {
            var referenceTransform = GetLaunchReferenceTransform();
            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            CheckpointTracker bestTarget = null;
            var bestDistance = float.MaxValue;

            for (var index = 0; index < trackers.Length; index++)
            {
                var tracker = trackers[index];
                if (tracker == null || tracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = referenceTransform.InverseTransformPoint(tracker.transform.position);
                if (localTarget.z <= 0f)
                {
                    continue;
                }

                var distance = localTarget.magnitude;
                if (distance > shellTargetDistance)
                {
                    continue;
                }

                var angle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                if (angle > shellTargetMaxAngle)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = tracker;
                }
            }

            return bestTarget;
        }

        private void SyncDebugState()
        {
            if (_activeDeployableHazard == null)
            {
                _activeDeployableHazard = null;
            }

            debugAvailablePowerUpPoints = availablePowerUpPoints;
            debugCheckpointsSinceLastPoint = _checkpointsSinceLastPoint;
            debugHasActiveDeployable = HasActiveDeployable();
            debugSecondsUntilNextAllowedUse = Mathf.Max(0f, _nextAllowedUseTime - Time.time);
        }

        private void RegisterActiveDeployable(PowerUpHazardBase deployableHazard)
        {
            if (_activeDeployableHazard != null)
            {
                _activeDeployableHazard.Disposed -= HandleActiveDeployableDisposed;
            }

            _activeDeployableHazard = deployableHazard;

            if (_activeDeployableHazard != null)
            {
                _activeDeployableHazard.Disposed += HandleActiveDeployableDisposed;
            }
        }

        private void HandleActiveDeployableDisposed(PowerUpHazardBase deployableHazard)
        {
            if (_activeDeployableHazard != deployableHazard)
            {
                return;
            }

            _activeDeployableHazard.Disposed -= HandleActiveDeployableDisposed;
            _activeDeployableHazard = null;
            SyncDebugState();
        }

        private void EnforcePowerUpUseCooldownFloor()
        {
            if (limitAnyPowerUpUseRate)
            {
                minimumSecondsBetweenAnyPowerUpUses = Mathf.Max(0.2f, minimumSecondsBetweenAnyPowerUpUses);
            }
        }

        public void NotifyPowerUpHit(PowerUpType powerUpType, KartController targetKart)
        {
            PowerUpHit?.Invoke(powerUpType, targetKart);
            AnyPowerUpHit?.Invoke(this, powerUpType, targetKart);
        }

        private void HandleCheckpointPassed(CheckpointTracker tracker, Checkpoint checkpoint)
        {
            if (!awardPointEveryXCheckpointsForBots || tracker == null || checkpoint == null)
            {
                return;
            }

            if (tracker != checkpointTracker || tracker.IsPlayer)
            {
                return;
            }

            var checkpointsNeeded = Mathf.Max(1, checkpointsPerPowerUpPoint);
            _checkpointsSinceLastPoint++;

            if (_checkpointsSinceLastPoint < checkpointsNeeded)
            {
                SyncDebugState();
                return;
            }

            _checkpointsSinceLastPoint -= checkpointsNeeded;
            AddPowerUpPoint(1);

            if (logCheckpointPointGain)
            {
                Debug.Log($"POWER UP POINT POR CHECKPOINTS: +1 cada {checkpointsNeeded} checkpoints", this);
            }

            SyncDebugState();
        }

        private void CacheReferences()
        {
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            _powerUpColorPropertyBlock ??= new MaterialPropertyBlock();

            if (powerUpColorRenderers == null || powerUpColorRenderers.Length == 0)
            {
                powerUpColorRenderers = kartController != null
                    ? kartController.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            }
        }

        private void SubscribeToCheckpointEvents()
        {
            if (_subscribedCheckpointTracker == checkpointTracker)
            {
                return;
            }

            UnsubscribeFromCheckpointEvents();

            if (checkpointTracker == null)
            {
                return;
            }

            checkpointTracker.CheckpointPassed += HandleCheckpointPassed;
            _subscribedCheckpointTracker = checkpointTracker;
        }

        private void UnsubscribeFromCheckpointEvents()
        {
            if (_subscribedCheckpointTracker == null)
            {
                return;
            }

            _subscribedCheckpointTracker.CheckpointPassed -= HandleCheckpointPassed;
            _subscribedCheckpointTracker = null;
        }

        private Transform GetLaunchReferenceTransform()
        {
            return kartController != null ? kartController.transform : transform;
        }

        private bool HasActiveDeployable()
        {
            if (!onlyOneActiveDeployableAtATime)
            {
                return false;
            }

            if (checkpointTracker != null)
            {
                if (checkpointTracker.IsPlayer)
                {
                    return applySingleDeployableLimitToPlayer && _activeDeployableHazard != null;
                }

                return applySingleDeployableLimitToBots && _activeDeployableHazard != null;
            }

            return _activeDeployableHazard != null;
        }

        private static void EnsureKinematicTriggerCollider(GameObject targetObject, float sphereRadius)
        {
            if (targetObject == null)
            {
                return;
            }

            var collider = targetObject.GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCollider = targetObject.AddComponent<SphereCollider>();
                sphereCollider.radius = Mathf.Max(0.1f, sphereRadius);
                collider = sphereCollider;
            }

            if (collider == null)
            {
                return;
            }

            collider.isTrigger = true;

            var rigidbody = targetObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = targetObject.AddComponent<Rigidbody>();
            }

            if (rigidbody == null)
            {
                return;
            }

            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private static GameObject CreateFallbackBanana(Vector3 position)
        {
            var root = new GameObject("BananaHazard");
            root.transform.position = position;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.6f, 0.12f, 0.6f);
            ApplyFallbackColor(visual, new Color(1f, 0.9f, 0.15f, 1f));

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                UnityEngine.Object.Destroy(visualCollider);
            }

            return root;
        }

        private static GameObject CreateFallbackShell(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("HomingShell");
            root.transform.SetPositionAndRotation(position, rotation);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.55f;
            ApplyFallbackColor(visual, new Color(0.65f, 0.75f, 0.9f, 1f));

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                UnityEngine.Object.Destroy(visualCollider);
            }

            return root;
        }

        private static void ApplyFallbackColor(GameObject targetObject, Color color)
        {
            if (targetObject == null)
            {
                return;
            }

            var renderer = targetObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.material.color = color;
        }

        private static void ConfigureRuntimeHazardObject(GameObject targetObject)
        {
            if (targetObject == null || !Application.isPlaying)
            {
                return;
            }

            targetObject.hideFlags = HideFlags.HideInHierarchy;
        }

        private void ApplyLastUsedPowerUpColor(PowerUpType powerUpType)
        {
            if (!colorKartByLastUsedPowerUp)
            {
                return;
            }

            CacheReferences();
            if (powerUpColorRenderers == null || powerUpColorRenderers.Length == 0 || _powerUpColorPropertyBlock == null)
            {
                return;
            }

            var targetColor = powerUpType switch
            {
                PowerUpType.Banana => bananaPowerUpColor,
                PowerUpType.Shell => shellPowerUpColor,
                PowerUpType.Mushroom => mushroomPowerUpColor,
                PowerUpType.Star => starPowerUpColor,
                _ => Color.white
            };

            foreach (var powerUpRenderer in powerUpColorRenderers)
            {
                if (powerUpRenderer == null)
                {
                    continue;
                }

                var sharedMaterial = powerUpRenderer.sharedMaterial;
                if (sharedMaterial == null)
                {
                    continue;
                }

                _powerUpColorPropertyBlock.Clear();

                if (sharedMaterial.HasProperty(BaseColorPropertyId))
                {
                    _powerUpColorPropertyBlock.SetColor(BaseColorPropertyId, targetColor);
                }
                else if (sharedMaterial.HasProperty(ColorPropertyId))
                {
                    _powerUpColorPropertyBlock.SetColor(ColorPropertyId, targetColor);
                }
                else
                {
                    continue;
                }

                powerUpRenderer.SetPropertyBlock(_powerUpColorPropertyBlock);
            }
        }
    }
}
