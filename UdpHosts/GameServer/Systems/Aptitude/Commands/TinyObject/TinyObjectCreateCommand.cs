using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Entities.TinyObject;
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
        // is where the projectile was, unless the def says to use Self's position.
        if (Params == null || Params.TinyObjectId == 0)
        {
            Logger.Debug("TinyObjectCreateCommand {CommandId} has no tiny object filled in, nothing created", Params?.Id);
            return true;
        }

        var owner = context.Initiator as CharacterEntity ?? context.Initiator?.Owner;
        var position = Params.AtSelf == 1 && context.Self != null ? context.Self.Position : context.InitPosition;

        if (Params.MinSpacing > 0)
        {
            foreach (var entity in context.Shard.Entities.Values)
            {
                if (entity is TinyObjectEntity other && other.TypeId == Params.TinyObjectId && ReferenceEquals(other.Owner, owner)
                    && Vector3.Distance(other.Position, position) < Params.MinSpacing)
                {
                    return true;
                }
            }
        }

        context.Shard.EntityMan.SpawnTinyObject(Params.TinyObjectId, position, owner, context.AbilityId, context.FromUltimate);
        if (Params.AlsoTinyObjectId != 0)
        {
            context.Shard.EntityMan.SpawnTinyObject(Params.AlsoTinyObjectId, position, owner, context.AbilityId, context.FromUltimate);
        }

        return true;
    }
}
