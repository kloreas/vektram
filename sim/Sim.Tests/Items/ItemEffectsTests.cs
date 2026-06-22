using System;
using Sim.Content;
using Sim.Items;
using Xunit;

namespace Sim.Tests.Items;

public class ItemEffectsTests
{
    private const string BallsJson = @"
{
  ""schemaVersion"": 1,
  ""balls"": [
    { ""id"": ""standard"", ""displayName"": ""Standard Shell"", ""type"": ""Standard"",
      ""gravityScale"": 1.0, ""windSensitivity"": 1.0, ""blastRadius"": 2.5,
      ""baseDamage"": 100.0, ""projectileSpeed"": 45.0 }
  ]
}";

    private static readonly BallCatalog Balls = BallCatalog.FromJson(BallsJson);

    // ── GrantBall seam ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolveGrantedBall_ReturnsReferencedBall()
    {
        var effect = new ItemEffect(ItemEffectKind.GrantBall, "standard", 0.0);

        BallDefinition ball = ItemEffects.ResolveGrantedBall(Balls, effect);

        Assert.Equal("standard", ball.Id);
        Assert.Equal(100.0, ball.BaseDamage);
    }

    [Fact]
    public void ResolveGrantedBall_DanglingBallId_ThrowsBallDataException()
    {
        var effect = new ItemEffect(ItemEffectKind.GrantBall, "ghost", 0.0);

        Assert.Throws<BallDataException>(() => ItemEffects.ResolveGrantedBall(Balls, effect));
    }

    [Fact]
    public void ResolveGrantedBall_WrongEffectKind_Throws()
    {
        var effect = new ItemEffect(ItemEffectKind.RestoreHp, null, 50.0);

        Assert.Throws<ArgumentException>(() => ItemEffects.ResolveGrantedBall(Balls, effect));
    }

    // ── RestoreHp seam ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolveRestoredHp_AddsAmount_BelowMax()
    {
        double hp = ItemEffects.ResolveRestoredHp(currentHp: 40.0, maxHp: 100.0, amount: 30.0);

        Assert.Equal(70.0, hp);
    }

    [Fact]
    public void ResolveRestoredHp_ClampsToMax_NoOverheal()
    {
        double hp = ItemEffects.ResolveRestoredHp(currentHp: 90.0, maxHp: 100.0, amount: 150.0);

        Assert.Equal(100.0, hp);
    }

    [Fact]
    public void ResolveRestoredHp_ZeroAmount_NoChange()
    {
        double hp = ItemEffects.ResolveRestoredHp(currentHp: 55.0, maxHp: 100.0, amount: 0.0);

        Assert.Equal(55.0, hp);
    }

    [Fact]
    public void ResolveRestoredHp_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => ItemEffects.ResolveRestoredHp(50.0, 100.0, -1.0));
    }
}
