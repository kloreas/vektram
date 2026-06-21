using System.Collections.Generic;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Runs a team-based turn-based match to completion.
/// Turn order is governed by <see cref="RoundRobinTurnOrderPolicy"/> (fair team interleaving).
/// Blast damage respects <see cref="MatchOptions.FriendlyFire"/> and <see cref="MatchOptions.SelfDamage"/>.
/// A match ends when exactly one team has surviving combatants, all teams are eliminated
/// simultaneously (Draw), or the turn cap is reached.
/// </summary>
/// <remarks>
/// The full turn/damage/win-condition engine lives in <see cref="MatchController"/>.
/// This class is a thin loop that supplies agent-chosen actions to the controller.
/// </remarks>
public sealed class MatchSimulator : IMatchSimulator
{
    private readonly IProjectileSimulator _projectileSim;

    /// <summary>Creates a <see cref="MatchSimulator"/> backed by the given projectile simulator.</summary>
    public MatchSimulator(IProjectileSimulator projectileSim) => _projectileSim = projectileSim;

    /// <inheritdoc/>
    public MatchResult Run(
        IReadOnlyList<CombatantEntry> entries,
        MatchOptions options,
        ITerrainQuery terrain,
        WorldEnvironment environment,
        uint seed)
    {
        int n = entries.Count;

        var combatants = new Combatant[n];
        var teamIds    = new int[n];
        var agents     = new IAgent[n];

        for (int i = 0; i < n; i++)
        {
            combatants[i] = entries[i].Combatant;
            teamIds[i]    = entries[i].TeamId;
            agents[i]     = entries[i].Agent;
        }

        var ctrl = new MatchController(
            _projectileSim, combatants, teamIds, options, terrain, environment, seed);

        while (!ctrl.IsOver)
        {
            var action = agents[ctrl.CurrentActorIndex].ChooseAction(ctrl.CurrentState);
            ctrl.ResolveTurn(action);
        }

        return ctrl.Result;
    }
}
