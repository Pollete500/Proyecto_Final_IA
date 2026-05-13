using System.Collections.Generic;
using KartGame.Core;
using KartGame.Kart;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartGame.UI
{
    /*
     * Script: ResultsUI.cs
     * Purpose: Shows the finish order when the race ends.
     * Attach To: A Canvas GameObject in the race scene.
     * Required Components: resultsPanel, rowTemplate and restartButton assigned in the Inspector.
     * Dependencies: RaceManager, LapManager.
     */
    public class ResultsUI : MonoBehaviour
    {
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private Button restartButton;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void Start()
        {
            if (resultsPanel != null) resultsPanel.SetActive(false);
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestartClicked);
            if (RaceManager.Instance != null) RaceManager.Instance.RaceStateChanged += HandleRaceStateChanged;
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null) RaceManager.Instance.RaceStateChanged -= HandleRaceStateChanged;
            if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        private void HandleRaceStateChanged(RaceState newState)
        {
            if (newState != RaceState.Finished) return;
            BuildResultRows();
            if (resultsPanel != null) resultsPanel.SetActive(true);
            if (titleText != null) titleText.text = "RACE FINISHED";
        }

        private void BuildResultRows()
        {
            foreach (var row in _spawnedRows) Destroy(row);
            _spawnedRows.Clear();

            if (rowTemplate == null || rowContainer == null) return;

            var allTrackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.None);
            var sorted = new List<CheckpointTracker>(allTrackers);
            sorted.Sort((a, b) =>
            {
                if (a.HasFinishedRace && b.HasFinishedRace) return a.FinishPlacement.CompareTo(b.FinishPlacement);
                if (a.HasFinishedRace) return -1;
                if (b.HasFinishedRace) return 1;
                var lap = b.CompletedLaps.CompareTo(a.CompletedLaps);
                return lap != 0 ? lap : b.LastPassedCheckpointIndex.CompareTo(a.LastPassedCheckpointIndex);
            });

            for (var i = 0; i < sorted.Count; i++)
            {
                var tracker = sorted[i];
                if (tracker == null) continue;

                var row = Instantiate(rowTemplate, rowContainer);
                row.SetActive(true);

                var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                var label = tracker.IsPlayer ? $"{tracker.name} (YOU)" : tracker.name;
                if (texts.Length >= 2) { texts[0].text = $"{i + 1}."; texts[1].text = label; }
                else if (texts.Length == 1) texts[0].text = $"{i + 1}. {label}";

                _spawnedRows.Add(row);
            }
        }

        private void HandleRestartClicked()
        {
            if (resultsPanel != null) resultsPanel.SetActive(false);
            RaceManager.Instance?.RestartRaceFlow();
        }
    }
}
