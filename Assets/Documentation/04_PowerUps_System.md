# Power-Ups System

## Status

Implemented (core loop complete). Banana and Shell are reserved for future trap/projectile pass.

## Scripts Implemented

- `Assets/Scripts/AI/PowerUps/PowerUpType.cs` — enum: None, MushroomBoost, Star, Banana, Shell
- `Assets/Scripts/AI/PowerUps/PowerUpManager.cs` — static helper that picks a power-up by race position
- `Assets/Scripts/AI/PowerUps/PowerUpInventory.cs` — per-kart component; stores and applies one item
- `Assets/Scripts/AI/PowerUps/BotPowerUpUser.cs` — AI bot auto-use logic (waits for a smart moment)
- `Assets/Scripts/Core/PowerUpBox.cs` — physical item box on track (trigger, respawns after delay)

## How The Loop Works

1. A kart's collider enters a `PowerUpBox` trigger on the track.
2. `PowerUpBox` asks `PositionManager` for that kart's live race position.
3. `PowerUpBox` calls `PowerUpInventory.TryAssignByRacePosition(position, racerCount)`.
4. `PowerUpInventory` stores the item (one item max; box is ignored if kart already has one).
5. **Player** presses `Space` → `PlayerKartInput` sends `UseStoredPowerUp` message → `PowerUpInventory.UseStoredPowerUp()` fires.
6. **Bots** are handled by `BotPowerUpUser`, which waits a random delay and then uses the item when aligned with the next checkpoint (for Mushroom) or immediately (for Star).
7. The box hides for `respawnDelay` seconds (default 8s) then reappears.

## Item Probability Table

Implemented in `PowerUpManager.GetPowerUpForPosition()`:

| Position band | Most likely item | Roll threshold |
|---|---|---|
| Top 33 % (leading) | Star | 75 % Star, 25 % Mushroom |
| Middle 33 % | Mushroom | 55 % Mushroom, 45 % Star |
| Bottom 33 % (last) | Mushroom | 80 % Mushroom, 20 % Star |

Banana and Shell are defined but log a warning and do nothing yet.

## Setup In The Race Scene

### Player kart

1. Add `PowerUpInventory` to the player kart root.
2. Tune `mushroomBoostMultiplier`, `mushroomBoostDuration`, `starDuration` in the Inspector.
3. Press `Space` in game to use the stored item.

### Bot karts

1. Add `PowerUpInventory` + `BotPowerUpUser` to each bot kart root.
2. `BotPowerUpUser` picks up timing automatically.

### Item boxes

1. Create a GameObject on the track, attach a `BoxCollider` and `PowerUpBox`.
2. Assign a child mesh as `visualRoot` (the mesh to hide on pickup).
3. Place boxes in PowerUpBoxes child container under TrackRoot for organisation.

### HUD

`RaceHUD` has an optional `itemText` (TextMeshProUGUI) field. Assign it to show the player's current item and the key hint. Leave unassigned to skip.

## KartController Integration

`PowerUpInventory` calls directly into `KartController`:
- `MushroomBoost` → `ApplyBoost(multiplier, duration)` — temporary top-speed increase.
- `Star` → `ApplyInvincibility(duration)` — sets `IsInvincible = true` for the duration (collision damage immunity hook is on the receiving kart's side).

## Future Work

- Banana: drop a trap object behind the kart that stuns karts that touch it.
- Shell: fire a projectile forward that stuns the first kart it hits.
- Sound effects and particle effects on pickup and use.
- Animated spin on the item box while available.
