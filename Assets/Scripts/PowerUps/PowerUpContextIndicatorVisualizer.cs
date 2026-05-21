using System.Collections.Generic;
using KartGame.AI.Reinforcement;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    public class PowerUpContextIndicatorVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private KartPowerUpAgent powerUpAgent;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateIndicators = true;

        [Header("Indicators")]
        [SerializeField] private Renderer pointReadyIndicator;
        [SerializeField] private Renderer enemiesAheadIndicator;
        [SerializeField] private Renderer enemiesBehindIndicator;
        [SerializeField] private Renderer nearbyHazardsIndicator;

        [Header("Indicator Colors")]
        [SerializeField] private Color pointReadyColor = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] private Color enemiesAheadColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color enemiesBehindColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color nearbyHazardsColor = Color.white;

        [Header("Fallback Sensing")]
        [SerializeField] private float fallbackNearbyEnemyDistance = 14f;
        [SerializeField] private float fallbackAheadSenseHalfAngle = 70f;
        [SerializeField] private float fallbackBehindSenseHalfAngle = 70f;
        [SerializeField] private float fallbackNearbyHazardRadius = 8f;

        private MaterialPropertyBlock _propertyBlock;

        public void EnsureIndicatorObjects()
        {
            if (!autoCreateIndicators)
            {
                return;
            }

            pointReadyIndicator = EnsureIndicatorRenderer(
                pointReadyIndicator,
                "PowerUpPointIndicator",
                new Vector3(0f, 1.8f, 0f),
                new Vector3(0.25f, 0.18f, 0.25f));

            enemiesAheadIndicator = EnsureIndicatorRenderer(
                enemiesAheadIndicator,
                "PowerUpAheadIndicator",
                new Vector3(0f, 0.65f, 1.8f),
                new Vector3(0.2f, 0.14f, 0.2f));

            enemiesBehindIndicator = EnsureIndicatorRenderer(
                enemiesBehindIndicator,
                "PowerUpBehindIndicator",
                new Vector3(0f, 0.65f, -1.8f),
                new Vector3(0.2f, 0.14f, 0.2f));

            nearbyHazardsIndicator = EnsureIndicatorRenderer(
                nearbyHazardsIndicator,
                "PowerUpHazardIndicator",
                new Vector3(0f, -0.55f, 0f),
                new Vector3(0.32f, 0.05f, 0.32f));
        }

        private void Awake()
        {
            CacheReferences();
            EnsureIndicatorObjects();
            RefreshIndicators();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureIndicatorObjects();
            RefreshIndicators();
        }

        private void OnValidate()
        {
            CacheReferences();
            EnsureIndicatorObjects();
            RefreshIndicators();
        }

        private void Update()
        {
            RefreshIndicators();
        }

        private void CacheReferences()
        {
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            powerUpAgent ??= GetComponent<KartPowerUpAgent>();
            powerUpAgent ??= GetComponentInParent<KartPowerUpAgent>();
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void RefreshIndicators()
        {
            CacheReferences();

            var hasPowerUpPoint = powerUpController != null && powerUpController.AvailablePowerUpPoints > 0;
            var enemiesAhead = powerUpAgent != null ? powerUpAgent.debugEnemiesAheadClose > 0 : CountEnemiesAhead() > 0;
            var enemiesBehind = powerUpAgent != null ? powerUpAgent.debugEnemiesBehindClose > 0 : CountEnemiesBehind() > 0;
            var nearbyHazards = CountNearbyHazards() > 0;

            SetIndicatorState(pointReadyIndicator, hasPowerUpPoint, pointReadyColor);
            SetIndicatorState(enemiesAheadIndicator, enemiesAhead, enemiesAheadColor);
            SetIndicatorState(enemiesBehindIndicator, enemiesBehind, enemiesBehindColor);
            SetIndicatorState(nearbyHazardsIndicator, nearbyHazards, nearbyHazardsColor);
        }

        private Renderer EnsureIndicatorRenderer(Renderer existingRenderer, string childName, Vector3 localPosition, Vector3 localScale)
        {
            var indicatorTransform = existingRenderer != null ? existingRenderer.transform : transform.Find(childName);
            GameObject indicatorObject;
            var wasCreatedNow = false;
            if (indicatorTransform == null)
            {
                indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicatorObject.name = childName;
                indicatorObject.transform.SetParent(transform, false);
                wasCreatedNow = true;
            }
            else
            {
                indicatorObject = indicatorTransform.gameObject;
            }

            if (wasCreatedNow)
            {
                indicatorObject.transform.localPosition = localPosition;
                indicatorObject.transform.localRotation = Quaternion.identity;
                indicatorObject.transform.localScale = localScale;
            }

            var collider = indicatorObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = indicatorObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            return renderer;
        }

        private void SetIndicatorState(Renderer indicatorRenderer, bool visible, Color color)
        {
            if (indicatorRenderer == null)
            {
                return;
            }

            indicatorRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            var sharedMaterial = indicatorRenderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                return;
            }

            _propertyBlock.Clear();

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                _propertyBlock.SetColor("_BaseColor", color);
            }
            else if (sharedMaterial.HasProperty("_Color"))
            {
                _propertyBlock.SetColor("_Color", color);
            }
            else
            {
                return;
            }

            indicatorRenderer.SetPropertyBlock(_propertyBlock);
        }

        private int CountEnemiesAhead()
        {
            return CountEnemies(true);
        }

        private int CountEnemiesBehind()
        {
            return CountEnemies(false);
        }

        private int CountEnemies(bool ahead)
        {
            if (kartController == null)
            {
                return 0;
            }

            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);
            var referenceTransform = kartController.transform;
            var enemyCount = 0;

            for (var index = 0; index < trackers.Length; index++)
            {
                var otherTracker = trackers[index];
                if (otherTracker == null || otherTracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = referenceTransform.InverseTransformPoint(otherTracker.transform.position);
                var planarDistance = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (planarDistance > fallbackNearbyEnemyDistance)
                {
                    continue;
                }

                if (ahead)
                {
                    if (localTarget.z <= 0f)
                    {
                        continue;
                    }

                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= fallbackAheadSenseHalfAngle)
                    {
                        enemyCount++;
                    }
                }
                else
                {
                    if (localTarget.z >= 0f)
                    {
                        continue;
                    }

                    var behindAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg);
                    if (behindAngle <= fallbackBehindSenseHalfAngle)
                    {
                        enemyCount++;
                    }
                }
            }

            return enemyCount;
        }

        private int CountNearbyHazards()
        {
            if (kartController == null)
            {
                return 0;
            }

            var referencePosition = kartController.transform.position;
            var hits = Physics.OverlapSphere(referencePosition, fallbackNearbyHazardRadius, ~0, QueryTriggerInteraction.Collide);
            var hazards = new HashSet<PowerUpHazardBase>();

            for (var index = 0; index < hits.Length; index++)
            {
                var hazard = hits[index] != null ? hits[index].GetComponentInParent<PowerUpHazardBase>() : null;
                if (hazard == null || hazard.OwnerKart == kartController)
                {
                    continue;
                }

                hazards.Add(hazard);
            }

            return hazards.Count;
        }
    }
}
