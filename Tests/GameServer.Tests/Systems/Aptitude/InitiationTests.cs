using System.Numerics;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude.Commands.Initiate;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class InitiationTests
{
    private readonly ActiveInitiationCommand _command = new(new ActiveInitiationCommandDef { Id = 1 });

    [Fact]
    public void ActiveInitiation_StampsTheActivationTimeAndPosition()
    {
        var self = new FakeTarget("self") { Position = new Vector3(1, 2, 3) };
        var context = NewContext(self: self);
        context.InitTime = 5000;
        self.Position = new Vector3(4, 5, 6);

        Assert.True(_command.Execute(context));

        Assert.True(context.Initiated);
        Assert.Equal(5000u, context.ActivationTime);
        Assert.Equal(new Vector3(4, 5, 6), context.InitPosition);
    }

    [Fact]
    public void ActiveInitiation_OnlyInitiatesOnce()
    {
        var self = new FakeTarget("self");
        var context = NewContext(self: self);
        context.InitTime = 5000;
        _command.Execute(context);

        context.InitTime = 9000;
        self.Position = new Vector3(7, 8, 9);

        Assert.True(_command.Execute(context));
        Assert.Equal(5000u, context.ActivationTime);
        Assert.Equal(Vector3.Zero, context.InitPosition);
    }
}
