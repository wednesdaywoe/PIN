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
        // Reconstructed: the def shipped empty. It ends a tiny object's one-shot effect (Fuel Air Bomb's blast, tiny 440)
        // and presumably turned the object into its next stage. What that stage was isn't in the data; where it is known
        // the create command makes it alongside (also_tiny_object_id), so here the spent object is only removed, which
        // otherwise would sit in the world forever.
        if (context.Self is TinyObjectEntity tiny)
        {
            context.Shard.EntityMan.RemoveTinyObject(tiny);
        }

        return true;
    }
}
