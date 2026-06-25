namespace Sim.Match;

/// <summary>
/// A single turn's submission: the shot to fire, plus an optional item to use first.
/// </summary>
/// <remarks>
/// A turn is always a shot ("a turn = a shot"); <see cref="ItemId"/> lets the acting
/// combatant optionally consume one item <em>before</em> that shot resolves. A
/// <see langword="null"/> <see cref="ItemId"/> is a plain fire-only turn, behaviourally
/// identical to submitting <see cref="Fire"/> on its own. Item use is server-authoritative:
/// it is resolved against the controller's item catalog and the actor's inventory, and an
/// unavailable item is rejected cleanly while the turn still fires.
/// </remarks>
/// <param name="Fire">The shot fired this turn (angle, speed, weapon).</param>
/// <param name="ItemId">
/// Id of the item to use before firing, or <see langword="null"/> for a fire-only turn.
/// </param>
public readonly record struct TurnAction(FireAction Fire, string? ItemId);
