using System;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Packets;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Sends the client a fabrication recipe list it never asked for, and sees whether it displays one. It
///     has to be unprompted: v1.6 deleted the crafting panel and left no Lua binding that can send
///     <c>Fabrication_FetchAllRecipes</c>. The receiving half is intact, so the reply still prints to the
///     client console and still raises <c>ON_FAB_FETCH_RECIPES</c>, which <c>Client/Addons/FabCart</c> binds.
///     <para>
///     Ids must come from <c>dbfabrication::Recipe</c>. Blueprint ids get silently dropped by the response
///     handler, which is the empty list CRAFT1b saw. See
///     <see cref="SDBInterface.GetFabricationRecipeIds" />.
///     </para>
/// </summary>
[ServerCommand(
    "Send yourself a fabrication recipe list, to see whether the client still displays one",
    "fabrecipes [count] | fabrecipes id <id> [id...]",
    "fabrecipes",
    "fabrecipe")]
public class FabRecipesServerCommand : ServerCommand
{
    /// <summary>
    ///     254, not 255. The client's decoder is chunked, and a count byte of exactly 255 means "another
    ///     chunk follows", so it reads the next count off the end of the message and drops the
    ///     connection. Only a short chunk terminates. PIN's Aero encoder writes one count byte and can't
    ///     express the repeat, so a message carries 254 of the table's 285 rows; the rest need a
    ///     hand-written encoder and aren't what this command is for.
    /// </summary>
    private const int MaxRecipes = 254;

    private const int DefaultRecipes = 10;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var player = context.SourcePlayer;
        if (player?.CharacterEntity == null)
        {
            SourceFeedback("Cannot send a recipe list without a player character", context);
            return;
        }

        uint[] recipes;

        // "id 124820 124829" names recipes outright, for asking whether one particular id resolves.
        if (parameters.Length > 1 && parameters[0].Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            recipes = parameters.Skip(1)
                                .Select(p => uint.TryParse(p, out var id) ? id : 0u)
                                .Where(id => id != 0)
                                .Take(MaxRecipes)
                                .ToArray();

            if (recipes.Length == 0)
            {
                SourceFeedback("No usable recipe ids in that list", context);
                return;
            }

            var unknown = recipes.Count(id => SDBInterface.GetFabricationRecipe(id) == null);
            if (unknown > 0)
            {
                SourceFeedback(
                    $"{unknown} of those {recipes.Length} are not in dbfabrication::Recipe; the client will drop them without printing a row",
                    context);
            }
        }
        else
        {
            var count = DefaultRecipes;
            if (parameters.Length > 0 && int.TryParse(parameters[0], out var asked) && asked > 0)
            {
                count = asked;
            }

            if (count > MaxRecipes)
            {
                SourceFeedback(
                    $"{count} is more than one message can carry; sending {MaxRecipes}. A chunk of exactly 255 reads as \"more follows\" and disconnects the client",
                    context);
                count = MaxRecipes;
            }

            recipes = SDBInterface.GetFabricationRecipeIds().Take(count).ToArray();
            if (recipes.Length == 0)
            {
                SourceFeedback("No fabrication recipes loaded — dbfabrication::Recipe is empty or pruned out", context);
                return;
            }
        }

        var response = new FabricationFetchAllRecipesResponse
        {
            Recipes = recipes,
        };

        player.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);

        Logger.Information(
            "fabrecipes: sent {Count} fabrication recipe id(s) to 0x{EntityId:X8}, first {First} last {Last}",
            recipes.Length,
            player.CharacterEntity.EntityId,
            recipes[0],
            recipes[^1]);

        SourceFeedback(
            $"Sent {recipes.Length} recipe ids ({recipes[0]}..{recipes[^1]}). Look in the client console for \"Fabrication Recipe List:\"",
            context);
    }
}
