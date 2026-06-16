using System;
using System.Linq;
using Sim.Core;
using Sim.Projectile;
using Sim.Terrain;
using Xunit;

namespace Sim.Tests.Projectile;

public class ProjectileSimulatorTests
{
    private static readonly IProjectileSimulator _sim = new ProjectileSimulator();

    private const double TestGravity = SimConstants.DefaultGravity;
    private const double TestSpeed   = 50.0;
    private static readonly Vec2D            Origin = Vec2D.Zero;
    private static readonly WorldEnvironment NoWind = WorldEnvironment.Default;

    // ── Known physics: range ─────────────────────────────────────────────────

    [Fact]
    public void Simulate_KnownAngleAndSpeed_LandsAtExpectedRange()
    {
        // Analytic: R = v² · sin(2θ) / g  →  2500 · 1 / 9.8 ≈ 255.10 m
        const double Angle        = 45.0;
        double       angleRad     = Angle * SimConstants.DegToRad;
        double       expectedRange = TestSpeed * TestSpeed * Math.Sin(2.0 * angleRad) / TestGravity;

        var result = _sim.Simulate(new FireCommand(Origin, Angle, TestSpeed, 0), NoWind, FlatTerrain.Ground);

        Assert.InRange(result.ImpactPoint.X, expectedRange - 0.05, expectedRange + 0.05);
    }

    // ── Known physics: apex height ────────────────────────────────────────────

    [Fact]
    public void Simulate_KnownAngleAndSpeed_ReachesExpectedApex()
    {
        // Analytic: H = (v · sin θ)² / (2g)  →  (35.355)² / 19.6 ≈ 63.78 m
        const double Angle      = 45.0;
        double       angleRad   = Angle * SimConstants.DegToRad;
        double       vy0        = TestSpeed * Math.Sin(angleRad);
        double       expectedApex = vy0 * vy0 / (2.0 * TestGravity);

        var result = _sim.Simulate(new FireCommand(Origin, Angle, TestSpeed, 0), NoWind, FlatTerrain.Ground);

        double maxY = result.Trajectory.Max(p => p.Position.Y);
        Assert.InRange(maxY, expectedApex - 0.05, expectedApex + 0.05);
    }

    // ── 45° maximum range ─────────────────────────────────────────────────────

    [Fact]
    public void Simulate_45Degrees_HasMaximumRangeAmongNearbyAngles()
    {
        double Range(double angle) =>
            _sim.Simulate(new FireCommand(Origin, angle, TestSpeed, 0), NoWind, FlatTerrain.Ground).ImpactPoint.X;

        Assert.True(Range(45.0) > Range(40.0));
        Assert.True(Range(45.0) > Range(50.0));
    }

    // ── Wind shifts landing point ─────────────────────────────────────────────

    [Fact]
    public void Simulate_PositiveWind_ShiftsLandingRight()
    {
        var cmd      = new FireCommand(Origin, 45.0, TestSpeed, 0);
        var noWind   = _sim.Simulate(cmd, NoWind,                                  FlatTerrain.Ground);
        var withWind = _sim.Simulate(cmd, new WorldEnvironment(TestGravity, 10.0), FlatTerrain.Ground);

        Assert.True(withWind.ImpactPoint.X > noWind.ImpactPoint.X);
    }

    [Fact]
    public void Simulate_NegativeWind_ShiftsLandingLeft()
    {
        var cmd      = new FireCommand(Origin, 45.0, TestSpeed, 0);
        var noWind   = _sim.Simulate(cmd, NoWind,                                   FlatTerrain.Ground);
        var withWind = _sim.Simulate(cmd, new WorldEnvironment(TestGravity, -10.0), FlatTerrain.Ground);

        Assert.True(withWind.ImpactPoint.X < noWind.ImpactPoint.X);
    }

    // ── Zero wind is horizontally symmetric ───────────────────────────────────

    [Fact]
    public void Simulate_ZeroWind_IsHorizontallySymmetric()
    {
        // A shot at θ and its mirror (180° − θ) must land symmetrically about x = 0.
        double forward  = _sim.Simulate(new FireCommand(Origin,  45.0, TestSpeed, 0), NoWind, FlatTerrain.Ground).ImpactPoint.X;
        double backward = _sim.Simulate(new FireCommand(Origin, 135.0, TestSpeed, 0), NoWind, FlatTerrain.Ground).ImpactPoint.X;

        Assert.InRange(forward + backward, -0.001, 0.001);
    }

    // ── Determinism: exact equality ───────────────────────────────────────────

    [Fact]
    public void Simulate_IdenticalInputs_ProduceIdenticalTrajectory()
    {
        var cmd = new FireCommand(Origin, 45.0, TestSpeed, 0);

        var r1 = _sim.Simulate(cmd, NoWind, FlatTerrain.Ground);
        var r2 = _sim.Simulate(cmd, NoWind, FlatTerrain.Ground);

        Assert.Equal(r1.ImpactPoint,       r2.ImpactPoint);
        Assert.Equal(r1.ImpactTime,        r2.ImpactTime);
        Assert.Equal(r1.Trajectory.Length, r2.Trajectory.Length);

        for (int i = 0; i < r1.Trajectory.Length; i++)
        {
            Assert.Equal(r1.Trajectory[i], r2.Trajectory[i]);
        }
    }

    // ── Fixed timestep: Time[i] == i × FixedTimestep, exact by construction ──

    [Fact]
    public void Simulate_TrajectoryTimes_EqualIndexTimesTimestep()
    {
        var result = _sim.Simulate(new FireCommand(Origin, 45.0, TestSpeed, 0), NoWind, FlatTerrain.Ground);

        for (int i = 0; i < result.Trajectory.Length; i++)
        {
            // The sim computes time as (i * FixedTimestep), so this must be exact.
            Assert.Equal(i * SimConstants.FixedTimestep, result.Trajectory[i].Time);
        }
    }

    // ── Vertical shot lands near origin X ────────────────────────────────────

    [Fact]
    public void Simulate_VerticalShot_LandsNearOriginX()
    {
        // cos(90°) = 0 → zero horizontal velocity → x stays 0 exactly.
        var result = _sim.Simulate(new FireCommand(Origin, 90.0, TestSpeed, 0), NoWind, FlatTerrain.Ground);

        Assert.InRange(result.ImpactPoint.X, -0.001, 0.001);
    }

    // ── Zero speed: stays at or near origin ──────────────────────────────────

    [Fact]
    public void Simulate_ZeroSpeed_ImpactsNearOrigin()
    {
        // Speed = 0 from (0, 0): first step falls below y = 0; interpolated impact = origin.
        var result = _sim.Simulate(new FireCommand(Origin, 45.0, 0.0, 0), NoWind, FlatTerrain.Ground);

        Assert.InRange(result.ImpactPoint.X, -0.001, 0.001);
        Assert.True(result.ImpactPoint.Y <= 0.0);
    }
}
