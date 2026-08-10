using System.Threading;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     Refills shields once a character has gone long enough without being hit. Any damage that lands
///     restarts the wait, so this only gets to run between fights rather than during one.
/// </summary>
public class ShieldSim
{
    private const ulong UpdateIntervalMs = 100;

    private readonly Shard _shard;
    private ulong _lastUpdate;

    public ShieldSim(Shard shard)
    {
        _shard = shard;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        // currentTime is a wall clock timestamp, so without this the first tick regens for decades
        var elapsedSeconds = _lastUpdate == 0 ? 0f : (currentTime - _lastUpdate) / 1000f;
        _lastUpdate = currentTime;

        if (elapsedSeconds <= 0f)
        {
            return;
        }

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is CharacterEntity character)
            {
                character.RechargeShields(currentTime, elapsedSeconds);
            }
        }
    }
}
