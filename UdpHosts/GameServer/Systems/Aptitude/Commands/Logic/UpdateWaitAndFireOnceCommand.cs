using System.Runtime.CompilerServices;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Logic;

/// <summary>
///     Sits in an effect's Update chain: once Duration ms (register-adjusted by Regop) have passed since the ability
///     started, runs Chain one time for that effect. Always succeeds, so the rest of the Update chain carries on. As
///     the client does it (apt::UpdateWaitAndFireOnceCommand, 0xbba120): a computed duration of 0 never fires, and the
///     "fired" mark is kept per context. Fuel Air Bomb's and Fungal Bloom's throws, Assassinate's strike and Gravity
///     Pull's damage all run from one of these.
///
///     The client checks every frame. Here an effect is only looked at every UpdateFrequency ms, so a wait that ends
///     between looks is booked with <see cref="AbilitySystem.Schedule" /> to fire on time, provided the effect lasts.
/// </summary>
public class UpdateWaitAndFireOnceCommand : Command, ICommand
{
    private readonly ConditionalWeakTable<Context, StrongBox<bool>> _booked = new();

    private UpdateWaitAndFireOnceCommandDef Params;

    public UpdateWaitAndFireOnceCommand(UpdateWaitAndFireOnceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var duration = AbilitySystem.RegistryOp(context.Register, Params.Duration, (Enums.Operand)Params.Regop);
        if (duration <= 0 || Params.Chain == 0 || _booked.TryGetValue(context, out _))
        {
            return true;
        }

        var fired = new StrongBox<bool>();
        _booked.AddOrUpdate(context, fired);
        var due = unchecked(context.InitTime + (uint)duration);
        if (unchecked((int)(context.Shard.CurrentTime - due)) >= 0)
        {
            Fire(context, fired, duration);
        }
        else
        {
            context.Abilities.Schedule(due, context, () => Fire(context, fired, duration));
        }

        return true;
    }

    private void Fire(Context context, StrongBox<bool> fired, float duration)
    {
        if (fired.Value)
        {
            return;
        }

        fired.Value = true;
        Logger.Debug("{Command} {CommandId}: firing chain {Chain} after {Duration} ms", nameof(UpdateWaitAndFireOnceCommand), Params.Id, Params.Chain, duration);
        var hint = context.ExecutionHint;
        context.ExecutionHint = ExecutionHint.UpdateEffect;
        context.Abilities.Factory.LoadChain(Params.Chain).Execute(context);
        context.ExecutionHint = hint;
    }
}
