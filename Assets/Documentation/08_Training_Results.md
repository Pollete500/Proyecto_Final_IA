# Training Results

## Status

Template ready. Fill this file after each real training run so the final defense includes evidence of learning.

## Training Session Template

### Run Identifier

- Example: `kart_ppo_left_turn_v1`

### Scene Used

- Example: `Assets/Scenes/TrainingScene.unity`

### Config Used

- `Assets/MLAgents/Config/kart_agent_config.yaml`
- or `Assets/MLAgents/Config/kart_agent_imitation_config.yaml`

### Trainer Command

- Example:
  `./Assets/Python/train_kart_agent.ps1 -RunId kart_ppo_left_turn_v1`

### Model Output

- Expected final model location:
  `Assets/MLAgents/TrainingLogs/<run-id>/KartAgent.onnx`

### Training Setup

- Number of training tracks:
- Number of agents in parallel:
- Decision period:
- Action space:
- Ray sensor settings:
- Reward settings changed from default:

### Quantitative Results

- Total training steps:
- Initial mean reward:
- Final mean reward:
- Approximate step at first stable lap:
- Approximate step at first full clean lap:

### Qualitative Before / After

Before training:

- What did the kart do in the first episodes?
- Did it reverse, hit walls, spin or idle?

After training:

- Could it stay on track?
- Could it complete checkpoints consistently?
- Could it complete a full lap?

### Media Evidence

- Screenshot path:
- Video path:
- TensorBoard screenshot path:

### Problems Found

- Example: wall hugging
- Example: spinning on spawn
- Example: overfitting to left turns

### Changes Applied

- Example: increased checkpoint density on curves
- Example: lowered wall collision termination severity
- Example: added imitation learning demo

### Conclusion

- Did the agent improve?
- Is the model ready for inference in race bots?
- What should be the next training iteration?
