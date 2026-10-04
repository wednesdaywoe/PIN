using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     Combines one of Self's live stats (an <see cref="AptitudeStat" />) into the register, as the client does
///     (tfLoadRegisterFromStatCommand, 0xbbf810). The data only reads Health (6), MaxHealth (7), DamageDealt (5),
///     MaxEnergy (12) and FireRateModifier (21), MaxHealth in all but a few. Only the health and shield stats are
///     tracked here; any other leaves the register as it was.
/// </summary>
public class LoadRegisterFromStatCommand : Command, ICommand
{
    private LoadRegisterFromStatCommandDef Params;

    public LoadRegisterFromStatCommand(LoadRegisterFromStatCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var stat = (AptitudeStat)Params.Stat;
        float? value = context.Self is CharacterEntity character
                           ? stat switch
                           {
                               AptitudeStat.Health => character.CurrentHealth,
                               AptitudeStat.MaxHealth => character.MaxHealth.Value,
                               AptitudeStat.Shields => character.CurrentShields,
                               AptitudeStat.MaxShields => character.MaxShields.Value,
                               _ => null,
                           }
                           : null;

        if (value == null)
        {
            Logger.Debug("{Command} {CommandId}: stat {Stat} of {Self} is not tracked, register left at {Register}", nameof(LoadRegisterFromStatCommand), Params.Id, stat, context.Self, context.Register);
            return true;
        }

        var before = context.Register;
        context.Register = AbilitySystem.RegistryOp(before, value.Value, (Operand)Params.Regop);
        Logger.Debug("{Command} {CommandId}: ({Before}, {Value} ({Stat}), {Op}) => {Register}", nameof(LoadRegisterFromStatCommand), Params.Id, before, value.Value, stat, (Operand)Params.Regop, context.Register);
        return true;
    }
}
