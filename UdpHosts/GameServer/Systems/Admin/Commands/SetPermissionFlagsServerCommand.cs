using System;
using System.Linq;
using GameServer.Entities.Character;
using static AeroMessages.GSS.V66.Character.Controller.PermissionFlagsData;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Toggles one of the character's permission flags by name. Several of them gate client-side
///     features outright — <c>detect_resources</c> is what turns on the scanning HUD, for instance —
///     so being able to flip one at a session is how those get tested at all.
/// </summary>
[ServerCommand("Toggle a character permission flag", "pflags [flag|list]", "pflags", "pflag", "float")]
public class SetPermissionFlagsServerCommand : ServerCommand
{
    /// <summary>Kept for the alias this command has always answered to; `float` alone still floats.</summary>
    private const CharacterPermissionFlags DefaultFlag = CharacterPermissionFlags.cheat_float;

    /// <summary>Arbitrary, and only needs to not collide with a real modifier's reference.</summary>
    private const uint FloatModifierRef = 99999991;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("Cannot change permission flags without a valid player character", context);
            return;
        }

        if (parameters.Length > 0 && parameters[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var names = Enum.GetNames<CharacterPermissionFlags>().Where(n => !n.StartsWith("unk_", StringComparison.Ordinal));
            SourceFeedback($"Flags: {string.Join(", ", names)}", context);
            return;
        }

        var flag = DefaultFlag;
        if (parameters.Length > 0 && !Enum.TryParse(parameters[0], ignoreCase: true, out flag))
        {
            SourceFeedback($"No permission flag named '{parameters[0]}' — try `pflags list`", context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (context.Target != null && context.Target is CharacterEntity commandTarget)
        {
            character = commandTarget;
        }

        var newValue = !character.CurrentPermissions[flag];
        character.SetPermissionFlag(flag, newValue);

        SourceFeedback($"Setting CharacterPermissionFlags.{flag} to {newValue}", context);

        if (flag != CharacterPermissionFlags.cheat_float)
        {
            return;
        }

        if (newValue)
        {
            character.AddStatModifier(FloatModifierRef, new CharacterEntity.ActiveStatModifier { Stat = Enums.StatModifierIdentifier.GravityMult, Value = 50, Op = 2 });
        }
        else
        {
            character.RemoveStatModifier(FloatModifierRef, Enums.StatModifierIdentifier.GravityMult);
        }
    }
}
