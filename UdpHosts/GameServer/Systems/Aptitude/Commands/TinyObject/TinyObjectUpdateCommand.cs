using GameServer.Entities.TinyObject;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.TinyObject;

public class TinyObjectUpdateCommand : Command, ICommand
{
    private TinyObjectUpdateCommandDef Params;

    public TinyObjectUpdateCommand(TinyObjectUpdateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Reconstructed: the def shipped empty. It moves a tiny object to its next stage: Fungal Bloom's spore mine
        // (389) bursts into a poison cloud (390), which then leaves a blinding cloud (391). The stage is filled in by
        // hand as NextTinyObjectId; where none is known the spent object is only removed, as Fuel Air Bomb's blast
        // (440), whose fire patch the create command already made alongside.
        if (context.Self is not TinyObjectEntity tiny)
        {
            return true;
        }

        if (Params?.NextTinyObjectId is > 0 and var next)
        {
            Logger.Debug("TinyObjectUpdateCommand {CommandId}: tiny object {From} becomes {To}", Params.Id, tiny.TypeId, next);
            context.Shard.EntityMan.SpawnTinyObject(next, tiny.Position, tiny.Owner, context.AbilityId, context.FromUltimate);
        }

        context.Shard.EntityMan.RemoveTinyObject(tiny);
        return true;
    }
}
