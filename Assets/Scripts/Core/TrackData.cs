using System;
using System.Collections.Generic;
using System.IO;
using KartGame.AI.Reinforcement;
using KartGame.Kart;
using KartGame.PowerUps;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartGame.Core
{
    /*
     * Script: TrackData.cs
     * Purpose: Stores track references such as checkpoints, spawn points and recovery points for a race scene.
     * Attach To: TrackRoot GameObject.
     * Required Components: None.
     * Dependencies: Checkpoint, RaceManager, CheckpointTracker.
     * Inspector Setup: Place child containers named Checkpoints, SpawnPoints, PowerUpBoxes and RespawnPoints under TrackRoot, then run Sync Child Collections.
     */
    [DefaultExecutionOrder(-1000)]
    public class TrackData : MonoBehaviour
    {
        [SerializeField] private Transform[] checkpoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] powerUpBoxes = Array.Empty<Transform>();
        [SerializeField] private Transform[] respawnPoints = Array.Empty<Transform>();
        [Header("Bot Spawns")]
        [SerializeField, Min(0)] private int noobBotCount = 2;
        [SerializeField, Min(0)] private int proBotCount = 2;
        [SerializeField, Min(0)] private int neutralBotCount = 2;
        [SerializeField] private GameObject noobBotPrefab;
        [SerializeField] private GameObject proBotPrefab;
        [SerializeField] private GameObject neutralBotPrefab;
        [SerializeField] private string spawnedBotsRootName = "SpawnedBots";
        [Space]
        [Header("Pickup Spawning")]
        [SerializeField] private bool enablePickupSpawning = true;
        [SerializeField] private bool spawnPickupsOnlyWhenRaceActive = true;
        [SerializeField, Min(0)] private int maxCoinsOnTrack = 6;
        [SerializeField, Min(0)] private int maxBananasOnTrack = 6;
        [SerializeField, Min(0.1f)] private float coinSpawnInterval = 8f;
        [SerializeField, Min(0.1f)] private float bananaSpawnInterval = 12f;
        [SerializeField] private bool spawnCoinsAsTrainingPickups;
        [SerializeField] private bool spawnBananasAsTrainingHazards;
        [SerializeField, Min(0f)] private float pickupSpawnHeight = 1.2f;
        [SerializeField, Min(1f)] private float pickupRaycastHeight = 25f;
        [SerializeField, Min(0f)] private float pickupSpawnLateralOffset = 3f;
        [SerializeField] private LayerMask pickupSurfaceLayerMask = ~0;
        [SerializeField] private GameObject coinPickupPrefab;
        [SerializeField] private GameObject bananaHazardPrefab;
        [SerializeField] private string spawnedCoinsRootName = "SpawnedCoins";
        [SerializeField] private string spawnedBananasRootName = "SpawnedBananas";
        [SerializeField] private bool logPickupSpawns;
        [Space]
        [SerializeField] private int lapsToWin = 3;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool closeCheckpointLoopGizmo = true;
        [SerializeField] private Color checkpointGizmoColor = new Color(1f, 0.78f, 0.2f, 0.9f);
        [SerializeField] private bool generatePowerUpUsageReport;
        [SerializeField] private bool generatePowerUpLearningReport;

        private readonly Dictionary<PowerUpType, int> _powerUpUseCounts = new Dictionary<PowerUpType, int>();
        private readonly Dictionary<PowerUpType, int> _powerUpFailedUseCounts = new Dictionary<PowerUpType, int>();
        private readonly Dictionary<PowerUpType, int> _powerUpHitCounts = new Dictionary<PowerUpType, int>();
        private readonly Dictionary<string, Dictionary<PowerUpType, int>> _perKartPowerUpUseCounts = new Dictionary<string, Dictionary<PowerUpType, int>>();
        private readonly Dictionary<string, Dictionary<PowerUpType, int>> _perKartPowerUpFailedUseCounts = new Dictionary<string, Dictionary<PowerUpType, int>>();
        private readonly Dictionary<string, Dictionary<PowerUpType, int>> _perKartPowerUpHitCounts = new Dictionary<string, Dictionary<PowerUpType, int>>();
        private readonly Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int> _decisionOutcomeCounts = new Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int>();
        private readonly Dictionary<string, Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int>> _perKartDecisionOutcomeCounts = new Dictionary<string, Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int>>();
        private readonly Dictionary<PowerUpType, int> _powerUpSuggestedCounts = new Dictionary<PowerUpType, int>();
        private readonly Dictionary<PowerUpType, int> _powerUpCorrectChoiceCounts = new Dictionary<PowerUpType, int>();
        private readonly Dictionary<string, Dictionary<PowerUpType, int>> _perKartPowerUpSuggestedCounts = new Dictionary<string, Dictionary<PowerUpType, int>>();
        private readonly Dictionary<string, Dictionary<PowerUpType, int>> _perKartPowerUpCorrectChoiceCounts = new Dictionary<string, Dictionary<PowerUpType, int>>();
        private bool _reportWritten;
        private float _nextCoinSpawnTime;
        private float _nextBananaSpawnTime;
        private Transform _spawnedCoinsRoot;
        private Transform _spawnedBananasRoot;
        private Transform _spawnedBotsRoot;
        private int _coinSpawnSerial;
        private int _bananaSpawnSerial;
        private bool _runtimeBotsSpawned;
        private bool ShouldGenerateAnyPowerUpReport => generatePowerUpUsageReport || generatePowerUpLearningReport;

        private void OnValidate()
        {
            lapsToWin = Mathf.Max(1, lapsToWin);
            noobBotCount = Mathf.Max(0, noobBotCount);
            proBotCount = Mathf.Max(0, proBotCount);
            neutralBotCount = Mathf.Max(0, neutralBotCount);
            maxCoinsOnTrack = Mathf.Max(0, maxCoinsOnTrack);
            maxBananasOnTrack = Mathf.Max(0, maxBananasOnTrack);
            coinSpawnInterval = Mathf.Max(0.1f, coinSpawnInterval);
            bananaSpawnInterval = Mathf.Max(0.1f, bananaSpawnInterval);
            pickupRaycastHeight = Mathf.Max(1f, pickupRaycastHeight);

#if UNITY_EDITOR
            if (coinPickupPrefab == null)
            {
                coinPickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CoinPickup.prefab");
            }

            if (bananaHazardPrefab == null)
            {
                bananaHazardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BananaHazard.prefab");
            }

            if (noobBotPrefab == null)
            {
                noobBotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Karts/Kart_Noob.prefab");
            }

            if (proBotPrefab == null)
            {
                proBotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Karts/Kart_Pro.prefab");
            }

            if (neutralBotPrefab == null)
            {
                neutralBotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Karts/Kart_Neutral.prefab");
            }
#endif
        }

        public int LapsToWin => Mathf.Max(1, lapsToWin);
        public int CheckpointCount => checkpoints?.Length ?? 0;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;
        public int NoobBotCount => Mathf.Max(0, noobBotCount);
        public int ProBotCount => Mathf.Max(0, proBotCount);
        public int NeutralBotCount => Mathf.Max(0, neutralBotCount);
        public int TotalBotCount => NoobBotCount + ProBotCount + NeutralBotCount;
        public GameObject NoobBotPrefab => noobBotPrefab;
        public GameObject ProBotPrefab => proBotPrefab;
        public GameObject NeutralBotPrefab => neutralBotPrefab;
        public int MaxCoinsOnTrack => Mathf.Max(0, maxCoinsOnTrack);
        public int MaxBananasOnTrack => Mathf.Max(0, maxBananasOnTrack);
        public float CoinSpawnInterval => Mathf.Max(0.1f, coinSpawnInterval);
        public float BananaSpawnInterval => Mathf.Max(0.1f, bananaSpawnInterval);
        public float PickupRaycastHeight => Mathf.Max(1f, pickupRaycastHeight);
        public Transform[] Checkpoints => checkpoints;
        public Transform[] SpawnPoints => spawnPoints;
        public Transform[] PowerUpBoxes => powerUpBoxes;
        public Transform[] RespawnPoints => respawnPoints;

        private void Awake()
        {
            if (Application.isPlaying && ShouldGenerateAnyPowerUpReport)
            {
                ResetPowerUpUsageTracking();
                KartPowerUpController.AnyPowerUpUsed += HandlePowerUpUsed;
                KartPowerUpController.AnyPowerUpHit += HandlePowerUpHit;
                KartPowerUpAgent.AnyDecisionEvaluated += HandlePowerUpDecisionEvaluated;
                KartPowerUpAgent.AnyPowerUpExecutionFailed += HandlePowerUpExecutionFailed;
            }

            if (Application.isPlaying)
            {
                SyncChildCollections();
                SpawnConfiguredBotsAtRuntime();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying || !enablePickupSpawning)
            {
                return;
            }

            SyncChildCollections();
            EnsurePickupSpawnRoots();
            ResetPickupSpawnTimers();
        }

        private void Update()
        {
            if (!Application.isPlaying || !enablePickupSpawning)
            {
                return;
            }

            if (spawnPickupsOnlyWhenRaceActive && RaceManager.Instance != null && !RaceManager.Instance.IsRaceActive())
            {
                return;
            }

            EnsurePickupSpawnRoots();

            if (maxCoinsOnTrack > 0 && Time.time >= _nextCoinSpawnTime)
            {
                TrySpawnCoinPickup();
                _nextCoinSpawnTime = Time.time + CoinSpawnInterval;
            }

            if (maxBananasOnTrack > 0 && Time.time >= _nextBananaSpawnTime)
            {
                TrySpawnBananaHazard();
                _nextBananaSpawnTime = Time.time + BananaSpawnInterval;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !ShouldGenerateAnyPowerUpReport)
            {
                return;
            }

            TryWritePowerUpUsageReport();
            KartPowerUpController.AnyPowerUpUsed -= HandlePowerUpUsed;
            KartPowerUpController.AnyPowerUpHit -= HandlePowerUpHit;
            KartPowerUpAgent.AnyDecisionEvaluated -= HandlePowerUpDecisionEvaluated;
            KartPowerUpAgent.AnyPowerUpExecutionFailed -= HandlePowerUpExecutionFailed;
        }

        private void OnApplicationQuit()
        {
            if (!ShouldGenerateAnyPowerUpReport)
            {
                return;
            }

            TryWritePowerUpUsageReport();
        }

        [ContextMenu("Sync Child Collections")]
        public void SyncChildCollections()
        {
            checkpoints = CollectDirectChildren("Checkpoints");
            spawnPoints = CollectDirectChildren("SpawnPoints");
            powerUpBoxes = CollectDirectChildren("PowerUpBoxes");
            respawnPoints = CollectDirectChildren("RespawnPoints");
        }

        public void SetLapsToWin(int value)
        {
            lapsToWin = Mathf.Max(1, value);
        }

        public void SetBotCounts(int noobCount, int proCount, int neutralCount)
        {
            noobBotCount = Mathf.Max(0, noobCount);
            proBotCount = Mathf.Max(0, proCount);
            neutralBotCount = Mathf.Max(0, neutralCount);
        }

        public void SetPickupSpawnEnabled(bool value)
        {
            enablePickupSpawning = value;
        }

        public Transform GetCheckpoint(int index)
        {
            if (checkpoints == null || checkpoints.Length == 0)
            {
                return null;
            }

            var clampedIndex = Mathf.Clamp(index, 0, checkpoints.Length - 1);
            return checkpoints[clampedIndex];
        }

        public Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            var clampedIndex = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[clampedIndex];
        }

        public bool TryGetRecoveryPose(Vector3 worldPosition, out Vector3 recoveryPosition, out Quaternion recoveryRotation)
        {
            var candidates = respawnPoints != null && respawnPoints.Length > 0
                ? respawnPoints
                : checkpoints != null && checkpoints.Length > 0
                    ? checkpoints
                    : spawnPoints;

            if (candidates == null || candidates.Length == 0)
            {
                recoveryPosition = transform.position;
                recoveryRotation = transform.rotation;
                return false;
            }

            var closestDistance = float.MaxValue;
            Transform closest = candidates[0];

            foreach (var candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                var distance = (candidate.position - worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            recoveryPosition = closest.position;
            recoveryRotation = closest.rotation;
            return true;
        }

        private Transform[] CollectDirectChildren(string rootName)
        {
            var root = transform.Find(rootName);
            if (root == null)
            {
                return Array.Empty<Transform>();
            }

            var collection = new Transform[root.childCount];
            for (var index = 0; index < root.childCount; index++)
            {
                collection[index] = root.GetChild(index);
            }

            return collection;
        }

        private void EnsurePickupSpawnRoots()
        {
            _spawnedCoinsRoot ??= EnsureChildContainer(spawnedCoinsRootName);
            _spawnedBananasRoot ??= EnsureChildContainer(spawnedBananasRootName);
        }

        private void EnsureRuntimeBotsRoot()
        {
            if (_spawnedBotsRoot != null)
            {
                return;
            }

            var existingRoot = GameObject.Find(spawnedBotsRootName);
            if (existingRoot != null)
            {
                existingRoot.transform.SetParent(null, true);
                _spawnedBotsRoot = existingRoot.transform;
                return;
            }

            var botsRootObject = new GameObject(spawnedBotsRootName);
            _spawnedBotsRoot = botsRootObject.transform;
        }

        private void SpawnConfiguredBotsAtRuntime()
        {
            if (_runtimeBotsSpawned)
            {
                return;
            }

            if (NoobBotCount <= 0 && ProBotCount <= 0 && NeutralBotCount <= 0)
            {
                return;
            }

            EnsureRuntimeBotsRoot();

            var spawnCursor = 0;
            SpawnRuntimeBotFamily(NoobBotPrefab, "Kart_Noob", NoobBotCount, ref spawnCursor);
            SpawnRuntimeBotFamily(ProBotPrefab, "Kart_Pro", ProBotCount, ref spawnCursor);
            SpawnRuntimeBotFamily(NeutralBotPrefab, "Kart_Neutral", NeutralBotCount, ref spawnCursor);

            _runtimeBotsSpawned = true;
        }

        private void SpawnRuntimeBotFamily(GameObject botPrefab, string botNamePrefix, int desiredCount, ref int spawnCursor)
        {
            if (desiredCount <= 0)
            {
                return;
            }

            for (var index = 1; index <= desiredCount; index++)
            {
                var botName = $"{botNamePrefix}_{index:00}";
                if (GameObject.Find(botName) != null)
                {
                    continue;
                }

                var spawnPoint = GetRuntimeBotSpawnPoint(spawnCursor++);
                var botObject = SpawnRuntimeBot(botPrefab, botName, spawnPoint);
                if (botObject == null)
                {
                    Debug.LogWarning($"No se pudo spawnear el bot '{botName}' en runtime.", this);
                    continue;
                }

                ConfigureRuntimeSpawnedBot(botObject, spawnPoint);
            }
        }

        private Transform GetRuntimeBotSpawnPoint(int spawnIndex)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return spawnPoints[Mathf.Abs(spawnIndex) % spawnPoints.Length];
            }

            if (checkpoints != null && checkpoints.Length > 0)
            {
                return checkpoints[Mathf.Abs(spawnIndex) % checkpoints.Length];
            }

            return null;
        }

        private GameObject SpawnRuntimeBot(GameObject botPrefab, string botName, Transform spawnPoint)
        {
            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            GameObject botObject;
            if (botPrefab != null)
            {
                botObject = Instantiate(botPrefab, spawnPosition, spawnRotation, _spawnedBotsRoot);
            }
            else
            {
                botObject = CreateRuntimeFallbackBot(botName, spawnPosition, spawnRotation);
                if (botObject != null)
                {
                    botObject.transform.SetParent(_spawnedBotsRoot, true);
                }
            }

            if (botObject != null)
            {
                botObject.name = botName;
            }

            return botObject;
        }

        private void ConfigureRuntimeSpawnedBot(GameObject botObject, Transform spawnPoint)
        {
            if (botObject == null)
            {
                return;
            }

            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            botObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var kartController = botObject.GetComponent<KartController>();
            if (kartController != null)
            {
                kartController.SetControlEnabled(true);
            }

            var checkpointTracker = botObject.GetComponent<CheckpointTracker>();
            if (checkpointTracker != null)
            {
                checkpointTracker.SetTrackData(this);
                checkpointTracker.SetPlayerFlag(false);
                checkpointTracker.SetRecoveryReference(spawnPoint);
                checkpointTracker.InitializeForRace(this);
                checkpointTracker.SetInitialSpawnPose(spawnPosition, spawnRotation);
            }

            var kartAgent = botObject.GetComponent<KartGame.AI.Reinforcement.KartAgent>();
            if (kartAgent != null)
            {
                var trainingSceneManager = FindFirstObjectByType<KartGame.AI.Reinforcement.TrainingSceneManager>();
                kartAgent.AutoAssignReferences(trainingSceneManager, this);
            }
        }

        private GameObject CreateRuntimeFallbackBot(string botName, Vector3 position, Quaternion rotation)
        {
            var kartObject = new GameObject(botName);
            kartObject.transform.SetPositionAndRotation(position, rotation);

            var rigidbody = kartObject.AddComponent<Rigidbody>();
            rigidbody.mass = 140f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var collider = kartObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.4f, 0.9f, 2.4f);

            kartObject.AddComponent<KartController>();
            var checkpointTracker = kartObject.AddComponent<CheckpointTracker>();
            checkpointTracker.SetTrackData(this);
            checkpointTracker.SetPlayerFlag(false);

            kartObject.AddComponent<AIKartInput>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(kartObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            visual.transform.localScale = new Vector3(1.35f, 0.6f, 2.2f);

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var material = new Material(shader)
                    {
                        color = new Color(0.35f, 0.35f, 0.35f)
                    };

                    renderer.sharedMaterial = material;
                }
            }

            return kartObject;
        }

        private Transform EnsureChildContainer(string childName)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            return childObject.transform;
        }

        private void ResetPickupSpawnTimers()
        {
            _nextCoinSpawnTime = Time.time + UnityEngine.Random.Range(0f, CoinSpawnInterval);
            _nextBananaSpawnTime = Time.time + UnityEngine.Random.Range(0f, BananaSpawnInterval);
        }

        private void TrySpawnCoinPickup()
        {
            if (CountActivePickups<CoinPickup>(_spawnedCoinsRoot) >= MaxCoinsOnTrack)
            {
                return;
            }

            if (!TryGetRandomPickupPose(out var position, out var rotation))
            {
                return;
            }

            var coinObject = coinPickupPrefab != null
                ? Instantiate(coinPickupPrefab, position, rotation, _spawnedCoinsRoot)
                : CreateFallbackCoin(position, rotation);

            if (coinObject == null)
            {
                return;
            }

            coinObject.name = $"TrackCoin_{++_coinSpawnSerial:00}";
            coinObject.transform.SetParent(_spawnedCoinsRoot, true);
            coinObject.transform.SetPositionAndRotation(position, rotation);

            var coinPickup = coinObject.GetComponent<CoinPickup>();
            if (coinPickup != null)
            {
                coinPickup.SetTrainingMode(spawnCoinsAsTrainingPickups);
            }

            if (logPickupSpawns)
            {
                Debug.Log($"Moneda generada: {coinObject.name}", this);
            }
        }

        private void TrySpawnBananaHazard()
        {
            if (CountActivePickups<BananaHazard>(_spawnedBananasRoot) >= MaxBananasOnTrack)
            {
                return;
            }

            if (!TryGetRandomPickupPose(out var position, out var rotation))
            {
                return;
            }

            var bananaObject = bananaHazardPrefab != null
                ? Instantiate(bananaHazardPrefab, position, rotation, _spawnedBananasRoot)
                : CreateFallbackBanana(position, rotation);

            if (bananaObject == null)
            {
                return;
            }

            bananaObject.name = $"TrackBanana_{++_bananaSpawnSerial:00}";
            bananaObject.transform.SetParent(_spawnedBananasRoot, true);
            bananaObject.transform.SetPositionAndRotation(position, rotation);

            var bananaHazard = bananaObject.GetComponent<BananaHazard>();
            if (bananaHazard != null)
            {
                bananaHazard.SetTrainingMode(spawnBananasAsTrainingHazards);
            }

            if (logPickupSpawns)
            {
                Debug.Log($"Platano generado: {bananaObject.name}", this);
            }
        }

        private bool TryGetRandomPickupPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (checkpoints == null || checkpoints.Length < 2)
            {
                if (spawnPoints != null && spawnPoints.Length > 0)
                {
                    var spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                    if (spawnPoint != null)
                    {
                        position = AdjustPickupHeight(spawnPoint.position);
                        rotation = spawnPoint.rotation;
                        return true;
                    }
                }

                return false;
            }

            var checkpointIndex = UnityEngine.Random.Range(0, checkpoints.Length);
            var current = checkpoints[checkpointIndex];
            var next = checkpoints[(checkpointIndex + 1) % checkpoints.Length];

            if (current == null || next == null)
            {
                return false;
            }

            var segment = next.position - current.position;
            var segmentLength = segment.magnitude;
            if (segmentLength <= 0.01f)
            {
                return false;
            }

            var direction = segment / segmentLength;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var alongFactor = UnityEngine.Random.Range(0.2f, 0.8f);
            var lateralOffset = UnityEngine.Random.Range(-pickupSpawnLateralOffset, pickupSpawnLateralOffset);

            var surfacePoint = current.position + direction * (segmentLength * alongFactor) + right * lateralOffset;
            position = AdjustPickupHeight(surfacePoint);
            rotation = Quaternion.LookRotation(direction, Vector3.up);
            return true;
        }

        private Vector3 AdjustPickupHeight(Vector3 basePoint)
        {
            var surfaceClearance = Mathf.Clamp(pickupSpawnHeight, 0.05f, 0.35f);
            var rayOrigin = basePoint + Vector3.up * PickupRaycastHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, PickupRaycastHeight * 2f, pickupSurfaceLayerMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * surfaceClearance;
            }

            return basePoint + Vector3.up * surfaceClearance;
        }

        private static int CountActivePickups<T>(Transform root) where T : Component
        {
            if (root == null)
            {
                return 0;
            }

            var pickups = root.GetComponentsInChildren<T>(true);
            var count = 0;
            for (var index = 0; index < pickups.Length; index++)
            {
                if (pickups[index] != null && pickups[index].gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private GameObject CreateFallbackCoin(Vector3 position, Quaternion rotation)
        {
            var coinObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinObject.name = "CoinPickup";
            coinObject.transform.SetPositionAndRotation(position, rotation);
            coinObject.transform.localScale = new Vector3(0.45f, 0.08f, 0.45f);

            var rigidbody = coinObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var coinPickup = coinObject.GetComponent<CoinPickup>() ?? coinObject.AddComponent<CoinPickup>();
            coinPickup.SetTrainingMode(spawnCoinsAsTrainingPickups);
            return coinObject;
        }

        private GameObject CreateFallbackBanana(Vector3 position, Quaternion rotation)
        {
            var bananaObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bananaObject.name = "BananaHazard";
            bananaObject.transform.SetPositionAndRotation(position, rotation);
            bananaObject.transform.localScale = new Vector3(0.6f, 0.12f, 0.6f);

            var rigidbody = bananaObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var bananaHazard = bananaObject.GetComponent<BananaHazard>() ?? bananaObject.AddComponent<BananaHazard>();
            bananaHazard.SetTrainingMode(spawnBananasAsTrainingHazards);
            return bananaObject;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || checkpoints == null || checkpoints.Length == 0)
            {
                return;
            }

            Gizmos.color = checkpointGizmoColor;

            for (var index = 0; index < checkpoints.Length; index++)
            {
                var current = checkpoints[index];
                if (current == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(current.position, 0.65f);

                Transform next = null;
                if (index < checkpoints.Length - 1)
                {
                    next = checkpoints[index + 1];
                }
                else if (closeCheckpointLoopGizmo && checkpoints.Length > 1)
                {
                    next = checkpoints[0];
                }

                if (next != null)
                {
                    Gizmos.DrawLine(current.position, next.position);
                }
            }
        }

        private void ResetPowerUpUsageTracking()
        {
            _reportWritten = false;
            _powerUpUseCounts.Clear();
            _powerUpFailedUseCounts.Clear();
            _powerUpHitCounts.Clear();
            _perKartPowerUpUseCounts.Clear();
            _perKartPowerUpFailedUseCounts.Clear();
            _perKartPowerUpHitCounts.Clear();
            _decisionOutcomeCounts.Clear();
            _perKartDecisionOutcomeCounts.Clear();
            _powerUpSuggestedCounts.Clear();
            _powerUpCorrectChoiceCounts.Clear();
            _perKartPowerUpSuggestedCounts.Clear();
            _perKartPowerUpCorrectChoiceCounts.Clear();

            var powerUpTypes = (PowerUpType[])Enum.GetValues(typeof(PowerUpType));
            for (var index = 0; index < powerUpTypes.Length; index++)
            {
                _powerUpUseCounts[powerUpTypes[index]] = 0;
                _powerUpFailedUseCounts[powerUpTypes[index]] = 0;
                _powerUpHitCounts[powerUpTypes[index]] = 0;
                _powerUpSuggestedCounts[powerUpTypes[index]] = 0;
                _powerUpCorrectChoiceCounts[powerUpTypes[index]] = 0;
            }

            var decisionOutcomes = (KartPowerUpAgent.PowerUpDecisionOutcome[])Enum.GetValues(typeof(KartPowerUpAgent.PowerUpDecisionOutcome));
            for (var index = 0; index < decisionOutcomes.Length; index++)
            {
                _decisionOutcomeCounts[decisionOutcomes[index]] = 0;
            }
        }

        private void HandlePowerUpUsed(KartPowerUpController sourceController, PowerUpType powerUpType)
        {
            IncrementPowerUpCount(_powerUpUseCounts, powerUpType);
            IncrementPerKartPowerUpCount(_perKartPowerUpUseCounts, GetKartName(sourceController), powerUpType);
        }

        private void HandlePowerUpHit(KartPowerUpController sourceController, PowerUpType powerUpType, KartController _)
        {
            IncrementPowerUpCount(_powerUpHitCounts, powerUpType);
            IncrementPerKartPowerUpCount(_perKartPowerUpHitCounts, GetKartName(sourceController), powerUpType);
        }

        private void HandlePowerUpExecutionFailed(KartPowerUpAgent sourceAgent, PowerUpType powerUpType)
        {
            IncrementPowerUpCount(_powerUpFailedUseCounts, powerUpType);
            IncrementPerKartPowerUpCount(_perKartPowerUpFailedUseCounts, GetKartName(sourceAgent), powerUpType);
        }

        private void HandlePowerUpDecisionEvaluated(
            KartPowerUpAgent sourceAgent,
            PowerUpType? chosenPowerUp,
            PowerUpType? suggestedPowerUp,
            KartPowerUpAgent.PowerUpDecisionOutcome decisionOutcome)
        {
            IncrementDecisionOutcomeCount(_decisionOutcomeCounts, decisionOutcome);
            IncrementPerKartDecisionOutcomeCount(_perKartDecisionOutcomeCounts, GetKartName(sourceAgent), decisionOutcome);

            if (suggestedPowerUp.HasValue)
            {
                IncrementPowerUpCount(_powerUpSuggestedCounts, suggestedPowerUp.Value);
                IncrementPerKartPowerUpCount(_perKartPowerUpSuggestedCounts, GetKartName(sourceAgent), suggestedPowerUp.Value);
            }

            if (decisionOutcome == KartPowerUpAgent.PowerUpDecisionOutcome.CorrectChoice)
            {
                var correctPowerUp = chosenPowerUp ?? suggestedPowerUp;
                if (correctPowerUp.HasValue)
                {
                    IncrementPowerUpCount(_powerUpCorrectChoiceCounts, correctPowerUp.Value);
                    IncrementPerKartPowerUpCount(_perKartPowerUpCorrectChoiceCounts, GetKartName(sourceAgent), correctPowerUp.Value);
                }
            }
        }

        private void TryWritePowerUpUsageReport()
        {
            if (_reportWritten)
            {
                return;
            }

            _reportWritten = true;

            var reportDirectory = Path.Combine(Application.dataPath, "Informes power ups");
            Directory.CreateDirectory(reportDirectory);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var sceneName = SceneManager.GetActiveScene().name;
            var reportPath = Path.Combine(reportDirectory, $"powerup_report_{sceneName}_{timestamp}.txt");

            var reportLines = new List<string>
            {
                "Kart Racing Power-Up Usage Report",
                "================================",
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Scene: {sceneName}",
                $"TrackRoot: {name}",
                string.Empty,
                "Totals",
                "------"
            };

            var totalUsed = 0;
            var powerUpTypes = (PowerUpType[])Enum.GetValues(typeof(PowerUpType));
            for (var index = 0; index < powerUpTypes.Length; index++)
            {
                var powerUpType = powerUpTypes[index];
                var count = _powerUpUseCounts.TryGetValue(powerUpType, out var storedCount) ? storedCount : 0;
                totalUsed += count;
                reportLines.Add($"{powerUpType}: {count}");
            }

            reportLines.Insert(reportLines.Count - powerUpTypes.Length, $"Total Power Ups Used: {totalUsed}");

            if (generatePowerUpLearningReport)
            {
                AppendLearningSummary(reportLines, powerUpTypes);
            }

            reportLines.Add(string.Empty);
            reportLines.Add("Per Kart");
            reportLines.Add("--------");

            var kartNames = CollectTrackedKartNames();
            if (kartNames.Count == 0)
            {
                reportLines.Add("No power up activity was tracked.");
            }
            else
            {
                foreach (var kartName in kartNames)
                {
                    var kartUseCounts = _perKartPowerUpUseCounts.TryGetValue(kartName, out var storedUseCounts)
                        ? storedUseCounts
                        : null;

                    var kartTotal = SumCounts(kartUseCounts);

                    reportLines.Add($"{kartName}: {kartTotal}");
                    for (var index = 0; index < powerUpTypes.Length; index++)
                    {
                        var powerUpType = powerUpTypes[index];
                        var count = GetPowerUpCount(kartUseCounts, powerUpType);
                        reportLines.Add($"  {powerUpType}: {count}");
                    }

                    if (generatePowerUpLearningReport)
                    {
                        AppendPerKartLearningSummary(reportLines, kartName, powerUpTypes);
                    }
                }
            }

            File.WriteAllLines(reportPath, reportLines);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        private void AppendLearningSummary(List<string> reportLines, PowerUpType[] powerUpTypes)
        {
            var totalUsed = SumCounts(_powerUpUseCounts.Values);
            var totalFailedUses = SumCounts(_powerUpFailedUseCounts.Values);
            var totalHits = SumCounts(_powerUpHitCounts.Values);
            var correctChoices = GetDecisionOutcomeCount(_decisionOutcomeCounts, KartPowerUpAgent.PowerUpDecisionOutcome.CorrectChoice);
            var wrongChoices = GetDecisionOutcomeCount(_decisionOutcomeCounts, KartPowerUpAgent.PowerUpDecisionOutcome.WrongChoice);
            var missedOpportunities = GetDecisionOutcomeCount(_decisionOutcomeCounts, KartPowerUpAgent.PowerUpDecisionOutcome.MissedOpportunity);
            var unnecessaryUses = GetDecisionOutcomeCount(_decisionOutcomeCounts, KartPowerUpAgent.PowerUpDecisionOutcome.UnnecessaryUse);
            var usesWithoutPoints = GetDecisionOutcomeCount(_decisionOutcomeCounts, KartPowerUpAgent.PowerUpDecisionOutcome.UseWithoutPoints);
            var evaluatedChoices = correctChoices + wrongChoices;
            var decisionAccuracy = evaluatedChoices > 0 ? (float)correctChoices / evaluatedChoices : -1f;
            var executionAttempts = totalUsed + totalFailedUses;
            var executionSuccessRate = executionAttempts > 0 ? (float)totalUsed / executionAttempts : -1f;

            reportLines.Add(string.Empty);
            reportLines.Add("Learning Summary");
            reportLines.Add("----------------");
            reportLines.Add($"Total Failed Uses: {totalFailedUses}");
            reportLines.Add($"Total Power Up Hits: {totalHits}");
            reportLines.Add($"Correct Choices: {correctChoices}");
            reportLines.Add($"Wrong Choices: {wrongChoices}");
            reportLines.Add($"Missed Opportunities: {missedOpportunities}");
            reportLines.Add($"Unnecessary Uses: {unnecessaryUses}");
            reportLines.Add($"Uses Without Points: {usesWithoutPoints}");
            reportLines.Add($"Overall Decision Accuracy: {(decisionAccuracy >= 0f ? $"{decisionAccuracy:P0}" : "N/A")}");
            reportLines.Add($"Overall Execution Success Rate: {(executionSuccessRate >= 0f ? $"{executionSuccessRate:P0}" : "N/A")}");
            reportLines.Add($"Overall Learning Verdict: {EvaluateOverallLearning(decisionAccuracy, executionSuccessRate, totalHits, evaluatedChoices)}");

            reportLines.Add(string.Empty);
            reportLines.Add("Per Power Up Learning");
            reportLines.Add("---------------------");

            for (var index = 0; index < powerUpTypes.Length; index++)
            {
                var powerUpType = powerUpTypes[index];
                var suggestedCount = GetPowerUpCount(_powerUpSuggestedCounts, powerUpType);
                var correctCount = GetPowerUpCount(_powerUpCorrectChoiceCounts, powerUpType);
                var usedCount = GetPowerUpCount(_powerUpUseCounts, powerUpType);
                var failedCount = GetPowerUpCount(_powerUpFailedUseCounts, powerUpType);
                var hitCount = GetPowerUpCount(_powerUpHitCounts, powerUpType);
                var typeDecisionAccuracy = suggestedCount > 0 ? (float)correctCount / suggestedCount : -1f;
                var typeExecutionAttempts = usedCount + failedCount;
                var typeExecutionSuccessRate = typeExecutionAttempts > 0 ? (float)usedCount / typeExecutionAttempts : -1f;

                reportLines.Add($"{powerUpType}:");
                reportLines.Add($"  Suggested Opportunities: {suggestedCount}");
                reportLines.Add($"  Correct Context Choices: {correctCount}");
                reportLines.Add($"  Successful Uses: {usedCount}");
                reportLines.Add($"  Failed Uses: {failedCount}");
                reportLines.Add($"  Hits: {hitCount}");
                reportLines.Add($"  Decision Accuracy: {(typeDecisionAccuracy >= 0f ? $"{typeDecisionAccuracy:P0}" : "N/A")}");
                reportLines.Add($"  Execution Success Rate: {(typeExecutionSuccessRate >= 0f ? $"{typeExecutionSuccessRate:P0}" : "N/A")}");
                reportLines.Add($"  Learning Verdict: {EvaluatePowerUpLearning(powerUpType, suggestedCount, typeDecisionAccuracy, typeExecutionSuccessRate, hitCount)}");
            }
        }

        private void AppendPerKartLearningSummary(List<string> reportLines, string kartName, PowerUpType[] powerUpTypes)
        {
            var decisionCounts = _perKartDecisionOutcomeCounts.TryGetValue(kartName, out var storedDecisionCounts)
                ? storedDecisionCounts
                : null;

            var failedCounts = _perKartPowerUpFailedUseCounts.TryGetValue(kartName, out var storedFailedCounts)
                ? storedFailedCounts
                : null;

            var hitCounts = _perKartPowerUpHitCounts.TryGetValue(kartName, out var storedHitCounts)
                ? storedHitCounts
                : null;

            reportLines.Add("  Learning:");
            reportLines.Add($"    Correct Choices: {GetDecisionOutcomeCount(decisionCounts, KartPowerUpAgent.PowerUpDecisionOutcome.CorrectChoice)}");
            reportLines.Add($"    Wrong Choices: {GetDecisionOutcomeCount(decisionCounts, KartPowerUpAgent.PowerUpDecisionOutcome.WrongChoice)}");
            reportLines.Add($"    Missed Opportunities: {GetDecisionOutcomeCount(decisionCounts, KartPowerUpAgent.PowerUpDecisionOutcome.MissedOpportunity)}");
            reportLines.Add($"    Unnecessary Uses: {GetDecisionOutcomeCount(decisionCounts, KartPowerUpAgent.PowerUpDecisionOutcome.UnnecessaryUse)}");
            reportLines.Add($"    Uses Without Points: {GetDecisionOutcomeCount(decisionCounts, KartPowerUpAgent.PowerUpDecisionOutcome.UseWithoutPoints)}");

            for (var index = 0; index < powerUpTypes.Length; index++)
            {
                var powerUpType = powerUpTypes[index];
                var failedCount = GetPowerUpCount(failedCounts, powerUpType);
                var hitCount = GetPowerUpCount(hitCounts, powerUpType);
                var suggestedCount = GetPowerUpCount(_perKartPowerUpSuggestedCounts.TryGetValue(kartName, out var storedSuggestedCounts) ? storedSuggestedCounts : null, powerUpType);
                var correctCount = GetPowerUpCount(_perKartPowerUpCorrectChoiceCounts.TryGetValue(kartName, out var storedCorrectCounts) ? storedCorrectCounts : null, powerUpType);
                reportLines.Add($"    {powerUpType} -> suggested: {suggestedCount}, correct: {correctCount}, failed: {failedCount}, hits: {hitCount}");
            }
        }

        private static void IncrementPowerUpCount(Dictionary<PowerUpType, int> counts, PowerUpType powerUpType)
        {
            if (!counts.ContainsKey(powerUpType))
            {
                counts[powerUpType] = 0;
            }

            counts[powerUpType]++;
        }

        private static void IncrementPerKartPowerUpCount(Dictionary<string, Dictionary<PowerUpType, int>> countsByKart, string kartName, PowerUpType powerUpType)
        {
            if (!countsByKart.TryGetValue(kartName, out var kartCounts))
            {
                kartCounts = new Dictionary<PowerUpType, int>();
                countsByKart[kartName] = kartCounts;
            }

            IncrementPowerUpCount(kartCounts, powerUpType);
        }

        private static void IncrementDecisionOutcomeCount(
            Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int> counts,
            KartPowerUpAgent.PowerUpDecisionOutcome outcome)
        {
            if (!counts.ContainsKey(outcome))
            {
                counts[outcome] = 0;
            }

            counts[outcome]++;
        }

        private static void IncrementPerKartDecisionOutcomeCount(
            Dictionary<string, Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int>> countsByKart,
            string kartName,
            KartPowerUpAgent.PowerUpDecisionOutcome outcome)
        {
            if (!countsByKart.TryGetValue(kartName, out var kartCounts))
            {
                kartCounts = new Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int>();
                countsByKart[kartName] = kartCounts;
            }

            IncrementDecisionOutcomeCount(kartCounts, outcome);
        }

        private static int GetPowerUpCount(Dictionary<PowerUpType, int> counts, PowerUpType powerUpType)
        {
            return counts != null && counts.TryGetValue(powerUpType, out var storedCount) ? storedCount : 0;
        }

        private static int GetDecisionOutcomeCount(
            Dictionary<KartPowerUpAgent.PowerUpDecisionOutcome, int> counts,
            KartPowerUpAgent.PowerUpDecisionOutcome outcome)
        {
            return counts != null && counts.TryGetValue(outcome, out var storedCount) ? storedCount : 0;
        }

        private static int SumCounts(Dictionary<PowerUpType, int> counts)
        {
            if (counts == null)
            {
                return 0;
            }

            var sum = 0;
            foreach (var count in counts.Values)
            {
                sum += count;
            }

            return sum;
        }

        private static int SumCounts(Dictionary<PowerUpType, int>.ValueCollection counts)
        {
            var sum = 0;
            foreach (var count in counts)
            {
                sum += count;
            }

            return sum;
        }

        private static string EvaluatePowerUpLearning(
            PowerUpType powerUpType,
            int suggestedCount,
            float decisionAccuracy,
            float executionSuccessRate,
            int hitCount)
        {
            if (suggestedCount <= 0)
            {
                return "Sin oportunidades suficientes.";
            }

            if (decisionAccuracy >= 0.7f && executionSuccessRate >= 0.6f)
            {
                if ((powerUpType == PowerUpType.Banana || powerUpType == PowerUpType.Shell) && hitCount <= 0)
                {
                    return "Elige bien, pero todavia no convierte en impactos.";
                }

                return "Si, parece aprenderlo.";
            }

            if (decisionAccuracy >= 0.4f || executionSuccessRate >= 0.4f)
            {
                return "Aprendizaje parcial.";
            }

            return "No parece aprenderlo todavia.";
        }

        private static string EvaluateOverallLearning(float decisionAccuracy, float executionSuccessRate, int totalHits, int evaluatedChoices)
        {
            if (evaluatedChoices <= 0)
            {
                return "Sin datos suficientes todavia.";
            }

            if (decisionAccuracy >= 0.65f && executionSuccessRate >= 0.6f)
            {
                return totalHits > 0
                    ? "Si, en conjunto parece aprender a usar los power ups."
                    : "Aprende a elegirlos, pero aun no saca mucho provecho real.";
            }

            if (decisionAccuracy >= 0.4f || executionSuccessRate >= 0.4f)
            {
                return "Aprendizaje parcial, aun inconsistente.";
            }

            return "No parece aprenderlos bien todavia.";
        }

        private HashSet<string> CollectTrackedKartNames()
        {
            var kartNames = new HashSet<string>();

            foreach (var kartName in _perKartPowerUpUseCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            foreach (var kartName in _perKartPowerUpFailedUseCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            foreach (var kartName in _perKartPowerUpHitCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            foreach (var kartName in _perKartDecisionOutcomeCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            foreach (var kartName in _perKartPowerUpSuggestedCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            foreach (var kartName in _perKartPowerUpCorrectChoiceCounts.Keys)
            {
                kartNames.Add(kartName);
            }

            return kartNames;
        }

        private static string GetKartName(KartPowerUpController sourceController)
        {
            if (sourceController == null)
            {
                return "Unknown";
            }

            var sourceKart = sourceController.GetComponentInParent<KartController>();
            return sourceKart != null ? sourceKart.name : sourceController.transform.root.name;
        }

        private static string GetKartName(KartPowerUpAgent sourceAgent)
        {
            if (sourceAgent == null)
            {
                return "Unknown";
            }

            var sourceKart = sourceAgent.GetComponentInParent<KartController>();
            return sourceKart != null ? sourceKart.name : sourceAgent.transform.root.name;
        }
    }
}
