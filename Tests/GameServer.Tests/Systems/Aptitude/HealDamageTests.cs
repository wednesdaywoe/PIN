using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Impact;
using GameServer.Systems.Aptitude.Commands.Register;
using NSubstitute;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class HealDamageTests
{
    private readonly CharacterEntity _target;
    private readonly Context _context = NewContext();

    public HealDamageTests()
    {
        _target = new CharacterEntity(Substitute.For<IShard>(), 0x100);
        _target.SetMaxHealth(1000, true);
        _target.SetCurrentHealth(400);
        _context.Targets.Push(_target);
    }

    [Fact]
    public void Heals_EveryTarget_ByHealpoints()
    {
        Heal(new HealDamageCommandDef { Healpoints = 250 });

        Assert.Equal(650, _target.CurrentHealth);
    }

    [Fact]
    public void Heal_StopsAtMaxHealth()
    {
        Heal(new HealDamageCommandDef { Healpoints = 5000 });

        Assert.Equal(1000, _target.CurrentHealth);
    }

    [Fact]
    public void HealpointsRegop_CombinesWithTheRegister()
    {
        // The data's most common shape: Healpoints 1 multiplied by an amount loaded into the register
        _context.Register = 120;

        Heal(new HealDamageCommandDef { Healpoints = 1, HealpointsRegop = 2 });

        Assert.Equal(520, _target.CurrentHealth);
    }

    [Fact]
    public void Usedmgdealt_ScalesByOne()
    {
        Heal(new HealDamageCommandDef { Healpoints = 100, Usedmgdealt = 1 });

        Assert.Equal(500, _target.CurrentHealth);
    }

    [Fact]
    public void NegativeAmount_HealsNothing()
    {
        Heal(new HealDamageCommandDef { Healpoints = -10 });

        Assert.Equal(400, _target.CurrentHealth);
    }

    private void Heal(HealDamageCommandDef def)
    {
        Assert.True(new HealDamageCommand(def).Execute(_context));
    }
}

public class LoadRegisterFromBonusTests
{
    private static readonly LoadRegisterFromBonusCommandDef Def = new()
    {
        RegisterVal_0 = 1f, RegisterVal_1 = 1.2f, RegisterVal_2 = 1.4f, RegisterVal_10 = 3f, BonusTrack = 1, BonusTrackCount = 3,
    };

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(2, 1.2f)]
    [InlineData(5, 1.4f)]
    [InlineData(100, 3f)]
    public void PicksTheValueForTheBonus(int bonus, float expected)
    {
        var context = NewContext();
        context.Bonus = bonus;

        new LoadRegisterFromBonusCommand(Def).Execute(context);

        Assert.Equal(expected, context.Register);
    }
}
