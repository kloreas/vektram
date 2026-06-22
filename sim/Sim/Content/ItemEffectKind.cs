namespace Sim.Content;

/// <summary>
/// Discriminator for an <see cref="ItemEffect"/>. Open for extension; stat/buff and
/// equipment effects belong to the modifier-stack system (#4).
/// </summary>
public enum ItemEffectKind
{
    /// <summary>Makes a shell available to fire; carries a <see cref="ItemEffect.BallId"/>.</summary>
    GrantBall,

    /// <summary>Restores hit points; carries an <see cref="ItemEffect.Amount"/>.</summary>
    RestoreHp,
}
