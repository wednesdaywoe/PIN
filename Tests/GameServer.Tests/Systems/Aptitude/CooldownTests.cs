using GameServer.Enums;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Activation;
using GameServer.Systems.Aptitude.Commands.Cooldown;
using GameServer.Systems.Aptitude.Commands.Requirement;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class CooldownTests
{
    private const uint Ability = 100;
    private const uint OtherAbility = 200;

    private readonly Context _context = NewContext();

    private CooldownSet Cooldowns => _context.Initiator.Cooldowns;

    [Fact]
    public void LocalCooldown_BlocksTheAbilityUntilReady()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { LocalCooldown = 500 });

        Assert.False(CheckLocal(Ability, 1000));
        Assert.False(CheckLocal(Ability, 1499));
        Assert.True(CheckLocal(Ability, 1500));
        Assert.True(CheckLocal(OtherAbility, 1000));
    }

    [Fact]
    public void CheckLocalOff_IgnoresTheLocalCooldown()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { LocalCooldown = 500 });

        Assert.True(Check(Ability, 1000, new TimeCooldownCommandDef { CheckLocal = 0, CheckGlobal = 1 }));
    }

    [Fact]
    public void GlobalCooldown_BlocksEveryAbility()
    {
        Assert.True(Check(OtherAbility, 1000, new TimeCooldownCommandDef { CheckGlobal = 1 }));

        Inflict(Ability, 1000, new InflictCooldownCommandDef { GlobalCooldown = 300 });

        Assert.False(Check(OtherAbility, 1299, new TimeCooldownCommandDef { CheckGlobal = 1 }));
        Assert.True(Check(OtherAbility, 1300, new TimeCooldownCommandDef { CheckGlobal = 1 }));
        Assert.True(Check(OtherAbility, 1299, new TimeCooldownCommandDef { CheckGlobal = 0 }));
    }

    [Fact]
    public void GlobalCooldown_IsReplacedNotExtended()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { GlobalCooldown = 1000 });
        Inflict(Ability, 1100, new InflictCooldownCommandDef { GlobalCooldown = 100 });

        Assert.Equal(1100u, Cooldowns.GlobalActivatedTime);
        Assert.Equal(1200u, Cooldowns.GlobalReadyAgainTime);
    }

    [Fact]
    public void CategoryCooldown_BlocksOtherAbilitiesInTheCategory()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { CategoryCooldown = 500, Category = 12 });

        Assert.False(Check(OtherAbility, 1000, new TimeCooldownCommandDef { CheckCategory = 1, Category = 12 }));
        Assert.True(Check(OtherAbility, 1000, new TimeCooldownCommandDef { CheckCategory = 1, Category = 13 }));
        Assert.True(Check(OtherAbility, 1000, new TimeCooldownCommandDef { CheckCategory = 0, Category = 12 }));
        Assert.True(Check(OtherAbility, 1500, new TimeCooldownCommandDef { CheckCategory = 1, Category = 12 }));
    }

    [Fact]
    public void CategoryCooldown_NeedsACategory()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { CategoryCooldown = 500 });

        Assert.Empty(Cooldowns.Category);
    }

    [Fact]
    public void PrecoolCount_GivesCharges()
    {
        var def = new InflictCooldownCommandDef { LocalCooldown = 1000, LocalCooldownPrecoolCount = 3 };

        Inflict(Ability, 10000, def);
        Assert.True(CheckLocal(Ability, 10000));
        Inflict(Ability, 10000, def);
        Assert.True(CheckLocal(Ability, 10000));
        Inflict(Ability, 10000, def);
        Assert.False(CheckLocal(Ability, 10000));

        // One charge comes back per duration
        Assert.True(CheckLocal(Ability, 11000));
        Assert.Equal(new CooldownSet.Cooldown(10000, 11000, 0, 2000), Cooldowns.Local[Ability]);
    }

    [Fact]
    public void UsingDuringCooldown_StartsNowAndKeepsTheLaterReadyTime()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { LocalCooldown = 5000 });
        Inflict(Ability, 2000, new InflictCooldownCommandDef { LocalCooldown = 1000 });

        Assert.Equal(new CooldownSet.Cooldown(2000, 6000, 0, 0), Cooldowns.Local[Ability]);
    }

    [Fact]
    public void DurationRegop_AppliesToLocalAndCategory()
    {
        _context.Register = 2;
        Inflict(Ability, 1000, new InflictCooldownCommandDef
        {
            LocalCooldown = 100, CategoryCooldown = 300, Category = 1, GlobalCooldown = 50, DurationRegop = (byte)Operand.MULTIPLY,
        });

        Assert.Equal(1200u, Cooldowns.Local[Ability].ReadyAgainTime);
        Assert.Equal(1600u, Cooldowns.Category[1].ReadyAgainTime);
        Assert.Equal(1050u, Cooldowns.GlobalReadyAgainTime);
    }

    [Fact]
    public void PrecoolRegop_AppliesToTheCount()
    {
        _context.Register = 3;
        Inflict(Ability, 10000, new InflictCooldownCommandDef { LocalCooldown = 1000, LocalCooldownPrecoolCount = 0, PrecoolRegop = (byte)Operand.ADD });

        Assert.Equal(2000u, Cooldowns.Local[Ability].PrecoolTime);
    }

    [Fact]
    public void Times_WrapAround()
    {
        Inflict(Ability, uint.MaxValue - 100, new InflictCooldownCommandDef { LocalCooldown = 500, GlobalCooldown = 500 });

        Assert.False(CheckLocal(Ability, 100));
        Assert.False(Check(Ability, 100, new TimeCooldownCommandDef { CheckGlobal = 1 }));
        Assert.True(CheckLocal(Ability, 400));
        Assert.True(Check(Ability, 400, new TimeCooldownCommandDef { CheckGlobal = 1 }));
    }

    [Fact]
    public void Flags_AreSentAfterTheTimesWithThePrecool()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { LocalCooldown = 1000, LocalCooldownPrecoolCount = 2, PreventReset = 1, MainSlot = 1 });

        var data = Cooldowns.ToData(1000);

        var cooldown = Assert.Single(data.ActiveCooldowns_Group1);
        Assert.Equal(Ability, cooldown.AbilityId);
        Assert.Equal(0u, cooldown.Activated_Time);
        Assert.Equal(1000u, cooldown.ReadyAgain_Time);
        Assert.Equal(new byte[] { 3, 0xe8, 0x03, 0, 0 }, cooldown.Unk);
        Assert.Empty(data.ActiveCooldowns_Group2);
    }

    [Fact]
    public void Flags_Stick()
    {
        Inflict(Ability, 1000, new InflictCooldownCommandDef { LocalCooldown = 100, PreventReset = 1 });
        Inflict(Ability, 2000, new InflictCooldownCommandDef { LocalCooldown = 100 });

        Assert.Equal(CooldownSet.PreventResetFlag, Cooldowns.Local[Ability].Flags);
    }

    [Fact]
    public void UninflictedGlobal_IsSentAsReadyNow()
    {
        var data = Cooldowns.ToData(123456);

        Assert.Equal(123456u, data.GlobalCooldown_Activated_Time);
        Assert.Equal(123456u, data.GlobalCooldown_ReadyAgain_Time);
    }

    [Fact]
    public void InstantActivation_InflictsCooldowns()
    {
        _context.AbilityId = Ability;
        _context.InitTime = 1000;

        Assert.True(new InstantActivationCommand(new InstantActivationCommandDef { LocalCooldown = 500, GlobalCooldown = 300, PreventReset = 1 }).Execute(_context));

        Assert.Equal(new CooldownSet.Cooldown(1000, 1500, CooldownSet.PreventResetFlag, 0), Cooldowns.Local[Ability]);
        Assert.Equal(1300u, Cooldowns.GlobalReadyAgainTime);
    }

    private void Inflict(uint abilityId, uint now, InflictCooldownCommandDef def)
    {
        _context.AbilityId = abilityId;
        _context.InitTime = now;
        Assert.True(new InflictCooldownCommand(def).Execute(_context));
    }

    private bool CheckLocal(uint abilityId, uint now) => Check(abilityId, now, new TimeCooldownCommandDef { CheckLocal = 1 });

    private bool Check(uint abilityId, uint now, TimeCooldownCommandDef def)
    {
        _context.AbilityId = abilityId;
        _context.InitTime = now;
        return new TimeCooldownCommand(def).Execute(_context);
    }
}
