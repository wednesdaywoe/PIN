namespace GameServer.StaticDB.Records.customdata;

public record ResourceNodeScanDefCommandDef : ICommandDef
{
    public uint Id { get; set; }

    /// <summary>
    ///     Metres from the scanning character a deposit can be and still appear in the result. Invented:
    ///     the shipped def is a shell, and only one instance documents itself — 34126's comment says
    ///     "Scans for thumper nodes in a 600 meter radius", which is where its value comes from.
    /// </summary>
    public float Range { get; set; }
}
