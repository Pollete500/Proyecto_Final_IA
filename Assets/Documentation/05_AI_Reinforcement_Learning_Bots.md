# AI Reinforcement Learning Bots

## Status

Implemented as a training-ready foundation. The project now contains:

- `Assets/Scripts/AI/Reinforcement/KartAgent.cs`
- `Assets/Scripts/AI/Reinforcement/AgentRewardManager.cs`
- `Assets/Scripts/AI/Reinforcement/TrainingSceneManager.cs`
- `Assets/MLAgents/Config/kart_agent_config.yaml`
- `Assets/MLAgents/Config/kart_agent_imitation_config.yaml`
- `Assets/Python/train_kart_agent.ps1`
- `Assets/Python/train_kart_agent_imitation.ps1`

Runtime race bots still use `AIKartInput.cs` for the MVP race scene. The ML-Agents path is prepared through the dedicated training scene and can later replace those bots with an imported `.onnx` model.

## Technique Used

- Unity ML-Agents PPO for reinforcement learning.
- Optional imitation learning with Behavioral Cloning and GAIL.
- Ray-based perception plus structured checkpoint observations.

## Scripts And Scene Objects

### `KartAgent.cs`

Responsibilities:

- Reset the kart at the start of each episode.
- Collect structured observations for the policy.
- Convert ML-Agents actions into `KartController.SetInput(...)`.
- Award checkpoint, lap and progress rewards.
- Penalize collisions, wrong direction, idling and off-track behavior.

Attach to:

- Training kart root.

Required components:

- `KartController`
- `CheckpointTracker`
- `Rigidbody`
- `Behavior Parameters`
- `DecisionRequester`

### `AgentRewardManager.cs`

Responsibilities:

- Centralize reward and penalty tuning.
- Keep training values editable from the Inspector.

Attach to:

- Same GameObject as `KartAgent`, or a dedicated training root.

### `TrainingSceneManager.cs`

Responsibilities:

- Hold the `TrackData` reference for the training scene.
- Provide randomized spawn poses.
- Register training agents in the scene.

Attach to:

- `TrainingSceneManager` GameObject.

## Recommended Training Scene Setup

Open:

- `Assets/Scenes/TrainingScene.unity`

Then run:

- `Tools > Kart Racing > ML-Agents > Build Training Prototype In Current Scene`

This generates:

- `TrackRoot`
- `TrainingGround`
- Checkpoints
- Spawn points
- Respawn points
- Track wall colliders tagged as `Wall`
- `TrainingSceneManager`
- `TrainingKartAgent`

## ML-Agents Components Required On The Training Kart

- `Behavior Parameters`
- `DecisionRequester`
- `RayPerceptionSensorComponent3D`
- `KartAgent`
- `AgentRewardManager`

## Behavior Parameters Configuration

Recommended default:

- `Behavior Name`: `KartAgent`
- `Behavior Type`: `Default` during training
- `Vector Observation Size`: `13`
- `Num Stacked Vectors`: `1`
- `Actions`: discrete with branch sizes `[3, 3]`
- `Max Step`: `5000` as a solid starting point

Meaning of the default discrete actions:

- Branch 0:
  - `0`: no throttle
  - `1`: accelerate
  - `2`: brake or reverse
- Branch 1:
  - `0`: no steering
  - `1`: steer left
  - `2`: steer right

`KartAgent` also supports continuous actions if you want to switch later to:

- steering `[-1, 1]`
- acceleration `[0, 1]`
- brake `[0, 1]`

If you switch to continuous actions, update `Behavior Parameters` accordingly.

## Decision Requester Configuration

- `Decision Period`: `1`
- `Take Actions Between Decisions`: enabled

The kart moves quickly enough that it benefits from a decision every Academy step.

## Ray Sensor Configuration

Recommended starting values:

- `Rays Per Direction`: `3`
- `Max Ray Degrees`: `70`
- `Sphere Cast Radius`: `0.45`
- `Ray Length`: `14`
- `Start Vertical Offset`: `0.75`
- `End Vertical Offset`: `0`
- `Detectable Tags`: `Wall`, `Checkpoint`
- `Ray Layer Mask`: `KartWall`, `KartCheckpoint`

## Observations Used By `KartAgent`

Structured vector observations gathered in `CollectObservations()`:

- Local velocity X
- Local velocity Z
- Normalized speed
- Local direction to next checkpoint X
- Local direction to next checkpoint Z
- Normalized distance to next checkpoint
- Alignment with checkpoint forward
- Alignment with direction to next checkpoint
- Grounded state
- Off-track state
- Normalized checkpoint progress
- Normalized lap progress
- Uprightness via `transform.up.y`

In addition to these, the ray perception sensor supplies wall and checkpoint distance context.

## Rewards And Penalties

Configured in `AgentRewardManager.cs` and used by `KartAgent.cs`.

Positive:

- Correct checkpoint reward
- Lap completion reward
- Small shaped reward for moving closer to the next checkpoint

Negative:

- Moving away from the next checkpoint
- Staying idle
- Facing the wrong direction
- Crossing a wrong checkpoint trigger
- Hitting walls
- Sliding against walls
- Entering off-track zones
- Falling out of bounds
- Timing out at max step

## When An Episode Ends

By default the episode ends when:

- The kart completes a lap
- The kart enters an off-track fail zone
- The kart falls below the out-of-bounds height
- The agent reaches `Max Step` and the trainer interrupts the episode

Wall collisions do not end the episode by default, because the agent often learns faster if it can continue after making a mistake.

## How To Train

### PPO only

1. Install Python dependencies:
   `pip install -r Assets/Python/requirements-mlagents.txt`
2. Open `Assets/Scenes/TrainingScene.unity`
3. Run the ML-Agents setup menu command
4. In PowerShell run:
   `./Assets/Python/train_kart_agent.ps1`
5. When the trainer starts listening, press Play in Unity

### Imitation-assisted training

1. Record a demonstration into `Assets/Data/demos/kart_demo.demo`
2. Switch the training kart to `Behavior Type = Heuristic Only` while recording
3. Drive several clean laps
4. Stop recording and restore `Behavior Type = Default`
5. Run:
   `./Assets/Python/train_kart_agent_imitation.ps1`
6. Press Play in Unity after the Python trainer starts listening

## How To Use The Trained Model In Unity

1. Copy the exported `.onnx` model into `Assets/MLAgents/Models/`
2. On the target bot kart, add:
   - `Behavior Parameters`
   - `DecisionRequester`
   - `KartAgent`
   - the same ray sensor setup
3. Assign the model in `Behavior Parameters`
4. Set `Behavior Type` to `Inference Only`
5. Disable `AIKartInput`

## How To Test That Training Setup Is Correct

1. Run the training scene setup menu.
2. Select `TrainingKartAgent`.
3. Verify that `Behavior Parameters`, `DecisionRequester`, `KartAgent` and `RayPerceptionSensorComponent3D` exist.
4. Check that checkpoint objects are tagged `Checkpoint`.
5. Check that track wall objects are tagged `Wall`.
6. Start the trainer from PowerShell.
7. Press Play in Unity.
8. Confirm the console shows connection to the external communicator and the kart starts taking actions.

## Current Limitations

- Only one prototype training track is generated automatically.
- The training prototype is a graybox, not the final low-poly circuit.
- Demo recording is documented but not fully automated by editor tooling yet.
- The trained model is not yet wired into the race bots by default.

## Future Improvements

- Add multiple training tracks in one scene for better generalization.
- Add curriculum progression from simple to complex layouts.
- Add richer off-track detection and recovery logic.
- Add support for training several agents in parallel areas.
