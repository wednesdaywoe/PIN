using System;
using System.Threading;
using GameServer.Data.Persistence;
using Serilog;

namespace GameServer.Systems.Persistence;

/// <summary>
///     Saves everyone who is playing, every so often, so that a session that ends without asking to end
///     still keeps what it earned.
///
///     The autosave is the load-bearing half rather than a safety net. <c>RequestLogout</c> is the tidy
///     exit and it does save, but the ordinary ways a PIN session actually finishes don't go through it:
///     the client is closed, or the connection times out, or the player dies and has to reconnect because
///     nothing sends <c>RequestRespawn</c> (NET-23). On any of those, the last autosave is the save.
///
///     The interval is the amount of progress a player can lose. A minute is short enough that losing it
///     is an annoyance rather than a session, and long enough that a shard with a full zone isn't
///     serializing inventories in its tick loop. Nothing is written when nothing changed, so an idle
///     player costs one serialize a minute and no disk at all.
/// </summary>
public class CharacterSaveSim
{
    private const ulong SaveIntervalMs = 60 * 1000;

    private readonly IShard _shard;
    private readonly ILogger _logger;

    private ulong _lastSave;

    public CharacterSaveSim(IShard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<CharacterSaveSim>();
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime - _lastSave < SaveIntervalMs)
        {
            return;
        }

        _lastSave = currentTime;

        foreach (var player in _shard.Clients.Values)
        {
            SaveIfChanged(player, "autosave");
        }
    }

    /// <summary>
    ///     Writes a player out if anything about them has moved since the last write. Safe to call on a
    ///     player who never finished logging in, which is what makes it safe to call from the disconnect
    ///     path.
    /// </summary>
    public void SaveIfChanged(IPlayer player, string reason)
    {
        if (player?.CharacterEntity == null || player.Inventory == null || player.CurrentZone == null)
        {
            return;
        }

        try
        {
            var state = player.Snapshot(DateTimeOffset.UtcNow);

            if (_shard.CharacterStore.Save(state))
            {
                _logger.Information(
                    "Saved character {CharacterId} ({Reason}): {ResourceCount} resource kind(s), {ItemCount} item(s), outpost {OutpostId}, {TimePlayed}s played",
                    state.CharacterId,
                    reason,
                    state.Resources.Count,
                    state.Items.Count,
                    state.LastOutpostId,
                    state.TimePlayedSecs);
            }
        }
        catch (Exception ex)
        {
            // A save must never be able to end a session. The store swallows its own IO failures; this
            // catches everything upstream of it, which is the snapshot walking a live inventory.
            _logger.Error(ex, "Failed to snapshot character {CharacterId} for {Reason}", player.CharacterId, reason);
        }
    }
}
