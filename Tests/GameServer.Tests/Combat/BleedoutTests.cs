using AeroMessages.GSS.V66.Character;
using GameServer.Systems.Combat;
using Xunit;

using Status = AeroMessages.GSS.V66.Character.CharacterStateData.CharacterStatus;

namespace GameServer.Tests.Combat;

/// <summary>
///     Pins <see cref="BleedoutSim.IsOverdue"/>, the rule that decides when the server stops waiting
///     for a downed player to give up and respawns them itself.
/// </summary>
/// <remarks>
///     Whether the client ever draws the give-up prompt is an in-game question (B1-B5 in
///     Docs/In-Game-Tests/Death-And-Respawn.md). These only hold the fallback to what it means to do,
///     because a fallback that never fires looks exactly like NET-23 unfixed: the player is stuck on
///     the floor and the session ends in a reconnect either way.
/// </remarks>
public class BleedoutTests
{
    private const ulong Deadline = 30_000;

    [Fact]
    public void ADownedPlayerPastTheDeadlineIsOverdue()
    {
        Assert.True(BleedoutSim.IsOverdue(Status.Incapacitated, Deadline, Deadline + 1));
    }

    [Fact]
    public void TheDeadlineItselfCounts()
    {
        Assert.True(BleedoutSim.IsOverdue(Status.Incapacitated, Deadline, Deadline));
    }

    [Fact]
    public void ADownedPlayerStillInsideTheDeadlineIsLeftAlone()
    {
        Assert.False(BleedoutSim.IsOverdue(Status.Incapacitated, Deadline, Deadline - 1));
    }

    /// <summary>
    ///     Zero is what <c>ClearRespawnOffer</c> writes, so a player who already tapped out and is
    ///     mid-respawn must not be respawned a second time by the clock.
    /// </summary>
    [Fact]
    public void NoDeadlineMeansNoForcedRespawn()
    {
        Assert.False(BleedoutSim.IsOverdue(Status.Incapacitated, 0, Deadline + 1));
    }

    /// <summary>
    ///     Every state other than Incapacitated is somebody else's business — Living is an ordinary
    ///     player, and Respawning is one already on the way back.
    /// </summary>
    [Theory]
    [InlineData(Status.Living)]
    [InlineData(Status.Respawning)]
    [InlineData(Status.Spawning)]
    [InlineData(Status.Dead)]
    [InlineData(Status.Ghost)]
    [InlineData(Status.Traumatized)]
    public void OnlyTheIncapacitatedAreForced(Status state)
    {
        Assert.False(BleedoutSim.IsOverdue(state, Deadline, Deadline + 1));
    }
}
