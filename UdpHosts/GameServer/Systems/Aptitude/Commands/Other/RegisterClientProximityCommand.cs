using System.Linq;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Entities.TinyObject;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Hostility;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Asks the client to report when someone comes within Radius of Self; the report arrives as a local proximity
///     success (<see cref="AbilitySystem.HandleLocalProximityAbilitySuccess" />). A tiny object never reaches the
///     client, so for one of those the server watches instead: every RetryInterval ms, while the effect that registered
///     this lasts, up to MaxTargets characters its owner may damage within Radius set off AbilityId and Chain. Fungal
///     Bloom's spore mine (tiny 389) bursts this way, and its blinding cloud (tiny 391) afflicts whoever walks in.
/// </summary>
public class RegisterClientProximityCommand : Command, ICommand
{
    private RegisterClientProximityCommandDef Params;

    public RegisterClientProximityCommand(RegisterClientProximityCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not TinyObjectEntity tiny)
        {
            return true;
        }

        var radius = AbilitySystem.RegistryOp(context.Register, Params.Radius, (Operand)Params.RadiusRegop);
        ScheduleCheck(context, tiny, radius);
        return true;
    }

    private void ScheduleCheck(Context context, TinyObjectEntity tiny, float radius)
    {
        var interval = Params.RetryInterval > 0 ? Params.RetryInterval : 500u;
        context.Abilities.Schedule(unchecked(context.Shard.CurrentTime + interval), context, () => Check(context, tiny, radius));
    }

    private void Check(Context context, TinyObjectEntity tiny, float radius)
    {
        if (!context.Shard.Entities.ContainsKey(tiny.EntityId))
        {
            return;
        }

        var attacker = (Entities.IEntity)tiny.Owner ?? tiny;
        var found = context.Shard.Entities.Values
                           .OfType<CharacterEntity>()
                           .Where(c => c.IsAlive && Vector3.Distance(c.Position, tiny.Position) <= radius && HostilityRules.CanDamage(attacker, c))
                           .OrderBy(c => Vector3.Distance(c.Position, tiny.Position))
                           .Take(Params.MaxTargets > 0 ? (int)Params.MaxTargets : int.MaxValue)
                           .ToArray();

        if (found.Length > 0)
        {
            Logger.Debug("{Command} {CommandId}: {Count} within {Radius} m of tiny object {TypeId}", nameof(RegisterClientProximityCommand), Params.Id, found.Length, radius, tiny.TypeId);
            var now = context.Shard.CurrentTime;
            if (Params.AbilityId != 0)
            {
                context.Abilities.HandleActivateAbility(context.Shard, context.Initiator, Params.AbilityId, now, new AptitudeTargets(found), initPosition: tiny.Position, fromUltimate: context.FromUltimate);
            }

            if (Params.Chain != 0)
            {
                var triggered = Context.CopyContext(context);
                triggered.Targets = new AptitudeTargets(found);
                triggered.InitPosition = tiny.Position;
                triggered.InitTime = now;
                triggered.ExecutionHint = ExecutionHint.Proximity;
                context.Abilities.Factory.LoadChain(Params.Chain).Execute(triggered);
            }
        }

        // Keeps watching while the object and the registering effect last; a mine that burst is gone by now
        if (context.Shard.Entities.ContainsKey(tiny.EntityId))
        {
            ScheduleCheck(context, tiny, radius);
        }
    }
}
