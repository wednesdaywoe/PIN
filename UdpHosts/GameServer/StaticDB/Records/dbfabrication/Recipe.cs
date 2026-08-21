namespace GameServer.StaticDB.Records.dbfabrication;

/// <summary>
///     One row of <c>dbfabrication::Recipe</c>, the table the client's fabrication messages resolve recipe
///     ids against. 285 rows, ids 124820..140556.
///     <para>
///     Not <c>dbitems::Blueprints</c>, which is the separate 6,251-entry id space behind the Lua
///     <c>Game.GetRecipeInfo</c> calls. The two don't overlap, and the
///     <c>Fabrication_FetchAllRecipes_Response</c> handler drops any id it can't find here without a row or
///     an error, so blueprint ids get you an empty list.
///     </para>
///     <para>
///     The client's record layout corroborates the 23 columns below: 92-byte stride, with
///     <c>LocalizedNameId</c> at 0, <c>LocalizedDescriptionId</c> at 0x30 and <c>Id</c> at 0x50.
///     </para>
/// </summary>
public record class Recipe
{
    public uint LocalizedNameId { get; set; }
    public uint BuildMax { get; set; }
    public uint ItemType { get; set; }
    public uint BuildCost { get; set; }
    public uint PostActionGroupId { get; set; }
    public uint TinkerCriticalSuccessId { get; set; }
    public uint ActionGroupId { get; set; }
    public uint Certificate { get; set; }
    public float QualityScale { get; set; }
    public uint BaseQuantity { get; set; }
    public float BaseBuildTime { get; set; }
    public uint PreActionGroupId { get; set; }
    public uint LocalizedDescriptionId { get; set; }
    public uint ResultMinQuality { get; set; }
    public float QualityScaleAutogen { get; set; }
    public uint TinkerRarityLevelsId { get; set; }
    public uint QualityBase { get; set; }
    public uint ResultMaxQuality { get; set; }
    public uint BaseActions { get; set; }
    public uint ResultLootTableId { get; set; }
    public uint Id { get; set; }
    public uint BaseLevel { get; set; }
    public uint QualityBaseAutogen { get; set; }
}
