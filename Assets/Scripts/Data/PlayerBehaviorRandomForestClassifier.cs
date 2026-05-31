using System;
using System.Collections.Generic;
using System.IO;
using KartGame.Kart;
using UnityEngine;

namespace KartGame.Data
{
    /*
     * Script: PlayerBehaviorRandomForestClassifier.cs
     * Purpose: Runs the exported player random forest JSON against the player's accumulated race metrics.
     * Attach To: Not required. ResultsScreen calls this helper at race end.
     * Required Components: None.
     * Dependencies: PlayerLapDataRecorder.PlayerRaceMetrics and Assets/Data/classifier_rules/player_classifier.json.
     */
    public static class PlayerBehaviorRandomForestClassifier
    {
        public readonly struct ClassificationResult
        {
            public readonly string Label;
            public readonly int Votes;
            public readonly int TotalVotes;
            public readonly float Confidence;

            public ClassificationResult(string label, int votes, int totalVotes)
            {
                Label = label;
                Votes = votes;
                TotalVotes = Mathf.Max(1, totalVotes);
                Confidence = votes / (float)TotalVotes;
            }
        }

        [Serializable]
        private class RandomForestModelJson
        {
            public string[] classLabels;
            public string[] featureColumns;
            public string[] rawFeatureColumns;
            public string[] allFeatureColumns;
            public float[] scalerMean;
            public float[] scalerScale;
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

        public static bool TryClassify(
            PlayerLapDataRecorder.PlayerRaceMetrics metrics,
            TextAsset modelAsset,
            out ClassificationResult result,
            out string error)
        {
            result = default;
            var json = modelAsset != null ? modelAsset.text : LoadDefaultModelJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "No se pudo cargar Assets/Data/classifier_rules/player_classifier.json";
                return false;
            }

            var model = JsonUtility.FromJson<RandomForestModelJson>(json);
            if (model == null || model.classLabels == null || model.classLabels.Length == 0 || model.trees == null || model.trees.Length == 0)
            {
                error = "El JSON del clasificador del jugador no tiene labels o arboles validos.";
                return false;
            }

            var featureColumns = model.allFeatureColumns ?? model.featureColumns ?? model.rawFeatureColumns;
            var features = BuildFeatures(metrics, featureColumns);
            ApplyScaler(features, model.scalerMean, model.scalerScale);

            var votes = new int[model.classLabels.Length];
            for (var treeIndex = 0; treeIndex < model.trees.Length; treeIndex++)
            {
                var predictedClassIndex = PredictTree(model.trees[treeIndex], features);
                if (predictedClassIndex >= 0 && predictedClassIndex < votes.Length)
                {
                    votes[predictedClassIndex]++;
                }
            }

            var bestIndex = 0;
            for (var classIndex = 1; classIndex < votes.Length; classIndex++)
            {
                if (votes[classIndex] > votes[bestIndex])
                {
                    bestIndex = classIndex;
                }
            }

            result = new ClassificationResult(model.classLabels[bestIndex], votes[bestIndex], model.trees.Length);
            error = null;
            return true;
        }

        private static float[] BuildFeatures(PlayerLapDataRecorder.PlayerRaceMetrics metrics, string[] featureColumns)
        {
            featureColumns ??= Array.Empty<string>();
            var features = new float[featureColumns.Length];

            var rawValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["lapTime_delta"] = metrics.LapTimeDelta,
                ["position_delta"] = metrics.PositionDelta,
                ["mapCollisions_total"] = metrics.MapCollisionsTotal,
                ["shellsUsed_total"] = metrics.ShellsUsedTotal,
                ["bananasUsed_total"] = metrics.BananasUsedTotal,
                ["mushroomsUsed_total"] = metrics.MushroomsUsedTotal,
                ["starsUsed_total"] = metrics.StarsUsedTotal,
                ["bananaHitsReceived_total"] = metrics.BananaHitsReceivedTotal,
                ["bananaHitsRecived_total"] = metrics.BananaHitsReceivedTotal,
                ["shellHitsReceived_total"] = metrics.ShellHitsReceivedTotal,
                ["shellHitsRecived_total"] = metrics.ShellHitsReceivedTotal
            };

            var aggressiveness = metrics.ShellsUsedTotal + metrics.BananasUsedTotal;
            var receivedHits = metrics.BananaHitsReceivedTotal + metrics.ShellHitsReceivedTotal;
            rawValues["aggressiveness"] = aggressiveness;
            rawValues["hit_ratio"] = receivedHits / (aggressiveness + 1f);
            rawValues["powerup_efficiency"] = aggressiveness / (metrics.MapCollisionsTotal + 1f);

            for (var index = 0; index < featureColumns.Length; index++)
            {
                features[index] = rawValues.TryGetValue(featureColumns[index], out var value) ? value : 0f;
            }

            return features;
        }

        private static void ApplyScaler(float[] features, float[] means, float[] scales)
        {
            if (features == null || means == null || scales == null)
            {
                return;
            }

            var count = Mathf.Min(features.Length, Mathf.Min(means.Length, scales.Length));
            for (var index = 0; index < count; index++)
            {
                var scale = Mathf.Abs(scales[index]) > 0.0001f ? scales[index] : 1f;
                features[index] = (features[index] - means[index]) / scale;
            }
        }

        private static int PredictTree(RandomForestTreeJson tree, float[] features)
        {
            if (tree == null || tree.predictedClassIndices == null || tree.predictedClassIndices.Length == 0)
            {
                return -1;
            }

            var featureIndices = tree.featureIndices ?? Array.Empty<int>();
            var thresholds = tree.thresholds ?? Array.Empty<float>();
            var leftChildren = tree.leftChildren ?? Array.Empty<int>();
            var rightChildren = tree.rightChildren ?? Array.Empty<int>();
            var nodeIndex = 0;
            while (nodeIndex >= 0 && nodeIndex < tree.predictedClassIndices.Length)
            {
                var leftChild = nodeIndex < leftChildren.Length ? leftChildren[nodeIndex] : -1;
                var rightChild = nodeIndex < rightChildren.Length ? rightChildren[nodeIndex] : -1;
                if (leftChild < 0 || rightChild < 0)
                {
                    return tree.predictedClassIndices[nodeIndex];
                }

                var featureIndex = nodeIndex < featureIndices.Length ? featureIndices[nodeIndex] : -1;
                var threshold = nodeIndex < thresholds.Length ? thresholds[nodeIndex] : 0f;
                var featureValue = featureIndex >= 0 && featureIndex < features.Length ? features[featureIndex] : 0f;
                nodeIndex = featureValue <= threshold ? leftChild : rightChild;
            }

            return -1;
        }

        private static string LoadDefaultModelJson()
        {
            var path = Path.Combine(Application.dataPath, "Data", "classifier_rules", "player_classifier.json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
