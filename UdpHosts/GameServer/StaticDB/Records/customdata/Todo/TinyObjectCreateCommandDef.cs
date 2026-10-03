namespace GameServer.StaticDB.Records.customdata;

public record TinyObjectCreateCommandDef : ICommandDef
{
    public uint Id { get; set; }

    /// <summary>
    ///     The dbcharacter::TinyObject row to create. Not shipped: the server-side def came out as an id and a comment,
    ///     so this is filled in by hand where the tiny object can be identified from the stats its effect reads.
    /// </summary>
    public uint TinyObjectId { get; set; }
}
