namespace Sim.Stats;

/// <summary>
/// The numeric channels of <see cref="Sim.Match.CombatantStats"/> that a
/// <see cref="StatModifier"/> can target. One value per modifiable field; the
/// <see cref="Sim.Match.CombatantStats.Element"/> enum is intentionally absent because it is
/// assigned (carried through from the base), not stacked.
/// </summary>
/// <remarks>
/// Values are contiguous from zero so <see cref="StatAssembler"/> can index per-channel
/// accumulators without reflection. <see cref="StatAssembler.ChannelCount"/> must equal the
/// number of values here; a guard test pins the two together.
/// </remarks>
public enum StatKind
{
    /// <summary>Maximum hit points (<see cref="Sim.Match.CombatantStats.MaxHp"/>).</summary>
    MaxHp = 0,

    /// <summary>Attacker-side damage-core multiplier (<see cref="Sim.Match.CombatantStats.DamageModifier"/>).</summary>
    DamageModifier = 1,

    /// <summary>Defender-side value feeding defence reduction (<see cref="Sim.Match.CombatantStats.Defense"/>).</summary>
    Defense = 2,

    /// <summary>Attacker-side core scaling stat (<see cref="Sim.Match.CombatantStats.Attack"/>).</summary>
    Attack = 3,

    /// <summary>Attacker-side armor penetration (<see cref="Sim.Match.CombatantStats.SunderArmor"/>).</summary>
    SunderArmor = 4,

    /// <summary>Defender-side armor feeding guard reduction (<see cref="Sim.Match.CombatantStats.BaseGuard"/>).</summary>
    BaseGuard = 5,

    /// <summary>Attacker-side crit probability, clamped to [0, 1] (<see cref="Sim.Match.CombatantStats.CritChance"/>).</summary>
    CritChance = 6,

    /// <summary>Attacker-side crit damage multiplier (<see cref="Sim.Match.CombatantStats.CritMultiplier"/>).</summary>
    CritMultiplier = 7,

    /// <summary>Defender-side dodge probability, clamped to [0, 1] (<see cref="Sim.Match.CombatantStats.Dodge"/>).</summary>
    Dodge = 8,

    /// <summary>Attacker-side elemental power (<see cref="Sim.Match.CombatantStats.ElementPower"/>).</summary>
    ElementPower = 9,

    /// <summary>Defender-side elemental resistance (<see cref="Sim.Match.CombatantStats.ElementResist"/>).</summary>
    ElementResist = 10,
}
