# Project Overview

## Goal

This Unity 6 project is a 3D low-poly arcade kart racing prototype for an AI Master's final project. The focus is a working MVP that demonstrates:

- A playable race with 1 player and 6 bots.
- Checkpoints, laps and live race positions.
- AI-driven enemies that can follow the track and later migrate to ML-Agents.
- Dynamic power-up assignment based on current race position.
- Player behavior logging and final behavior classification.

## Current MVP Foundation

The first architecture pass already includes:

- `KartController.cs` for Rigidbody-based arcade driving.
- `PlayerKartInput.cs` for keyboard input.
- `AIKartInput.cs` for temporary checkpoint-following bots.
- `CameraFollow.cs` for a simple chase camera.
- `TrackData.cs`, `Checkpoint.cs` and `CheckpointTracker.cs` for track progress.
- `LapManager.cs`, `PositionManager.cs` and `RaceManager.cs` for race flow.
- `KartRacingSetupTools.cs` editor utilities to scaffold a prototype scene quickly.

## Recommended Folder Structure

The project is organized to support the full GDD scope:

- `Assets/Scenes/`
- `Assets/Scripts/Core/`
- `Assets/Scripts/Kart/`
- `Assets/Scripts/AI/Reinforcement/`
- `Assets/Scripts/AI/Classification/`
- `Assets/Scripts/AI/PowerUps/`
- `Assets/Scripts/UI/`
- `Assets/Scripts/Utilities/`
- `Assets/Prefabs/`
- `Assets/MLAgents/`
- `Assets/Data/`
- `Assets/Python/`
- `Assets/Documentation/`

## Scene Bootstrap Workflow

Use the Unity menu:

`Tools > Kart Racing > MVP Setup`

Useful commands:

- `Build Minimal Prototype Scene`: creates a flat prototype scene with track root, checkpoints, spawn points, player, bots, camera and race managers.
- `Create Track Root`: creates the scene hierarchy containers for checkpoints and track data.
- `Create Race Systems`: creates `RaceManager`, `LapManager` and `PositionManager`.
- `Create Player Kart` and `Create AI Kart`: create kart roots with the required components already attached.
- `Auto Configure Selected Track`: numbers checkpoints and refreshes `TrackData`.

## Dependencies Between Systems

- `RaceManager` coordinates the race lifecycle.
- `TrackData` holds scene references for checkpoints and spawn points.
- Each kart needs `KartController` and `CheckpointTracker`.
- `LapManager` listens for completed laps and decides finish order.
- `PositionManager` calculates live standings from tracker progress.
- Power-ups, ML-Agents and the behavior classifier will connect to this base rather than replacing it.

## How To Verify The Base Prototype

1. Open `Assets/Scenes/SampleScene.unity` or a new race scene.
2. Run `Tools > Kart Racing > MVP Setup > Build Minimal Prototype Scene`.
3. Press Play.
4. Confirm the countdown runs.
5. Confirm the player moves with `WASD` or arrow keys.
6. Confirm AI karts move toward checkpoints.
7. Confirm `R` resets the player kart.

## Current Limitations

- The bot controller is heuristic, not ML-Agents yet.
- There is no HUD, countdown UI or results screen yet.
- No power-ups are active yet.
- The prototype track is logic-first and visually minimal.

## Next Planned Steps

1. Add the power-up inventory and item behaviors.
2. Add ML-Agents training scene and agent script.
3. Add player stats logging, CSV export and behavior classifier.
4. Add results UI and documentation updates per system.
