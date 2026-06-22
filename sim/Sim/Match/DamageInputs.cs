using Sim.Content;
using Sim.Core;

namespace Sim.Match;

/// <summary>
/// Complete, already-resolved inputs to one blast-damage computation.
/// </summary>
/// <remarks>
/// All randomness and table lookups are resolved by the caller (the match controller does
/// the seeded crit/miss rolls and the element-advantage lookup) and passed in here, so
/// <see cref="DamageCalculator.Compute"/> stays a pure function.
/// </remarks>
/// <param name="ImpactPoint">World position where the projectile detonated.</param>
/// <param name="TargetPosition">World position of the combatant receiving the blast.</param>
/// <param name="BaseDamage">Shell base damage at the blast centre, in HP.</param>
/// <param name="BlastRadius">Blast radius in metres; targets at or beyond it take no damage.</param>
/// <param name="Attacker">Effective stats of the firing combatant.</param>
/// <param name="Defender">Effective stats of the combatant being hit.</param>
/// <param name="Tuning">Damage formula divisors, caps, and curve constants.</param>
/// <param name="ModeMultiplier">Room/mode damage scalar (1.0 = no adjustment).</param>
/// <param name="ElementAdvantage">
/// Resolved advantage multiplier for the attacker's element vs the defender's element.
/// </param>
/// <param name="IsCrit">Whether this hit is a critical (resolved by the caller's seeded roll).</param>
/// <param name="IsMiss">Whether this hit is dodged (resolved by the caller's seeded roll).</param>
public readonly record struct DamageInputs(
    Vec2D          ImpactPoint,
    Vec2D          TargetPosition,
    double         BaseDamage,
    double         BlastRadius,
    CombatantStats Attacker,
    CombatantStats Defender,
    CombatTuning   Tuning,
    double         ModeMultiplier,
    double         ElementAdvantage,
    bool           IsCrit,
    bool           IsMiss);
