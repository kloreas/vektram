using System;
using System.Collections.Generic;
using Sim.Match;

namespace Sim.Tests.Match;

/// <summary>
/// Test-only agent that executes a predetermined sequence of actions,
/// then repeats a fixed fallback for every subsequent turn.
/// </summary>
internal sealed class ScriptedAgent : IAgent
{
    private readonly Queue<FireAction> _queue;
    private readonly FireAction        _fallback;

    public ScriptedAgent(IEnumerable<FireAction> actions, FireAction fallback)
    {
        _queue    = new Queue<FireAction>(actions);
        _fallback = fallback;
    }

    /// <summary>Convenience constructor: always returns the same action every turn.</summary>
    public ScriptedAgent(FireAction always) : this(Array.Empty<FireAction>(), always) { }

    public FireAction ChooseAction(MatchState state)
        => _queue.Count > 0 ? _queue.Dequeue() : _fallback;
}
