# X-Ray Plugin for CounterStrikeSharp

A comprehensive X-Ray plugin that provides admin users with the ability to see enemy players and utilities through walls with intelligent glow effects.

## Features

### 🎯 Player-Specific Visibility
- **Individual XRay**: Only the player with XRay activated can see the glow effects
- **No more team-wide visibility**: Fixes the bug where entire teams could see glows when one player activated XRay
- **Controlled via CheckTransmit**: Uses CounterStrikeSharp's transmission system for precise visibility control

### 🌈 Health-Based Dynamic Colors
- **Smart color system**: Enemy glow changes color based on their current health
- **Green to Red spectrum**: 
  - 100 HP = Bright Green
  - 50 HP = Yellow/Orange  
  - 1 HP = Bright Red
- **Real-time updates**: Colors update immediately when enemies take damage

### 💥 Utility Detection & Glow
- **Complete utility tracking**: Detects and highlights all thrown utilities:
  - HE Grenades
  - Flashbangs
  - Smoke Grenades
  - Incendiary Grenades
  - Molotovs
  - Decoy Grenades
- **Red glow for utilities**: All enemy utilities glow bright red for easy identification
- **Automatic cleanup**: Utility glows are automatically removed when grenades explode or expire

### 🛡️ Crash Prevention & Stability
- **Proper entity management**: All glow entities are properly tracked and cleaned up
- **Disconnect handling**: Automatic cleanup when players disconnect
- **Round transition safety**: Cleans up effects between rounds
- **Memory leak prevention**: Comprehensive cleanup systems

## Commands

| Command | Description | Usage |
|---------|-------------|-------|
| `css_xray <player>` | Toggle X-Ray for a specific player | `css_xray john` |
| `css_removexray` | Remove X-Ray from all players | `css_removexray` |
| `css_listxray` | List players with active X-Ray | `css_listxray` |

## Installation

1. Copy `XRayPlugin.cs` and `XRayPlugin.csproj` to your CounterStrikeSharp plugins directory
2. Build the plugin using `dotnet build`
3. Load the plugin on your server

## Authorization

The plugin uses a hardcoded Steam ID for authorization. Update the `AUTHORIZED_STEAM_ID` constant in the code:

```csharp
private const ulong AUTHORIZED_STEAM_ID = 76561199076538983; // Replace with your Steam ID
```

## Technical Details

### Visibility Control
The plugin uses CounterStrikeSharp's `CheckTransmit` listener to control which players can see specific glow entities. This ensures that only players with XRay active can see the glow effects, fixing the team-wide visibility issue.

### Health Tracking
Player health is tracked through the `EventPlayerHurt` event and cached for real-time color updates. The color calculation uses a red-green spectrum to provide immediate visual feedback about enemy health status.

### Utility Detection
The plugin listens for `EventGrenadeThrown` and creates glow entities that follow the thrown utilities. Cleanup is handled through various detonation and expiration events.

## Version History

- **v5.0.0**: Major rewrite with player-specific visibility, health-based colors, and utility glow
- **v4.0.0**: Previous version with team-wide visibility issues

## Troubleshooting

### Glow visible to entire team
This has been fixed in v5.0.0. The plugin now uses CheckTransmit to ensure only XRay users see the glows.

### Colors not updating
Make sure the plugin is properly listening to player hurt events. Check server logs for any event registration errors.

### Utility glows not appearing
Verify that the utility model paths are correct for your server's game files. The plugin uses standard CS2 weapon models.

## Configuration

No external configuration files are needed. All settings are controlled through the hardcoded constants in the plugin source code.