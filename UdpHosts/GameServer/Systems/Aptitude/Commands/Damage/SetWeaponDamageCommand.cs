using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class SetWeaponDamageCommand : Command, ICommand
{
    private SetWeaponDamageCommandDef Params;

    public SetWeaponDamageCommand(SetWeaponDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = context.Self as CharacterEntity ?? context.Self?.Owner;
        if (character == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve a character from {Self}", nameof(SetWeaponDamageCommand), Params.Id, context.Self);
            return true;
        }

        float value = AbilitySystem.RegistryOp(context.Register, Params.Dmgmaxvalue, (Operand)Params.DamageRegop);
        if (Params.Lerpfallheight == 1 || Params.Lerpenergy == 1)
        {
            // Fall height and energy are client simulated; until they are tracked we can only use the max value
            Logger.Debug("{Command} {CommandId} lerps by fallheight/energy which is not simulated, using max value {Value}", nameof(SetWeaponDamageCommand), Params.Id, value);
        }

        var prior = new WeaponDamageActiveContext
        {
            Character = character,
            PriorOverride = character.WeaponDamageOverride,
            PriorMultiplier = character.WeaponDamageMultiplier,
        };

        if (Params.Multiply == 1)
        {
            character.WeaponDamageMultiplier = value;
        }
        else
        {
            character.WeaponDamageOverride = value;
        }

        context.Actives.TryAdd(this, prior);
        return true;
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not WeaponDamageActiveContext prior)
        {
            return;
        }

        prior.Character.WeaponDamageOverride = prior.PriorOverride;
        prior.Character.WeaponDamageMultiplier = prior.PriorMultiplier;
    }
}

public class WeaponDamageActiveContext : ICommandActiveContext
{
    public CharacterEntity Character { get; set; }
    public float? PriorOverride { get; set; }
    public float PriorMultiplier { get; set; }
}
