using System;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Cooldown;

public class InflictCooldownCommand : Command, ICommand
{
    private InflictCooldownCommandDef Params;

    public InflictCooldownCommand(InflictCooldownCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::InflictCooldownCommand): DurationRegop applies to both the local and the category
        // duration, and a precool count of N leaves N - 1 durations already run, which gives N charges.
        // Not done: the client takes the local ability id from two context fields before the ability id, and sets the
        // MainSlot flag from a context field too; neither is mapped yet.
        var cooldowns = context.Initiator?.Cooldowns;
        if (cooldowns == null)
        {
            return false;
        }

        var now = context.InitTime;
        var localDuration = Duration(context, Params.LocalCooldown);
        var categoryDuration = Duration(context, Params.CategoryCooldown);
        var flags = (byte)((Params.PreventReset != 0 ? CooldownSet.PreventResetFlag : 0) | (Params.MainSlot != 0 ? CooldownSet.MainSlotFlag : 0));

        if (Params.GlobalCooldown != 0)
        {
            cooldowns.InflictGlobal(now, Params.GlobalCooldown);
        }

        if (Params.LocalCooldown != 0)
        {
            var precool = PrecoolTime(context, Params.LocalCooldownPrecoolCount, Params.PrecoolRegop, localDuration);
            cooldowns.InflictLocal(context.AbilityId, now, localDuration, precool, flags);
        }

        if (Params.CategoryCooldown != 0 && Params.Category != 0)
        {
            var precool = PrecoolTime(context, Params.CategoryCooldownPrecoolCount, Params.CategoryPrecoolRegop, categoryDuration);
            cooldowns.InflictCategory(Params.Category, now, categoryDuration, precool, flags);
        }

        Logger.Information(
            "Ability {AbilityId} inflicts cooldowns: global {Global} ms, local {Local} ms, category {Category} {CategoryDuration} ms",
            context.AbilityId,
            Params.GlobalCooldown,
            Params.LocalCooldown != 0 ? localDuration : 0,
            Params.Category,
            Params.CategoryCooldown != 0 && Params.Category != 0 ? categoryDuration : 0);

        if (context.Initiator is CharacterEntity { IsPlayerControlled: true } character)
        {
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(new AbilityCooldowns { Data = cooldowns.ToData(now) }, character.EntityId);
        }

        return true;
    }

    private uint Duration(Context context, uint duration)
    {
        return (uint)Math.Max(0, (int)AbilitySystem.RegistryOp(context.Register, duration, (Operand)Params.DurationRegop));
    }

    private static uint PrecoolTime(Context context, uint count, byte regop, uint duration)
    {
        var charges = (int)AbilitySystem.RegistryOp(context.Register, count, (Operand)regop);
        return (uint)Math.Max(charges - 1, 0) * duration;
    }
}
