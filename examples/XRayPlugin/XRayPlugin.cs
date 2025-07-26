using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Linq;

namespace XRayPlugin;

[MinimumApiVersion(80)]
public class XRayPlugin : BasePlugin
{
    public override string ModuleName => "X-Ray Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp & Contributors";
    public override string ModuleDescription => "A plugin that provides X-Ray functionality for admins - highlights enemies with glow effect";

    // Track which players have X-Ray active
    private readonly HashSet<ulong> _xrayActivePlayers = new();
    
    // Authorized Steam ID for X-Ray functionality
    private const ulong AUTHORIZED_STEAM_ID = 76561199076538983;

    public override void Load(bool hotReload)
    {
        Console.WriteLine("X-Ray Plugin loaded!");
        
        // Register event handlers for cleanup when players disconnect
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            // Remove player from X-Ray tracking when they disconnect
            _xrayActivePlayers.Remove(player.SteamID);
        }
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        Console.WriteLine("X-Ray Plugin unloaded!");
        // Clean up on unload
        RemoveAllXRayEffects();
        _xrayActivePlayers.Clear();
    }

    [ConsoleCommand("css_xray", "Apply X-Ray effect to a specific player")]
    [CommandHelper(minArgs: 1, usage: "[player_name_part]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnXRayCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        // Check if caller is authorized
        if (caller == null || caller.SteamID != AUTHORIZED_STEAM_ID)
        {
            commandInfo.ReplyToCommand("You are not authorized to use this command.");
            return;
        }
        var targetNamePart = commandInfo.GetArg(1).ToLower();
        
        if (string.IsNullOrWhiteSpace(targetNamePart))
        {
            commandInfo.ReplyToCommand("Usage: css_xray <player_name_part>");
            return;
        }

        // Find players by partial name match
        var allPlayers = Utilities.GetPlayers();
        var matchingPlayers = allPlayers.Where(p => 
            p.PlayerName.ToLower().Contains(targetNamePart)).ToList();

        if (matchingPlayers.Count == 0)
        {
            commandInfo.ReplyToCommand($"No players found matching '{targetNamePart}'");
            return;
        }

        if (matchingPlayers.Count > 1)
        {
            var playerNames = string.Join(", ", matchingPlayers.Select(p => p.PlayerName));
            commandInfo.ReplyToCommand($"Multiple players found matching '{targetNamePart}': {playerNames}. Please be more specific.");
            return;
        }

        var targetPlayer = matchingPlayers.First();
        
        if (targetPlayer.PlayerPawn.Value == null)
        {
            commandInfo.ReplyToCommand($"Player '{targetPlayer.PlayerName}' has no valid pawn");
            return;
        }

        // Check if player is in a valid team (not spectator)
        var targetTeam = (CsTeam)targetPlayer.TeamNum;
        if (targetTeam == CsTeam.None || targetTeam == CsTeam.Spectator)
        {
            commandInfo.ReplyToCommand($"Player '{targetPlayer.PlayerName}' is not in a valid team (T/CT)");
            return;
        }

        // Check if X-Ray is already active for this player
        var steamId = targetPlayer.SteamID;
        if (_xrayActivePlayers.Contains(steamId))
        {
            // Remove X-Ray instead of showing error - toggle functionality
            RemoveXRayForPlayer(targetPlayer);
            _xrayActivePlayers.Remove(steamId);
            
            var callerNameRemove = caller?.PlayerName ?? "Console";
            commandInfo.ReplyToCommand($"X-Ray effect removed from player '{targetPlayer.PlayerName}' by {callerNameRemove}");
            targetPlayer.PrintToChat($"[X-Ray] X-Ray effect has been removed by {callerNameRemove}");
            return;
        }

        ApplyXRayEffect(targetPlayer);
        _xrayActivePlayers.Add(steamId);
        
        var callerName = caller?.PlayerName ?? "Console";
        commandInfo.ReplyToCommand($"X-Ray effect applied to player '{targetPlayer.PlayerName}' by {callerName}");
        
        // Also notify the target player
        targetPlayer.PrintToChat($"[X-Ray] X-Ray effect has been applied to you by {callerName}. Enemy players will now glow red!");
    }

    [ConsoleCommand("css_removexray", "Remove X-Ray effect from all players")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRemoveXRayCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        // Check if caller is authorized
        if (caller == null || caller.SteamID != AUTHORIZED_STEAM_ID)
        {
            commandInfo.ReplyToCommand("You are not authorized to use this command.");
            return;
        }
        if (_xrayActivePlayers.Count == 0)
        {
            commandInfo.ReplyToCommand("No X-Ray effects are currently active");
            return;
        }

        RemoveAllXRayEffects();
        _xrayActivePlayers.Clear();
        
        var callerName = caller?.PlayerName ?? "Console";
        commandInfo.ReplyToCommand($"All X-Ray effects removed by {callerName}");
        
        // Notify all players
        Server.PrintToChatAll($"[X-Ray] All X-Ray effects have been removed by {callerName}");
    }

    [ConsoleCommand("css_listxray", "List players with active X-Ray effects")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnListXRayCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        // Check if caller is authorized
        if (caller == null || caller.SteamID != AUTHORIZED_STEAM_ID)
        {
            commandInfo.ReplyToCommand("You are not authorized to use this command.");
            return;
        }
        if (_xrayActivePlayers.Count == 0)
        {
            commandInfo.ReplyToCommand("No X-Ray effects are currently active");
            return;
        }

        var allPlayers = Utilities.GetPlayers();
        var activeXRayPlayers = allPlayers.Where(p => _xrayActivePlayers.Contains(p.SteamID)).Select(p => p.PlayerName);
        
        var playerList = string.Join(", ", activeXRayPlayers);
        commandInfo.ReplyToCommand($"Players with active X-Ray: {playerList}");
    }

    private void ApplyXRayEffect(CCSPlayerController targetPlayer)
    {
        var targetTeam = (CsTeam)targetPlayer.TeamNum;
        var allPlayers = Utilities.GetPlayers();

        foreach (var player in allPlayers)
        {
            if (player.PlayerPawn.Value == null || player == targetPlayer)
                continue;

            var playerTeam = (CsTeam)player.TeamNum;
            
            // Only apply glow to opposing team members
            if (IsOpposingTeam(targetTeam, playerTeam))
            {
                ApplyGlowToPlayer(player);
            }
        }
    }

    private bool IsOpposingTeam(CsTeam team1, CsTeam team2)
    {
        // Check if teams are opposing (T vs CT)
        return (team1 == CsTeam.Terrorist && team2 == CsTeam.CounterTerrorist) ||
               (team1 == CsTeam.CounterTerrorist && team2 == CsTeam.Terrorist);
    }

    private void ApplyGlowToPlayer(CCSPlayerController player)
    {
        if (player.PlayerPawn.Value == null)
            return;

        var pawn = player.PlayerPawn.Value;
        
        // Configure glow properties for X-Ray effect
        // Using bright red color for enemy visibility
        pawn.Glow.GlowColorOverride = Color.FromArgb(255, 255, 0, 0); // Bright red
        pawn.Glow.GlowType = 3; // Glow type 3 should work through walls (like spectator X-ray)
        pawn.Glow.GlowRange = 0; // Unlimited range (0 = no limit)
        pawn.Glow.GlowRangeMin = 0; // No minimum range
        pawn.Glow.Glowing = true; // Enable the glow
        
        // Mark the glow property as changed for network transmission
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_Glow");
    }

    private void RemoveAllXRayEffects()
    {
        var allPlayers = Utilities.GetPlayers();

        foreach (var player in allPlayers)
        {
            if (player.PlayerPawn.Value == null)
                continue;

            RemoveGlowFromPlayer(player);
        }
    }

    private void RemoveXRayForPlayer(CCSPlayerController targetPlayer)
    {
        var targetTeam = (CsTeam)targetPlayer.TeamNum;
        var allPlayers = Utilities.GetPlayers();

        foreach (var player in allPlayers)
        {
            if (player.PlayerPawn.Value == null || player == targetPlayer)
                continue;

            var playerTeam = (CsTeam)player.TeamNum;
            
            // Remove glow from opposing team members
            if (IsOpposingTeam(targetTeam, playerTeam))
            {
                RemoveGlowFromPlayer(player);
            }
        }
    }

    private void RemoveGlowFromPlayer(CCSPlayerController player)
    {
        if (player.PlayerPawn.Value == null)
            return;

        var pawn = player.PlayerPawn.Value;
        
        // Disable the glow effect
        pawn.Glow.Glowing = false;
        
        // Reset glow properties to default values
        pawn.Glow.GlowColorOverride = Color.FromArgb(255, 255, 255, 255); // White
        pawn.Glow.GlowType = 0; // Default glow type
        pawn.Glow.GlowRange = 0;
        pawn.Glow.GlowRangeMin = 0;
        
        // Mark the glow property as changed for network transmission
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_Glow");
    }
}