using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Listeners;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace XRayPlugin;

[MinimumApiVersion(80)]
public class XRayPlugin : BasePlugin
{
    public override string ModuleName => "X-Ray Plugin";
    public override string ModuleVersion => "5.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp & Contributors";
    public override string ModuleDescription => "A plugin that provides X-Ray functionality for admins - highlights enemies with health-based glow colors and utility detection, with player-specific visibility using dynamic prop entities";

    // Track which players have X-Ray active
    private readonly HashSet<ulong> _xrayActivePlayers = new();
    
    // Track glowing entities for each enemy player (per X-Ray player)
    private readonly Dictionary<ulong, Dictionary<int, Tuple<CBaseModelEntity, CBaseModelEntity>>> _xrayGlowEntities = new();
    
    // Track utility glow entities (per X-Ray player)
    private readonly Dictionary<ulong, List<Tuple<CBaseEntity, CBaseModelEntity, CBaseModelEntity>>> _xrayUtilityEntities = new();
    
    // Track player health for damage-based color changes
    private readonly Dictionary<int, int> _playerHealths = new();
    
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
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventHegrenadeDetonate>(OnHegrenadeDetonate);
        RegisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonate);
        RegisterEventHandler<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);
        RegisterEventHandler<EventSmokegrenadeExpired>(OnSmokegrenadeExpired);
        
        // Register CheckTransmit listener for player-specific glow visibility
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        
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

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsPlayerValid(player))
            return HookResult.Continue;

        // Update health tracking for damage-based glow colors
        _playerHealths[player.Slot] = @event.Health;
        
        // Update glow colors for this player if they're being tracked by any XRay user
        Server.NextFrame(() => UpdateGlowColorsForPlayer(player));
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsPlayerValid(player))
            return HookResult.Continue;

        var weaponName = @event.Weapon;
        
        // Track utility grenades for XRay users
        if (IsUtilityWeapon(weaponName))
        {
            Server.NextFrame(() => CreateUtilityGlowForXRayUsers(player, weaponName));
        }
        
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnHegrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        // Clean up utility glows when grenades explode
        Server.NextFrame(() => CleanupUtilityGlowByEntityId(@event.Entityid));
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnFlashbangDetonate(EventFlashbangDetonate @event, GameEventInfo info)
    {
        // Clean up utility glows when flashbangs explode
        Server.NextFrame(() => CleanupUtilityGlowByEntityId(@event.Entityid));
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
    {
        // Clean up utility glows when smokes deploy (but keep them for smoke duration)
        // We'll let the expired event handle the cleanup
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnSmokegrenadeExpired(EventSmokegrenadeExpired @event, GameEventInfo info)
    {
        // Clean up utility glows when smoke expires
        Server.NextFrame(() => CleanupUtilityGlowByEntityId(@event.Entityid));
        return HookResult.Continue;
    }

    // Control what entities are visible to each player (player-specific glow visibility)
    private void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (_xrayActivePlayers.Count == 0)
            return;

        foreach ((CCheckTransmitInfo info, CCSPlayerController? receiver) in infoList)
        {
            if (receiver == null || !IsPlayerValid(receiver))
                continue;

            var receiverSteamId = receiver.SteamID;
            
            // Only modify transmission for players with XRay active
            if (!_xrayActivePlayers.Contains(receiverSteamId))
                continue;

            // This player has XRay - ensure they can see their glow entities
            if (_xrayGlowEntities.ContainsKey(receiverSteamId))
            {
                foreach (var glowEntities in _xrayGlowEntities[receiverSteamId].Values)
                {
                    // Make sure glow entities are transmitted to XRay user
                    if (glowEntities.Item2?.IsValid == true)
                        info.TransmitEntities.Add(glowEntities.Item2);
                }
            }

            // Ensure they can see utility glow entities
            if (_xrayUtilityEntities.ContainsKey(receiverSteamId))
            {
                foreach (var utilityGlow in _xrayUtilityEntities[receiverSteamId])
                {
                    if (utilityGlow.Item2?.IsValid == true)
                        info.TransmitEntities.Add(utilityGlow.Item2);
                }
            }
        }

        // Hide glow entities from players who don't have XRay active
        foreach ((CCheckTransmitInfo info, CCSPlayerController? receiver) in infoList)
        {
            if (receiver == null || !IsPlayerValid(receiver))
                continue;

            var receiverSteamId = receiver.SteamID;
            
            // If this player doesn't have XRay, hide all glow entities from them
            if (_xrayActivePlayers.Contains(receiverSteamId))
                continue;

            // Hide all glow entities from non-XRay players
            foreach (var xrayPlayerEntities in _xrayGlowEntities.Values)
            {
                foreach (var glowEntities in xrayPlayerEntities.Values)
                {
                    if (glowEntities.Item2?.IsValid == true)
                        info.TransmitEntities.Remove(glowEntities.Item2);
                }
            }

            // Hide all utility glow entities from non-XRay players
            foreach (var utilityEntities in _xrayUtilityEntities.Values)
            {
                foreach (var utilityGlow in utilityEntities)
                {
                    if (utilityGlow.Item2?.IsValid == true)
                        info.TransmitEntities.Remove(utilityGlow.Item2);
                }
            }
        }
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

        // Set up glow properties with health-based color
        var glowColor = GetHealthBasedGlowColor(enemyPlayer);
        modelGlow.Glow.GlowColorOverride = glowColor;
        modelGlow.Glow.GlowRange = 5000; // Long range
        modelGlow.Glow.GlowTeam = 0; // Don't use team-based visibility (we control via CheckTransmit)
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
        
        // Also clear utility glows
        ClearAllUtilityGlows();
        
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
            
            // Clean up their health tracking
            _playerHealths.Remove(player.Slot);
            
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
            
            // Clean up their utility glow entities
            if (_xrayUtilityEntities.ContainsKey(steamId))
            {
                foreach (var utilityGlow in _xrayUtilityEntities[steamId])
                {
                    if (utilityGlow.Item2?.IsValid == true)
                        utilityGlow.Item2.AcceptInput("Kill");
                    if (utilityGlow.Item3?.IsValid == true)
                        utilityGlow.Item3.AcceptInput("Kill");
                }
                _xrayUtilityEntities.Remove(steamId);
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
        _playerHealths.Clear();
    }

    private void UpdateGlowColorsForPlayer(CCSPlayerController player)
    {
        if (!IsPlayerValid(player))
            return;

        var playerSlot = player.Slot;
        
        // Update glow color for this player in all XRay users' glow entities
        foreach (var xrayGlowDict in _xrayGlowEntities.Values)
        {
            if (xrayGlowDict.ContainsKey(playerSlot))
            {
                var glowEntities = xrayGlowDict[playerSlot];
                if (glowEntities.Item2?.IsValid == true)
                {
                    var newColor = GetHealthBasedGlowColor(player);
                    glowEntities.Item2.Glow.GlowColorOverride = newColor;
                }
            }
        }
    }

    private Color GetHealthBasedGlowColor(CCSPlayerController player)
    {
        if (!IsPlayerValid(player))
            return Color.FromArgb(255, 0, 255, 0); // Default green

        int health = 100; // Default full health
        
        // Get current health from tracking or player pawn
        if (_playerHealths.ContainsKey(player.Slot))
        {
            health = _playerHealths[player.Slot];
        }
        else if (player.PlayerPawn.Value?.Health > 0)
        {
            health = player.PlayerPawn.Value.Health;
            _playerHealths[player.Slot] = health; // Cache it
        }

        // Clamp health between 0 and 100
        health = Math.Max(0, Math.Min(100, health));

        // Calculate color: Green (full health) to Red (low health)
        // At 100 health: RGB(0, 255, 0) - bright green
        // At 0 health: RGB(255, 0, 0) - bright red
        int red = (int)((100 - health) * 2.55f);   // 0 to 255
        int green = (int)(health * 2.55f);         // 255 to 0
        int blue = 0;                              // Always 0 for red-green spectrum

        return Color.FromArgb(255, red, green, blue);
    }

    private bool IsUtilityWeapon(string weaponName)
    {
        return weaponName switch
        {
            "hegrenade" => true,
            "flashbang" => true,
            "smokegrenade" => true,
            "incgrenade" => true,
            "molotov" => true,
            "decoy" => true,
            _ => false
        };
    }

    private void CreateUtilityGlowForXRayUsers(CCSPlayerController thrower, string weaponName)
    {
        if (!IsPlayerValid(thrower))
            return;

        var throwerTeam = (CsTeam)thrower.TeamNum;
        
        // Find the most recently thrown grenade entity
        // This is a simplified approach - in production you might want more sophisticated tracking
        AddTimer(0.1f, () => FindAndGlowRecentUtility(thrower, throwerTeam, weaponName));
    }

    private void FindAndGlowRecentUtility(CCSPlayerController thrower, CsTeam throwerTeam, string weaponName)
    {
        // Find projectile entities that match the weapon type
        var projectiles = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("*grenade*")
            .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("*projectile*"))
            .Where(e => e.IsValid);

        foreach (var projectile in projectiles)
        {
            if (projectile == null || !projectile.IsValid)
                continue;

            // Create glow for opposing team XRay users
            foreach (var xrayPlayer in Utilities.GetPlayers())
            {
                if (!IsPlayerValid(xrayPlayer) || !_xrayActivePlayers.Contains(xrayPlayer.SteamID))
                    continue;

                var xrayTeam = (CsTeam)xrayPlayer.TeamNum;
                
                // Only highlight utilities from opposing team
                if (IsOpposingTeam(xrayTeam, throwerTeam))
                {
                    CreateUtilityGlowEntity(xrayPlayer, projectile, weaponName);
                }
            }
            
            // For simplicity, we'll assume the first matching entity is the one we want
            // In a production plugin, you'd want more sophisticated matching
            break;
        }
    }

    private void CreateUtilityGlowEntity(CCSPlayerController xrayPlayer, CBaseEntity utilityEntity, string weaponName)
    {
        if (!IsPlayerValid(xrayPlayer) || utilityEntity == null || !utilityEntity.IsValid)
            return;

        var xraySteamId = xrayPlayer.SteamID;

        // Create prop_dynamic entities for utility glow
        var modelGlow = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
        var modelRelay = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");

        if (modelGlow == null || modelRelay == null || !modelGlow.IsValid || !modelRelay.IsValid)
        {
            Console.WriteLine($"[X-Ray] Failed to create utility glow entities for {weaponName}");
            return;
        }

        // Use a simple model for utilities (you might want to get the actual grenade model)
        string utilityModel = GetUtilityModel(weaponName);

        // Configure the relay entity (invisible, follows the utility)
        modelRelay.SetModel(utilityModel);
        modelRelay.Spawnflags = 256u; // Don't collide with anything
        modelRelay.RenderMode = RenderMode_t.kRenderNone; // Invisible
        modelRelay.DispatchSpawn();

        // Configure the glow entity (visible with red glow)
        modelGlow.SetModel(utilityModel);
        modelGlow.Spawnflags = 256u; // Don't collide with anything
        modelGlow.DispatchSpawn();

        // Set up red glow for utilities
        modelGlow.Glow.GlowColorOverride = Color.FromArgb(255, 255, 0, 0); // Bright red
        modelGlow.Glow.GlowRange = 3000; // Medium range for utilities
        modelGlow.Glow.GlowTeam = 0; // Controlled via CheckTransmit
        modelGlow.Glow.GlowType = 3; // Through-wall visibility
        modelGlow.Glow.GlowRangeMin = 50;
        modelGlow.Glow.Glowing = true;

        // Make the relay follow the utility entity
        modelRelay.AcceptInput("FollowEntity", utilityEntity, modelRelay, "!activator");
        // Make the glow entity follow the relay
        modelGlow.AcceptInput("FollowEntity", modelRelay, modelGlow, "!activator");

        // Store the entities for cleanup later
        if (!_xrayUtilityEntities.ContainsKey(xraySteamId))
        {
            _xrayUtilityEntities[xraySteamId] = new List<Tuple<CBaseEntity, CBaseModelEntity, CBaseModelEntity>>();
        }
        
        _xrayUtilityEntities[xraySteamId].Add(new Tuple<CBaseEntity, CBaseModelEntity, CBaseModelEntity>(utilityEntity, modelRelay, modelGlow));

        Console.WriteLine($"[X-Ray] Created utility glow for {weaponName} visible to {xrayPlayer.PlayerName}");
    }

    private string GetUtilityModel(string weaponName)
    {
        // Use appropriate models for different utilities
        // These are example paths - you might need to adjust based on available models
        return weaponName switch
        {
            "hegrenade" => "models/weapons/w_eq_fraggrenade.mdl",
            "flashbang" => "models/weapons/w_eq_flashbang.mdl", 
            "smokegrenade" => "models/weapons/w_eq_smokegrenade.mdl",
            "incgrenade" => "models/weapons/w_eq_incendiarygrenade.mdl",
            "molotov" => "models/weapons/w_eq_molotov.mdl",
            "decoy" => "models/weapons/w_eq_decoy.mdl",
            _ => "models/weapons/w_eq_fraggrenade.mdl" // Default
        };
    }

    private void CleanupUtilityGlowByEntityId(int entityId)
    {
        foreach (var xraySteamId in _xrayUtilityEntities.Keys.ToList())
        {
            var utilityList = _xrayUtilityEntities[xraySteamId];
            
            for (int i = utilityList.Count - 1; i >= 0; i--)
            {
                var utilityGlow = utilityList[i];
                
                // Check if this utility entity matches the ID or is no longer valid
                if (utilityGlow.Item1?.Index == entityId || utilityGlow.Item1?.IsValid != true)
                {
                    // Clean up the glow entities
                    if (utilityGlow.Item2?.IsValid == true)
                        utilityGlow.Item2.AcceptInput("Kill");
                    if (utilityGlow.Item3?.IsValid == true)
                        utilityGlow.Item3.AcceptInput("Kill");

                    utilityList.RemoveAt(i);
                    Console.WriteLine($"[X-Ray] Cleaned up utility glow entity {entityId}");
                }
            }
        }
    }

    private void ClearAllUtilityGlows()
    {
        foreach (var utilityList in _xrayUtilityEntities.Values)
        {
            foreach (var utilityGlow in utilityList)
            {
                if (utilityGlow.Item2?.IsValid == true)
                    utilityGlow.Item2.AcceptInput("Kill");
                if (utilityGlow.Item3?.IsValid == true)
                    utilityGlow.Item3.AcceptInput("Kill");
            }
            utilityList.Clear();
        }
        _xrayUtilityEntities.Clear();
        Console.WriteLine("[X-Ray] Cleared all utility glow effects");
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
            
            // Clean up utility glow entities for this player
            if (_xrayUtilityEntities.ContainsKey(steamId))
            {
                foreach (var utilityGlow in _xrayUtilityEntities[steamId])
                {
                    if (utilityGlow.Item2?.IsValid == true)
                        utilityGlow.Item2.AcceptInput("Kill");
                    if (utilityGlow.Item3?.IsValid == true)
                        utilityGlow.Item3.AcceptInput("Kill");
                }
                _xrayUtilityEntities.Remove(steamId);
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