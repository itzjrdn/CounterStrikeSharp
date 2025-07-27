# Flying Scoutsman Plugin

A simple and efficient Flying Scoutsman gamemode plugin for CounterStrikeSharp that focuses on core mechanics without complex team management or scoring systems.

## Features

### Core Mechanics
- **Low Gravity**: Set to 200 (vs default 800) for enhanced aerial movement
- **High Air Acceleration**: Set to 2000 (vs default 12) for smooth in-air movement
- **No Fall Damage**: Complete fall damage elimination
- **No Crouch Slowdown**: Full speed maintained while crouching mid-air
- **Perfect Accuracy**: Both scoped and no-scope shots are fully accurate while airborne

### Weapon Management
- **SSG08 Only**: Players automatically receive SSG08 and knife on spawn
- **Buy Menu Disabled**: Complete buy menu lockout during gamemode
- **Pickup Prevention**: Blocks pickup of non-allowed weapons from ground

### User Experience
- **Round Start Message**: "Flying Scoutsman Mode Active – Float and Flick!" displayed on HUD
- **Simple Toggle**: `/scoutsonly` command to enable/disable the gamemode
- **Map Loading**: `css_flyingsmap` command loads the Flying Scoutsman workshop map

## Commands

| Command | Description |
|---------|-------------|
| `css_flyingsmap` | Load Flying Scoutsman workshop map and activate gamemode |
| `css_scoutsonly` or `scoutsonly` | Toggle Flying Scoutsman mode on/off |

## Installation

1. Build the plugin using the .NET 8 SDK:
   ```bash
   dotnet build FlyingScoutsmanPlugin.csproj
   ```

2. Copy the generated DLL to your CounterStrikeSharp plugins directory:
   ```
   addons/counterstrikesharp/plugins/FlyingScoutsmanPlugin/
   ```

3. Restart your server or use hot reload if supported.

## Configuration

The plugin uses server console variables (cvars) to control game mechanics:

### Active Gamemode Settings
- `sv_gravity 200` - Low gravity for floating movement
- `sv_airaccelerate 2000` - High air acceleration for smooth control
- `mp_falldamage 0` - Disable fall damage
- `mp_buytime 0` - Disable buy menu entirely
- `weapon_accuracy_nospread 1` - Perfect accuracy
- `weapon_recoil_scale 0` - No recoil
- `sv_enablebunnyhopping 1` - Enable bunnyhopping
- `sv_autobunnyhopping 1` - Auto bunnyhop
- `sv_staminamax 0` - Disable stamina system
- `sv_staminalandcost 0` - No crouch slowdown
- `sv_staminajumpcost 0` - No jump stamina cost

### Default Settings (when disabled)
- `sv_gravity 800` - Normal gravity
- `sv_airaccelerate 12` - Default air acceleration
- `mp_falldamage 1` - Normal fall damage
- `mp_buytime 60` - Default buy time
- All accuracy and movement settings reset to defaults

## Technical Details

- **API Compatibility**: CounterStrikeSharp API v1.0.328+ (`MinimumApiVersion(80)`)
- **Event Handling**: Minimal event processing for performance
- **Error Safety**: Comprehensive try-catch blocks prevent crashes
- **Entity Management**: Careful weapon handling to prevent server entity overflow

## Usage

1. **Load Map and Activate**: Use `css_flyingsmap` to load the workshop map and automatically enable the gamemode
2. **Manual Toggle**: Use `css_scoutsonly` to toggle the gamemode on any map
3. **Players**: Will automatically receive SSG08 and knife on spawn, buy menu disabled
4. **Movement**: Enjoy enhanced aerial movement with perfect accuracy

The plugin automatically handles all server settings and player weapons without requiring additional configuration.