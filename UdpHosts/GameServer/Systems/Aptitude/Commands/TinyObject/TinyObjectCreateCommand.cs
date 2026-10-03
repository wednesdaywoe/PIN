using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.TinyObject;

public class TinyObjectCreateCommand : Command, ICommand
{
    private TinyObjectCreateCommandDef Params;

    public TinyObjectCreateCommand(TinyObjectCreateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Server-only and shipped without its fields, so which tiny object to make is filled in by hand in the def file,
        // only where it could be identified. The object appears at InitPosition, which for an impact or period ability
        // is where the projectile was.
        if (Params == null || Params.TinyObjectId == 0)
        {
            Logger.Debug("TinyObjectCreateCommand {CommandId} has no tiny object filled in, nothing created", Params?.Id);
            return true;
        }

        var owner = context.Initiator as CharacterEntity ?? context.Initiator?.Owner;
        context.Shard.EntityMan.SpawnTinyObject(Params.TinyObjectId, context.InitPosition, owner, context.AbilityId);
        return true;
    }
}
