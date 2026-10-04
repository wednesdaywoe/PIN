using System.Runtime.CompilerServices;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Duration;

/// <summary>
///     Keeps an effect going while its character wears the battleframe it had when the effect started (Notchanged), so
///     an effect tied to a frame ends at a frame swap. Crater's dome lockout (effect 8322) lasts this long. Classtype is
///     unread, 0 in every def seen; Notchanged = 0 passes, its meaning unknown.
/// </summary>
public class BattleFrameDurationCommand : Command, ICommand
{
    private readonly ConditionalWeakTable<Context, StrongBox<uint>> _startingFrame = new();

    private BattleFrameDurationCommandDef Params;

    public BattleFrameDurationCommand(BattleFrameDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Notchanged != 1 || context.Self is not CharacterEntity { CurrentLoadout: { } loadout } character)
        {
            return Params.Negate != 1;
        }

        var start = _startingFrame.GetValue(context, _ => new StrongBox<uint>(loadout.ChassisID)).Value;
        var unchanged = loadout.ChassisID == start;
        if (!unchanged)
        {
            Logger.Debug("{Command} {CommandId}: {Character} changed battleframe {Start} -> {Now}", nameof(BattleFrameDurationCommand), Params.Id, character, start, loadout.ChassisID);
        }

        return unchanged != (Params.Negate == 1);
    }
}
