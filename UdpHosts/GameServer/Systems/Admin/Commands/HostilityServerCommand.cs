using AeroMessages.GSS.V66;
using GameServer.Entities;
using GameServer.Systems.Hostility;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Show the hostility stance between you and the current target", "hostility", "hostility", "stance")]
public class HostilityServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("Cannot check hostility without a valid player character", context);
            return;
        }

        var source = context.SourcePlayer.CharacterEntity;
        var target = context.Target ?? GetGunTarget(source, context);

        if (target == null)
        {
            SourceFeedback("No target, use /target or aim at something", context);
            return;
        }

        SourceFeedback($"{Describe(source)} -> {Describe(target)}", context);
        SourceFeedback($"Stance {HostilityRules.GetStance(source, target)}, can damage: {HostilityRules.CanDamage(source, target)}", context);
        SourceFeedback($"Reverse stance {HostilityRules.GetStance(target, source)}, can damage: {HostilityRules.CanDamage(target, source)}", context);
    }

    private static string Describe(IEntity entity)
    {
        var info = entity.HostilityInfo;
        var faction = info.Flags.HasFlag(HostilityInfoData.HostilityFlags.Faction) ? info.FactionId.ToString() : "none";
        var team = info.Flags.HasFlag(HostilityInfoData.HostilityFlags.Team) ? info.TeamId.ToString() : "none";

        return $"{entity.AeroEntityId} (faction {faction}, team {team})";
    }
}
