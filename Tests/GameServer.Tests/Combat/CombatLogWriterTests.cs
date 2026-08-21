using System;
using System.IO;
using System.Linq;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer;
using GameServer.Entities;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Proves a hit lands on disk in a shape a spreadsheet can open. The accumulation is pinned separately
///     in <see cref="CombatLogTests"/>.
/// </summary>
[Collection("CombatLog")]
public class CombatLogWriterTests : IDisposable
{
    private readonly string _directory;

    public CombatLogWriterTests()
    {
        CombatLog.ResetForTests();
        _directory = Path.Combine(Path.GetTempPath(), $"pin-combatlog-{Guid.NewGuid():N}");
        CombatLog.Init(_directory, null);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void AHitIsWrittenUnderAHeaderRow()
    {
        CombatLog.RecordHit(
            new StubEntity(0xAB),
            new DamageInfo { Amount = 115.4f, WeaponId = 86997, WeaponName = "Main 86997 (Type 12121 - R36)" },
            115,
            0,
            885,
            1000);

        var lines = File.ReadAllLines(TheOnlyFile("hits"));

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("time,shard_ms,attacker,", lines[0], StringComparison.Ordinal);
        Assert.Contains("86997", lines[1], StringComparison.Ordinal);
        Assert.Contains("115.4", lines[1], StringComparison.Ordinal);
        Assert.Contains("885", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AWeaponNameWithACommaStaysOneColumn()
    {
        CombatLog.RecordHit(
            new StubEntity(1),
            new DamageInfo { Amount = 1, WeaponName = "Rifle, custom" },
            1,
            0,
            0,
            1000);

        var row = File.ReadAllLines(TheOnlyFile("hits"))[1];

        Assert.Contains("\"Rifle, custom\"", row, StringComparison.Ordinal);
    }

    [Fact]
    public void ATargetThatWasNeverHitGetsNoKillRow()
    {
        CombatLog.RecordDeath(new StubEntity(7), null, 1000, 5000);

        Assert.Empty(Directory.GetFiles(_directory, "kills-*.csv"));
    }

    [Fact]
    public void AKillRowCarriesTheRollup()
    {
        var target = new StubEntity(9);

        CombatLog.RecordHit(target, Shot(), 100, 0, 900, 1000);
        CombatLog.RecordHit(target, Shot(), 100, 0, 800, 1500);
        CombatLog.RecordHit(target, Shot(), 800, 0, 0, 2000);
        CombatLog.RecordDeath(target, null, 1000, 2000);

        var row = File.ReadAllLines(TheOnlyFile("kills"))[1].Split(',');

        // ...,weapons,hits,damage,elapsed_ms,damage_per_second
        Assert.Equal("3", row[^4]);
        Assert.Equal("1000", row[^3]);
        Assert.Equal("1000", row[^2]);

        // 1000 points over the 1000ms between first and last hit
        Assert.Equal("1000", row[^1]);
    }

    private static DamageInfo Shot()
    {
        return new DamageInfo { Amount = 100, WeaponId = 1, WeaponName = "rifle" };
    }

    private string TheOnlyFile(string prefix)
    {
        return Directory.GetFiles(_directory, $"{prefix}-*.csv").Single();
    }

    private sealed class StubEntity : IEntity
    {
        public StubEntity(ulong entityId)
        {
            EntityId = entityId;
        }

        public ulong EntityId { get; }

        public AeroMessages.Common.EntityId AeroEntityId => default;

        public IShard Shard => null;

        public Vector3 Position { get; set; }

        public Quaternion Orientation { get; set; }

        public HostilityInfoData HostilityInfo { get; set; }

        public bool IsInteractable() => false;

        public bool CanBeInteractedBy(IEntity other) => false;

        public byte GetInteractionType() => 0;

        public uint GetInteractionDuration() => 0;

        public bool IsGlobalScope() => false;

        public float GetScopeRange() => 0f;
    }
}
