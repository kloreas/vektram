using System.Collections.Generic;
using Sim.Core;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Runs a 1v1 turn-based match to completion.
/// Combatant 0 acts on even turns; combatant 1 acts on odd turns.
/// Every shot's blast is applied to BOTH combatants — self-damage from a poorly aimed shot is intentional.
/// </summary>
public sealed class MatchSimulator : IMatchSimulator
{
    private readonly IProjectileSimulator _projectileSim;

    /// <summary>Creates a <see cref="MatchSimulator"/> backed by the given projectile simulator.</summary>
    public MatchSimulator(IProjectileSimulator projectileSim) => _projectileSim = projectileSim;

    /// <inheritdoc/>
    public MatchResult Run(
        Combatant combatant0,
        Combatant combatant1,
        IAgent agent0,
        IAgent agent1,
        ITerrainQuery terrain,
        WorldEnvironment environment,
        uint seed)
    {
        var combatants = new[] { combatant0, combatant1 };
        var agents     = new[] { agent0, agent1 };
        var log        = new List<TurnEvent>(SimConstants.MaxTurnsPerMatch);

        for (int turn = 0; turn < SimConstants.MaxTurnsPerMatch; turn++)
        {
            int actorIndex = turn % 2;

            var state  = new MatchState(combatants[actorIndex], combatants[1 - actorIndex], terrain, environment, turn);
            var action = agents[actorIndex].ChooseAction(state);

            var command = new FireCommand(combatants[actorIndex].Position, action.AngleDegrees, action.Speed, seed);
            var shot    = _projectileSim.Simulate(command, environment, terrain);

            double hp0Before = combatants[0].Hp;
            double hp1Before = combatants[1].Hp;

            double damage0 = DamageCalculator.Compute(
                shot.ImpactPoint, combatants[0].Position,
                action.Weapon, combatants[actorIndex].Stats, combatants[0].Stats);

            double damage1 = DamageCalculator.Compute(
                shot.ImpactPoint, combatants[1].Position,
                action.Weapon, combatants[actorIndex].Stats, combatants[1].Stats);

            combatants[0] = combatants[0] with { Hp = hp0Before - damage0 };
            combatants[1] = combatants[1] with { Hp = hp1Before - damage1 };

            log.Add(new TurnEvent(
                turn,
                actorIndex,
                action,
                shot.ImpactPoint,
                new CombatantTurnResult(damage0, hp0Before, combatants[0].Hp),
                new CombatantTurnResult(damage1, hp1Before, combatants[1].Hp)));

            bool c0Defeated = combatants[0].IsDefeated;
            bool c1Defeated = combatants[1].IsDefeated;

            if (c0Defeated && c1Defeated)
                return new MatchResult(MatchOutcome.Draw, null, turn + 1, log);
            if (c0Defeated)
                return new MatchResult(MatchOutcome.Player1Wins, 1, turn + 1, log);
            if (c1Defeated)
                return new MatchResult(MatchOutcome.Player0Wins, 0, turn + 1, log);
        }

        return new MatchResult(MatchOutcome.MaxTurnsReached, null, SimConstants.MaxTurnsPerMatch, log);
    }
}
