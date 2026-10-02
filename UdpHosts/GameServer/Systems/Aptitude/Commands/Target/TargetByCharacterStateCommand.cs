using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using static AeroMessages.GSS.V66.Character.CharacterStateData;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByCharacterStateCommand : Command, ICommand
{
    private TargetByCharacterStateCommandDef Params;

    public TargetByCharacterStateCommand(TargetByCharacterStateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetByCharacterStateCommand): filters in place and keeps targets that are not characters
        context.Targets.RemoveAll(target => target is CharacterEntity character && !Matches(character.CharacterState.State));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }

    private bool Matches(CharacterStatus state)
    {
        return (Params.Respawning == 1 && state == CharacterStatus.Respawning)
               || (Params.Incapacitated == 1 && state == CharacterStatus.Incapacitated)
               || (Params.Traumatized == 1 && state == CharacterStatus.Traumatized)
               || (Params.Ghost == 1 && state == CharacterStatus.Ghost)
               || (Params.Living == 1 && state == CharacterStatus.Living)
               || (Params.Dead == 1 && state == CharacterStatus.Dead)
               || (Params.Spawning == 1 && state == CharacterStatus.Spawning);
    }
}