# Kart Controller

## Purpose

The kart control layer handles movement, player input, temporary bot driving and the follow camera. It is the base that later power-ups, hit reactions and ML-Agents will extend.

## Scripts Involved

- `Assets/Scripts/Kart/KartController.cs`
- `Assets/Scripts/Kart/PlayerKartInput.cs`
- `Assets/Scripts/Kart/AIKartInput.cs`
- `Assets/Scripts/Kart/CameraFollow.cs`
- `Assets/Scripts/Kart/CheckpointTracker.cs`

## What Each Script Does

### `KartController.cs`

Responsibilities:

- Accelerate and reverse with Rigidbody physics.
- Steer with arcade-style turning.
- Apply braking.
- Clamp top speed.
- Apply boost, stun and invincibility state.
- Reset kart transform and velocity.

Attach to:

- Player kart root.
- AI kart root.

Required components:

- `Rigidbody`
- `Collider`

### `PlayerKartInput.cs`

Responsibilities:

- Read `WASD` and arrow keys.
- Forward steering, acceleration and braking to `KartController`.
- Trigger respawn with `R`.
- Reserve `Space` for power-up usage.

Attach to:

- Player kart root.

Required components:

- `KartController`

### `AIKartInput.cs`

Responsibilities:

- Drive toward the next checkpoint.
- Reduce speed in tighter turns.
- Apply simple obstacle avoidance.
- Serve as the temporary bot brain before ML-Agents integration.

Attach to:

- AI kart root.

Required components:

- `KartController`
- `CheckpointTracker`

### `CameraFollow.cs`

Responsibilities:

- Follow the player from behind.
- Smooth camera movement.
- Add velocity-based look-ahead.

Attach to:

- Main camera.

Required components:

- `Camera`

### `CheckpointTracker.cs`

Responsibilities:

- Track lap progress per kart.
- Store next checkpoint target.
- Provide live progress data for positions.
- Store the recovery point used by respawn.
- Auto-respawn stuck AI karts while keeping player respawn manual by default.

Attach to:

- Player kart root.
- AI kart root.

## Inspector Setup

### `KartController`

Tune these values first:

- `Acceleration Force`: how quickly the kart picks up speed.
- `Reverse Acceleration Force`: reverse speed strength.
- `Max Speed`: planar speed cap.
- `Steering Strength`: turn rate.
- `Braking Force`: how hard the kart slows down when braking.
- `Lateral Grip`: how much side slip is removed each physics step.

### `PlayerKartInput`

- `Reverse Speed Threshold`: defines when `S` brakes versus when it starts reversing.
- `Force Enable Control On Input`: re-enables the player kart if race flow left control disabled unexpectedly during testing.

### `AIKartInput`

- `Steering Sensitivity`
- `Brake Angle`
- `Corner Acceleration`
- `Obstacle Ray Distance`

### `CameraFollow`

- `Offset`
- `Look Offset`
- `Follow Smooth Time`
- `Rotation Lerp Speed`

## How It Connects To Other Systems

- `RaceManager` enables or disables kart control during countdown and finish.
- `CheckpointTracker` feeds progress to `LapManager` and `PositionManager`.
- Future power-up scripts will call:
  - `ApplyBoost`
  - `ApplyStun`
  - `ApplyInvincibility`
- Future ML-Agents code can replace `AIKartInput` without changing `KartController`.

## How To Test

1. Use the MVP setup tool.
2. Press Play.
3. Check that the player accelerates with `W`.
4. Check that `S` brakes the kart before reversing.
5. Check that `A` and `D` turn the kart.
6. Flip or stop the kart and press `R`.
7. Confirm AI karts move between checkpoints without direct player input.

## Current Limitations

- The controller is arcade-focused and not drift-heavy yet.
- There is no suspension or wheel animation system.
- AI logic is deterministic and simple.

## Future Improvements

- Add drift and mini-turbo support.
- Add visual wheel steering and leaning.
- Add terrain-based grip modifiers.
- Replace `AIKartInput` with ML-Agents policy control.
