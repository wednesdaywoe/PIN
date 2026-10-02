using System.Linq;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Reads and reloads the vein types PIN defines itself — what a deposit of each one pays.
/// </summary>
/// <remarks>
///     Read-only apart from <c>reload</c>, unlike its sibling <see cref="ResourceDepositServerCommand"/>.
///     A deposit's centre can only be established by standing on it, so that command has to write; a
///     payout table is just numbers and belongs in the file, where it can be diffed. What this gives
///     the running game is the loop that file otherwise lacks: edit
///     <c>StaticDB/CustomData/resource_node_type.json</c>, <c>veintype reload</c>, thump, look.
/// </remarks>
[ServerCommand(
    "Show the resource node types PIN defines, and re-read them from disk",
    "veintype list | show <nodeTypeId> | reload",
    "veintype")]
public class ResourceNodeTypeServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length == 0)
        {
            SourceFeedback("veintype list | show <nodeTypeId> | reload", context);
            return;
        }

        switch (parameters[0].ToLowerInvariant())
        {
            case "list": List(context); break;
            case "show": Show(parameters, context); break;
            case "reload": Reload(context); break;
            default: SourceFeedback($"Unknown veintype verb '{parameters[0]}'", context); break;
        }
    }

    private void List(ServerCommandContext context)
    {
        var overrides = CustomDBInterface.GetResourceNodeTypeOverrides();

        if (overrides.Count == 0)
        {
            SourceFeedback($"No custom node types in {CustomDBLoader.ResourceNodeTypePath}", context);
            return;
        }

        foreach (var row in overrides.Values.OrderBy(row => row.Id))
        {
            var items = string.Join(", ", row.Resources.Select(resource => resource.ItemId.ToString()));
            SourceFeedback($"[{row.Id}] {row.Name}: pays {row.Resources.Count} item(s) - {items}", context);
        }
    }

    private void Show(string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 2)
        {
            SourceFeedback("veintype show <nodeTypeId>", context);
            return;
        }

        var nodeTypeId = ParseUIntParameter(parameters[1]);

        // Reads through the merged table on purpose, so this answers for shipped types too and shows
        // what a deposit would actually draw from rather than what the file happens to say.
        var nodeType = SDBInterface.GetResourceNodeType(nodeTypeId);
        if (nodeType == null)
        {
            SourceFeedback($"No ResourceNodeType {nodeTypeId}, shipped or custom", context);
            return;
        }

        var custom = CustomDBInterface.GetResourceNodeTypeOverrides().ContainsKey(nodeTypeId) ? "custom" : "shipped";
        SourceFeedback($"[{nodeTypeId}] {nodeType.Name} ({custom})", context);

        var rows = SDBInterface.GetResourceNodeTypeResources(nodeTypeId);
        if (rows.Count == 0)
        {
            SourceFeedback("  pays nothing, so a deposit of it cannot be placed", context);
            return;
        }

        foreach (var row in rows)
        {
            SourceFeedback(
                $"  item {row.ItemId}: centre {row.CenterLow}-{row.CenterHigh}, rim {row.EdgeLow}-{row.EdgeHigh}, quality {row.ItemQualityLow}-{row.ItemQualityHigh}",
                context);
        }
    }

    /// <summary>
    ///     Re-reads the file and lays it back over the shipped table. Deposits already placed keep
    ///     their node type id, so a deposit whose payout was just edited pays the new figures on its
    ///     next thump with no need to place it again.
    /// </summary>
    private void Reload(ServerCommandContext context)
    {
        var applied = CustomDBInterface.ReloadResourceNodeTypeOverrides();
        SourceFeedback($"Applied {applied} custom node type(s) from {CustomDBLoader.ResourceNodeTypePath}", context);
    }
}
