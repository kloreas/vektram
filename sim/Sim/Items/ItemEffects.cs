using System;
using Sim.Content;

namespace Sim.Items;

/// <summary>
/// Pure resolution seams that turn a data-defined <see cref="ItemEffect"/> into a concrete
/// outcome the server applies. These stay free of any <c>Sim.Match</c> dependency:
/// <see cref="ResolveRestoredHp"/> works on plain doubles, and the granted-ball seam returns
/// content data.
/// </summary>
/// <remarks>
/// System #3 stops at resolution: applying the outcome inside a live turn (an item-use action
/// in the match loop) and the equipment / modifier-stack stat assembly belong to system #4.
/// </remarks>
public static class ItemEffects
{
    /// <summary>
    /// Resolves a <see cref="ItemEffectKind.GrantBall"/> effect to its shell definition.
    /// </summary>
    /// <exception cref="ArgumentException">The effect is not a <see cref="ItemEffectKind.GrantBall"/>.</exception>
    /// <exception cref="BallDataException">The referenced ball id is not in the catalog.</exception>
    public static BallDefinition ResolveGrantedBall(BallCatalog ballCatalog, ItemEffect effect)
    {
        if (ballCatalog is null) throw new ArgumentNullException(nameof(ballCatalog));
        if (effect.Kind != ItemEffectKind.GrantBall)
            throw new ArgumentException($"Effect is {effect.Kind}, not GrantBall.", nameof(effect));

        return ballCatalog.Get(effect.BallId!);
    }

    /// <summary>
    /// Resolves a <see cref="ItemEffectKind.RestoreHp"/> heal: returns the new HP after adding
    /// <paramref name="amount"/> to <paramref name="currentHp"/>, clamped to
    /// <paramref name="maxHp"/> (no overheal).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is negative.</exception>
    public static double ResolveRestoredHp(double currentHp, double maxHp, double amount)
    {
        if (amount < 0.0) throw new ArgumentException("Heal amount must be >= 0.", nameof(amount));

        double healed = currentHp + amount;
        return healed > maxHp ? maxHp : healed;
    }
}
