using Core.Data;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Guid packing, which the server relies on in two places that would both fail quietly: the low byte
///     carries the entity's controller id and is masked off all over the codebase to get a lookup key, and
///     the timestamp loses its low 8 bits on the way in so a round trip is lossy by design.
/// </summary>
public class EntityGuidTests
{
    [Fact]
    public void ParseUndoesPacking()
    {
        var packed = new EntityGuid(serverId: 7, timestamp: 0x11223300, counter: 0x4455, type: 0x66);
        var parsed = EntityGuid.Parse(packed.Full);

        Assert.Equal(packed.ServerId, parsed.ServerId);
        Assert.Equal(packed.Timestamp, parsed.Timestamp);
        Assert.Equal(packed.Counter, parsed.Counter);
        Assert.Equal(packed.Type, parsed.Type);
    }

    /// <summary>
    ///     `entityId &amp; 0xFF` is how the rest of the server asks what kind of entity a guid belongs to.
    /// </summary>
    [Fact]
    public void TheTypeIsTheLowByte()
    {
        var guid = new EntityGuid(serverId: 1, timestamp: 0xDEADBE00, counter: 0x1234, type: 0x2F);

        Assert.Equal(0x2FUL, guid.Full & 0xFF);
        Assert.Equal(0x2F, EntityGuid.Parse(guid.Full).Type);
    }

    /// <summary>
    ///     Masking the type off is how a lookup key is formed, so the rest of the guid has to survive it.
    /// </summary>
    [Fact]
    public void MaskingOffTheTypeLeavesTheEntityKey()
    {
        var first = new EntityGuid(serverId: 3, timestamp: 0x01020300, counter: 99, type: 0x01);
        var second = new EntityGuid(serverId: 3, timestamp: 0x01020300, counter: 99, type: 0x7F);

        Assert.NotEqual(first.Full, second.Full);
        Assert.Equal(first.Full & 0xffffffffffffff00, second.Full & 0xffffffffffffff00);
    }

    [Fact]
    public void TheServerIdIsTheTopByte()
    {
        var guid = new EntityGuid(serverId: 0xAB, timestamp: 0, counter: 0, type: 0);

        Assert.Equal(0xABUL, guid.Full >> 56);
    }

    /// <summary>
    ///     Only 24 bits of timestamp fit, so the low 8 are dropped on the way in. Two timestamps inside the
    ///     same 256ms produce the same guid, which is what the counter is there to separate.
    /// </summary>
    [Fact]
    public void TheTimestampLosesItsLowByte()
    {
        var guid = new EntityGuid(serverId: 0, timestamp: 0x000000FF, counter: 0, type: 0);

        Assert.Equal(0u, EntityGuid.Parse(guid.Full).Timestamp);
        Assert.Equal(new EntityGuid(0, 0, 0, 0).Full, guid.Full);
    }
}
