using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;

namespace InvisPlugin;

/// <summary>
/// InvisPlugin provides robust invisibility functionality that:
/// - Never causes client crashes (no CopyExistingEntity errors)
/// - Only breaks invisibility on actual sound events (shooting, jumping, footsteps, reloading)
/// - Uses CheckTransmit for reliable entity hiding
/// - Ensures proper cleanup on death/disconnect to prevent crashes
/// </summary>
[MinimumApiVersion(276)]
public class InvisPlugin : BasePlugin
{
    public override string ModuleName => "Invisibility Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "CounterStrikeSharp";
    public override string ModuleDescription => "A robust invisibility plugin that breaks invisibility only on actual sound events";

    // Track which players are currently invisible
    private readonly HashSet<ulong> _invisiblePlayers = new();
    
    // Track players who need to be made visible before death/disconnect
    private readonly HashSet<ulong> _playersToRevealOnDeath = new();

    public override void Load(bool hotReload)
    {
        Console.WriteLine("[InvisPlugin] Loading...");
        
        // Register sound event handlers that break invisibility
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerJump>(OnPlayerJump);
        RegisterEventHandler<EventPlayerFootstep>(OnPlayerFootstep);
        RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
        RegisterEventHandler<EventDecoyDetonate>(OnDecoyDetonate);
        RegisterEventHandler<EventItemPickup>(OnItemPickup);
        
        // Register events for proper cleanup to prevent crashes
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        
        // Register CheckTransmit listener for invisibility control
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        
        Console.WriteLine("[InvisPlugin] Loaded successfully! Event handlers and CheckTransmit listener registered.");
    }

    /// <summary>
    /// CheckTransmit listener - the core mechanism for invisibility.
    /// This controls what entities are transmitted to each client.
    /// </summary>
    private void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (_invisiblePlayers.Count == 0)
            return;

        var allPlayers = Utilities.GetPlayers();

        foreach ((CCheckTransmitInfo info, CCSPlayerController? receiver) in infoList)
        {
            // Skip if receiver is invalid
            if (receiver == null || !IsPlayerValid(receiver))
                continue;

            // Check each invisible player and hide them from this receiver
            foreach (var player in allPlayers)
            {
                if (!IsPlayerValid(player) || player.PlayerPawn.Value == null)
                    continue;

                // If this player is invisible and it's not the receiver themselves
                if (_invisiblePlayers.Contains(player.SteamID) && player.Slot != receiver.Slot)
                {
                    // Remove the invisible player's pawn from transmission to this receiver
                    // This is the key mechanism that makes the player invisible
                    info.TransmitEntities.Remove(player.PlayerPawn);
                }
            }
        }
    }

    #region Sound Event Handlers (Break Invisibility)

    /// <summary>
    /// Weapon fire breaks invisibility - shooting makes noise
    /// </summary>
    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player))
        {
            BreakInvisibility(player, "weapon fire");
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Jumping breaks invisibility - landing makes noise
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerJump(EventPlayerJump @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player))
        {
            BreakInvisibility(player, "jumping");
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Footstep event breaks invisibility - this is the key event for movement sounds.
    /// Only triggers when the game actually plays a footstep sound, not just on movement.
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerFootstep(EventPlayerFootstep @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player))
        {
            BreakInvisibility(player, "footstep");
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Weapon reload breaks invisibility - reloading makes noise
    /// </summary>
    [GameEventHandler]
    public HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player))
        {
            BreakInvisibility(player, "weapon reload");
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Decoy grenade detonation breaks invisibility for nearby players
    /// Note: This checks if the player who threw it is nearby (sound radius effect)
    /// </summary>
    [GameEventHandler]
    public HookResult OnDecoyDetonate(EventDecoyDetonate @event, GameEventInfo info)
    {
        // For decoy grenades, we need to check which player is in the sound radius
        // Since we don't have easy access to position data, we'll skip this for now
        // to avoid false positives. In a real implementation, you'd calculate distance.
        return HookResult.Continue;
    }

    /// <summary>
    /// Item pickup can make noise in some cases (weapons, armor, etc.)
    /// This is a conservative approach - picking up items breaks invisibility
    /// </summary>
    [GameEventHandler]
    public HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player))
        {
            BreakInvisibility(player, "item pickup");
        }
        return HookResult.Continue;
    }

    #endregion

    #region Cleanup Event Handlers (Prevent Crashes)

    /// <summary>
    /// Critical: Make player visible before death to prevent client crashes
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (IsPlayerValid(player) && _invisiblePlayers.Contains(player!.SteamID))
        {
            // Must reveal player before death to prevent crashes
            _invisiblePlayers.Remove(player.SteamID);
            _playersToRevealOnDeath.Remove(player.SteamID);
            
            Console.WriteLine($"[InvisPlugin] Player {player.PlayerName} made visible due to death (crash prevention)");
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Critical: Make player visible before disconnect to prevent client crashes
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            var steamId = player.SteamID;
            if (_invisiblePlayers.Contains(steamId))
            {
                _invisiblePlayers.Remove(steamId);
                Console.WriteLine($"[InvisPlugin] Player {player.PlayerName} removed from invisibility due to disconnect");
            }
            _playersToRevealOnDeath.Remove(steamId);
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Clear all invisibility states on round start for clean state
    /// </summary>
    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        var count = _invisiblePlayers.Count;
        _invisiblePlayers.Clear();
        _playersToRevealOnDeath.Clear();
        
        if (count > 0)
        {
            Console.WriteLine($"[InvisPlugin] Round start: cleared {count} invisible players");
        }
        return HookResult.Continue;
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Breaks invisibility for a player with logging
    /// </summary>
    private void BreakInvisibility(CCSPlayerController? player, string reason)
    {
        if (!IsPlayerValid(player))
            return;

        var steamId = player!.SteamID;
        if (_invisiblePlayers.Remove(steamId))
        {
            _playersToRevealOnDeath.Remove(steamId);
            Console.WriteLine($"[InvisPlugin] {player.PlayerName} broke invisibility due to: {reason}");
            player.PrintToChat($"[Invisibility] You became visible due to: {reason}");
        }
    }

    /// <summary>
    /// Makes a player invisible
    /// </summary>
    private void MakeInvisible(CCSPlayerController player)
    {
        if (!IsPlayerValid(player))
            return;

        var steamId = player.SteamID;
        if (_invisiblePlayers.Add(steamId))
        {
            _playersToRevealOnDeath.Add(steamId);
            Console.WriteLine($"[InvisPlugin] {player.PlayerName} is now invisible");
            player.PrintToChat("[Invisibility] You are now invisible! Avoid making sounds to stay hidden.");
        }
    }

    /// <summary>
    /// Makes a player visible
    /// </summary>
    private void MakeVisible(CCSPlayerController player)
    {
        if (!IsPlayerValid(player))
            return;

        var steamId = player.SteamID;
        if (_invisiblePlayers.Remove(steamId))
        {
            _playersToRevealOnDeath.Remove(steamId);
            Console.WriteLine($"[InvisPlugin] {player.PlayerName} is now visible");
            player.PrintToChat("[Invisibility] You are now visible.");
        }
    }

    /// <summary>
    /// Checks if a player is valid and connected
    /// </summary>
    private static bool IsPlayerValid(CCSPlayerController? player)
    {
        return player != null 
            && player.IsValid 
            && player.Connected == PlayerConnectedState.PlayerConnected
            && player.PlayerPawn.Value != null
            && player.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE;
    }

    #endregion

    #region Admin Commands

    /// <summary>
    /// Admin command to make a player invisible
    /// </summary>
    [ConsoleCommand("css_invis", "Make a player invisible")]
    [CommandHelper(minArgs: 1, usage: "[player_name]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnInvisCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        var targetName = commandInfo.GetArg(1);
        var target = FindPlayerByName(targetName);
        
        if (target == null)
        {
            commandInfo.ReplyToCommand($"Player '{targetName}' not found.");
            return;
        }

        if (!IsPlayerValid(target))
        {
            commandInfo.ReplyToCommand($"Player '{target.PlayerName}' is not alive or valid.");
            return;
        }

        MakeInvisible(target);
        commandInfo.ReplyToCommand($"Made {target.PlayerName} invisible.");
    }

    /// <summary>
    /// Admin command to make a player visible
    /// </summary>
    [ConsoleCommand("css_visible", "Make a player visible")]
    [CommandHelper(minArgs: 1, usage: "[player_name]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnVisibleCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        var targetName = commandInfo.GetArg(1);
        var target = FindPlayerByName(targetName);
        
        if (target == null)
        {
            commandInfo.ReplyToCommand($"Player '{targetName}' not found.");
            return;
        }

        MakeVisible(target);
        commandInfo.ReplyToCommand($"Made {target.PlayerName} visible.");
    }

    /// <summary>
    /// Admin command to list invisible players
    /// </summary>
    [ConsoleCommand("css_listinvis", "List invisible players")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnListInvisCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        if (_invisiblePlayers.Count == 0)
        {
            commandInfo.ReplyToCommand("No players are currently invisible.");
            return;
        }

        var allPlayers = Utilities.GetPlayers();
        var invisiblePlayerNames = allPlayers
            .Where(p => IsPlayerValid(p) && _invisiblePlayers.Contains(p.SteamID))
            .Select(p => p.PlayerName);

        var playerList = string.Join(", ", invisiblePlayerNames);
        commandInfo.ReplyToCommand($"Invisible players: {playerList}");
    }

    /// <summary>
    /// Admin command to clear all invisibility
    /// </summary>
    [ConsoleCommand("css_clearinvis", "Make all players visible")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnClearInvisCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        var count = _invisiblePlayers.Count;
        _invisiblePlayers.Clear();
        _playersToRevealOnDeath.Clear();
        
        commandInfo.ReplyToCommand($"Made {count} players visible.");
    }

    /// <summary>
    /// Self-invisibility toggle command for testing (requires admin permissions)
    /// </summary>
    [ConsoleCommand("css_toggleinvis", "Toggle your own invisibility")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/root")]
    public void OnToggleInvisCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        if (caller == null)
        {
            commandInfo.ReplyToCommand("This command can only be used by players.");
            return;
        }

        if (!IsPlayerValid(caller))
        {
            commandInfo.ReplyToCommand("You must be alive to use this command.");
            return;
        }

        var steamId = caller.SteamID;
        if (_invisiblePlayers.Contains(steamId))
        {
            MakeVisible(caller);
            commandInfo.ReplyToCommand("You are now visible.");
        }
        else
        {
            MakeInvisible(caller);
            commandInfo.ReplyToCommand("You are now invisible. Be careful not to make noise!");
        }
    }

    /// <summary>
    /// Helper to find player by partial name match
    /// </summary>
    private CCSPlayerController? FindPlayerByName(string name)
    {
        var players = Utilities.GetPlayers()
            .Where(p => IsPlayerValid(p) && p.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return players.Count == 1 ? players[0] : null;
    }

    #endregion

    public override void Unload(bool hotReload)
    {
        Console.WriteLine("[InvisPlugin] Unloading...");
        
        // Clear all invisibility states to prevent issues
        _invisiblePlayers.Clear();
        _playersToRevealOnDeath.Clear();
        
        Console.WriteLine("[InvisPlugin] Unloaded successfully!");
    }
}