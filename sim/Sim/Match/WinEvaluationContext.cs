using System.Collections.Generic;

namespace Sim.Match;

/// <summary>
/// Read-only snapshot of match progress handed to <see cref="WinConditionEvaluator"/> after each
/// turn. It carries everything any CURRENT condition needs — the set of teams (with alive counts
/// and tiebreak tallies) and the turn position.
/// </summary>
/// <remarks>
/// EXTENSION BOUNDARY: a future condition that needs new match state (e.g. hold-a-zone needs zone
/// control, which does not exist yet) adds a field here plus one evaluator case — a small, named
/// extension, never an engine rewrite. Conditions that only read existing progress (survive-N-turns;
/// defeat-a-boss-target, already expressible as last-team-standing with the boss as its own team)
/// need no change here.
/// </remarks>
/// <param name="TurnNumber">Turns completed so far.</param>
/// <param name="MaxTurns">The mode's turn cap.</param>
/// <param name="Standings">
/// Per-team standings, one entry per team id 0..N-1 in ascending order (a team with zero living
/// combatants is still present, with <see cref="TeamStanding.AliveCount"/> 0).
/// </param>
public readonly record struct WinEvaluationContext(
    int                         TurnNumber,
    int                         MaxTurns,
    IReadOnlyList<TeamStanding> Standings);
