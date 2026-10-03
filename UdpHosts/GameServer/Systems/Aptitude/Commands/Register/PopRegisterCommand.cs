using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     Takes the top off the register stack and combines it into the register with Regop. Fails on an empty stack, as
///     the client's does.
/// </summary>
public class PopRegisterCommand : Command, ICommand
{
    private PopRegisterCommandDef Params;

    public PopRegisterCommand(PopRegisterCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (!context.RegisterStack.TryPop(out var top))
        {
            Logger.Debug("{Command} {CommandId}: register stack is empty", nameof(PopRegisterCommand), Params.Id);
            return false;
        }

        float prevValue = context.Register;
        context.Register = AbilitySystem.RegistryOp(prevValue, top, (Operand)Params.Regop);
        Logger.Debug("{Command} {CommandId}: ({prevValue}, {top}, {op}) => {register}", nameof(PopRegisterCommand), Params.Id, prevValue, top, (Operand)Params.Regop, context.Register);
        return true;
    }
}
