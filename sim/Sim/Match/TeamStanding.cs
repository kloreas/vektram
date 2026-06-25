namespace Sim.Match;

/// <summary>
/// Per-team aggregate snapshot the win-condition evaluator reads. The match controller builds one
/// per team each turn from cheap running tallies (pure sums — no RNG), so the evaluator stays pure
/// and never rescans the turn log.
/// </summary>
/// <param name="TeamId">The team this standing summarizes.</param>
/// <param name="AliveCount">Number of living combatants on the team (HP &gt; 0).</param>
/// <param name="TotalHpRemaining">Sum of living-combatant HP, floored at 0.</param>
/// <param name="TotalDamageDealt">Cumulative damage this team has dealt to enemies so far.</param>
public readonly record struct TeamStanding(
    int    TeamId,
    int    AliveCount,
    double TotalHpRemaining,
    double TotalDamageDealt);
