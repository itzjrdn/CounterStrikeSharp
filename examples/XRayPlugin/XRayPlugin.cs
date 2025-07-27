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
    public override string ModuleVersion => "4.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp & Contributors";
    public override string ModuleDescription => "A plugin that provides X-Ray functionality for admins - highlights enemies with glow effect through walls using dynamic prop entities";

    // Track which players have X-Ray active
    private readonly HashSet<ulong> _xrayActivePlayers = new();
    
    // Track glowing entities for each enemy player (per X-Ray player)
    private readonly Dictionary<ulong, Dictionary<int, Tuple<CBaseModelEntity, CBaseModelEntity>>> _xrayGlowEntities = new();
    
    // Authorized Steam ID for X-Ray functionality
    private const ulong AUTHORIZED_STEAM_ID = 76561199076538983;

    public override void Load(bool hotReload)
    {
        Console.WriteLine("X-Ray Plugin loaded!");
        
        // Register event handlers for proper X-Ray management
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        
        Console.WriteLine("X-Ray Plugin: Event handlers registered successfully!");
    }

    // Event-driven X-Ray updates instead of constant timer
    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        // Update X-Ray effects when players spawn
        Server.NextFrame(() => UpdateXRayForAllPlayers());
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        // Update X-Ray effects at round start
        Server.NextFrame(() => UpdateXRayForAllPlayers());
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        // Update X-Ray effects when players change teams
        Server.NextFrame(() => UpdateXRayForAllPlayers());
        return HookResult.Continue;
    }

    // Update X-Ray effects only when needed (event-driven)
    private void UpdateXRayForAllPlayers()
    {
        if (_xrayActivePlayers.Count == 0)
        {
            // No X-Ray active, ensure all glows are cleared
            ClearAllGlowEffects();
            return;
        }

        var allPlayers = Utilities.GetPlayers();

        foreach (var xrayPlayer in allPlayers)
        {
            if (!IsPlayerValid(xrayPlayer) || !_xrayActivePlayers.Contains(xrayPlayer.SteamID))
                continue;

            var xrayTeam = (CsTeam)xrayPlayer.TeamNum;
            var xraySteamId = xrayPlayer.SteamID;

            // Ensure this X-Ray player has an entry in the glow entities dictionary
            if (!_xrayGlowEntities.ContainsKey(xraySteamId))
            {
                _xrayGlowEntities[xraySteamId] = new Dictionary<int, Tuple<CBaseModelEntity, CBaseModelEntity>>();
            }

            // Apply glow to enemies for this X-Ray player
            foreach (var enemyPlayer in allPlayers)
            {
                if (!IsPlayerValid(enemyPlayer) || enemyPlayer == xrayPlayer)
                    continue;

                var enemyTeam = (CsTeam)enemyPlayer.TeamNum;
                var enemySlot = enemyPlayer.Slot;
                
                // Only highlight opposing team members
                if (IsOpposingTeam(xrayTeam, enemyTeam))
                {
                    // Check if this enemy already has a glow entity for this X-Ray player
                    if (!_xrayGlowEntities[xraySteamId].ContainsKey(enemySlot))
                    {
                        CreateGlowEntityForEnemy(xrayPlayer, enemyPlayer);
                    }
                }
                else
                {
                    // Remove glow if enemy changed teams or is no longer an enemy
                    RemoveGlowEntityForEnemy(xrayPlayer, enemyPlayer);
                }
            }

            // Clean up glow entities for players who disconnected
            CleanupDisconnectedPlayerGlows(xrayPlayer, allPlayers);
        }
    }

    private void CreateGlowEntityForEnemy(CCSPlayerController xrayPlayer, CCSPlayerController enemyPlayer)
    {
        if (!IsPlayerValid(xrayPlayer) || !IsPlayerValid(enemyPlayer) || enemyPlayer.PlayerPawn.Value == null)
            return;

        var xraySteamId = xrayPlayer.SteamID;
        var enemySlot = enemyPlayer.Slot;
        var enemyPawn = enemyPlayer.PlayerPawn.Value;

        // Create two prop_dynamic entities: one relay and one glow
        var modelGlow = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
        var modelRelay = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");

        if (modelGlow == null || modelRelay == null || !modelGlow.IsValid || !modelRelay.IsValid)
        {
            Console.WriteLine($"[X-Ray] Failed to create glow entities for {enemyPlayer.PlayerName}");
            return;
        }

        // Get the enemy player's model
        var playerCBodyComponent = enemyPawn.CBodyComponent;
        if (playerCBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState?.ModelName == null)
        {
            Console.WriteLine($"[X-Ray] Failed to get model for {enemyPlayer.PlayerName}");
            modelGlow.AcceptInput("Kill");
            modelRelay.AcceptInput("Kill");
            return;
        }

        string modelName = playerCBodyComponent.SceneNode.GetSkeletonInstance().ModelState.ModelName;

        // Configure the relay entity (invisible, follows the player)
        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u; // Don't collide with anything
        modelRelay.RenderMode = RenderMode_t.kRenderNone; // Invisible
        modelRelay.DispatchSpawn();

        // Configure the glow entity (visible with glow effect)
        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u; // Don't collide with anything
        modelGlow.DispatchSpawn();

        // Set up glow properties (green like in the screenshots)
        modelGlow.Glow.GlowColorOverride = Color.FromArgb(255, 0, 255, 0); // Bright green
        modelGlow.Glow.GlowRange = 5000; // Long range
        modelGlow.Glow.GlowTeam = -1; // Visible to all teams
        modelGlow.Glow.GlowType = 3; // Spectator-like through-wall visibility
        modelGlow.Glow.GlowRangeMin = 100;
        modelGlow.Glow.Glowing = true;

        // Make the relay follow the enemy player
        modelRelay.AcceptInput("FollowEntity", enemyPawn, modelRelay, "!activator");
        // Make the glow entity follow the relay
        modelGlow.AcceptInput("FollowEntity", modelRelay, modelGlow, "!activator");

        // Store the entities for cleanup later
        _xrayGlowEntities[xraySteamId][enemySlot] = new Tuple<CBaseModelEntity, CBaseModelEntity>(modelRelay, modelGlow);

        Console.WriteLine($"[X-Ray] Created glow entities for {enemyPlayer.PlayerName} visible to {xrayPlayer.PlayerName}");
    }

    private void RemoveGlowEntityForEnemy(CCSPlayerController xrayPlayer, CCSPlayerController enemyPlayer)
    {
        var xraySteamId = xrayPlayer.SteamID;
        var enemySlot = enemyPlayer.Slot;

        if (!_xrayGlowEntities.ContainsKey(xraySteamId) || !_xrayGlowEntities[xraySteamId].ContainsKey(enemySlot))
            return;

        var glowEntities = _xrayGlowEntities[xraySteamId][enemySlot];
        
        // Clean up the entities
        if (glowEntities.Item1?.IsValid == true)
            glowEntities.Item1.AcceptInput("Kill");
        if (glowEntities.Item2?.IsValid == true)
            glowEntities.Item2.AcceptInput("Kill");

        _xrayGlowEntities[xraySteamId].Remove(enemySlot);
        Console.WriteLine($"[X-Ray] Removed glow entities for {enemyPlayer.PlayerName}");
    }

    private void CleanupDisconnectedPlayerGlows(CCSPlayerController xrayPlayer, List<CCSPlayerController> allPlayers)
    {
        var xraySteamId = xrayPlayer.SteamID;
        
        if (!_xrayGlowEntities.ContainsKey(xraySteamId))
            return;

        var activeSlots = allPlayers.Where(p => IsPlayerValid(p)).Select(p => p.Slot).ToHashSet();
        var slotsToRemove = _xrayGlowEntities[xraySteamId].Keys.Where(slot => !activeSlots.Contains(slot)).ToList();

        foreach (var slot in slotsToRemove)
        {
            var glowEntities = _xrayGlowEntities[xraySteamId][slot];
            
            if (glowEntities.Item1?.IsValid == true)
                glowEntities.Item1.AcceptInput("Kill");
            if (glowEntities.Item2?.IsValid == true)
                glowEntities.Item2.AcceptInput("Kill");

            _xrayGlowEntities[xraySteamId].Remove(slot);
        }
    }

    private void ClearAllGlowEffects()
    {
        foreach (var xrayPlayerEntities in _xrayGlowEntities.Values)
        {
            foreach (var glowEntities in xrayPlayerEntities.Values)
            {
                if (glowEntities.Item1?.IsValid == true)
                    glowEntities.Item1.AcceptInput("Kill");
                if (glowEntities.Item2?.IsValid == true)
                    glowEntities.Item2.AcceptInput("Kill");
            }
            xrayPlayerEntities.Clear();
        }
        _xrayGlowEntities.Clear();
        Console.WriteLine("[X-Ray] Cleared all glow effects");
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            var steamId = player.SteamID;
            
            // Remove player from X-Ray tracking when they disconnect
            _xrayActivePlayers.Remove(steamId);
            
            // Clean up their glow entities
            if (_xrayGlowEntities.ContainsKey(steamId))
            {
                foreach (var glowEntities in _xrayGlowEntities[steamId].Values)
                {
                    if (glowEntities.Item1?.IsValid == true)
                        glowEntities.Item1.AcceptInput("Kill");
                    if (glowEntities.Item2?.IsValid == true)
                        glowEntities.Item2.AcceptInput("Kill");
                }
                _xrayGlowEntities.Remove(steamId);
            }
            
            // Also remove any glow entities created for this player (when they were enemies)
            foreach (var xrayPlayerEntities in _xrayGlowEntities.Values)
            {
                if (xrayPlayerEntities.ContainsKey(player.Slot))
                {
                    var glowEntities = xrayPlayerEntities[player.Slot];
                    if (glowEntities.Item1?.IsValid == true)
                        glowEntities.Item1.AcceptInput("Kill");
                    if (glowEntities.Item2?.IsValid == true)
                        glowEntities.Item2.AcceptInput("Kill");
                    xrayPlayerEntities.Remove(player.Slot);
                }
            }
        }
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        Console.WriteLine("X-Ray Plugin unloaded!");
        // Clean up all effects
        ClearAllGlowEffects();
        _xrayActivePlayers.Clear();
    }

    private static bool IsPlayerValid(CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
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
            _xrayActivePlayers.Remove(steamId);
            
            // Clean up glow entities for this player
            if (_xrayGlowEntities.ContainsKey(steamId))
            {
                foreach (var glowEntities in _xrayGlowEntities[steamId].Values)
                {
                    if (glowEntities.Item1?.IsValid == true)
                        glowEntities.Item1.AcceptInput("Kill");
                    if (glowEntities.Item2?.IsValid == true)
                        glowEntities.Item2.AcceptInput("Kill");
                }
                _xrayGlowEntities.Remove(steamId);
            }
            
            var callerNameRemove = caller?.PlayerName ?? "Console";
            commandInfo.ReplyToCommand($"X-Ray effect removed from player '{targetPlayer.PlayerName}' by {callerNameRemove}");
            targetPlayer.PrintToChat($"[X-Ray] X-Ray effect has been removed by {callerNameRemove}");
            return;
        }

        _xrayActivePlayers.Add(steamId);
        
        var callerName = caller?.PlayerName ?? "Console";
        commandInfo.ReplyToCommand($"X-Ray effect applied to player '{targetPlayer.PlayerName}' by {callerName}");
        
        // Also notify the target player
        targetPlayer.PrintToChat($"[X-Ray] X-Ray effect has been applied to you by {callerName}. Enemy players will now glow green!");
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

        _xrayActivePlayers.Clear();
        
        // Immediately remove all X-Ray effects
        ClearAllGlowEffects();
        
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

    private bool IsOpposingTeam(CsTeam team1, CsTeam team2)
    {
        // Check if teams are opposing (T vs CT)
        return (team1 == CsTeam.Terrorist && team2 == CsTeam.CounterTerrorist) ||
               (team1 == CsTeam.CounterTerrorist && team2 == CsTeam.Terrorist);
    }
}