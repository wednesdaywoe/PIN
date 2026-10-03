using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     Saves the register on the context's register stack. Every shipped def uses Regop 0, which leaves the register as
///     it is. An effect keeps one context for its whole life, so a value pushed by its apply chain is still there for its
///     update ticks: Creeping Death's cloud pushes its starting size and grows it by one each tick.
/// </summary>
public class PushRegisterCommand : Command, ICommand
{
    // The client refuses past 100, logging "Register stack overflow"
    private const int MaxDepth = 100;

    private PushRegisterCommandDef Params;

    public PushRegisterCommand(PushRegisterCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.RegisterStack.Count > MaxDepth)
        {
            Logger.Warning("{Command} {CommandId}: register stack overflow in ability {AbilityId}", nameof(PushRegisterCommand), Params.Id, context.AbilityId);
            return true;
        }

        context.RegisterStack.Push(context.Register);
        return true;
    }
}
