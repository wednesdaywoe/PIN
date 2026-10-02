using GameServer.StaticDB;
using Xunit;

namespace GameServer.Tests.StaticDB;

public class WeaponTemplateModifierTests
{
    [Fact]
    public void NoModifier_ReturnsBase()
    {
        Assert.Equal(100u, SDBUtils.WeaponTemplateModifier(100u, null, null));
        Assert.Equal(1.5f, SDBUtils.WeaponTemplateModifier(1.5f, null, null));
    }

    [Fact]
    public void Modifier_IsAddedBeforeMultiplier()
    {
        Assert.Equal(120u, SDBUtils.WeaponTemplateModifier(100u, -20, 1.5f));
        Assert.Equal(6f, SDBUtils.WeaponTemplateModifier(1f, 2f, 2f));
    }

    [Fact]
    public void UnsignedResult_BelowZero_SaturatesToZero()
    {
        // float -> uint casts saturate on .NET 9+, so an oversized negative modifier floors at 0
        Assert.Equal(0u, SDBUtils.WeaponTemplateModifier(10u, -20, null));
    }
}
