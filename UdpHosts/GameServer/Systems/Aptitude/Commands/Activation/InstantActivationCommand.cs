using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude.Commands.Cooldown;

namespace GameServer.Systems.Aptitude.Commands.Activation;

public class InstantActivationCommand : Command, ICommand
{
    private readonly InflictCooldownCommand _inflictCooldown;

    public InstantActivationCommand(InstantActivationCommandDef par)
: base(par)
    {
        // As the client (apt::InstantActivationCommand), which holds an InflictCooldownCommand built from the same fields.
        // Not done: the client skips it and succeeds when an object on its context says so; that object isn't mapped yet.
        _inflictCooldown = new InflictCooldownCommand(new InflictCooldownCommandDef
        {
            Id = par.Id,
            LocalCooldown = par.LocalCooldown,
            LocalCooldownPrecoolCount = par.LocalCooldownPrecoolCount,
            GlobalCooldown = par.GlobalCooldown,
            CategoryCooldown = par.CategoryCooldown,
            CategoryCooldownPrecoolCount = par.CategoryCooldownPrecoolCount,
            Category = par.Category,
            DurationRegop = par.DurationRegop,
            PrecoolRegop = par.PrecoolRegop,
            CategoryPrecoolRegop = par.CategoryPrecoolRegop,
            PreventReset = par.PreventReset,
        });
    }

    public bool Execute(Context context)
    {
        return _inflictCooldown.Execute(context);
    }
}
