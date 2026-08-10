using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Effect;

public class ApplyPermanentEffectCommand : Command, ICommand
{
    private ApplyPermanentEffectCommandDef Params;

    public ApplyPermanentEffectCommand(ApplyPermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The original aptgss table was server side only and is lost; EffectId comes from
        // curated custom data and is 0 for rows that have not been researched yet.
        if (Params.EffectId == 0)
        {
            Logger.Debug("{Command} {CommandId} has no curated EffectId, skipping", nameof(ApplyPermanentEffectCommand), Params.Id);
            return true;
        }

        if (Params.DurationSeconds != 0)
        {
            Logger.Debug("{Command} {CommandId} specifies DurationSeconds {Duration} which is not implemented", nameof(ApplyPermanentEffectCommand), Params.Id, Params.DurationSeconds);
        }

        context.Abilities.DoApplyEffect(Params.EffectId, context.Self, context);
        return true;
    }
}
