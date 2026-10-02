using System;
using System.Collections.Generic;
using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.Systems.Aptitude;
using NSubstitute;
using Serilog.Core;

namespace GameServer.Tests.Systems.Aptitude;

internal static class AptitudeTestHelpers
{
    public static Context NewContext(TestFactory factory = null, FactionStances factions = null, IAptitudeTarget self = null)
    {
        var shard = Substitute.For<IShard>();
        shard.Abilities.Returns(new AbilitySystem(factory ?? new TestFactory(), factions));
        return new Context(shard, self ?? new FakeTarget("self"));
    }

    public static Chain NewChain(params ICommand[] commands) => new() { Id = 1, Commands = [.. commands] };

    public static FakeCommand Returns(bool result) => new(_ => result);
}

internal class FakeTarget(string name, byte factionId = 0) : IAptitudeTarget
{
    public ulong EntityId => 0;
    public EntityId AeroEntityId => default;
    public IShard Shard => throw new NotSupportedException();
    public Vector3 Position => Vector3.Zero;
    public HostilityInfoData HostilityInfo { get; } = new() { Flags = HostilityInfoData.HostilityFlags.Faction, FactionId = factionId };
    public CharacterEntity Owner => null;

    public List<EffectState> GetActiveEffects() => [];
    public EffectState AddEffect(Effect effect, Context context) => throw new NotSupportedException();
    public void ClearEffect(EffectState state) => throw new NotSupportedException();
    public void SetStatusEffect(byte index, ushort time, StatusEffectData data) => throw new NotSupportedException();
    public void ClearStatusEffect(byte index, ushort time, uint debugEffectId) => throw new NotSupportedException();

    public override string ToString() => name;
}

/// <summary>
///     Command that runs a delegate, for building chains without the SDB
/// </summary>
internal class FakeCommand(Func<Context, bool> execute) : ICommand
{
    public uint Id { get; set; }

    public int Executions { get; private set; }

    public bool Execute(Context context)
    {
        Executions++;
        return execute(context);
    }
}

/// <summary>
///     Factory that serves chains registered by the test instead of loading them from the SDB
/// </summary>
internal class TestFactory() : Factory(Logger.None)
{
    public Dictionary<uint, Chain> Chains { get; } = [];

    public override Chain LoadChain(uint chainId) => Chains[chainId];

    public void Add(uint chainId, params ICommand[] commands) => Chains[chainId] = new Chain { Id = chainId, Commands = [.. commands] };
}
