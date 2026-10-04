namespace GameServer.StaticDB.Records.customdata;

public record TinyObjectCreateCommandDef : ICommandDef
{
    public uint Id { get; set; }

    /// <summary>
    ///     The dbcharacter::TinyObject row to create. Not shipped: the server-side def came out as an id and a comment,
    ///     so this is filled in by hand where the tiny object can be identified from the stats its effect reads.
    /// </summary>
    public uint TinyObjectId { get; set; }

    /// <summary>
    ///     Create the object where the chain's Self stands now, not at InitPosition. For an object dropped repeatedly by
    ///     an effect on a moving character (Poison Trail's original form), InitPosition is where the ability started.
    /// </summary>
    public byte AtSelf { get; set; }

    /// <summary>
    ///     Skip creating it when one of the same type from the same owner is closer than this, in metres. PIN's own
    ///     rule, not shipped: an effect dropping clouds every 200 ms would otherwise stack thirty on someone standing still.
    /// </summary>
    public float MinSpacing { get; set; }

    /// <summary>
    ///     A second tiny object created at the same spot. PIN's own field: where a blast should turn into a lingering
    ///     area through TinyObjectUpdate, which shipped without data and isn't built, both are made at once
    ///     (Fuel Air Bomb's blast and fire patch).
    /// </summary>
    public uint AlsoTinyObjectId { get; set; }
}
