using System.Collections.Generic;
using GameServer.StaticDB.Records.dbvisualrecords;
using GameServer.Systems.Hazards;
using Xunit;

namespace GameServer.Tests.Hazards;

/// <summary>
///     The client owns the water reading, so these pin how the byte is unpacked and how it is read
///     against the thresholds a water row carries. The unpacking itself is checked against the 2016
///     capture rather than only against itself — see <see cref="WadingInTheCaptureIsNotAHazard"/>.
/// </summary>
public class SubmersionTests
{
    /// <summary>The six rows' ids and physics materials in table order, as the retail db stores them.</summary>
    private static readonly WaterDesc[] _table =
    [
        new() { Id = 10001, PhysicsMaterialId = 10011 }, new() { Id = 10002, PhysicsMaterialId = 10032 },
        new() { Id = 10003, PhysicsMaterialId = 10033 }, new() { Id = 10008, PhysicsMaterialId = 10037 },
        new() { Id = 10110, PhysicsMaterialId = 10011 }, new() { Id = 10111, PhysicsMaterialId = 10049 },
    ];

    /// <summary>The materials zone 448's chunk water collision uses: standard, ooze and marsh.</summary>
    private static readonly HashSet<uint> _zone448Water = [10011, 10032, 10037];

    /// <summary>
    ///     <c>dbvisualrecords::WaterDesc</c> row 10001, the standard water, copied out of the retail db.
    /// </summary>
    private static WaterDesc StandardWater => new()
    {
        Id = 10001,
        MovingRestrictedPercent = 0.33f,
        DrowningPercent = 0.735291f,
        DyingPercent = 1f,
    };

    [Fact]
    public void TheLowNibbleIsTheLevelAndTheHighNibbleIsTheDescription()
    {
        var submersion = Submersion.Read(0x3C);

        Assert.Equal(12, submersion.Level);
        Assert.Equal(3, submersion.DescIndex);
    }

    [Fact]
    public void ZeroIsDry()
    {
        var submersion = Submersion.Read(0);

        Assert.False(submersion.InWater);
        Assert.Equal(WaterHazard.None, submersion.Against(StandardWater));
    }

    /// <summary>
    ///     Every non-zero reading in the 2016 capture is between 1 and 5 with the description nibble at 0,
    ///     and that session's player never drowned. 5/15 also lands exactly on the row's
    ///     <c>moving_restricted_percent</c> of 0.33, which is what fixes the denominator at 15.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void WadingInTheCaptureIsNotAHazard(byte raw)
    {
        var submersion = Submersion.Read(raw);

        Assert.Equal(0, submersion.DescIndex);
        Assert.True(submersion.InWater);
        Assert.Equal(WaterHazard.None, submersion.Against(StandardWater));
    }

    [Fact]
    public void FiveFifteenthsIsExactlyTheMovementRestrictionThreshold()
    {
        Assert.Equal(0.33f, Submersion.Read(5).Depth, 2);
    }

    [Fact]
    public void ChestDeepDrowns()
    {
        // 11/15 is 0.7333, just under; 12/15 is 0.8, just over.
        Assert.Equal(WaterHazard.None, Submersion.Read(11).Against(StandardWater));
        Assert.Equal(WaterHazard.Drowning, Submersion.Read(12).Against(StandardWater));
    }

    [Fact]
    public void OnlyAFullNibbleIsFullySubmerged()
    {
        Assert.Equal(WaterHazard.Drowning, Submersion.Read(14).Against(StandardWater));
        Assert.Equal(WaterHazard.Dying, Submersion.Read(Submersion.MaxLevel).Against(StandardWater));
    }

    [Fact]
    public void TheDescriptionNibbleDoesNotChangeTheLevel()
    {
        Assert.Equal(WaterHazard.Dying, Submersion.Read(0xFF).Against(StandardWater));
        Assert.Equal(15, Submersion.Read(0xFF).DescIndex);
    }

    /// <summary>
    ///     A water row the server couldn't resolve has to be harmless rather than defaulted. Row 10003
    ///     starts killing at 0.328 of your height, so falling back to "some water row" instead of "no
    ///     water row" would drown people standing in puddles.
    /// </summary>
    [Fact]
    public void UnknownWaterIsHarmless()
    {
        Assert.Equal(WaterHazard.None, Submersion.Read(Submersion.MaxLevel).Against(null));
    }

    /// <summary>
    ///     Row 10110 ships with <c>dying_percent</c> of 1 but there are rows that don't, and a row with a
    ///     threshold of zero means unset, not "kills on contact".
    /// </summary>
    [Fact]
    public void AZeroThresholdIsUnsetRatherThanInstant()
    {
        var unset = new WaterDesc { Id = 1, DrowningPercent = 0f, DyingPercent = 0f };

        Assert.Equal(WaterHazard.None, Submersion.Read(Submersion.MaxLevel).Against(unset));
    }

    /// <summary>
    ///     Row 10003, which is the reason the description nibble can't be guessed at.
    /// </summary>
    [Fact]
    public void ShallowKillingWaterKillsShallow()
    {
        var caustic = new WaterDesc { Id = 10003, DrowningPercent = 0.0844978f, DyingPercent = 0.328042f };

        Assert.Equal(WaterHazard.Drowning, Submersion.Read(2).Against(caustic));
        Assert.Equal(WaterHazard.Dying, Submersion.Read(5).Against(caustic));
    }

    [Fact]
    public void TheDescriptionNibbleIsARowOfTheTable()
    {
        Assert.Equal(10001u, Submersion.Read(0x05).DescribedBy(_table).Id);
        Assert.Equal(10003u, Submersion.Read(0x25).DescribedBy(_table).Id);
        Assert.Equal(10111u, Submersion.Read(0x55).DescribedBy(_table).Id);
    }

    [Fact]
    public void ANibblePastTheTableFallsBackToRowZeroAsTheClientDoes()
    {
        Assert.Equal(10001u, Submersion.Read(0x65).DescribedBy(_table).Id);
        Assert.Equal(10001u, Submersion.Read(0xF5).DescribedBy(_table).Id);
    }

    [Fact]
    public void AnEmptyTableDescribesNothing()
    {
        Assert.Null(Submersion.Read(0x05).DescribedBy([]));
    }

    [Fact]
    public void ARowTheZonesWaterUsesIsBelieved()
    {
        var (ooze, oozeCorroborated) = Submersion.Read(0x15).DescribedBy(_table, _zone448Water);
        var (marsh, marshCorroborated) = Submersion.Read(0x35).DescribedBy(_table, _zone448Water);

        Assert.Equal(10002u, ooze.Id);
        Assert.True(oozeCorroborated);
        Assert.Equal(10008u, marsh.Id);
        Assert.True(marshCorroborated);
    }

    [Fact]
    public void ENV1sNibbleTwoIsNotTakenForCausticWaterIn448()
    {
        var (water, corroborated) = Submersion.Read(0x25).DescribedBy(_table, _zone448Water);

        Assert.Equal(10001u, water.Id);
        Assert.False(corroborated);
    }

    [Fact]
    public void WithoutTheMapEveryNibbleIsStandardWater()
    {
        var (water, corroborated) = Submersion.Read(0x35).DescribedBy(_table, new HashSet<uint>());

        Assert.Equal(10001u, water.Id);
        Assert.False(corroborated);
    }
}
