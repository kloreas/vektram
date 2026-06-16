using Sim.Core;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Terrain;

public class ProjectileTerrainTests
{
    private static readonly IProjectileSimulator _sim = new ProjectileSimulator();

    private const double TestGravity = SimConstants.DefaultGravity;
    private const double TestSpeed   = 50.0;
    private static readonly Vec2D            Origin = Vec2D.Zero;
    private static readonly WorldEnvironment NoWind = WorldEnvironment.Default;

    // Private helper: terrain that rises linearly — GetHeight(x) = originHeight + slope * x.
    private sealed class SlopedTerrain : ITerrainQuery
    {
        private readonly double _slope;
        private readonly double _originHeight;

        public SlopedTerrain(double slope, double originHeight = 0.0)
        {
            _slope        = slope;
            _originHeight = originHeight;
        }

        public double GetHeight(double x) => _originHeight + _slope * x;
    }

    // ── Elevated flat terrain ─────────────────────────────────────────────────

    [Fact]
    public void Simulate_ElevatedFlatTerrain_ImpactYEqualsTerrainHeight()
    {
        const double elevation = 10.0;
        var cmd     = new FireCommand(new Vec2D(0.0, elevation), 45.0, TestSpeed, 0);
        var terrain = new FlatTerrain(elevation);

        var result  = _sim.Simulate(cmd, NoWind, terrain);

        Assert.InRange(result.ImpactPoint.Y, elevation - 0.05, elevation + 0.05);
    }

    [Fact]
    public void Simulate_ElevatedFlatTerrain_RangeMatchesFlatGroundRange()
    {
        // Both shots fire the same parabola relative to their terrain surface;
        // horizontal range must match regardless of absolute elevation.
        const double elevation  = 10.0;
        var cmdFlat     = new FireCommand(Vec2D.Zero,               45.0, TestSpeed, 0);
        var cmdElevated = new FireCommand(new Vec2D(0.0, elevation), 45.0, TestSpeed, 0);

        double rangeFlat     = _sim.Simulate(cmdFlat,     NoWind, FlatTerrain.Ground).ImpactPoint.X;
        double rangeElevated = _sim.Simulate(cmdElevated, NoWind, new FlatTerrain(elevation)).ImpactPoint.X;

        Assert.InRange(rangeElevated - rangeFlat, -0.05, 0.05);
    }

    // ── Linear slope: impact lies on the slope surface ───────────────────────

    [Fact]
    public void Simulate_LinearSlope_ImpactLiesOnSlopeSurface()
    {
        var terrain = new SlopedTerrain(slope: 0.5);

        var result = _sim.Simulate(new FireCommand(Origin, 45.0, TestSpeed, 0), NoWind, terrain);

        double expectedY = terrain.GetHeight(result.ImpactPoint.X);
        Assert.InRange(result.ImpactPoint.Y, expectedY - 0.05, expectedY + 0.05);
    }

    // ── Terrain is actually consulted ─────────────────────────────────────────

    [Fact]
    public void Simulate_RisingSlope_ImpactsStrictlyEarlierThanFlatGround()
    {
        // A slope rising in the firing direction intercepts the parabola
        // earlier than flat ground. If terrain is silently ignored, both
        // impact X values would be identical — this test detects that regression.
        var terrainSloped = new SlopedTerrain(slope: 0.1);
        var cmd           = new FireCommand(Origin, 45.0, TestSpeed, 0);

        double rangeFlat   = _sim.Simulate(cmd, NoWind, FlatTerrain.Ground).ImpactPoint.X;
        double rangeSloped = _sim.Simulate(cmd, NoWind, terrainSloped).ImpactPoint.X;

        Assert.True(rangeSloped < rangeFlat,
            $"Expected sloped impact X ({rangeSloped:F2} m) < flat impact X ({rangeFlat:F2} m)");
    }

    // ── Determinism on non-flat terrain ───────────────────────────────────────

    [Fact]
    public void Simulate_IdenticalInputsWithSlopedTerrain_ProduceIdenticalResults()
    {
        var terrain = new SlopedTerrain(slope: 0.2);
        var cmd     = new FireCommand(Origin, 45.0, TestSpeed, 0);

        var r1 = _sim.Simulate(cmd, NoWind, terrain);
        var r2 = _sim.Simulate(cmd, NoWind, terrain);

        Assert.Equal(r1.ImpactPoint, r2.ImpactPoint);
        Assert.Equal(r1.ImpactTime,  r2.ImpactTime);
    }

    // ── Robustness: non-ascending shots launched from terrain surface ─────────

    [Theory]
    [InlineData(  0.0)]   // horizontal
    [InlineData(-30.0)]   // downward at 30°
    [InlineData(-90.0)]   // straight down
    public void Simulate_NonAscendingShot_FromTerrainSurface_ImpactsAtOrNearOrigin(double angle)
    {
        // When launched horizontally or downward from terrain level, the
        // projectile is already at or below ground after the first step.
        // Interpolation must recover the origin — not fly backward or NaN.
        var result = _sim.Simulate(
            new FireCommand(Vec2D.Zero, angle, TestSpeed, 0),
            NoWind,
            FlatTerrain.Ground);

        Assert.InRange(result.ImpactPoint.X, -0.001, 0.001);
        Assert.InRange(result.ImpactPoint.Y, -0.001, 0.001);
    }
}
