namespace Sim.Match;

/// <summary>How a match ended.</summary>
public enum MatchOutcome
{
    /// <summary>Combatant 0 survived; combatant 1 was defeated.</summary>
    Player0Wins,

    /// <summary>Combatant 1 survived; combatant 0 was defeated.</summary>
    Player1Wins,

    /// <summary>
    /// Both combatants were defeated in the same turn (e.g. shooter caught in their own blast).
    /// Resolved deterministically — no tiebreaker needed since neither side survives.
    /// </summary>
    Draw,

    /// <summary>Neither combatant was defeated within the <see cref="Sim.Core.SimConstants.MaxTurnsPerMatch"/> cap.</summary>
    MaxTurnsReached
}
