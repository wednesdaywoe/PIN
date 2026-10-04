using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Keeps only the targets that are NPCs. A server-only command whose defs shipped as bare ids, so this reading comes
///     from where it is used: Gravity Pull asks "is the initiator an NPC" (TargetInitiator, TargetByNPC, HasTargets) to
///     pick NPC or player damage numbers, and Absorption Bomb narrows its hostiles to NPCs before pulling them in.
/// </summary>
public class TargetByNPCCommand : Command, ICommand
{
    private TargetByNPCCommandDef Params;

    public TargetByNPCCommand(TargetByNPCCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Targets.RemoveAll(target => target is not CharacterEntity { IsPlayerControlled: false });
        return true;
    }
}
