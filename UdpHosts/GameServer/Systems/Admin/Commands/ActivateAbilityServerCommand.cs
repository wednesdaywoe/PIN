using GameServer.StaticDB;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Admin.Commands;

// Runs an ability's chain exactly as a keypress would, except the client never asked for it and so
// never predicted it. That difference is the whole point: it's the one way to run a real chain with
// its real effects and timings while leaving client-side prediction out of the picture, which is
// what separates "this chain's effects don't apply or clear properly" from "the client is holding a
// predicted copy the server can't reach".
[ServerCommand("Activate an ability", "ability <abilityId>", "ability", "useability", "activateability", "apt_ability")]
public class ActivateAbilityServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Invalid number of parameters for ability command", context);
            return;
        }

        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("Cannot activate an ability without a valid player character", context);
            return;
        }

        uint abilityId = ParseUIntParameter(parameters[0]);
        var abilityData = SDBInterface.GetAbilityData(abilityId);
        if (abilityData == null)
        {
            SourceFeedback("No ability with this id", context);
            return;
        }

        if (abilityData.Chain == 0)
        {
            SourceFeedback($"Ability {abilityId} has no chain to run", context);
            return;
        }

        IAptitudeTarget initiator = context.SourcePlayer.CharacterEntity;
        var targets = context.Target is IAptitudeTarget commandTarget
            ? new AptitudeTargets(commandTarget)
            : new AptitudeTargets();

        var shard = context.Shard;
        SourceFeedback($"Activating ability {abilityId} (chain {abilityData.Chain}) server-side, unpredicted", context);
        shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, shard.CurrentTime, targets);
    }
}
