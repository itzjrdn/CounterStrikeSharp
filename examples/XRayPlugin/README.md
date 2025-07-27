# X-Ray Plugin for CounterStrikeSharp

A CounterStrikeSharp plugin that provides X-Ray functionality for administrators, allowing them to highlight enemy players with a glow effect similar to spectator X-Ray using dynamic prop entities.

## Features

- **Steam ID-based authorization**: Restricted to specific authorized Steam ID
- **Player search**: Find players by partial name matching
- **Team-based targeting**: Only highlights opposing team members
- **Error handling**: Comprehensive validation and user feedback
- **Multiple commands**: Apply, remove, and list X-Ray effects
- **Advanced prop entity system**: Uses dynamic prop entities for reliable glow visibility

## Commands

### `/css_xray <player_name_part>`
Applies or removes X-Ray effect for the specified player (toggle functionality). When applied, enemy team members glow bright green and are visible through walls.

**Usage Examples:**
- `/css_xray john` - Toggles X-Ray for player with "john" in their name
- `/css_xray Player123` - Toggles X-Ray for player with "Player123" in their name

**Requirements:**
- Authorized Steam ID (76561199076538983)
- Target player must be in a valid team (T/CT, not spectator)
- Player name part must match exactly one player

**Behavior:**
- If X-Ray is not active: Applies X-Ray effect
- If X-Ray is already active: Removes X-Ray effect

### `/css_removexray`
Removes all active X-Ray effects from all players.

**Requirements:**
- Authorized Steam ID (76561199076538983)

### `/css_listxray`
Lists all players who currently have X-Ray effects active.

**Requirements:**
- Authorized Steam ID (76561199076538983)

## Installation

1. Copy `XRayPlugin.dll` to your CounterStrikeSharp plugins directory
2. Restart your server or reload plugins
3. Ensure the authorized Steam ID is correctly set in the code

## How It Works

The plugin uses an advanced prop entity system for reliable X-Ray visibility:

1. When X-Ray is applied to a player, the plugin creates dynamic prop entities for each enemy
2. These entities copy the enemy player's model and follow their movements
3. A bright green glow effect is applied to these entities with through-wall visibility
4. The X-Ray player can see glowing outlines of enemies through walls during live gameplay
5. The effect persists until manually removed, toggled off, or server restart
6. All entities are automatically cleaned up when players disconnect

## Technical Implementation

- **Prop Entity System**: Creates `prop_dynamic` entities that follow enemy players
- **Two-Entity Approach**: Uses a relay entity (invisible) and a glow entity (visible)
- **Model Copying**: Copies the exact player model for accurate representation
- **Follow Mechanics**: Uses `FollowEntity` input to track player movement
- **Automatic Cleanup**: Properly destroys entities when no longer needed

## Additional Features

- **Toggle Functionality**: Using `/css_xray` on a player who already has X-Ray will remove it
- **Automatic Cleanup**: Players and entities are removed from tracking when they disconnect
- **Smart Team Detection**: Only highlights actual opposing teams (T vs CT)
- **Entity Management**: Efficiently manages and cleans up dynamic entities
- **Event-Driven Updates**: Updates X-Ray effects based on game events (spawn, team change, etc.)

## Technical Details

- **API Version**: CounterStrikeSharp 1.0.328+
- **Framework**: .NET 8.0
- **Glow Implementation**: Uses `CGlowProperty` with type 3 for through-wall visibility on prop entities
- **Entity System**: Dynamic prop entities with FollowEntity mechanics
- **Team Detection**: Supports Terrorist vs Counter-Terrorist team opposition
- **Authorization**: Hardcoded Steam ID-based access control

## Performance Considerations

- **Efficient Entity Management**: Only creates entities for actual enemies
- **Event-Driven**: Updates only when necessary (player spawn, team change, round start)
- **Proper Cleanup**: Automatically destroys unused entities to prevent memory leaks
- **Smart Tracking**: Maintains entity dictionaries for fast lookup and cleanup

## Limitations

- Currently restricted to one specific Steam ID (easily configurable)
- Only works for players in active teams (T/CT)
- Creates additional entities on the server (minimal performance impact)

## Error Messages

- `"You are not authorized to use this command."` - User lacks required authorization
- `"No players found matching '<n>'"` - No players match the search criteria
- `"Multiple players found matching '<n>': <list>"` - Multiple matches found, be more specific
- `"Player '<n>' has no valid pawn"` - Player entity is invalid
- `"Player '<n>' is not in a valid team (T/CT)"` - Player is spectator or unassigned
- `"No X-Ray effects are currently active"` - No players have X-Ray when trying to remove all

## Version History

- **v4.0.1**: Fixed issue where XRay glow effects only appeared after players respawned - now appears immediately on existing players
- **v4.0.0**: Major rewrite using dynamic prop entity system for reliable X-Ray visibility
- **v3.0.0**: Event-driven approach with improved glow configuration
- **v2.0.0**: Enhanced features and Steam ID authorization
- **v1.0.0**: Initial implementation with basic glow functionality

## License

This plugin is part of the CounterStrikeSharp project and follows the same licensing terms.