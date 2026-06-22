namespace Sim.Match;

/// <summary>
/// Final blast damage plus a breakdown of each contributing factor, for UI, replay, and
/// debugging. Server-authoritative: the client displays these values, it never recomputes them.
/// </summary>
/// <param name="FinalDamage">Damage applied to the combatant, in HP. Always ≥ 0.</param>
/// <param name="IsMiss">The hit was dodged; <see cref="FinalDamage"/> is 0.</param>
/// <param name="IsCrit">The hit was critical; the crit multiplier is folded into the core.</param>
/// <param name="Falloff">Distance falloff factor applied (1.0 at centre).</param>
/// <param name="GuardReduce">Guard reduction fraction applied, in [0, cap].</param>
/// <param name="DefenceReduce">Defence reduction fraction applied, in [0, cap].</param>
/// <param name="AttackFactor">Attacker's attack-scaling factor (<c>floor + Attack × scale</c>).</param>
/// <param name="ElementBonus">Additive element-layer damage after the advantage multiplier.</param>
/// <param name="ModeMultiplier">Room/mode scalar applied to the core.</param>
public readonly record struct DamageResult(
    double FinalDamage,
    bool   IsMiss,
    bool   IsCrit,
    double Falloff,
    double GuardReduce,
    double DefenceReduce,
    double AttackFactor,
    double ElementBonus,
    double ModeMultiplier);
