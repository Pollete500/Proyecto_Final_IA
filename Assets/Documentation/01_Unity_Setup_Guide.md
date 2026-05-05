# Unity Setup Guide

## Purpose

This guide explains how to prepare the Unity 6 project so the base kart racing prototype runs correctly.

## Unity Version

- Recommended editor: `Unity 6000.3.5f1`

## Required Packages

Already present in the project:

- `com.unity.inputsystem`
- `com.unity.ml-agents`
- `com.unity.render-pipelines.universal`
- `com.unity.ugui`

## Input Configuration

`PlayerKartInput.cs` uses the Input System package directly through `Keyboard.current`.

Recommended Player Settings:

- `Edit > Project Settings > Player > Active Input Handling`
- Use `Input System Package (New)` or `Both`

If the player kart does not react to keys, this is the first setting to check.

## Scene Setup Options

### Fastest option

Use:

`Tools > Kart Racing > MVP Setup > Build Minimal Prototype Scene`

This creates:

- `TrackRoot` with containers for checkpoints, spawns, power-up boxes and respawn points.
- A simple oval checkpoint layout.
- A flat prototype ground.
- `RaceSystems` with `RaceManager`, `LapManager` and `PositionManager`.
- `PlayerKart`.
- `AIKart_01` to `AIKart_06`.
- `Main Camera` with `CameraFollow`.

### Manual option

Use the menu commands separately:

- `Create Track Root`
- `Auto Configure Selected Track`
- `Create Race Systems`
- `Create Player Kart`
- `Create AI Kart`
- `Create Follow Camera`

### ML-Agents training option

Open `Assets/Scenes/TrainingScene.unity` and use:

`Tools > Kart Racing > ML-Agents > Build Training Prototype In Current Scene`

This creates:

- `TrackRoot` with checkpoints, spawn points and respawn points.
- A simple oval training track.
- Physical wall colliders tagged for ray perception.
- `TrainingSceneManager`.
- `TrainingKartAgent` already configured with:
  - `KartAgent`
  - `AgentRewardManager`
  - `Behavior Parameters`
  - `DecisionRequester`
  - `RayPerceptionSensorComponent3D`

## Required Scene Objects

### TrackRoot

Attach:

- `TrackData.cs`

Child containers expected:

- `Checkpoints`
- `SpawnPoints`
- `PowerUpBoxes`
- `RespawnPoints`
- `TrackBounds`
- `OffTrackZones`

### RaceSystems

Attach:

- `RaceManager.cs`
- `LapManager.cs`
- `PositionManager.cs`

### Player Kart

Attach:

- `Rigidbody`
- `Collider`
- `KartController.cs`
- `PlayerKartInput.cs`
- `CheckpointTracker.cs`

### AI Kart

Attach:

- `Rigidbody`
- `Collider`
- `KartController.cs`
- `AIKartInput.cs`
- `CheckpointTracker.cs`

### Main Camera

Attach:

- `Camera`
- `AudioListener`
- `CameraFollow.cs`

### Training Kart Agent

Attach:

- `Rigidbody`
- `Collider`
- `KartController.cs`
- `CheckpointTracker.cs`
- `KartAgent.cs`
- `AgentRewardManager.cs`
- `Behavior Parameters`
- `DecisionRequester`
- `RayPerceptionSensorComponent3D`

## Inspector Variables To Review First

### `KartController`

- `Acceleration Force`
- `Max Speed`
- `Steering Strength`
- `Lateral Grip`
- `Ground Probe Distance`

### `CheckpointTracker`

- `Track Data`
- `Is Player`
- `Auto Respawn If Stuck`

### `RaceManager`

- `Countdown Duration`
- `Auto Register Scene Racers`
- `Auto Place Racers On Spawn Points`
- `Finish Race When Player Finishes`

### `Behavior Parameters`

- `Behavior Name`: `KartAgent`
- `Behavior Type`: `Default` for training, `Heuristic Only` for recording demos, `Inference Only` for using a trained model in-scene
- `Vector Observation Size`: `13`
- `Actions`: recommended default is discrete with branch sizes `[3, 3]`

### `DecisionRequester`

- `Decision Period`: `1`
- `Take Actions Between Decisions`: enabled

## How To Test

1. Open the target scene.
2. Run the MVP setup menu command.
3. Press Play.
4. Verify the countdown starts.
5. Verify the player kart moves and turns.
6. Verify the bots drive around the prototype circuit.
7. Verify the player can reset with `R`.

## How To Start ML-Agents Training

1. Let Unity resolve the `com.unity.ml-agents` package after opening the project.
2. Create a Python environment and install:
   `pip install -r Assets/Python/requirements-mlagents.txt`
3. Open `Assets/Scenes/TrainingScene.unity`.
4. Run `Tools > Kart Racing > ML-Agents > Build Training Prototype In Current Scene`.
5. In PowerShell run:
   `./Assets/Python/train_kart_agent.ps1`
6. Wait until `mlagents-learn` says it is listening.
7. Return to Unity and press Play.

For imitation-assisted training, first record a `.demo` file into `Assets/Data/demos/`, then run:

`./Assets/Python/train_kart_agent_imitation.ps1`

## Current Limitations

- The generated scene is a functional graybox, not final art.
- Power-up boxes are placeholders only.
- The setup tool does not create UI yet.

## Future Improvements

- Add a custom editor window with parameter presets.
- Generate multiple track templates, not only the oval prototype.
- Auto-create HUD and results canvas.
- Add richer multi-track ML-Agents scene bootstrap support.
