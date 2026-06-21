using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Ai;

/// <summary>
/// Rule-based AI agent. Each turn it grid-searches over a range of launch angles,
/// simulates each candidate, and fires at the angle whose predicted impact lands
/// closest to the nearest enemy in X.
/// </summary>
public sealed class BotAgent : IAgent
{
    private readonly IProjectileSimulator _projectileSim;
    private readonly Weapon               _weapon;
    private readonly BotDifficulty        _difficulty;
    private readonly SimRandom            _rng;
    private readonly uint                 _seed;

    /// <summary>Creates a <see cref="BotAgent"/>.</summary>
    /// <param name="projectileSim">Physics backend used to evaluate candidate angles.</param>
    /// <param name="weapon">Weapon fired each turn. Per-turn weapon selection is a future hook.</param>
    /// <param name="difficulty">Search budget, aim noise, and wind-compensation tuning.</param>
    /// <param name="seed">Seed for this bot's RNG. Also forwarded as <see cref="FireCommand.Seed"/>.</param>
    public BotAgent(IProjectileSimulator projectileSim, Weapon weapon, BotDifficulty difficulty, uint seed)
    {
        _projectileSim = projectileSim;
        _weapon        = weapon;
        _difficulty    = difficulty;
        _seed          = seed;
        _rng           = new SimRandom(seed);
    }

    /// <inheritdoc/>
    public FireAction ChooseAction(MatchState state)
    {
        if (state.Enemies.Count == 0)
            return new FireAction(90.0, _weapon.ProjectileSpeed, _weapon);

        Combatant target = SelectTarget(state.Enemies, state.Self.Position);

        double dx = target.Position.X - state.Self.Position.X;
        double angleMin, angleMax;
        if      (dx > 0) { angleMin =   1.0; angleMax =  89.0; }
        else if (dx < 0) { angleMin =  91.0; angleMax = 179.0; }
        else             { angleMin =   1.0; angleMax = 179.0; }

        // Scale modeled wind by difficulty factor so the search trajectory matches the bot's "awareness"
        var modeledEnv = new WorldEnvironment(
            state.Environment.Gravity,
            state.Environment.WindX * _difficulty.WindCompensationFactor);

        double bestAngle = SearchBestAngle(
            state.Self.Position, target, angleMin, angleMax, modeledEnv, state.Terrain);

        // Always consume one RNG sample per turn so sequences stay identical across difficulties.
        // When AimNoiseDegrees == 0 the offset computes to 0 but the state still advances.
        double noise = (_rng.NextDouble() * 2.0 - 1.0) * _difficulty.AimNoiseDegrees;

        return new FireAction(bestAngle + noise, _weapon.ProjectileSpeed, _weapon);
    }

    /// <summary>
    /// Returns the living enemy closest to <paramref name="selfPos"/> by squared distance.
    /// Difficulty-scaled target selection (e.g. lowest-HP priority) is a future knob here.
    /// </summary>
    private static Combatant SelectTarget(IReadOnlyList<Combatant> enemies, Vec2D selfPos)
    {
        Combatant best  = enemies[0];
        double    minSq = (enemies[0].Position - selfPos).LengthSquared;

        for (int i = 1; i < enemies.Count; i++)
        {
            double sq = (enemies[i].Position - selfPos).LengthSquared;
            if (sq < minSq) { minSq = sq; best = enemies[i]; }
        }

        return best;
    }

    /// <summary>
    /// Evaluates <see cref="BotDifficulty.SearchBudget"/> evenly-spaced angles over
    /// [<paramref name="angleMin"/>, <paramref name="angleMax"/>] and returns the one whose
    /// simulated impact lands closest to the target in X.
    /// </summary>
    private double SearchBestAngle(
        Vec2D            origin,
        Combatant        target,
        double           angleMin,
        double           angleMax,
        WorldEnvironment modeledEnv,
        ITerrainQuery    terrain)
    {
        int    budget    = _difficulty.SearchBudget;
        double bestAngle = angleMin;
        double bestScore = double.MaxValue;

        for (int i = 0; i < budget; i++)
        {
            // budget == 1: evaluate the midpoint (max-range guess at ~45°/135°) rather than angleMin.
            // budget >= 2: uniform grid from angleMin to angleMax inclusive.
            double angle = budget == 1
                ? (angleMin + angleMax) * 0.5
                : angleMin + (angleMax - angleMin) * i / (budget - 1);

            var cmd  = new FireCommand(origin, angle, _weapon.ProjectileSpeed, _seed);
            var shot = _projectileSim.Simulate(cmd, modeledEnv, terrain);

            // Scoring by impact.X is correct only on flat terrain where Y distance is zero.
            // Extension point: switch to full 2D distance to target when terrain becomes non-flat.
            double score = Math.Abs(shot.ImpactPoint.X - target.Position.X);

            if (score < bestScore) { bestScore = score; bestAngle = angle; }
        }

        // Future local refinement note: the score function is BIMODAL on flat terrain —
        // a low arc and a high arc can both reach the same X. Any refinement pass must
        // preserve both candidates rather than running a single-mode local descent.

        return bestAngle;
    }
}
