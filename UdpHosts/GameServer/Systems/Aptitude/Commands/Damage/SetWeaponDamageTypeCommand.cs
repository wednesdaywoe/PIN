using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class SetWeaponDamageTypeCommand : Command, ICommand
{
    private SetWeaponDamageTypeCommandDef Params;

    public SetWeaponDamageTypeCommand(SetWeaponDamageTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = context.Self as CharacterEntity ?? context.Self?.Owner;
        if (character == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve a character from {Self}", nameof(SetWeaponDamageTypeCommand), Params.Id, context.Self);
            return true;
        }

        if (Params.BonusAmt != 0)
        {
            // Semantics of BonusAmt are unresearched (flat bonus vs multiplier), so it is not applied yet
            Logger.Debug("{Command} {CommandId} specifies BonusAmt {BonusAmt} which is not implemented", nameof(SetWeaponDamageTypeCommand), Params.Id, Params.BonusAmt);
        }

        var prior = new WeaponDamageTypeActiveContext
        {
            Character = character,
            PriorOverride = character.WeaponDamageTypeOverride,
        };

        character.WeaponDamageTypeOverride = Params.DamageType;
        context.Actives.TryAdd(this, prior);
        return true;
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not WeaponDamageTypeActiveContext prior)
        {
            return;
        }

        prior.Character.WeaponDamageTypeOverride = prior.PriorOverride;
    }
}

public class WeaponDamageTypeActiveContext : ICommandActiveContext
{
    public CharacterEntity Character { get; set; }
    public byte? PriorOverride { get; set; }
}
