using AeroMessages.GSS.V66.Character.Controller;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class ConsumeSuperChargeCommand : Command, ICommand
{
    private ConsumeSuperChargeCommandDef Params;

    public ConsumeSuperChargeCommand(ConsumeSuperChargeCommandDef par)
    : base(par)
    {
        Params = par;
    }

    /// <summary>
    ///     Takes Percent points off the ultimate meter, a percent of a full meter rather than of what's left; almost every
    ///     def spends 100. A negative Percent is a gain (-5 on a kill-type event, -500 to refill). Kept within 0 to 100
    ///     unless AllowOvercharge.
    /// </summary>
    public bool Execute(Context context)
    {
        if ((context.Self ?? context.Initiator) is CharacterEntity character)
        {
            var percent = AbilitySystem.RegistryOp(context.Register, Params.Percent, (Operand)Params.PercentRegop);
            var before = character.SuperCharge;
            character.SetSuperCharge(before - percent, Params.AllowOvercharge == 1);
            Logger.Debug("{Command} {CommandId}: ultimate meter {Before} -> {After} on {Character}", nameof(ConsumeSuperChargeCommand), Params.Id, before, character.SuperCharge, character);
            return true;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(ConsumeSuperChargeCommand), Params.Id);

        return false;
    }
}