using System.Collections.Generic;

namespace Sim.Match;

/// <summary>Complete, immutable outcome of a finished match.</summary>
public readonly struct MatchResult
{
    /// <summary>How the match ended.</summary>
    public MatchOutcome Outcome { get; }

    /// <summary>
    /// Index of the winner (0 or 1), or <see langword="null"/> for
    /// <see cref="MatchOutcome.Draw"/> and <see cref="MatchOutcome.MaxTurnsReached"/>.
    /// </summary>
    public int? WinnerIndex { get; }

    /// <summary>Total number of turns played (equals <see cref="Log"/> length).</summary>
    public int TurnCount { get; }

    /// <summary>Ordered log of every turn for playback and auditing.</summary>
    public IReadOnlyList<TurnEvent> Log { get; }

    /// <inheritdoc cref="MatchResult"/>
    public MatchResult(MatchOutcome outcome, int? winnerIndex, int turnCount, IReadOnlyList<TurnEvent> log)
    {
        Outcome     = outcome;
        WinnerIndex = winnerIndex;
        TurnCount   = turnCount;
        Log         = log;
    }
}
