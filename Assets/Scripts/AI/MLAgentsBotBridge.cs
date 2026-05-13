using KartGame.AI.Reinforcement;
using KartGame.Kart;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace KartGame.AI
{
    /*
     * Script: MLAgentsBotBridge.cs
     * Purpose: Switches a race bot between AIKartInput (heuristic) and KartAgent (ML-Agents inference) at startup.
     *          If preferMLAgents is true and a model is assigned in BehaviorParameters, the bot runs inference.
     *          Otherwise falls back to AIKartInput so the race always works even without a trained model.
     * Attach To: AI kart root that has both AIKartInput and KartAgent components.
     */
    public class MLAgentsBotBridge : MonoBehaviour
    {
        [SerializeField] private bool preferMLAgents = true;

        [Header("Auto-resolved — leave blank to auto-find")]
        [SerializeField] private AIKartInput aiKartInput;
        [SerializeField] private KartAgent kartAgent;
        [SerializeField] private BehaviorParameters behaviorParameters;

        public bool IsRunningMLAgents { get; private set; }

        private void Awake()
        {
            aiKartInput ??= GetComponent<AIKartInput>();
            kartAgent ??= GetComponent<KartAgent>();
            behaviorParameters ??= GetComponent<BehaviorParameters>();
            ApplyMode();
        }

        public void SwitchToML() { preferMLAgents = true; ApplyMode(); }
        public void SwitchToHeuristic() { preferMLAgents = false; ApplyMode(); }

        private void ApplyMode()
        {
            var hasModel = behaviorParameters != null && behaviorParameters.Model != null;
            var useML = preferMLAgents && hasModel && kartAgent != null;

            if (useML) behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
            if (aiKartInput != null) aiKartInput.enabled = !useML;
            if (kartAgent != null) kartAgent.enabled = useML;

            IsRunningMLAgents = useML;
            var reason = !hasModel ? " (no model assigned)" : "";
            Debug.Log($"[MLAgentsBotBridge] {gameObject.name} → {(useML ? "ML-Agents inference" : "heuristic AIKartInput")}{reason}");
        }
    }
}
