# InvisPlugin for CounterStrikeSharp

A robust invisibility plugin for CS2 servers using CounterStrikeSharp that provides sound-based invisibility mechanics without causing client crashes.

## Features

- **Crash-Free Operation**: Never causes client crashes (no CopyExistingEntity errors)
- **Sound-Based Detection**: Invisibility breaks only on actual sound events, not movement or button presses
- **Robust API Usage**: Uses official CS2 sound events and CheckTransmit for reliable functionality
- **Proper Cleanup**: Ensures players are made visible before death/disconnect to prevent crashes
- **Admin Controls**: Console commands for managing invisibility states

## How It Works

### Invisibility Mechanism
The plugin uses CounterStrikeSharp's `CheckTransmit` listener to control what entities are transmitted to each client. When a player is invisible, their pawn entity is not transmitted to other players, making them invisible.

### Sound Detection
Invisibility is broken by these sound-producing events:
- **Weapon Fire** (`weapon_fire`) - Shooting any weapon
- **Player Jump** (`player_jump`) - Jumping (landing makes sound)
- **Player Footstep** (`player_footstep`) - Moving with audible footsteps
- **Weapon Reload** (`weapon_reload`) - Reloading weapons
- **Item Pickup** (`item_pickup`) - Picking up items (conservative approach)

### What Does NOT Break Invisibility
- Releasing shift key
- Moving slowly without making footstep sounds
- Looking around
- Crouching silently
- Standing still

## Console Commands

All commands require `@css/root` permissions:

- `css_invis <player_name>` - Make a player invisible
- `css_visible <player_name>` - Make a player visible
- `css_listinvis` - List all currently invisible players
- `css_clearinvis` - Make all players visible
- `css_toggleinvis` - Toggle your own invisibility (client-only, useful for testing)

## Installation

1. Place the compiled `InvisPlugin.dll` in your CounterStrikeSharp plugins directory
2. Restart the server or use hot reload
3. Plugin will automatically register all event handlers and the CheckTransmit listener

## Technical Details

### Crash Prevention
- Players are automatically made visible before death (`player_death` event)
- Players are removed from invisibility tracking on disconnect (`player_disconnect` event)
- All invisibility states are cleared on round start for clean state management

### Event-Driven Architecture
The plugin only processes invisibility changes when actual sound events occur, making it efficient and responsive. It doesn't rely on timers or constant polling.

### Sound Event Accuracy
The plugin uses the game's own sound event system (`player_footstep`, `weapon_fire`, etc.) to determine when sounds are actually produced, ensuring accuracy with the game's audio engine.

## Configuration

No configuration files are needed. The plugin works out of the box with sensible defaults.

## Requirements

- CounterStrikeSharp API version 276 or higher
- .NET 8.0 runtime
- CS2 server with CounterStrikeSharp installed

## Building from Source

```bash
cd examples/InvisPlugin
dotnet build
```

The compiled DLL will be in `bin/Debug/net8.0/InvisPlugin.dll`.

## License

This plugin follows the same license as CounterStrikeSharp.