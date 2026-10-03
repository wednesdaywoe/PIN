using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

public class HealDamageCommand : Command, ICommand
{
    // Damage types the client marks as overheal (tfHealDamageCommand, FUN_00ebc1a0): it doesn't cap these at missing health
    private static readonly byte[] OverhealTypes = [9, 10, 13, 25];

    private HealDamageCommandDef Params;

    public HealDamageCommand(HealDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client: the register op on Healpoints, then Weapondamage multiplies by the context's weapon damage
        // (the value SetWeaponDamage writes) and Usedmgdealt by a float scale the context starts at 1. Nothing on the
        // server sets that scale, so Usedmgdealt changes nothing here. The client only reports the heal as negative
        // damage for the combat text; health is the server's to change.
        var amount = (int)AbilitySystem.RegistryOp(context.Register, Params.Healpoints, (Operand)Params.HealpointsRegop);
        if (Params.Weapondamage == 1)
        {
            var character = context.Initiator as CharacterEntity ?? context.Initiator?.Owner;
            var weapon = character?.GetActiveWeaponDetails();
            if (weapon == null)
            {
                Logger.Debug("{Command} {CommandId} heals by weapon damage but {Initiator} has no active weapon", nameof(HealDamageCommand), Params.Id, context.Initiator);
                return true;
            }

            amount = (int)(amount * character.GetEffectiveWeaponDamage(weapon.Weapon));
        }

        if (amount <= 0)
        {
            Logger.Debug("{Command} {CommandId} computed {Amount}, nothing to heal", nameof(HealDamageCommand), Params.Id, amount);
            return true;
        }

        if (System.Array.IndexOf(OverhealTypes, Params.DamageType) >= 0)
        {
            Logger.Debug("{Command} {CommandId} is an overheal type ({DamageType}), capped at max health anyway", nameof(HealDamageCommand), Params.Id, Params.DamageType);
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is CharacterEntity healed)
            {
                var restored = healed.Heal(amount);
                Logger.Debug("{Command} {CommandId} healed {Target} for {Restored} of {Amount}", nameof(HealDamageCommand), Params.Id, target, restored, amount);
            }
        }

        return true;
    }
}
