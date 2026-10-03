using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     Combines the top of the register stack into the register with Regop, leaving the stack as it is. Fails on an
///     empty stack, as the client's does.
/// </summary>
public class PeekRegisterCommand : Command, ICommand
{
    private PeekRegisterCommandDef Params;

    public PeekRegisterCommand(PeekRegisterCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (!context.RegisterStack.TryPeek(out var top))
        {
            Logger.Debug("{Command} {CommandId}: register stack is empty", nameof(PeekRegisterCommand), Params.Id);
            return false;
        }

        float prevValue = context.Register;
        context.Register = AbilitySystem.RegistryOp(prevValue, top, (Operand)Params.Regop);
        Logger.Debug("{Command} {CommandId}: ({prevValue}, {top}, {op}) => {register}", nameof(PeekRegisterCommand), Params.Id, prevValue, top, (Operand)Params.Regop, context.Register);
        return true;
    }
}
