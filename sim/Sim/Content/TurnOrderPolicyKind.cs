namespace Sim.Content;

/// <summary>
/// Selects which turn-order policy a mode uses. Authored as data in
/// <c>/content/data/modes.json</c> and mapped to an <c>ITurnOrderPolicy</c> at match setup.
/// </summary>
/// <remarks>
/// Only <see cref="RoundRobin"/> exists today; this enum is the data-driven SELECTION seam.
/// The planned agility/delay policy (ADR-0004) lands as a new value here plus one mapping case
/// in the controller — never a branch in the match engine.
/// </remarks>
public enum TurnOrderPolicyKind
{
    /// <summary>
    /// Teams alternate; living members cycle in roster order
    /// (<c>Sim.Match.RoundRobinTurnOrderPolicy</c>).
    /// </summary>
    RoundRobin
}
