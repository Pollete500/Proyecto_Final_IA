using System.Collections;
using KartGame.Core;
using TMPro;
using UnityEngine;

namespace KartGame.UI
{
    /*
     * Script: CountdownUI.cs
     * Purpose: Displays the pre-race countdown (3-2-1-GO!) and hides once the race starts.
     * Attach To: A Canvas GameObject in the race scene.
     * Required Components: TextMeshProUGUI assigned in the Inspector.
     * Dependencies: RaceManager singleton.
     */
    public class CountdownUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private float goDisplayDuration = 0.8f;

        private void Start()
        {
            if (countdownText != null)
                countdownText.gameObject.SetActive(false);

            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.RaceStateChanged += HandleRaceStateChanged;

                if (RaceManager.Instance.CurrentState == RaceState.Countdown)
                    StartCoroutine(RunCountdownDisplay());
            }
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
                RaceManager.Instance.RaceStateChanged -= HandleRaceStateChanged;
        }

        private void HandleRaceStateChanged(RaceState newState)
        {
            if (newState == RaceState.Countdown)
                StartCoroutine(RunCountdownDisplay());
        }

        private IEnumerator RunCountdownDisplay()
        {
            if (countdownText == null) yield break;

            countdownText.gameObject.SetActive(true);

            while (RaceManager.Instance != null && RaceManager.Instance.CurrentState == RaceState.Countdown)
            {
                var remaining = RaceManager.Instance.CountdownRemaining;
                var displayNumber = Mathf.CeilToInt(remaining);
                countdownText.text = displayNumber > 0 ? displayNumber.ToString() : "";
                yield return null;
            }

            countdownText.text = "GO!";
            yield return new WaitForSeconds(goDisplayDuration);
            countdownText.gameObject.SetActive(false);
        }
    }
}
