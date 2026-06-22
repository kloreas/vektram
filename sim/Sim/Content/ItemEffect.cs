namespace Sim.Content;

/// <summary>
/// The data-defined effect of using an item. The active fields depend on
/// <see cref="Kind"/>: <see cref="ItemEffectKind.GrantBall"/> uses <see cref="BallId"/>;
/// <see cref="ItemEffectKind.RestoreHp"/> uses <see cref="Amount"/>.
/// </summary>
/// <remarks>
/// Effects are pure data — there is no item logic in C#. The server resolves an effect
/// through the seams (a ball lookup, an HP clamp) and applies the outcome; the client
/// only displays.
/// </remarks>
/// <param name="Kind">Which effect this is.</param>
/// <param name="BallId">For <see cref="ItemEffectKind.GrantBall"/>: the shell id granted. Otherwise <see langword="null"/>.</param>
/// <param name="Amount">For <see cref="ItemEffectKind.RestoreHp"/>: hit points restored. Otherwise 0.</param>
public readonly record struct ItemEffect(
    ItemEffectKind Kind,
    string?        BallId,
    double         Amount);
