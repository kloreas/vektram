namespace Sim.Stats;

/// <summary>
/// What produced a <see cref="StatModifier"/>. Tagging the source makes every modifier
/// auditable and removable: dropping all modifiers with a given
/// <see cref="ModifierSource.SourceId"/> and re-assembling reproduces the without-that-source
/// result — the seam for unequipping gear and expiring buffs.
/// </summary>
public enum ModifierSourceType
{
    /// <summary>The combatant's base profile (class/level). Progression (#6) owns its values.</summary>
    Base,

    /// <summary>An equipped piece from the <see cref="Sim.Content.EquipmentCatalog"/>.</summary>
    Equipment,

    /// <summary>A socketed rune. Full rune trees are a later slice; the source type is the seam.</summary>
    Rune,

    /// <summary>A cosmetic. Cosmetics never grant power (ADR-0006): a costume contributes zero modifiers.</summary>
    Costume,

    /// <summary>A temporary buff. Mid-match application/expiry is deferred; the source type is the seam.</summary>
    Buff,
}
