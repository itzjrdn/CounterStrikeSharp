using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace FlyingScoutsmanPlugin;

[MinimumApiVersion(80)]
public class FlyingScoutsmanPlugin : BasePlugin
{
    public override string ModuleName => "Flying Scoutsman Plugin";
    public override string ModuleVersion => "3.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp & Contributors";
    public override string ModuleDescription => "Stable Flying Scoutsman gamemode with perfect accuracy, low gravity, and SSG08-only gameplay";

    // Game state
    private bool _gameModeActive = false;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Flying Scoutsman Plugin v3.0 loaded!");
        
        // Register event handlers
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("Flying Scoutsman Plugin unloaded!");
        
        // Reset server settings if hot reloading
        if (hotReload && _gameModeActive)
        {
            ResetServerSettings();
        }
    }

    [ConsoleCommand("css_flyingsmap", "Load the Flying Scoutsman map")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnFlyingScoutsmanMapCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        Logger.LogInformation("Loading Flying Scoutsman map via server command...");
        
        // Execute server command to load the workshop map
        Server.ExecuteCommand("host_workshop_map 3512209106");
        
        // Activate game mode
        ActivateGameMode();
        
        commandInfo.ReplyToCommand("Flying Scoutsman map loading initiated!");
    }

    [ConsoleCommand("css_scoutsonly", "Toggle Flying Scoutsman mode")]
    [ConsoleCommand("scoutsonly", "Toggle Flying Scoutsman mode")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnScoutsOnlyCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_gameModeActive)
        {
            DeactivateGameMode();
            commandInfo.ReplyToCommand("Flying Scoutsman mode disabled!");
        }
        else
        {
            ActivateGameMode();
            commandInfo.ReplyToCommand("Flying Scoutsman mode enabled!");
        }
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        Logger.LogInformation("Flying Scoutsman: Round started");
        
        // Show HUD message to all players
        ShowRoundStartMessage();
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        Logger.LogInformation($"Flying Scoutsman: Player {player.PlayerName} spawned");
        
        // Give weapons with a delay to ensure player is fully spawned
        Server.NextFrame(() =>
        {
            if (player.IsValid && player.PawnIsAlive)
            {
                GivePlayerWeapons(player);
            }
        });
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        var player = @event.Userid;
        var weapon = @event.Weapon;
        
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        // Allow only SSG08 and knives
        if (weapon != "ssg08" && !weapon.Contains("knife"))
        {
            Logger.LogDebug($"Flying Scoutsman: Blocked {weapon} usage by {player.PlayerName}");
            return HookResult.Handled; // Block the shot
        }
        
        return HookResult.Continue;
    }

    private void ActivateGameMode()
    {
        _gameModeActive = true;
        
        // Apply server settings for Flying Scoutsman
        ApplyServerSettings();
        
        // Give all current players weapons
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive).ToList();
        foreach (var player in players)
        {
            GivePlayerWeapons(player);
        }
        
        // Notify all players
        Server.PrintToChatAll($"{ChatColors.LightBlue}[Flying Scoutsman] {ChatColors.White}Game mode activated!");
        Server.PrintToChatAll($"{ChatColors.Yellow}[Flying Scoutsman] {ChatColors.White}Float and Flick! SSG08 and knives only!");
        Server.PrintToChatAll($"{ChatColors.Green}[Flying Scoutsman] {ChatColors.White}Flying Scoutsman mode enabled!");
        
        Logger.LogInformation("Flying Scoutsman mode activated");
    }

    private void DeactivateGameMode()
    {
        _gameModeActive = false;
        
        // Reset server settings
        ResetServerSettings();
        
        // Notify all players
        Server.PrintToChatAll($"{ChatColors.LightRed}[Flying Scoutsman] {ChatColors.White}Game mode deactivated!");
        
        Logger.LogInformation("Flying Scoutsman mode deactivated");
    }

    private void ApplyServerSettings()
    {
        // Core Flying Scoutsman settings with CS2-compatible cvars only
        Server.ExecuteCommand("sv_gravity 200");                  // Low gravity
        Server.ExecuteCommand("sv_airaccelerate 2000");          // High air acceleration
        Server.ExecuteCommand("mp_buytime 0");                   // Disable buy menu entirely
        Server.ExecuteCommand("mp_buy_anywhere 0");              // Disable buy anywhere
        
        // Movement and accuracy enhancements
        Server.ExecuteCommand("sv_enablebunnyhopping 1");         // Enable bunnyhopping
        Server.ExecuteCommand("sv_autobunnyhopping 1");           // Auto bunnyhop
        Server.ExecuteCommand("sv_staminamax 0");                 // Disable stamina
        Server.ExecuteCommand("sv_staminalandcost 0");            // No crouch slowdown
        Server.ExecuteCommand("sv_staminajumpcost 0");            // No jump stamina cost
        
        // Perfect accuracy for weapons (CS2 compatible)
        Server.ExecuteCommand("weapon_accuracy_nospread 1");      // No spread
        Server.ExecuteCommand("sv_infinite_ammo 1");              // Infinite ammo
        
        Logger.LogInformation("Applied Flying Scoutsman server settings");
    }

    private void ResetServerSettings()
    {
        // Reset to default server settings
        Server.ExecuteCommand("sv_gravity 800");                  // Default gravity
        Server.ExecuteCommand("sv_airaccelerate 12");             // Default air acceleration  
        Server.ExecuteCommand("mp_buytime 60");                   // Default buy time
        Server.ExecuteCommand("mp_buy_anywhere 0");               // Keep buy anywhere disabled
        
        // Reset movement settings
        Server.ExecuteCommand("sv_enablebunnyhopping 0");         // Disable bunnyhopping
        Server.ExecuteCommand("sv_autobunnyhopping 0");           // Disable auto bunnyhop
        Server.ExecuteCommand("sv_staminamax 80");                // Default stamina
        Server.ExecuteCommand("sv_staminalandcost 30");           // Default crouch cost
        Server.ExecuteCommand("sv_staminajumpcost 8");            // Default jump cost
        
        // Reset accuracy settings
        Server.ExecuteCommand("weapon_accuracy_nospread 0");      // Default spread
        Server.ExecuteCommand("sv_infinite_ammo 0");              // Default ammo
        
        Logger.LogInformation("Reset server settings to defaults");
    }

    private void ShowRoundStartMessage()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        
        foreach (var player in players)
        {
            try
            {
                // Show HUD message
                player.PrintToCenter("Flying Scoutsman Mode Active – Float and Flick!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to show round start message to player {player.PlayerName}");
            }
        }
        
        Logger.LogDebug($"Showed round start message to {players.Count} players");
    }

    private void GivePlayerWeapons(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;

        try
        {
            // Remove all weapons first to prevent conflicts
            RemoveAllWeapons(player);
            
            // Add small delay before giving weapons
            Server.NextFrame(() =>
            {
                if (player.IsValid && player.PawnIsAlive)
                {
                    // Give SSG08
                    player.GiveNamedItem(CsItem.SSG08);
                    
                    // Give appropriate knife based on team
                    if (player.Team == CsTeam.Terrorist)
                    {
                        player.GiveNamedItem(CsItem.DefaultKnifeT);
                    }
                    else if (player.Team == CsTeam.CounterTerrorist)
                    {
                        player.GiveNamedItem(CsItem.DefaultKnifeCT);
                    }
                    
                    Logger.LogDebug($"Gave SSG08 and knife to {player.PlayerName}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to give weapons to player {player.PlayerName}");
        }
    }

    private void RemoveAllWeapons(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;

        try
        {
            var weaponServices = player.PlayerPawn.Value.WeaponServices;
            if (weaponServices == null) return;

            // Create a copy of the weapons list to avoid modification during iteration
            var weapons = weaponServices.MyWeapons.Where(w => w?.Value != null).ToList();
            
            foreach (var weapon in weapons)
            {
                if (weapon?.Value?.DesignerName != null)
                {
                    weapon.Value.Remove();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to remove weapons from player {player.PlayerName}");
        }
    }
}