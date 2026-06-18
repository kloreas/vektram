namespace Sim.Match;

/// <summary>
/// Decides the action for one turn. Implemented by human input adapters, scripted test agents,
/// and future AI bots alike. The match engine is fully agnostic to who is playing.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Returns the action to take for the current turn.
    /// Must not alter any external state — treat it as a pure query.
    /// </summary>
    /// <param name="state">Snapshot of the world from this agent's perspective.</param>
    FireAction ChooseAction(MatchState state);
}
