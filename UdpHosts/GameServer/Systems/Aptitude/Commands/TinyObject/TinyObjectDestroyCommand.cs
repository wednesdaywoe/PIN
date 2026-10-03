using GameServer.Entities.TinyObject;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.TinyObject;

public class TinyObjectDestroyCommand : Command, ICommand
{
    private TinyObjectDestroyCommandDef Params;

    public TinyObjectDestroyCommand(TinyObjectDestroyCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Reconstructed: the def shipped empty. Every use seen sits in a tiny object's own remove chain, where the
        // object being destroyed is the one the effect is on.
        if (context.Self is TinyObjectEntity tiny)
        {
            context.Shard.EntityMan.RemoveTinyObject(tiny);
        }

        return true;
    }
}
