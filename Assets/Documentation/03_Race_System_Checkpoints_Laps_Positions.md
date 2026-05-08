# Race System, Checkpoints, Laps And Positions

## Purpose

This system coordinates the race flow and calculates objective progress for every racer.

## Scripts Involved

- `Assets/Scripts/Core/TrackData.cs`
- `Assets/Scripts/Core/Checkpoint.cs`
- `Assets/Scripts/Kart/CheckpointTracker.cs`
- `Assets/Scripts/Core/LapManager.cs`
- `Assets/Scripts/Core/PositionManager.cs`
- `Assets/Scripts/Core/RaceManager.cs`

## System Responsibilities

### `TrackData.cs`

Stores:

- Ordered checkpoint transforms.
- Spawn points.
- Power-up box points.
- Respawn points.
- Number of laps required to win.

Attach to:

- `TrackRoot`

### `Checkpoint.cs`

Responsibilities:

- Mark a trigger as a race checkpoint.
- Tell the kart tracker that a checkpoint was crossed.
- Hold the checkpoint index inside the loop.

Attach to:

- Each checkpoint trigger object.

Required components:

- Trigger `Collider`

### `CheckpointTracker.cs`

Responsibilities:

- Keep per-racer lap count.
- Keep last checkpoint reached.
- Keep next checkpoint target.
- Support respawn at last valid recovery point.
- Auto-respawn blocked bots while keeping the player on manual respawn with `R` by default.

Attach to:

- Every kart.

### `LapManager.cs`

Responsibilities:

- Listen to `CheckpointTracker` lap completion events.
- Detect when a racer reaches the required lap count.
- Assign final finish order.

Attach to:

- `RaceSystems`

### `PositionManager.cs`

Responsibilities:

- Compute live standings using:
  - completed laps,
  - last checkpoint index,
  - distance to next checkpoint.

Attach to:

- `RaceSystems`

### `RaceManager.cs`

Responsibilities:

- Find and register racers.
- Place racers on spawn points.
- Run the starting countdown.
- Enable and disable control.
- Finish the race when the player finishes or everyone finishes.

Attach to:

- `RaceSystems`

## Checkpoint Ordering Rule

The current implementation assumes:

- Checkpoints are ordered in loop order.
- The last checkpoint in the array completes the lap.

For the prototype generator, this is handled automatically. If you build a custom track manually, keep that rule in mind.

If you already have checkpoint objects nested inside modular track pieces, you can repair or populate the `TrackData` array with:

- `Tools > Kart Racing > Track Data > Populate Checkpoints From Source`

## Inspector Setup

### `TrackData`

- Fill `Checkpoints` in loop order.
- Fill `SpawnPoints` in grid order.
- Fill `RespawnPoints` for safe recovery.
- Set `Laps To Win` to `3` for the MVP.
- Use `Draw Gizmos` to show checkpoint routing and `Close Checkpoint Loop Gizmo` if you want the last gizmo line to connect back to the first checkpoint.
- For modular scenes, use the Track Data populate tool to collect checkpoint transforms recursively from another hierarchy root.

### `RaceManager`

- Assign `TrackData`.
- Assign `LapManager`.
- Assign `PositionManager`.
- Enable auto registration unless you want to manage racers manually.

### `CheckpointTracker`

- Set `Is Player` only on the human kart.
- Enable `Auto Respawn If Stuck` for bots.
- Leave player auto-respawn disabled unless you explicitly want forced recovery.

## How It Connects To Other Systems

- Bots use `CheckpointTracker.NextCheckpoint`.
- Power-up probabilities will use `PositionManager`.
- Behavior logging will use final position, lap time and off-track events.
- Results UI will use `RaceManager`, `LapManager` and `PositionManager`.

## How To Test

1. Build the prototype scene from the setup tools.
2. Press Play.
3. Confirm the countdown happens before karts start moving.
4. Drive through the full checkpoint loop.
5. Verify that laps increase after crossing the last checkpoint in sequence.
6. Verify that AI karts update their relative order while racing.
7. Confirm the race ends when the player finishes if `Finish Race When Player Finishes` is enabled.

## Current Limitations

- There is no visible HUD for lap and position yet.
- Wrong-direction penalties are not implemented yet.
- Off-track zones are not wired yet.

## Future Improvements

- Add HUD bindings for lap, time and position.
- Add explicit finish line and wrong-direction detection.
- Add sector times and minimap support.
- Add custom respawn points per track segment.
