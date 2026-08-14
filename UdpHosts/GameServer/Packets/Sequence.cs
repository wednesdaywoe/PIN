namespace GameServer.Packets;

/// <summary>
///     Comparisons on the 16-bit sequence numbers every sequenced channel counts with. They wrap,
///     so "later than" can't be a plain greater-than: the packet after 65535 is 0, and a comparison
///     that doesn't know it stops acking for the rest of the session.
/// </summary>
public static class Sequence
{
    /// <summary>
    ///     Whether <paramref name="sequenceNumber" /> is later than <paramref name="than" />, over
    ///     the half of the number space either side of it. Serial arithmetic, the same rule TCP and
    ///     RFC 1982 use: subtract and read the result as signed, so 0 comes out after 65535 and
    ///     65535 comes out before 0.
    /// </summary>
    public static bool IsAfter(ushort sequenceNumber, ushort than)
    {
        return unchecked((short)(sequenceNumber - than)) > 0;
    }

    /// <summary>
    ///     Whether <paramref name="sequenceNumber" /> is at or before <paramref name="than" />,
    ///     which for a cumulative ack means it is covered by it.
    /// </summary>
    public static bool IsAtOrBefore(ushort sequenceNumber, ushort than)
    {
        return unchecked((short)(than - sequenceNumber)) >= 0;
    }
}
