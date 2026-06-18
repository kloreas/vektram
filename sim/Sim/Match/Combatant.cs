using Sim.Core;

namespace Sim.Match;

/// <summary>
/// Snapshot of a single combatant's live state during a match.
/// </summary>
/// <param name="Position">
/// World position in metres. Y must equal <c>terrain.GetHeight(Position.X)</c>;
/// callers are responsible for snapping to the surface before passing to the match engine.
/// </param>
/// <param name="Hp">Current hit points. May go negative after damage; check <see cref="IsDefeated"/>.</param>
/// <param name="Stats">Immutable stat block.</param>
public readonly record struct Combatant(Vec2D Position, double Hp, CombatantStats Stats)
{
    /// <summary><see langword="true"/> when HP has reached or dropped below zero.</summary>
    public bool IsDefeated => Hp <= 0.0;
}
