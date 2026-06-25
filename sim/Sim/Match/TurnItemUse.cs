using Sim.Content;

namespace Sim.Match;

/// <summary>
/// Record of an item use attempted on a turn, logged on <see cref="TurnEvent.ItemUse"/>
/// for replay and UI. Present only when the actor submitted an item; a fire-only turn
/// logs <see langword="null"/>.
/// </summary>
/// <remarks>
/// Server-authoritative: the client displays this record, it never produces it. When
/// <see cref="Applied"/> is <see langword="false"/> the use was rejected (item unknown,
/// not held, or its effect unresolvable), the inventory is unchanged, and the turn still
/// fired as a fire-only shot.
/// </remarks>
/// <param name="ItemId">The item the actor attempted to use.</param>
/// <param name="Kind">
/// The effect kind resolved from the item definition, or <see langword="null"/> when the
/// item id was not in the catalog (and so no kind could be determined).
/// </param>
/// <param name="Applied">
/// <see langword="true"/> when the item was consumed and its effect applied; otherwise the
/// use was rejected and had no effect.
/// </param>
/// <param name="HpRestored">
/// For an applied <see cref="ItemEffectKind.RestoreHp"/>: the HP actually restored after
/// clamping to max (no overheal). Zero otherwise.
/// </param>
/// <param name="GrantedBallId">
/// For an applied <see cref="ItemEffectKind.GrantBall"/>: the id of the shell that drove
/// this turn's shot. <see langword="null"/> otherwise.
/// </param>
public readonly record struct TurnItemUse(
    string          ItemId,
    ItemEffectKind? Kind,
    bool            Applied,
    double          HpRestored,
    string?         GrantedBallId);
