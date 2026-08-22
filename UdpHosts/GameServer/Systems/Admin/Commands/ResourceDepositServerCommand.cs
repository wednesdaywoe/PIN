using System.Globalization;
using System.Linq;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Places a zone's resource deposits by walking to where their centers should be.
/// </summary>
/// <remarks>
///     Same doctrine as <see cref="SpawnGroupServerCommand"/>: the server holds no terrain, so a
///     player standing somewhere is the only proof a coordinate is real ground, and the running game
///     is the editor. A deposit only decides things in X and Y, so the stakes are lower than a
///     monster's — but a center nobody can stand on is still a deposit nobody can thump.
/// </remarks>
[ServerCommand(
    "Place a resource deposit centered where you are standing",
    "deposit list | add <nodeTypeId> [radius] [richness] | remove <depositId> | radius <depositId> <metres> | richness <depositId> <multiplier> | reload",
    "deposit")]
public class ResourceDepositServerCommand : ServerCommand
{
    private const float DefaultRadius = 35f;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length == 0)
        {
            SourceFeedback("deposit list | add <nodeTypeId> [radius] [richness] | remove <depositId> | radius <depositId> <metres> | richness <depositId> <multiplier> | reload", context);
            return;
        }

        var zoneId = context.Shard.ZoneId;

        switch (parameters[0].ToLowerInvariant())
        {
            case "list": List(zoneId, context); break;
            case "add": Add(zoneId, parameters, context); break;
            case "remove": Remove(zoneId, parameters, context); break;
            case "radius": Radius(zoneId, parameters, context); break;
            case "richness": Richness(zoneId, parameters, context); break;
            case "reload": Reload(context); break;
            default: SourceFeedback($"Unknown deposit verb '{parameters[0]}'", context); break;
        }
    }

    private static ResourceDeposit Resolve(uint zoneId, string parameter, out uint depositId)
    {
        var deposits = CustomDBInterface.GetZoneResourceDeposits(zoneId);

        if (!uint.TryParse(parameter, out depositId) || !deposits.TryGetValue(depositId, out var deposit))
        {
            return null;
        }

        return deposit;
    }

    private void List(uint zoneId, ServerCommandContext context)
    {
        var deposits = CustomDBInterface.GetZoneResourceDeposits(zoneId);

        if (deposits.Count == 0)
        {
            SourceFeedback($"Zone {zoneId} has no resource deposits", context);
            return;
        }

        foreach (var deposit in deposits.Values.OrderBy(d => d.Id))
        {
            var nodeType = SDBInterface.GetResourceNodeType(deposit.NodeTypeId);
            var distance = context.SourcePlayer?.CharacterEntity != null
                ? $", {System.Numerics.Vector3.Distance(deposit.Position, context.SourcePlayer.CharacterEntity.Position):0}m away"
                : string.Empty;
            var richness = deposit.Richness > 0f && deposit.Richness != 1f ? $", richness x{deposit.Richness:0.##}" : string.Empty;
            SourceFeedback(
                $"[{deposit.Id}] {deposit.Name}: node type {deposit.NodeTypeId} ({nodeType?.Name ?? "unknown"}), radius {deposit.Radius:0}m{richness} at {deposit.Position}{distance}",
                context);
        }
    }

    private void Add(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.CharacterEntity == null)
        {
            SourceFeedback("Placing a deposit needs a player character to stand at its center", context);
            return;
        }

        if (parameters.Length is < 2 or > 4)
        {
            SourceFeedback("deposit add <nodeTypeId> [radius] [richness]", context);
            return;
        }

        var nodeTypeId = ParseUIntParameter(parameters[1]);
        var nodeType = SDBInterface.GetResourceNodeType(nodeTypeId);
        if (nodeType == null)
        {
            SourceFeedback($"No ResourceNodeType {nodeTypeId}", context);
            return;
        }

        if (SDBInterface.GetResourceNodeTypeResources(nodeTypeId).Count == 0)
        {
            SourceFeedback($"Node type {nodeTypeId} ({nodeType.Name}) has no yield rows, so a deposit of it pays nothing", context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;

        if (character.IsAirborne)
        {
            SourceFeedback("You are airborne. Land first, or the center is placed where you are floating.", context);
            return;
        }

        if (character.PlacedPosition != null)
        {
            SourceFeedback("You were put here rather than walking here, which is not proof of ground. Walk a few steps first.", context);
            return;
        }

        var radius = parameters.Length >= 3 ? ParseUIntParameter(parameters[2]) : DefaultRadius;

        var richness = 1f;
        if (parameters.Length == 4 && !TryParseRichness(parameters[3], out richness, context))
        {
            return;
        }

        var deposit = new ResourceDeposit
        {
            Id = CustomDBInterface.NextResourceDepositId(zoneId),
            ZoneId = zoneId,
            Name = nodeType.Name,
            NodeTypeId = nodeTypeId,
            Position = character.Position,
            Radius = radius,
            Richness = richness,
        };

        CustomDBInterface.AddResourceDeposit(deposit);
        CustomDBInterface.SaveResourceDeposits();

        SourceFeedback($"[{deposit.Id}] {deposit.Name}: centered at {deposit.Position}, radius {deposit.Radius:0}m, richness x{deposit.Richness:0.##}", context);
    }

    private void Remove(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 2)
        {
            SourceFeedback("deposit remove <depositId>", context);
            return;
        }

        var deposit = Resolve(zoneId, parameters[1], out var depositId);
        if (deposit == null)
        {
            SourceFeedback($"Zone {zoneId} has no deposit {depositId}", context);
            return;
        }

        CustomDBInterface.RemoveResourceDeposit(zoneId, depositId);
        CustomDBInterface.SaveResourceDeposits();
        SourceFeedback($"Removed deposit [{depositId}] {deposit.Name}", context);
    }

    private void Radius(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 3)
        {
            SourceFeedback("deposit radius <depositId> <metres>", context);
            return;
        }

        var deposit = Resolve(zoneId, parameters[1], out var depositId);
        if (deposit == null)
        {
            SourceFeedback($"Zone {zoneId} has no deposit {depositId}", context);
            return;
        }

        deposit.Radius = ParseUIntParameter(parameters[2]);
        CustomDBInterface.SaveResourceDeposits();
        SourceFeedback($"[{depositId}] {deposit.Name} now reaches {deposit.Radius:0}m from its center", context);
    }

    /// <summary>
    ///     Bounded rather than open, because the two ways to get this wrong both look like the payout
    ///     code failing: zero pays nothing and reads as a broken thumper, and a figure large enough to
    ///     overflow the rounding pays a number nobody asked for.
    /// </summary>
    private bool TryParseRichness(string parameter, out float richness, ServerCommandContext context)
    {
        if (!float.TryParse(parameter, NumberStyles.Float, CultureInfo.InvariantCulture, out richness) || richness is <= 0f or > 100f)
        {
            SourceFeedback("Richness is a multiplier greater than 0 and at most 100 — 1 is the shipped gradient untouched", context);
            richness = 1f;
            return false;
        }

        return true;
    }

    private void Richness(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 3)
        {
            SourceFeedback("deposit richness <depositId> <multiplier>", context);
            return;
        }

        var deposit = Resolve(zoneId, parameters[1], out var depositId);
        if (deposit == null)
        {
            SourceFeedback($"Zone {zoneId} has no deposit {depositId}", context);
            return;
        }

        if (!TryParseRichness(parameters[2], out var richness, context))
        {
            return;
        }

        deposit.Richness = richness;
        CustomDBInterface.SaveResourceDeposits();
        SourceFeedback($"[{depositId}] {deposit.Name} now pays x{deposit.Richness:0.##} of its node type's gradient", context);
    }

    private void Reload(ServerCommandContext context)
    {
        CustomDBInterface.ReloadResourceDeposits();
        SourceFeedback($"Reloaded deposits from {CustomDBLoader.ResourceDepositPath}", context);
    }
}
