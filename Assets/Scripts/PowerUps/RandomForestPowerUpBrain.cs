using System;
using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.PowerUps
{
    public class RandomForestPowerUpBrain : MonoBehaviour
    {
        [Serializable]
        private class RandomForestModelJson
        {
            public string[] classLabels;
            public string[] featureColumns;
            public RandomForestTreeJson[] trees;
        }

        [Serializable]
        private class RandomForestTreeJson
        {
            public int[] featureIndices;
            public float[] thresholds;
            public int[] leftChildren;
            public int[] rightChildren;
            public int[] predictedClassIndices;
        }

        private sealed class RankedPrediction
        {
            public string Label { get; }
            public int Votes { get; }
            public float VoteRatio { get; }

            public RankedPrediction(string label, int votes, float voteRatio)
            {
                Label = label;
                Votes = votes;
                VoteRatio = voteRatio;
            }
        }

        private sealed class RandomForestRuntimeTree
        {
            private readonly int[] _featureIndices;
            private readonly float[] _thresholds;
            private readonly int[] _leftChildren;
            private readonly int[] _rightChildren;
            private readonly int[] _predictedClassIndices;

            public RandomForestRuntimeTree(RandomForestTreeJson treeJson)
            {
                _featureIndices = treeJson.featureIndices ?? Array.Empty<int>();
                _thresholds = treeJson.thresholds ?? Array.Empty<float>();
                _leftChildren = treeJson.leftChildren ?? Array.Empty<int>();
                _rightChildren = treeJson.rightChildren ?? Array.Empty<int>();
                _predictedClassIndices = treeJson.predictedClassIndices ?? Array.Empty<int>();
            }

            public int PredictClassIndex(int[] featureValues)
            {
                if (_predictedClassIndices.Length == 0)
                {
                    return -1;
                }

                var nodeIndex = 0;
                while (nodeIndex >= 0 && nodeIndex < _predictedClassIndices.Length)
                {
                    var leftChild = nodeIndex < _leftChildren.Length ? _leftChildren[nodeIndex] : -1;
                    var rightChild = nodeIndex < _rightChildren.Length ? _rightChildren[nodeIndex] : -1;
                    if (leftChild < 0 || rightChild < 0)
                    {
                        return _predictedClassIndices[nodeIndex];
                    }

                    var featureIndex = nodeIndex < _featureIndices.Length ? _featureIndices[nodeIndex] : -1;
                    var threshold = nodeIndex < _thresholds.Length ? _thresholds[nodeIndex] : 0f;
                    var featureValue = featureIndex >= 0 && featureIndex < featureValues.Length
                        ? featureValues[featureIndex]
                        : 0;

                    nodeIndex = featureValue <= threshold ? leftChild : rightChild;
                }

                return -1;
            }
        }

        private sealed class RandomForestRuntimeModel
        {
            private readonly string[] _classLabels;
            private readonly RandomForestRuntimeTree[] _trees;

            private RandomForestRuntimeModel(string[] classLabels, RandomForestRuntimeTree[] trees)
            {
                _classLabels = classLabels;
                _trees = trees;
            }

            public static RandomForestRuntimeModel FromJson(string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var modelJson = JsonUtility.FromJson<RandomForestModelJson>(json);
                if (modelJson == null || modelJson.classLabels == null || modelJson.classLabels.Length == 0 || modelJson.trees == null || modelJson.trees.Length == 0)
                {
                    return null;
                }

                var runtimeTrees = new RandomForestRuntimeTree[modelJson.trees.Length];
                for (var treeIndex = 0; treeIndex < modelJson.trees.Length; treeIndex++)
                {
                    runtimeTrees[treeIndex] = new RandomForestRuntimeTree(modelJson.trees[treeIndex]);
                }

                return new RandomForestRuntimeModel(modelJson.classLabels, runtimeTrees);
            }

            public List<RankedPrediction> RankPredictions(int[] featureValues)
            {
                var votes = new int[_classLabels.Length];
                for (var treeIndex = 0; treeIndex < _trees.Length; treeIndex++)
                {
                    var predictedClassIndex = _trees[treeIndex].PredictClassIndex(featureValues);
                    if (predictedClassIndex >= 0 && predictedClassIndex < votes.Length)
                    {
                        votes[predictedClassIndex]++;
                    }
                }

                var rankedPredictions = new List<RankedPrediction>(_classLabels.Length);
                var totalVotes = Mathf.Max(1, _trees.Length);
                for (var classIndex = 0; classIndex < _classLabels.Length; classIndex++)
                {
                    rankedPredictions.Add(new RankedPrediction(
                        _classLabels[classIndex],
                        votes[classIndex],
                        votes[classIndex] / (float)totalVotes));
                }

                rankedPredictions.Sort((left, right) =>
                {
                    var voteComparison = right.Votes.CompareTo(left.Votes);
                    return voteComparison != 0
                        ? voteComparison
                        : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
                });

                return rankedPredictions;
            }
        }

        [Header("References")]
        [SerializeField] private KartPowerUpController powerUpController;
        [SerializeField] private KartController kartController;
        [SerializeField] private CheckpointTracker checkpointTracker;
        [SerializeField] private TextAsset modelJson;

        [Header("Decision Timing")]
        [SerializeField] private float decisionInterval = 0.25f;
        [SerializeField] private bool requireRaceToBeActive = true;
        [SerializeField] private bool logPredictedPowerUps;

        [Header("Enemy Sensing")]
        [SerializeField] private float nearbyEnemyDistance = 14f;
        [SerializeField] private float aheadSenseHalfAngle = 70f;
        [SerializeField] private float behindSenseHalfAngle = 70f;

        [Header("Hazard Sensing")]
        [SerializeField] private float nearbyHazardDistance = 10f;

        [Header("Wall Rays")]
        [SerializeField] private LayerMask wallSenseMask = ~0;
        [SerializeField] private float wallSenseStartHeight = 0.75f;
        [SerializeField] private float wallSenseDistance = 8f;
        [SerializeField] private int wallSenseRaysPerDirection = 2;
        [SerializeField] private float wallSenseMaxDegrees = 45f;

        [Header("Straight Section Detection")]
        [SerializeField] private bool ignoreStraightSectionMaxTargetAngle;
        [SerializeField] private float straightSectionMaxTargetAngle = 12f;
        [SerializeField] private int maxWallHitsForStraightSection = 1;

        [Header("Runtime Debug")]
        public bool debugHasLoadedModel;
        public bool debugCanUseAnyPowerUp;
        public bool debugPredictedPowerUpAvailable;
        public string debugPredictedLabel;
        public float debugPredictedVoteRatio;
        public int debugEnemiesAhead;
        public int debugEnemiesBehind;
        public int debugBananasAhead;
        public int debugShellsBehind;
        public bool debugStraightSection;
        public PowerUpType debugPredictedPowerUp;

        private readonly int[] _featureValues = new int[5];
        private RandomForestRuntimeModel _runtimeModel;
        private float _nextDecisionTime;
        private CheckpointTracker _bestAheadTarget;

        private void Awake()
        {
            CacheReferences();
            LoadModel();
        }

        private void OnEnable()
        {
            CacheReferences();
            LoadModel();
        }

        private void OnValidate()
        {
            CacheReferences();
            LoadModel();
        }

        private void Update()
        {
            CacheReferences();
            if (powerUpController == null || kartController == null || checkpointTracker == null || _runtimeModel == null)
            {
                return;
            }

            if (Time.time < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = Time.time + Mathf.Max(0.05f, decisionInterval);
            RefreshContext();
            debugCanUseAnyPowerUp = CanUseAnyPowerUp();

            if (!debugCanUseAnyPowerUp)
            {
                return;
            }

            if (requireRaceToBeActive && RaceManager.Instance != null && !RaceManager.Instance.IsRaceActive())
            {
                return;
            }

            if (!kartController.IsControlEnabled)
            {
                return;
            }

            TryUsePredictedPowerUp();
        }

        private void CacheReferences()
        {
            powerUpController ??= GetComponent<KartPowerUpController>();
            powerUpController ??= GetComponentInParent<KartPowerUpController>();
            kartController ??= GetComponent<KartController>();
            kartController ??= GetComponentInParent<KartController>();
            checkpointTracker ??= GetComponent<CheckpointTracker>();
            checkpointTracker ??= GetComponentInParent<CheckpointTracker>();
        }

        private void LoadModel()
        {
            _runtimeModel = modelJson != null ? RandomForestRuntimeModel.FromJson(modelJson.text) : null;
            debugHasLoadedModel = _runtimeModel != null;
        }

        private void RefreshContext()
        {
            debugEnemiesAhead = 0;
            debugEnemiesBehind = 0;
            debugBananasAhead = 0;
            debugShellsBehind = 0;
            debugStraightSection = IsStraightSection();
            debugPredictedPowerUpAvailable = false;
            debugPredictedLabel = string.Empty;
            debugPredictedVoteRatio = 0f;
            _bestAheadTarget = null;

            var referenceTransform = kartController.transform;
            var bestAheadDistance = float.MaxValue;
            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.InstanceID);

            for (var trackerIndex = 0; trackerIndex < trackers.Length; trackerIndex++)
            {
                var otherTracker = trackers[trackerIndex];
                if (otherTracker == null || otherTracker == checkpointTracker)
                {
                    continue;
                }

                var localTarget = referenceTransform.InverseTransformPoint(otherTracker.transform.position);
                var planarDistance = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (planarDistance > nearbyEnemyDistance)
                {
                    continue;
                }

                if (localTarget.z > 0f)
                {
                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= aheadSenseHalfAngle)
                    {
                        debugEnemiesAhead++;
                        if (planarDistance < bestAheadDistance)
                        {
                            bestAheadDistance = planarDistance;
                            _bestAheadTarget = otherTracker;
                        }
                    }

                    continue;
                }

                var behindAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg);
                if (behindAngle <= behindSenseHalfAngle)
                {
                    debugEnemiesBehind++;
                }
            }

            var hazardHits = Physics.OverlapSphere(referenceTransform.position, nearbyHazardDistance, ~0, QueryTriggerInteraction.Collide);
            var seenHazards = new HashSet<PowerUpHazardBase>();

            for (var hitIndex = 0; hitIndex < hazardHits.Length; hitIndex++)
            {
                var hit = hazardHits[hitIndex];
                var hazard = hit != null ? hit.GetComponentInParent<PowerUpHazardBase>() : null;
                if (hazard == null || hazard.OwnerKart == kartController || !seenHazards.Add(hazard))
                {
                    continue;
                }

                var localHazard = referenceTransform.InverseTransformPoint(hazard.transform.position);
                if (hazard.PowerUpType == PowerUpType.Banana && localHazard.z > 0f)
                {
                    var aheadAngle = Mathf.Abs(Mathf.Atan2(localHazard.x, localHazard.z) * Mathf.Rad2Deg);
                    if (aheadAngle <= aheadSenseHalfAngle)
                    {
                        debugBananasAhead++;
                    }
                }
                else if (hazard.PowerUpType == PowerUpType.Shell && localHazard.z < 0f)
                {
                    var behindAngle = Mathf.Abs(Mathf.Atan2(localHazard.x, -localHazard.z) * Mathf.Rad2Deg);
                    if (behindAngle <= behindSenseHalfAngle)
                    {
                        debugShellsBehind++;
                    }
                }
            }

            _featureValues[0] = debugStraightSection ? 1 : 0;
            _featureValues[1] = debugEnemiesAhead;
            _featureValues[2] = debugEnemiesBehind;
            _featureValues[3] = debugBananasAhead;
            _featureValues[4] = debugShellsBehind;
        }

        private bool IsStraightSection()
        {
            if (checkpointTracker == null || checkpointTracker.NextCheckpoint == null || kartController == null)
            {
                return false;
            }

            var localTarget = kartController.transform.InverseTransformPoint(checkpointTracker.NextCheckpoint.position);
            var targetAngle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
            var wallHits = CountWallHits();
            var angleMatches = ignoreStraightSectionMaxTargetAngle || targetAngle <= straightSectionMaxTargetAngle;
            return angleMatches && wallHits <= maxWallHitsForStraightSection;
        }

        private int CountWallHits()
        {
            if (kartController == null)
            {
                return 0;
            }

            var hitCount = 0;
            var referenceTransform = kartController.transform;
            var origin = referenceTransform.position + Vector3.up * wallSenseStartHeight;
            var raysPerDirection = Mathf.Max(0, wallSenseRaysPerDirection);
            var stepAngle = raysPerDirection > 0 ? wallSenseMaxDegrees / raysPerDirection : 0f;

            for (var rayIndex = -raysPerDirection; rayIndex <= raysPerDirection; rayIndex++)
            {
                var yaw = stepAngle * rayIndex;
                var direction = Quaternion.Euler(0f, yaw, 0f) * referenceTransform.forward;
                if (Physics.Raycast(origin, direction, wallSenseDistance, wallSenseMask, QueryTriggerInteraction.Ignore))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        private bool CanUseAnyPowerUp()
        {
            return powerUpController != null &&
                   (powerUpController.CanAttemptPowerUp(PowerUpType.Banana) ||
                    powerUpController.CanAttemptPowerUp(PowerUpType.Shell) ||
                    powerUpController.CanAttemptPowerUp(PowerUpType.Mushroom) ||
                    powerUpController.CanAttemptPowerUp(PowerUpType.Star));
        }

        private void TryUsePredictedPowerUp()
        {
            var rankedPredictions = _runtimeModel.RankPredictions(_featureValues);
            if (rankedPredictions == null || rankedPredictions.Count == 0)
            {
                return;
            }

            debugPredictedLabel = rankedPredictions[0].Label;
            debugPredictedVoteRatio = rankedPredictions[0].VoteRatio;

            for (var predictionIndex = 0; predictionIndex < rankedPredictions.Count; predictionIndex++)
            {
                var prediction = rankedPredictions[predictionIndex];
                if (!TryMapLabelToPowerUp(prediction.Label, out var powerUpType))
                {
                    continue;
                }

                if (!powerUpController.CanAttemptPowerUp(powerUpType))
                {
                    continue;
                }

                debugPredictedPowerUpAvailable = true;
                debugPredictedPowerUp = powerUpType;
                debugPredictedLabel = prediction.Label;
                debugPredictedVoteRatio = prediction.VoteRatio;

                var used = powerUpType == PowerUpType.Shell
                    ? powerUpController.UsePowerUp(powerUpType, _bestAheadTarget)
                    : powerUpController.UsePowerUp(powerUpType);

                if (!used)
                {
                    continue;
                }

                if (logPredictedPowerUps)
                {
                    Debug.Log(
                        $"RF POWER UP: {powerUpType} | voto={prediction.VoteRatio:0.00} | recta={debugStraightSection} | delante={debugEnemiesAhead} | atras={debugEnemiesBehind} | bananasDelante={debugBananasAhead} | conchasAtras={debugShellsBehind}",
                        this);
                }

                return;
            }
        }

        private static bool TryMapLabelToPowerUp(string label, out PowerUpType powerUpType)
        {
            powerUpType = PowerUpType.Mushroom;
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            switch (label.Trim().ToLowerInvariant())
            {
                case "banana":
                    powerUpType = PowerUpType.Banana;
                    return true;
                case "shell":
                    powerUpType = PowerUpType.Shell;
                    return true;
                case "mushroom":
                    powerUpType = PowerUpType.Mushroom;
                    return true;
                case "star":
                    powerUpType = PowerUpType.Star;
                    return true;
                default:
                    return false;
            }
        }
    }
}
