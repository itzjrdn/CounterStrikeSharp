using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlyingScoutsmanPlugin;

[MinimumApiVersion(80)]
public class FlyingScoutsmanPlugin : BasePlugin
{
    public override string ModuleName => "Flying Scoutsman Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp & Contributors";
    public override string ModuleDescription => "A full-featured Flying Scoutsman gamemode with 5v5 teams, first to 13 rounds, weapon restrictions, and flying mechanics";

    // Game state
    private bool _gameModeActive = false;
    private int _roundsPlayed = 0;
    private int _terroristRounds = 0;
    private int _counterTerroristRounds = 0;
    private const int MAX_ROUNDS = 13;
    private const float GRAVITY_SCALE = 0.3f; // Reduced gravity for flying mechanics
    private readonly List<CCSPlayerController> _playersToRespawn = new();

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Flying Scoutsman Plugin loaded!");
        
        // Register event handlers
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        
        // If hot reload, apply to existing players
        if (hotReload)
        {
            ApplyFlyingMechanicsToAllPlayers();
        }
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("Flying Scoutsman Plugin unloaded!");
        
        // Reset gravity for all players if hot reloading
        if (hotReload)
        {
            ResetGravityForAllPlayers();
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
        _gameModeActive = true;
        _roundsPlayed = 0;
        _terroristRounds = 0;
        _counterTerroristRounds = 0;
        
        // Notify all players
        Server.PrintToChatAll($"{ChatColors.LightBlue}[Flying Scoutsman] {ChatColors.White}Loading Flying Scoutsman map!");
        Server.PrintToChatAll($"{ChatColors.Green}[Flying Scoutsman] {ChatColors.White}Game mode activated! First to {MAX_ROUNDS} rounds wins!");
        
        commandInfo.ReplyToCommand("Flying Scoutsman map loading initiated!");
    }

    [ConsoleCommand("css_fs_start", "Start Flying Scoutsman mode")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnStartFlyingScoutsmanCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _gameModeActive = true;
        _roundsPlayed = 0;
        _terroristRounds = 0;
        _counterTerroristRounds = 0;
        
        // Balance teams
        BalanceTeams();
        
        // Apply flying mechanics to all players
        ApplyFlyingMechanicsToAllPlayers();
        
        Server.PrintToChatAll($"{ChatColors.LightBlue}[Flying Scoutsman] {ChatColors.White}Game mode started! First to {MAX_ROUNDS} rounds wins!");
        UpdateScoreHUD();
        
        commandInfo.ReplyToCommand("Flying Scoutsman mode started!");
    }

    [ConsoleCommand("css_fs_stop", "Stop Flying Scoutsman mode")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnStopFlyingScoutsmanCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _gameModeActive = false;
        ResetGravityForAllPlayers();
        
        Server.PrintToChatAll($"{ChatColors.LightRed}[Flying Scoutsman] {ChatColors.White}Game mode stopped!");
        
        commandInfo.ReplyToCommand("Flying Scoutsman mode stopped!");
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        Logger.LogInformation("Flying Scoutsman: Round started");
        
        // Clear respawn list
        _playersToRespawn.Clear();
        
        // Increment round counter
        _roundsPlayed++;
        
        // Ensure teams are balanced
        BalanceTeams();
        
        // Apply flying mechanics to all players
        Server.NextFrame(() =>
        {
            ApplyFlyingMechanicsToAllPlayers();
            RemoveRestrictedWeapons();
            EquipAllowedWeapons();
        });
        
        // Update HUD
        UpdateScoreHUD();
        
        // Announce round info
        Server.PrintToChatAll($"{ChatColors.Green}[Flying Scoutsman] {ChatColors.White}Round {_roundsPlayed} - T:{_terroristRounds} CT:{_counterTerroristRounds}");
        Server.PrintToChatAll($"{ChatColors.Yellow}[Flying Scoutsman] {ChatColors.White}Only SSG 08 and knives allowed!");
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        Logger.LogInformation($"Flying Scoutsman: Round ended, winner: {@event.Winner}");
        
        // Update scores based on winner
        if (@event.Winner == (int)CsTeam.Terrorist)
        {
            _terroristRounds++;
        }
        else if (@event.Winner == (int)CsTeam.CounterTerrorist)
        {
            _counterTerroristRounds++;
        }
        
        // Check for game winner
        if (_terroristRounds >= MAX_ROUNDS)
        {
            DeclareWinner(CsTeam.Terrorist);
        }
        else if (_counterTerroristRounds >= MAX_ROUNDS)
        {
            DeclareWinner(CsTeam.CounterTerrorist);
        }
        else
        {
            // Update HUD with new scores
            UpdateScoreHUD();
            
            // Announce scores
            Server.PrintToChatAll($"{ChatColors.LightBlue}[Flying Scoutsman] {ChatColors.White}Round {_roundsPlayed} complete!");
            Server.PrintToChatAll($"{ChatColors.Green}[Flying Scoutsman] {ChatColors.White}Score - T:{_terroristRounds} CT:{_counterTerroristRounds}");
            
            int remaining = MAX_ROUNDS - Math.Max(_terroristRounds, _counterTerroristRounds);
            Server.PrintToChatAll($"{ChatColors.Yellow}[Flying Scoutsman] {ChatColors.White}{remaining} more round(s) needed to win!");
        }
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        Logger.LogInformation($"Flying Scoutsman: Player {player.PlayerName} spawned");
        
        // Apply flying mechanics with a slight delay to ensure player is fully spawned
        Server.NextFrame(() =>
        {
            ApplyFlyingMechanics(player);
            
            // Remove all weapons and give allowed ones
            RemoveAllWeapons(player);
            GiveAllowedWeapons(player);
        });
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        var victim = @event.Userid;
        if (victim == null || !victim.IsValid || victim.IsBot) return HookResult.Continue;

        Logger.LogInformation($"Flying Scoutsman: Player {victim.PlayerName} died");
        
        // Add to respawn list for next round
        if (!_playersToRespawn.Contains(victim))
        {
            _playersToRespawn.Add(victim);
        }
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!_gameModeActive) return HookResult.Continue;

        var player = @event.Userid;
        var weapon = @event.Weapon;
        
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        // Check if weapon is allowed
        if (!IsWeaponAllowed(weapon))
        {
            Logger.LogInformation($"Flying Scoutsman: Player {player.PlayerName} fired restricted weapon: {weapon}");
            
            // Remove the weapon and give SSG08
            Server.NextFrame(() =>
            {
                RemoveAllWeapons(player);
                GiveAllowedWeapons(player);
                player.PrintToChat($"{ChatColors.Red}[Flying Scoutsman] {ChatColors.White}Only SSG 08 and knives allowed!");
            });
        }
        
        return HookResult.Continue;
    }

    private void BalanceTeams()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected).ToList();
        
        if (players.Count < 2) return;

        Logger.LogInformation($"Flying Scoutsman: Balancing teams with {players.Count} players");
        
        // Shuffle players for random distribution
        var random = new Random();
        players = players.OrderBy(x => random.Next()).ToList();
        
        // Assign players to teams alternately
        for (int i = 0; i < players.Count; i++)
        {
            var team = (i % 2 == 0) ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
            
            if (players[i].Team != team)
            {
                players[i].ChangeTeam(team);
            }
        }
        
        var tCount = players.Count(p => p.Team == CsTeam.Terrorist);
        var ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        
        Server.PrintToChatAll($"{ChatColors.LightBlue}[Flying Scoutsman] {ChatColors.White}Teams balanced: T({tCount}) vs CT({ctCount})");
    }

    private void ApplyFlyingMechanicsToAllPlayers()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive).ToList();
        
        foreach (var player in players)
        {
            ApplyFlyingMechanics(player);
        }
        
        Logger.LogInformation($"Flying Scoutsman: Applied flying mechanics to {players.Count} players");
    }

    private void ApplyFlyingMechanics(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;
        
        try
        {
            // Apply reduced gravity
            player.PlayerPawn.Value.GravityScale = GRAVITY_SCALE;
            Logger.LogDebug($"Flying Scoutsman: Applied gravity scale {GRAVITY_SCALE} to {player.PlayerName}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to apply flying mechanics to player {player.PlayerName}");
        }
    }

    private void ResetGravityForAllPlayers()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        
        foreach (var player in players)
        {
            try
            {
                if (player.PlayerPawn?.Value != null)
                {
                    player.PlayerPawn.Value.GravityScale = 1.0f; // Reset to normal gravity
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to reset gravity for player {player.PlayerName}");
            }
        }
        
        Logger.LogInformation($"Flying Scoutsman: Reset gravity for {players.Count} players");
    }

    private void RemoveRestrictedWeapons()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive).ToList();
        
        foreach (var player in players)
        {
            RemoveRestrictedWeaponsFromPlayer(player);
        }
    }

    private void RemoveRestrictedWeaponsFromPlayer(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;

        try
        {
            var weaponServices = player.PlayerPawn.Value.WeaponServices;
            if (weaponServices == null) return;

            // Remove all weapons that are not allowed
            var weapons = weaponServices.MyWeapons.ToList();
            foreach (var weapon in weapons)
            {
                if (weapon?.Value?.DesignerName != null)
                {
                    var weaponName = weapon.Value.DesignerName;
                    if (!IsWeaponAllowed(weaponName))
                    {
                        weapon.Value.Remove();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to remove restricted weapons from player {player.PlayerName}");
        }
    }

    private void RemoveAllWeapons(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;

        try
        {
            player.RemoveWeapons();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to remove weapons from player {player.PlayerName}");
        }
    }

    private void EquipAllowedWeapons()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive).ToList();
        
        foreach (var player in players)
        {
            GiveAllowedWeapons(player);
        }
    }

    private void GiveAllowedWeapons(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null) return;

        try
        {
            // Give SSG08 (Scout)
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
            
            Logger.LogDebug($"Flying Scoutsman: Gave allowed weapons to {player.PlayerName}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to give weapons to player {player.PlayerName}");
        }
    }

    private bool IsWeaponAllowed(string weaponName)
    {
        var allowedWeapons = new[]
        {
            "weapon_ssg08",     // SSG08 Scout
            "weapon_knife",     // CT Knife
            "weapon_knife_t"    // T Knife
        };
        
        return allowedWeapons.Contains(weaponName);
    }

    private void UpdateScoreHUD()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        
        var scoreMessage = $"T: {_terroristRounds} | CT: {_counterTerroristRounds} | Round: {_roundsPlayed}";
        
        foreach (var player in players)
        {
            try
            {
                // Display score in center of screen
                player.PrintToCenter(scoreMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to update HUD for player {player.PlayerName}");
            }
        }
    }

    private void DeclareWinner(CsTeam winningTeam)
    {
        Logger.LogInformation($"Flying Scoutsman: Game ended, winner: {winningTeam}");
        
        var teamName = winningTeam == CsTeam.Terrorist ? "Terrorists" : "Counter-Terrorists";
        var teamColor = winningTeam == CsTeam.Terrorist ? ChatColors.Orange : ChatColors.Blue;
        
        // Announce winner
        Server.PrintToChatAll($"{ChatColors.Gold}======================================");
        Server.PrintToChatAll($"{ChatColors.Gold}[Flying Scoutsman] GAME OVER!");
        Server.PrintToChatAll($"{teamColor}WINNERS: {teamName}");
        Server.PrintToChatAll($"{ChatColors.White}Final Score: T:{_terroristRounds} CT:{_counterTerroristRounds}");
        Server.PrintToChatAll($"{ChatColors.Gold}======================================");
        
        // Display winner on HUD
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        foreach (var player in players)
        {
            try
            {
                player.PrintToCenter($"GAME OVER! {teamName} WIN! Final: T:{_terroristRounds} CT:{_counterTerroristRounds}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to display winner to player {player.PlayerName}");
            }
        }
        
        // Reset game state
        _gameModeActive = false;
        _roundsPlayed = 0;
        _terroristRounds = 0;
        _counterTerroristRounds = 0;
        
        // Reset player gravity
        ResetGravityForAllPlayers();
    }
}