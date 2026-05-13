using KartGame.Core;
using KartGame.Kart;
using TMPro;
using UnityEngine;

namespace KartGame.UI
{
    /*
     * Script: RaceHUD.cs
     * Purpose: Shows live lap count, race position and elapsed time while the race is active.
     * Attach To: A Canvas GameObject in the race scene.
     * Required Components: Three TextMeshProUGUI fields assigned in the Inspector.
     * Dependencies: RaceManager, PositionManager.
     */
    public class RaceHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI lapText;
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private GameObject hudRoot;

        private CheckpointTracker _playerTracker;
        private PositionManager _positionManager;
        private int _totalLaps;

        private static readonly string[] OrdinalSuffixes = { "th", "st", "nd", "rd" };

        private void Start()
        {
            if (hudRoot != null) hudRoot.SetActive(false);

            if (RaceManager.Instance != null)
                RaceManager.Instance.RaceStateChanged += HandleRaceStateChanged;

            _positionManager = FindFirstObjectByType<PositionManager>();

            var trackers = FindObjectsByType<CheckpointTracker>(FindObjectsSortMode.None);
            foreach (var t in trackers)
            {
                if (t.IsPlayer) { _playerTracker = t; break; }
            }
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
                RaceManager.Instance.RaceStateChanged -= HandleRaceStateChanged;
        }

        private void Update()
        {
            if (RaceManager.Instance == null || RaceManager.Instance.CurrentState != RaceState.Racing) return;
            RefreshLapText();
            RefreshPositionText();
            RefreshTimeText();
        }

        private void HandleRaceStateChanged(RaceState newState)
        {
            var show = newState == RaceState.Racing || newState == RaceState.Finished;
            if (hudRoot != null) hudRoot.SetActive(show);

            if (newState == RaceState.Racing && RaceManager.Instance?.TrackData != null)
                _totalLaps = RaceManager.Instance.TrackData.LapsToWin;
        }

        private void RefreshLapText()
        {
            if (lapText == null || _playerTracker == null) return;
            var currentLap = Mathf.Clamp(_playerTracker.CompletedLaps + 1, 1, _totalLaps);
            lapText.text = $"LAP {currentLap}/{_totalLaps}";
        }

        private void RefreshPositionText()
        {
            if (positionText == null || _positionManager == null || _playerTracker == null) return;
            var pos = _positionManager.GetPosition(_playerTracker);
            positionText.text = $"{pos}{GetOrdinalSuffix(pos)}";
        }

        private void RefreshTimeText()
        {
            if (timeText == null || RaceManager.Instance == null) return;
            var elapsed = RaceManager.Instance.RaceElapsedTime;
            var m = Mathf.FloorToInt(elapsed / 60f);
            var s = Mathf.FloorToInt(elapsed % 60f);
            var cs = Mathf.FloorToInt((elapsed % 1f) * 100f);
            timeText.text = $"{m:00}:{s:00}.{cs:00}";
        }

        private static string GetOrdinalSuffix(int n)
        {
            if (n >= 11 && n <= 13) return "th";
            var mod = n % 10;
            return mod >= 1 && mod <= 3 ? OrdinalSuffixes[mod] : OrdinalSuffixes[0];
        }
    }
}
