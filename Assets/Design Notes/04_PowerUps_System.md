# Power-Ups System

## Status

Planned. Not implemented yet in this pass.

## Planned Scripts

- `PowerUpType.cs`
- `PowerUpManager.cs`
- `PowerUpBox.cs`
- `PowerUpInventory.cs`
- `PowerUpBase.cs`
- `BananaPowerUp.cs`
- `ShellPowerUp.cs`
- `MushroomBoostPowerUp.cs`
- `StarPowerUp.cs`
- `BotPowerUpUser.cs`

## Planned Responsibilities

- Assign a power-up based on race position.
- Store one equipped power-up per racer.
- Let player and bots use items.
- Apply boost, stun and invincibility through `KartController`.

## Current Integration Notes

- `PlayerKartInput` already reserves `Space` for item usage through `UseStoredPowerUp`.
- `PositionManager` is ready to feed the probability table.

## Next Update

This file should be expanded once the first power-up loop is implemented.
