# Flying Scoutsman Plugin for CounterStrikeSharp

A comprehensive Flying Scoutsman gamemode plugin for CounterStrikeSharp, featuring full-fledged gameplay mechanics with 5v5 teams, first-to-13 rounds scoring, weapon restrictions, and flying mechanics.

## Features

### Server-Side Map Loading
- Command `/FlyingSMap` executes `host_workshop_map 3512209106` on the server console
- Automatically loads the Flying Scoutsman map from the workshop

### Game Rules
- **5v5 Teams**: Automatically creates and balances teams for competitive gameplay
- **First to 13**: Enforces "Best of 25" rule - first team to win 13 rounds wins the match
- **Flying Mechanics**: Reduced gravity (30% of normal) for enhanced aerial movement
- **Weapon Restrictions**: Players can only use SSG 08 (Scout) and knives

### Advanced Game Logic
- Automatically respawns players each round
- Real-time score tracking and winner declaration
- Comprehensive round management with automated team switching
- Weapon enforcement with automatic removal of restricted weapons

### User Experience
- **HUD Updates**: Real-time score display in center of screen
- **Chat Messages**: Detailed game state information and announcements
- **Winner Celebration**: Special announcements and HUD displays for match winners

## Commands

- `css_flyingsmap` - Load the Flying Scoutsman map and activate game mode
- `css_fs_start` - Start Flying Scoutsman mode on current map
- `css_fs_stop` - Stop Flying Scoutsman mode and reset players

## Installation

1. Place the compiled `FlyingScoutsmanPlugin.dll` in your CounterStrikeSharp plugins directory
2. The plugin will automatically load on server start
3. Use the commands above to activate the gamemode

## API Compatibility

- Requires CounterStrikeSharp API version 80 or higher
- Built for .NET 8.0
- Compatible with CounterStrikeSharp version 1.0.328+

## Technical Details

### Flying Mechanics
The plugin applies a gravity scale of 0.3 to all players, providing the characteristic "flying" movement of the gamemode while maintaining responsive controls.

### Team Balancing
Players are automatically distributed between Terrorist and Counter-Terrorist teams using a randomized alternating assignment system.

### Weapon System
- Automatically removes all restricted weapons from players
- Provides SSG 08 and appropriate team knife on spawn
- Monitors weapon fire events to enforce restrictions

### Score Management
- Tracks rounds won by each team
- Displays real-time scores via HUD
- Automatically declares winners when 13 rounds are reached

## Events Handled

- `round_start` - Applies flying mechanics, balances teams, enforces weapon restrictions
- `round_end` - Updates scores, checks for game winner
- `player_spawn` - Applies flying mechanics and weapon restrictions to individual players
- `player_death` - Tracks player deaths for respawn management
- `weapon_fire` - Enforces weapon restrictions in real-time

This plugin provides a complete Flying Scoutsman experience with all the features expected from the popular gamemode, ensuring competitive integrity while maintaining the fun and accessibility that makes Flying Scoutsman enjoyable for all skill levels.