using System.Numerics;
using GameServer.Systems.Aptitude.Commands.Movement;
using Xunit;

namespace GameServer.Tests.Aptitude;

public class TeleportTests
{
    [Fact]
    public void LandsShortOfWhereTheProjectileStruck()
    {
        var destination = TeleportCommand.Destination(Vector3.Zero, new Vector3(10f, 0f, 2f));

        Assert.Equal(new Vector3(10f - TeleportCommand.StopShort, 0f, 2f), destination);
    }

    [Fact]
    public void AlreadyThatClose_StaysPut()
    {
        Assert.Equal(Vector3.Zero, TeleportCommand.Destination(Vector3.Zero, new Vector3(1f, 0f, 0f)));
    }
}
