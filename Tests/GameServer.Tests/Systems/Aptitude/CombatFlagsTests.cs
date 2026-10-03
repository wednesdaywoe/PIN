using GameServer;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.SetFlags;
using GameServer.Systems.Combat;
using NSubstitute;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;
using Flags = AeroMessages.GSS.V66.Character.CombatFlagsData.CharacterCombatFlags;

namespace GameServer.Tests.Systems.Aptitude;

public class CombatFlagsTests
{
    private readonly CharacterEntity _character = new(Substitute.For<IShard>(), 0x200);

    // Effect 2967's apply chain: a three-second knockdown
    private static readonly CombatFlagsCommandDef Knockdown = new()
    {
        Id = 293919, RestrictMovement = 1, RestrictWeapon = 1, RestrictAbilities = 1, KnockDown = 1, RestrictMelee = 1, RestrictInteraction = 1,
    };

    private static readonly CombatFlagsCommandDef Root = new() { Id = 2, RestrictMovement = 1 };

    // Runs the step as an effect's apply chain would, and returns the context its removal needs
    private Context Apply(CombatFlagsCommandDef def)
    {
        var context = NewContext(self: _character);
        new CombatFlagsCommand(def).Execute(context);
        foreach (var pair in context.Actives)
        {
            pair.Key.OnApply(context, pair.Value);
        }

        return context;
    }

    private static void Remove(Context context)
    {
        foreach (var pair in context.Actives)
        {
            pair.Key.OnRemove(context, pair.Value);
        }
    }

    [Fact]
    public void ToWire_MapsEachSwitchToItsBit()
    {
        var flags = new CombatFlagsCommand(Knockdown).ToWire();

        Assert.Equal(Flags.restrict_movement | Flags.restrict_weapon | Flags.restrict_abilities | Flags.knock_down | Flags.restrict_melee | Flags.restrict_interaction, flags);
    }

    [Fact]
    public void Stun_HoldsWhileTheEffectLasts()
    {
        var stun = Apply(Knockdown);

        Assert.True(_character.MovementRestricted);
        Assert.True(_character.WeaponRestricted);

        Remove(stun);

        Assert.False(_character.MovementRestricted);
        Assert.False(_character.WeaponRestricted);
        Assert.Equal((Flags)0, _character.ActiveCombatFlags);
    }

    [Fact]
    public void Root_StopsMovementButNotShooting()
    {
        Apply(Root);

        Assert.True(_character.MovementRestricted);
        Assert.False(_character.WeaponRestricted);
    }

    [Fact]
    public void OverlappingEffects_FlagStaysUntilTheLastEnds()
    {
        var stun = Apply(Knockdown);
        var root = Apply(Root);

        Remove(stun);
        Assert.True(_character.MovementRestricted);
        Assert.False(_character.WeaponRestricted);

        Remove(root);
        Assert.False(_character.MovementRestricted);
    }

    [Fact]
    public void SomeoneElsesEffectOnAPlayer_Applies()
    {
        var player = new CharacterEntity(Substitute.For<IShard>(), 0x300) { Player = Substitute.For<INetworkPlayer>() };
        var context = new Context(Substitute.For<IShard>(), _character) { Self = player };

        new CombatFlagsCommand(Knockdown).Execute(context);

        Assert.Single(context.Actives);
    }

    [Fact]
    public void ImmuneDeath_LeavesOneHealth()
    {
        _character.SetMaxHealth(1000, true);
        _character.SetCurrentHealth(300);
        var context = Apply(new CombatFlagsCommandDef { Id = 3, ImmuneDeath = 1 });

        _character.TakeDamage(new DamageInfo { Amount = 5000 });
        Assert.Equal(1, _character.CurrentHealth);

        Remove(context);
        Assert.False(_character.ImmuneDeath);
    }

    [Fact]
    public void PlayersOwnEffect_LeftToTheClient()
    {
        var player = new CharacterEntity(Substitute.For<IShard>(), 0x300) { Player = Substitute.For<INetworkPlayer>() };
        var context = new Context(Substitute.For<IShard>(), player);

        new CombatFlagsCommand(Knockdown).Execute(context);

        Assert.Empty(context.Actives);
    }
}
