using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     Passes when the character's equipped items give a nonzero AttributeId, as the client's
///     tfRequireItemAttributeCommand (0xeab310) decides it. Variants of one ability branch on this: Crater only takes its
///     one-use dome lockout (effect 8322) on a module with Crater Dome health (1254).
/// </summary>
public class RequireItemAttributeCommand : Command, ICommand
{
    private RequireItemAttributeCommandDef Params;

    public RequireItemAttributeCommand(RequireItemAttributeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if ((context.Self ?? context.Initiator) is not CharacterEntity character)
        {
            Logger.Debug("{Command} {CommandId} has no character to read attribute {AttributeId} from", nameof(RequireItemAttributeCommand), Params.Id, Params.AttributeId);
            return false;
        }

        var value = character.GetItemAttribute((ushort)Params.AttributeId);
        Logger.Debug("{Command} {CommandId}: attribute {AttributeId} is {Value} on {Character}", nameof(RequireItemAttributeCommand), Params.Id, Params.AttributeId, value, character);
        return value != 0f;
    }
}
