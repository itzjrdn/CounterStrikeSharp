# X-Ray Plugin for CounterStrikeSharp

A CounterStrikeSharp plugin that provides X-Ray functionality for administrators, allowing them to highlight enemy players with a glow effect similar to spectator X-Ray.

## Features

- **Admin-only command**: Restricted to users with `@css/admin` permission
- **Player search**: Find players by partial name matching
- **Team-based targeting**: Only highlights opposing team members
- **Error handling**: Comprehensive validation and user feedback
- **Multiple commands**: Apply, remove, and list X-Ray effects

## Commands

### `/css_xray <player_name_part>`
Applies X-Ray effect to the specified player, making enemy team members glow red and visible through walls.

**Usage Examples:**
- `/css_xray john` - Applies X-Ray to player with "john" in their name
- `/css_xray Player123` - Applies X-Ray to player with "Player123" in their name

**Requirements:**
- `@css/admin` permission
- Target player must be in a valid team (T/CT, not spectator)
- Player name part must match exactly one player

### `/css_removexray`
Removes all active X-Ray effects from all players.

**Requirements:**
- `@css/admin` permission

### `/css_listxray`
Lists all players who currently have X-Ray effects active.

**Requirements:**
- `@css/admin` permission

## Installation

1. Copy `XRayPlugin.dll` to your CounterStrikeSharp plugins directory
2. Restart your server or reload plugins
3. Ensure admin permissions are properly configured

## How It Works

1. When X-Ray is applied to a player, all opposing team members will glow bright red
2. The glow effect is visible through walls (similar to spectator X-Ray)
3. Only enemies are highlighted - teammates and the target player are not affected
4. The effect persists until manually removed or server restart

## Technical Details

- **API Version**: CounterStrikeSharp 1.0.328+
- **Framework**: .NET 8.0
- **Glow Implementation**: Uses `CGlowProperty` with type 3 for through-wall visibility
- **Team Detection**: Supports Terrorist vs Counter-Terrorist team opposition

## Limitations

- The glow effect is visible to all players, not just the target player
- Only works for players in active teams (T/CT)
- Requires admin permissions to use

## Error Messages

- `"No players found matching '<name>'"` - No players match the search criteria
- `"Multiple players found matching '<name>': <list>"` - Multiple matches found, be more specific
- `"Player '<name>' has no valid pawn"` - Player entity is invalid
- `"Player '<name>' is not in a valid team (T/CT)"` - Player is spectator or unassigned
- `"X-Ray is already active for player '<name>'"` - X-Ray already applied to this player

## License

This plugin is part of the CounterStrikeSharp project and follows the same licensing terms.