namespace Sim.Stats;

/// <summary>
/// How a <see cref="StatModifier"/> combines into its stat channel. The fixed calculation
/// order is <see cref="Flat"/> → <see cref="AdditivePercent"/> → <see cref="MultiplicativePercent"/>
/// (see <see cref="StatAssembler"/>).
/// </summary>
public enum ModifierOp
{
    /// <summary>Added to the channel base before any percentage applies (e.g. <c>+20</c> Attack).</summary>
    Flat,

    /// <summary>Summed with other additive percents into one factor (<c>0.10</c> = +10%).</summary>
    AdditivePercent,

    /// <summary>Compounded multiplicatively, each as its own factor (<c>0.10</c> = ×1.10).</summary>
    MultiplicativePercent,
}
