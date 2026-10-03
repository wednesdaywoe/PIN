using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

/// <summary>
///     The second press of a throw-then-trigger ability (Poison Ball, Shield Wall, Electrical Storm): bursts every
///     projectile of <c>AmmoTypeId</c> the shooter still has in the air, running the ammo's airburst ability where
///     each one is now. Nothing in flight is not a failure; the throw may already have landed.
/// </summary>
public class DetonateProjectilesCommand : Command, ICommand
{
    private DetonateProjectilesCommandDef Params;

    public DetonateProjectilesCommand(DetonateProjectilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var shooter = context.Initiator as CharacterEntity ?? context.Initiator?.Owner ?? context.Self as CharacterEntity;
        if (shooter == null)
        {
            return true;
        }

        var detonated = context.Shard.ProjectileSim.Detonate(shooter, Params.AmmoTypeId);
        Logger.Debug("{Command} {CommandId} detonated {Count} projectile(s) of ammo {AmmoType}", nameof(DetonateProjectilesCommand), Params.Id, detonated, Params.AmmoTypeId);
        return true;
    }
}
