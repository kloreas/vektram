using Sim.Core;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Projectile;

public class ShellPhysicsTests
{
    private const double LaunchAngleDegrees = 45.0;
    private const double LaunchSpeed        = 50.0;

    private static readonly ProjectileSimulator Simulator = new();
    private static readonly FireCommand StandardShot =
        new(new Vec2D(0.0, 0.0), LaunchAngleDegrees, LaunchSpeed, Seed: 0);

    [Fact]
    public void NeutralPhysics_NoWind_IsExactlyEqualToThreeArgPath()
    {
        WorldEnvironment env = WorldEnvironment.Default;

        ShotResult baseline = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground);
        ShotResult neutral  = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground, ShellPhysics.Neutral);

        AssertExactlyEqual(baseline, neutral);
    }

    [Fact]
    public void NeutralPhysics_WithWind_IsExactlyEqualToThreeArgPath()
    {
        WorldEnvironment env = new(SimConstants.DefaultGravity, WindX: 7.5);

        ShotResult baseline = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground);
        ShotResult neutral  = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground, ShellPhysics.Neutral);

        AssertExactlyEqual(baseline, neutral);
    }

    [Fact]
    public void HigherGravityScale_ShortensRange()
    {
        WorldEnvironment env = WorldEnvironment.Default;
        var standard = new ShellPhysics(GravityScale: 1.0, WindSensitivity: 1.0);
        var heavy    = new ShellPhysics(GravityScale: 1.4, WindSensitivity: 1.0);

        double standardRange = ImpactX(env, standard);
        double heavyRange    = ImpactX(env, heavy);

        Assert.True(heavyRange < standardRange,
            $"Heavy shell range {heavyRange} should be shorter than standard {standardRange}.");
    }

    [Fact]
    public void HigherWindSensitivity_IncreasesDownwindDrift()
    {
        WorldEnvironment env = new(SimConstants.DefaultGravity, WindX: 10.0);
        var lessSensitive = new ShellPhysics(GravityScale: 1.0, WindSensitivity: 1.0);
        var moreSensitive = new ShellPhysics(GravityScale: 1.0, WindSensitivity: 2.0);

        double lessDrift = ImpactX(env, lessSensitive);
        double moreDrift = ImpactX(env, moreSensitive);

        Assert.True(moreDrift > lessDrift,
            $"More wind-sensitive shell impact X {moreDrift} should exceed less sensitive {lessDrift}.");
    }

    [Fact]
    public void SameShellAndInputs_ProducesIdenticalTrajectory()
    {
        WorldEnvironment env = new(SimConstants.DefaultGravity, WindX: 3.0);
        var physics = new ShellPhysics(GravityScale: 1.2, WindSensitivity: 0.8);

        ShotResult first  = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground, physics);
        ShotResult second = Simulator.Simulate(StandardShot, env, FlatTerrain.Ground, physics);

        AssertExactlyEqual(first, second);
    }

    [Fact]
    public void StarterShells_ProducePairwiseDistinctImpacts()
    {
        WorldEnvironment env = new(SimConstants.DefaultGravity, WindX: 8.0);
        var standard = new ShellPhysics(GravityScale: 1.0,  WindSensitivity: 1.0);
        var heavy    = new ShellPhysics(GravityScale: 1.4,  WindSensitivity: 0.5);
        var light    = new ShellPhysics(GravityScale: 0.75, WindSensitivity: 1.7);

        double standardX = ImpactX(env, standard);
        double heavyX    = ImpactX(env, heavy);
        double lightX    = ImpactX(env, light);

        Assert.NotEqual(standardX, heavyX);
        Assert.NotEqual(standardX, lightX);
        Assert.NotEqual(heavyX, lightX);
    }

    private static double ImpactX(WorldEnvironment env, ShellPhysics physics) =>
        Simulator.Simulate(StandardShot, env, FlatTerrain.Ground, physics).ImpactPoint.X;

    private static void AssertExactlyEqual(ShotResult expected, ShotResult actual)
    {
        Assert.Equal(expected.ImpactPoint, actual.ImpactPoint);
        Assert.Equal(expected.ImpactTime, actual.ImpactTime);
        Assert.Equal(expected.Trajectory.Length, actual.Trajectory.Length);
        for (int i = 0; i < expected.Trajectory.Length; i++)
            Assert.Equal(expected.Trajectory[i], actual.Trajectory[i]);
    }
}
