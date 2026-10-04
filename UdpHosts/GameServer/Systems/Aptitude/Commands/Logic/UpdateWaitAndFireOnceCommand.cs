using System.Runtime.CompilerServices;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Logic;

/// <summary>
///     Sits in an effect's Update chain: once Duration ms (register-adjusted by Regop) have passed since the ability
///     started, runs Chain one time for that effect. Always succeeds, so the rest of the Update chain carries on. As
///     the client does it (apt::UpdateWaitAndFireOnceCommand, 0xbba120): a computed duration of 0 never fires, and the
///     "fired" mark is kept per context. Fuel Air Bomb's throw, Bash's and Fortify's damage and Gravity Pull's damage
///     all run from one of these.
/// </summary>
public class UpdateWaitAndFireOnceCommand : Command, ICommand
{
    private static readonly object Fired = new();

    private readonly ConditionalWeakTable<Context, object> _fired = new();

    private UpdateWaitAndFireOnceCommandDef Params;

    public UpdateWaitAndFireOnceCommand(UpdateWaitAndFireOnceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var duration = AbilitySystem.RegistryOp(context.Register, Params.Duration, (Enums.Operand)Params.Regop);
        if (duration <= 0 || Params.Chain == 0 || context.Shard.CurrentTime - context.InitTime < duration)
        {
            return true;
        }

        if (_fired.TryGetValue(context, out _))
        {
            return true;
        }

        _fired.AddOrUpdate(context, Fired);
        Logger.Debug("{Command} {CommandId}: firing chain {Chain} after {Duration} ms", nameof(UpdateWaitAndFireOnceCommand), Params.Id, Params.Chain, duration);
        context.Abilities.Factory.LoadChain(Params.Chain).Execute(context);
        return true;
    }
}
