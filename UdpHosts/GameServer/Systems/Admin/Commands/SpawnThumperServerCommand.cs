using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Calls a thumper down at the player's feet, owned by the player.
/// </summary>
/// <remarks>
///     The retail route to a thumper is the client's own calldown UI, which sends
///     <c>ResourceNodeBeaconCalldownRequest</c> and needs a beacon item in the inventory to offer the
///     option at all — and item delivery is what
///     <see href="../../../../Docs/In-Game-Tests/Inventory.html">I1</see> says is broken. This is the way
///     in that doesn't depend on that, so the completion payout can be tested on its own.
///
///     Ownership is the whole point of the command. Zone 448 already spawns a debug thumper in
///     <c>TempSpawnTestEntities</c>, but the Aero owns it, and an NPC contributes no participant, so
///     that one pays nobody when it finishes.
/// </remarks>
[ServerCommand(
    "Call down a thumper where you are standing",
    "thumper [<beaconCalldownDefId>]",
    "thumper",
    "spawn_thumper")]
public class SpawnThumperServerCommand : ServerCommand
{
    // The def the Coral Forest debug thumper uses, and the only one that has ever been seen working.
    private const uint DefaultBeaconCalldownDefId = 766269;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("Need a player character to own the thumper", context);
            return;
        }

        uint defId = parameters.Length > 0 ? ParseUIntParameter(parameters[0]) : DefaultBeaconCalldownDefId;

        var commandDef = SDBInterface.GetResourceNodeBeaconCalldownCommandDef(defId);
        if (commandDef == null)
        {
            SourceFeedback($"No ResourceNodeBeaconCalldownCommandDef {defId}", context);
            return;
        }

        var position = character.Position;

        // Same resolution the real calldown path uses: the deposit under your feet, or barren ground.
        var nodeType = context.Shard.Resources.ResolveNodeType(position);
        context.Shard.EncounterMan.CreateThumper(nodeType, position, character, commandDef);

        if (character.PlacedPosition != null)
        {
            SourceFeedback("Warning: you were put here rather than having walked here, so this may be inside the terrain", context);
        }

        var deposit = context.Shard.Resources.FindDepositAt(position);
        SourceFeedback(
            deposit == null
                ? $"Thumper {defId} called down at {position}, on barren ground (node type {nodeType})"
                : $"Thumper {defId} called down at {position}, in deposit [{deposit.Id}] {deposit.Name} (node type {nodeType})",
            context);

        Logger.Information(
            "thumper {DefId} for {Owner} at {Position}, calldown {CalldownTimeMs}ms, landed ability {LandedAbility}, completed ability {CompletedAbility}",
            defId,
            character.EntityId,
            position,
            commandDef.CalldownTimeMs,
            commandDef.LandedAbility,
            commandDef.CompletedAbility);
    }
}
