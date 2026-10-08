using GameServer.Enums;
using GameServer.Systems.Encounters.Encounters;
using Xunit;

namespace GameServer.Tests.Encounters;

/// <summary>
///     Pins which ability marks each stage of a thumper's life (DATA-18). Landing and departure come from
///     the calldown def, so the tests pass their own ids through and check they come back. The two
///     literals in between have no shipped source and are pinned as they are.
/// </summary>
public class ThumperStageAbilityTests
{
    private const uint Landed = 111;
    private const uint Completed = 123;

    [Fact]
    public void LandingAndDepartureComeFromTheDef()
    {
        Assert.Equal(Landed, Thumper.CountdownAbility(ThumperState.LANDING, Landed, Completed));
        Assert.Equal(Completed, Thumper.CountdownAbility(ThumperState.COMPLETED, Landed, Completed));
    }

    [Fact]
    public void AThumperThatFinishesOnItsOwnLeavesTheWayACollectedOneDoes()
    {
        // 34216 was the old departure: the launch without the wind-down, named by no shipped data.
        Assert.NotEqual(34216u, Thumper.CountdownAbility(ThumperState.COMPLETED, Landed, Completed));
    }

    [Fact]
    public void TheDrillingStagesKeepTheirServerSideAbilities()
    {
        Assert.Equal(34579u, Thumper.CountdownAbility(ThumperState.WARMINGUP, Landed, Completed));
        Assert.Equal(34215u, Thumper.CountdownAbility(ThumperState.THUMPING, Landed, Completed));
    }

    [Theory]
    [InlineData(ThumperState.CLOSING)]
    [InlineData(ThumperState.LEAVING)]
    [InlineData(ThumperState.DESTROYED)]
    public void OtherStagesFireNothing(ThumperState state)
    {
        Assert.Equal(0u, Thumper.CountdownAbility(state, Landed, Completed));
    }
}
