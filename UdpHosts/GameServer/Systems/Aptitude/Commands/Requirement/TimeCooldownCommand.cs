using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class TimeCooldownCommand : Command, ICommand
{
    private TimeCooldownCommandDef Params;

    public TimeCooldownCommand(TimeCooldownCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::TimeCooldownCommand): fails while any of the checked cooldowns is running.
        // Not done: the client also fails within Duration of a time it keeps on the context, which isn't mapped yet.
        var cooldowns = context.Initiator?.Cooldowns;
        if (cooldowns == null)
        {
            return true;
        }

        var now = context.InitTime;
        return (Params.CheckLocal == 0 || cooldowns.IsLocalReady(context.AbilityId, now))
               && (Params.CheckGlobal == 0 || cooldowns.IsGlobalReady(now))
               && (Params.CheckCategory == 0 || Params.Category == 0 || cooldowns.IsCategoryReady(Params.Category, now));
    }
}