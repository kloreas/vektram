using Sim.Content;

namespace Sim.Match;

/// <summary>
/// Effective (final) stat block a combatant brings into the damage formula.
/// </summary>
/// <remarks>
/// These are the resolved numbers the formula consumes; it never knows where they came
/// from. The equipment / modifier-stack system (#4) will assemble these from base stats +
/// gear + runes + buffs. All fields beyond the three-argument constructor default to
/// neutral, so <see cref="Default"/> behaves like a stat-less combatant.
/// </remarks>
/// <param name="MaxHp">Maximum hit points. Must be &gt; 0.</param>
/// <param name="DamageModifier">
/// Attacker-side buff multiplier applied to the damage core (DDTank
/// <c>CurrentDamagePlus × CurrentShootMinus</c>). 1.0 = no bonus.
/// </param>
/// <param name="Defense">Defender-side value feeding <c>defenceReduce</c> (DDTank <c>Defence</c>).</param>
public readonly record struct CombatantStats(
    double MaxHp,
    double DamageModifier,
    double Defense)
{
    /// <summary>Attacker-side core scaling stat (DDTank <c>Attack</c>).</summary>
    public double Attack { get; init; } = 0.0;

    /// <summary>Attacker-side armor penetration; reduces the target's guard (DDTank <c>SunderArmor</c>).</summary>
    public double SunderArmor { get; init; } = 0.0;

    /// <summary>Defender-side armor feeding <c>guardReduce</c> (DDTank <c>BaseGuard</c>).</summary>
    public double BaseGuard { get; init; } = 0.0;

    /// <summary>Attacker-side critical-hit probability in [0, 1]. 0 = never crits.</summary>
    public double CritChance { get; init; } = 0.0;

    /// <summary>Attacker-side multiplier applied to the damage core on a critical hit. 1.0 = no bonus.</summary>
    public double CritMultiplier { get; init; } = 1.0;

    /// <summary>Defender-side dodge probability in [0, 1]; a successful dodge zeroes the hit (DDTank <c>miss</c>).</summary>
    public double Dodge { get; init; } = 0.0;

    /// <summary>The combatant's element (attacker side selects the advantage row).</summary>
    public Element Element { get; init; } = Element.None;

    /// <summary>Attacker-side elemental power added (above defender resist) to the element bonus.</summary>
    public double ElementPower { get; init; } = 0.0;

    /// <summary>Defender-side elemental resistance subtracted from the attacker's element power.</summary>
    public double ElementResist { get; init; } = 0.0;

    /// <summary>100 HP, 1.0× modifier, 0 defense, all other stats neutral.</summary>
    public static CombatantStats Default { get; } = new(100.0, 1.0, 0.0);
}
