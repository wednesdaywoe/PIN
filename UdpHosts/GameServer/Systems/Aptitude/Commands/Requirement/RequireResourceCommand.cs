using System;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     Checks a resource off a character, and moves it. Despite the name it is a debit as well as a
///     test: <c>amount</c> is signed, and 34 of the table's 41 rows are negative.
/// </summary>
/// <remarks>
///     Negative spends, positive grants, and a spend that the character cannot cover fails the
///     requirement so the chain stops. The 41 rows cite 13 resource ids in the 30325..77413 range —
///     ability charges and mission tokens, not crafting materials. Blueprint crafting does not come
///     through here; see <see cref="Crafting.BlueprintCrafting" />.
///     <para>
///     <c>behavior</c> is zero on 40 of the 41 rows and 4 on the one exception (id 234362, a +20
///     grant), so what the column selects between was never exercised and is ignored.
///     <c>apply_to_army</c> is zero on every row.
///     </para>
/// </remarks>
public class RequireResourceCommand : Command, ICommand
{
    private RequireResourceCommandDef Params;

    public RequireResourceCommand(RequireResourceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character || character.Player?.Inventory == null)
        {
            // NPCs have no inventory to charge. Failing here would stop every monster chain that
            // happens to carry the command, so let it through and say so once.
            Logger.Debug(
                "[{Command} {CommandId}] target has no inventory, passing without charging {Amount} of {ResourceSdbId}",
                nameof(RequireResourceCommand),
                Params.Id,
                Params.Amount,
                Params.ResourceSdbId);
            return true;
        }

        var inventory = character.Player.Inventory;

        if (Params.Amount >= 0)
        {
            if (Params.Amount > 0)
            {
                inventory.AddResource(Params.ResourceSdbId, (uint)Params.Amount);
            }

            return true;
        }

        var cost = (uint)Math.Abs(Params.Amount);
        if (!inventory.ConsumeResource(Params.ResourceSdbId, cost))
        {
            Logger.Debug(
                "[{Command} {CommandId}] short of resource {ResourceSdbId}: needs {Cost}, holds {Held}",
                nameof(RequireResourceCommand),
                Params.Id,
                Params.ResourceSdbId,
                cost,
                inventory.GetResourceQuantity(Params.ResourceSdbId));
            return false;
        }

        return true;
    }
}
