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
    public class TrackData : MonoBehaviour
    {
        [SerializeField] private Transform[] checkpoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] powerUpBoxes = Array.Empty<Transform>();
        [SerializeField] private Transform[] respawnPoints = Array.Empty<Transform>();
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
        private bool ShouldGenerateAnyPowerUpReport => generatePowerUpUsageReport || generatePowerUpLearningReport;

        public int LapsToWin => Mathf.Max(1, lapsToWin);
        public int CheckpointCount => checkpoints?.Length ?? 0;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;
        public Transform[] Checkpoints => checkpoints;
        public Transform[] SpawnPoints => spawnPoints;
        public Transform[] PowerUpBoxes => powerUpBoxes;
        public Transform[] RespawnPoints => respawnPoints;

        private void Awake()
        {
            if (!Application.isPlaying || !ShouldGenerateAnyPowerUpReport)
            {
                return;
            }

            ResetPowerUpUsageTracking();
            KartPowerUpController.AnyPowerUpUsed += HandlePowerUpUsed;
            KartPowerUpController.AnyPowerUpHit += HandlePowerUpHit;
            KartPowerUpAgent.AnyDecisionEvaluated += HandlePowerUpDecisionEvaluated;
            KartPowerUpAgent.AnyPowerUpExecutionFailed += HandlePowerUpExecutionFailed;
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
