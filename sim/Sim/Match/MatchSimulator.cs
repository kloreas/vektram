using System.Collections.Generic;
using Sim.Core;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Runs a team-based turn-based match to completion.
/// Turn order is governed by <see cref="RoundRobinTurnOrderPolicy"/> (fair team interleaving).
/// Blast damage respects <see cref="MatchOptions.FriendlyFire"/> and <see cref="MatchOptions.SelfDamage"/>.
/// A match ends when exactly one team has surviving combatants, or all teams are eliminated
/// simultaneously (Draw), or the turn cap is reached.
/// </summary>
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

        var living = new List<int>(n);
        for (int i = 0; i < n; i++) living.Add(i);

        // Agility/delay-based ordering is the planned future policy (see ADR-0004).
        ITurnOrderPolicy policy = new RoundRobinTurnOrderPolicy(teamIds);
        var log = new List<TurnEvent>(SimConstants.MaxTurnsPerMatch);

        for (int turn = 0; turn < SimConstants.MaxTurnsPerMatch; turn++)
        {
            int actorIdx  = policy.NextActor(living);
            int actorTeam = teamIds[actorIdx];

            var allies  = BuildTeammates(actorIdx, actorTeam, teamIds, combatants, living);
            var enemies = BuildEnemies(actorTeam, teamIds, combatants, living);
            var state   = new MatchState(combatants[actorIdx], allies, enemies, terrain, environment, turn);
            var action  = agents[actorIdx].ChooseAction(state);

            var command = new FireCommand(combatants[actorIdx].Position, action.AngleDegrees, action.Speed, seed);
            var shot    = _projectileSim.Simulate(command, environment, terrain);

            var actorStats = combatants[actorIdx].Stats;
            var results    = new CombatantTurnResult[n];

            for (int i = 0; i < n; i++)
            {
                bool isActor = i == actorIdx;
                bool isAlly  = teamIds[i] == actorTeam;

                // Self and ally checks are independent levers (see MatchOptions).
                bool apply = isActor
                    ? options.SelfDamage
                    : (!isAlly || options.FriendlyFire);

                double hpBefore = combatants[i].Hp;
                double damage   = apply
                    ? DamageCalculator.Compute(shot.ImpactPoint, combatants[i].Position, action.Weapon, actorStats, combatants[i].Stats)
                    : 0.0;

                combatants[i] = combatants[i] with { Hp = hpBefore - damage };
                results[i]    = new CombatantTurnResult(damage, hpBefore, combatants[i].Hp);
            }

            log.Add(new TurnEvent(turn, actorIdx, action, shot.ImpactPoint, results));

            // Remove newly defeated combatants from the turn-order pool.
            for (int k = living.Count - 1; k >= 0; k--)
                if (combatants[living[k]].IsDefeated)
                    living.RemoveAt(k);

            // Determine which teams still have living members.
            var aliveTeams = new HashSet<int>();
            foreach (int i in living) aliveTeams.Add(teamIds[i]);

            if (aliveTeams.Count == 0)
                return new MatchResult(MatchOutcome.Draw, null, turn + 1, log);

            if (aliveTeams.Count == 1)
            {
                int winnerTeam = -1;
                foreach (int t in aliveTeams) winnerTeam = t;
                var outcome = winnerTeam == 0 ? MatchOutcome.Team0Wins : MatchOutcome.Team1Wins;
                return new MatchResult(outcome, winnerTeam, turn + 1, log);
            }
        }

        return new MatchResult(MatchOutcome.MaxTurnsReached, null, SimConstants.MaxTurnsPerMatch, log);
    }

    private static IReadOnlyList<Combatant> BuildTeammates(
        int actorIdx, int actorTeam, int[] teamIds, Combatant[] combatants, List<int> living)
    {
        var result = new List<Combatant>();
        foreach (int i in living)
            if (i != actorIdx && teamIds[i] == actorTeam)
                result.Add(combatants[i]);
        return result;
    }

    private static IReadOnlyList<Combatant> BuildEnemies(
        int actorTeam, int[] teamIds, Combatant[] combatants, List<int> living)
    {
        var result = new List<Combatant>();
        foreach (int i in living)
            if (teamIds[i] != actorTeam)
                result.Add(combatants[i]);
        return result;
    }
}
