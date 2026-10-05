namespace GameServer.StaticDB.Records.customdata;

public record TinyObjectUpdateCommandDef : ICommandDef
{
    public uint Id { get; set; }

    /// <summary>
    ///     The tiny object this one turns into, made where it stands. PIN's field, filled by hand from the chains: the
    ///     server-side def shipped as a bare id. Zero means the object just ends.
    /// </summary>
    public uint NextTinyObjectId { get; set; }
}