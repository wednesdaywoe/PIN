using System.Collections.Generic;
using System.Threading;
using AeroMessages.GSS.V66.Character;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Combat;

/// <summary>
///     Stops waiting for a downed player to press the give-up key and respawns them anyway.
/// </summary>
/// <remarks>
///     Without this a player who ignores the prompt — or whose client never draws it — is downed
///     forever, which is the same dead end NET-23 describes with extra steps. The forced respawn is
///     the floor under the feature rather than a nicety.
/// </remarks>
public class BleedoutSim
{
    private const ulong UpdateIntervalMs = 250;

    private readonly Shard _shard;
    private readonly ILogger _logger;
    private readonly List<CharacterEntity> _expired = new();
    private ulong _lastUpdate;

    public BleedoutSim(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<BleedoutSim>();
    }

    /// <summary>
    ///     Whether a downed character has waited long enough that the server should respawn them
    ///     without being asked.
    /// </summary>
    /// <remarks>
    ///     Split out from the tick because it is the whole safety net: if it never returns true, a
    ///     player whose client never drew the prompt stays down for the rest of the session and the
    ///     symptom is indistinguishable from the bug this exists to fix. A predicate that silently
    ///     never fires is worth pinning offline.
    /// </remarks>
    public static bool IsOverdue(CharacterStateData.CharacterStatus state, ulong respawnForcedAt, ulong currentTime)
        => state == CharacterStateData.CharacterStatus.Incapacitated
           && respawnForcedAt != 0
           && currentTime >= respawnForcedAt;

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        _lastUpdate = currentTime;

        // Respawning mutates the shard's entities, so collect first and act after. NET-21 was a set
        // mutated mid-iteration on this same thread.
        _expired.Clear();

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is CharacterEntity character
                && character.Player != null
                && IsOverdue(character.CharacterState.State, character.RespawnForcedAt, currentTime))
            {
                _expired.Add(character);
            }
        }

        foreach (var character in _expired)
        {
            _logger.Information(
                "Player {EntityId} did not tap out in time, respawning them",
                character.EntityId);
            character.Player.Respawn();
        }
    }
}
