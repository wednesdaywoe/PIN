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
        string blocking = null;
        uint readyAgain = 0;
        if (Params.CheckLocal != 0 && !cooldowns.IsLocalReady(context.AbilityId, now))
        {
            (blocking, readyAgain) = ("local", cooldowns.Local[context.AbilityId].ReadyAgainTime);
        }
        else if (Params.CheckGlobal != 0 && !cooldowns.IsGlobalReady(now))
        {
            (blocking, readyAgain) = ("global", cooldowns.GlobalReadyAgainTime);
        }
        else if (Params.CheckCategory != 0 && Params.Category != 0 && !cooldowns.IsCategoryReady(Params.Category, now))
        {
            (blocking, readyAgain) = ($"category {Params.Category}", cooldowns.Category[Params.Category].ReadyAgainTime);
        }

        if (blocking == null)
        {
            return true;
        }

        Logger.Information("Ability {AbilityId} refused: {Cooldown} cooldown has {Remaining} ms left", context.AbilityId, blocking, (int)(readyAgain - now));
        return false;
    }
}