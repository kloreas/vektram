namespace Sim.Match;

/// <summary>How a match ended.</summary>
public enum MatchOutcome
{
    /// <summary>Team 0 is the last team with at least one living combatant.</summary>
    Team0Wins,

    /// <summary>Team 1 is the last team with at least one living combatant.</summary>
    Team1Wins,

    /// <summary>
    /// All remaining teams were eliminated in the same turn (e.g. the shooter's blast
    /// knocked out the last enemy while simultaneously defeating themselves or an ally).
    /// </summary>
    Draw,

    /// <summary>No team was fully eliminated within the <see cref="Sim.Core.SimConstants.MaxTurnsPerMatch"/> cap.</summary>
    MaxTurnsReached
}
