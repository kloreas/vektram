using System;
using System.Collections.Generic;
using Sim.Content;
using Sim.Core;
using Sim.Items;
using Sim.Projectile;
using Sim.Terrain;

namespace Sim.Match;

/// <summary>
/// Stateful, agent-agnostic match driver.
/// Advances the match exactly one turn per <see cref="ResolveTurn"/> call; the caller
/// supplies each <see cref="FireAction"/>, so any source — human input, AI, replay —
/// can drive the match without this class needing to know the source.
/// </summary>
/// <remarks>
/// For fully-automated (agent-driven) matches use <see cref="MatchSimulator"/> instead;
/// its <c>Run</c> method is expressed as a thin loop over this controller.
/// </remarks>
public sealed class MatchController
{
    private readonly IProjectileSimulator _projectileSim;
    private readonly MatchOptions         _options;
    private readonly ITerrainQuery        _terrain;
    private readonly WorldEnvironment     _environment;
    private readonly uint                 _seed;
    private readonly CombatRules          _rules;
    private readonly SimRandom            _rng;

    private readonly int[]            _teamIds;
    private readonly Combatant[]      _combatants;   // live; only Hp changes between turns
    private readonly Inventory[]      _inventories;  // live; per-combatant, server-authoritative
    private readonly ItemCatalog?     _itemCatalog;
    private readonly BallCatalog?     _ballCatalog;
    private readonly List<int>        _living;
    private readonly ITurnOrderPolicy _policy;
    private readonly List<TurnEvent>  _log;

    private bool        _isOver;
    private MatchResult _result;
    private int         _currentActorIndex;
    private int         _turnNumber;

    /// <summary>
    /// Creates a controller ready for the first turn.
    /// <see cref="CurrentActorIndex"/> and <see cref="CurrentState"/> are valid immediately.
    /// </summary>
    /// <param name="projectileSim">Physics backend; the same instance is used for every shot.</param>
    /// <param name="combatants">Initial combatant state indexed by roster position.</param>
    /// <param name="teamIds">Team membership for each roster slot. Must be parallel to <paramref name="combatants"/>.</param>
    /// <param name="options">Friendly-fire and self-damage rules.</param>
    /// <param name="terrain">Ground surface used for projectile collision.</param>
    /// <param name="environment">Gravity and wind for the match.</param>
    /// <param name="seed">
    /// Forwarded as-is to every <see cref="FireCommand"/> and used to seed the crit/miss RNG.
    /// </param>
    /// <param name="rules">
    /// Damage tuning, room/mode multiplier, and element table. When <see langword="null"/>,
    /// <see cref="CombatRules.Default"/> is used (engine fallback, no mode/element adjustment).
    /// </param>
    /// <param name="inventories">
    /// Starting per-combatant inventory, parallel to <paramref name="combatants"/>. When
    /// <see langword="null"/>, every combatant starts with <see cref="Inventory.Empty"/>.
    /// Mutated server-side as items are consumed; read back via <see cref="InventoryOf"/>.
    /// </param>
    /// <param name="itemCatalog">
    /// Item definitions used to resolve a <see cref="TurnAction.ItemId"/>. When
    /// <see langword="null"/>, any item use is rejected cleanly (the turn fires fire-only).
    /// </param>
    /// <param name="ballCatalog">
    /// Shell definitions used to resolve a <see cref="ItemEffectKind.GrantBall"/> effect. When
    /// <see langword="null"/>, a GrantBall use is rejected cleanly.
    /// </param>
    public MatchController(
        IProjectileSimulator      projectileSim,
        IReadOnlyList<Combatant>  combatants,
        IReadOnlyList<int>        teamIds,
        MatchOptions              options,
        ITerrainQuery             terrain,
        WorldEnvironment          environment,
        uint                      seed,
        CombatRules?              rules = null,
        IReadOnlyList<Inventory>? inventories = null,
        ItemCatalog?              itemCatalog = null,
        BallCatalog?              ballCatalog = null)
    {
        int n = combatants.Count;

        _projectileSim = projectileSim;
        _options       = options;
        _terrain       = terrain;
        _environment   = environment;
        _seed          = seed;
        _rules         = rules ?? CombatRules.Default;
        _rng           = new SimRandom(seed);
        _itemCatalog   = itemCatalog;
        _ballCatalog   = ballCatalog;

        _teamIds = new int[n];
        for (int i = 0; i < n; i++) _teamIds[i] = teamIds[i];

        _combatants = new Combatant[n];
        for (int i = 0; i < n; i++) _combatants[i] = combatants[i];

        _inventories = new Inventory[n];
        for (int i = 0; i < n; i++)
            _inventories[i] = inventories is not null ? inventories[i] : Inventory.Empty;

        _living = new List<int>(n);
        for (int i = 0; i < n; i++) _living.Add(i);

        _policy = new RoundRobinTurnOrderPolicy(_teamIds);
        _log    = new List<TurnEvent>(SimConstants.MaxTurnsPerMatch);

        _turnNumber        = 0;
        _isOver            = false;
        _currentActorIndex = _policy.NextActor(_living);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary><see langword="true"/> once the match has reached a terminal state.</summary>
    public bool IsOver => _isOver;

    /// <summary>
    /// Roster index of the combatant whose turn it is.
    /// Valid until <see cref="IsOver"/> becomes <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the match is over.</exception>
    public int CurrentActorIndex
        => !_isOver
            ? _currentActorIndex
            : throw new InvalidOperationException(
                "Match is over; CurrentActorIndex is no longer valid. Check IsOver first.");

    /// <summary>
    /// World snapshot from the perspective of the acting combatant, ready to pass to an
    /// <see cref="IAgent"/> or display to a human player.
    /// Valid until <see cref="IsOver"/> becomes <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the match is over.</exception>
    public MatchState CurrentState
    {
        get
        {
            if (_isOver)
                throw new InvalidOperationException(
                    "Match is over; CurrentState is no longer valid. Check IsOver first.");

            int actorTeam = _teamIds[_currentActorIndex];
            var allies    = BuildTeammates(_currentActorIndex, actorTeam);
            var enemies   = BuildEnemies(actorTeam);
            return new MatchState(
                _combatants[_currentActorIndex], allies, enemies, _terrain, _environment, _turnNumber);
        }
    }

    /// <summary>
    /// Final outcome of the match.
    /// Valid only once <see cref="IsOver"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the match is not yet over.</exception>
    public MatchResult Result
        => _isOver
            ? _result
            : throw new InvalidOperationException(
                "Match is not yet over; Result is only valid after IsOver is true.");

    /// <summary>
    /// The live inventory for a roster slot (server-authoritative). Reflects all item use
    /// resolved so far.
    /// </summary>
    /// <param name="combatantIndex">Roster index, parallel to the combatants passed at construction.</param>
    public Inventory InventoryOf(int combatantIndex) => _inventories[combatantIndex];

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a fire-only turn for the current actor. Equivalent to
    /// <see cref="ResolveTurn(TurnAction)"/> with no item.
    /// </summary>
    /// <param name="action">The shot the acting combatant fires this turn.</param>
    /// <returns>The full record of this turn.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsOver"/> is already true.</exception>
    public TurnEvent ResolveTurn(FireAction action) => ResolveTurn(new TurnAction(action, null));

    /// <summary>
    /// Applies <paramref name="action"/> for the current actor: optionally uses an item first
    /// (heal or shell swap), simulates the projectile, computes blast damage for all
    /// combatants, updates their HP, removes any newly-defeated combatants from the turn order,
    /// and evaluates the win condition.
    /// </summary>
    /// <remarks>
    /// An item, when present, is resolved and applied <em>before</em> the shot: a
    /// <see cref="ItemEffectKind.RestoreHp"/> raises the actor's HP (so a same-turn
    /// self-blast subtracts from the post-heal total), and a
    /// <see cref="ItemEffectKind.GrantBall"/> swaps the shell so this turn's trajectory
    /// <em>and</em> damage both come from the granted ball. An unavailable item is rejected
    /// cleanly: no effect, inventory unchanged, the turn fires fire-only, and the rejection is
    /// logged on <see cref="TurnEvent.ItemUse"/>.
    /// </remarks>
    /// <param name="action">The item (optional) and shot for this turn.</param>
    /// <returns>The full record of this turn.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsOver"/> is already true.</exception>
    public TurnEvent ResolveTurn(TurnAction action)
    {
        if (_isOver)
            throw new InvalidOperationException(
                "ResolveTurn called after the match is over. Check IsOver before calling ResolveTurn.");

        int actorIdx  = _currentActorIndex;
        int actorTeam = _teamIds[actorIdx];
        int n         = _combatants.Length;

        FireAction fire = action.Fire;

        // Item use is resolved before the shot. The no-item path uses ShellPhysics.Neutral and
        // the weapon's own damage/blast, so it is bit-identical to the pre-item simulator path.
        ShellPhysics shotPhysics     = ShellPhysics.Neutral;
        double       shotBaseDamage  = fire.Weapon.BaseDamage;
        double       shotBlastRadius = fire.Weapon.BlastRadius;
        TurnItemUse? itemUse         = null;

        if (action.ItemId is not null)
            itemUse = ApplyItem(action.ItemId, actorIdx, ref shotPhysics, ref shotBaseDamage, ref shotBlastRadius);

        var cmd        = new FireCommand(_combatants[actorIdx].Position, fire.AngleDegrees, fire.Speed, _seed);
        var shot       = _projectileSim.Simulate(cmd, _environment, _terrain, shotPhysics);
        var actorStats = _combatants[actorIdx].Stats;
        var results    = new CombatantTurnResult[n];

        for (int i = 0; i < n; i++)
        {
            bool isActor = i == actorIdx;
            bool isAlly  = _teamIds[i] == actorTeam;

            // Self and ally checks are independent levers (see MatchOptions).
            bool apply = isActor
                ? _options.SelfDamage
                : (!isAlly || _options.FriendlyFire);

            double hpBefore = _combatants[i].Hp;
            double damage   = 0.0;
            bool   isCrit   = false;
            bool   isMiss   = false;

            if (apply)
            {
                CombatantStats defenderStats = _combatants[i].Stats;

                // Rolls are consumed only when the relevant chance is non-zero, so neutral-stat
                // matches draw no RNG and stay bit-for-bit deterministic across formula versions.
                isMiss = defenderStats.Dodge > 0.0 && _rng.NextDouble() < defenderStats.Dodge;
                isCrit = !isMiss && actorStats.CritChance > 0.0 && _rng.NextDouble() < actorStats.CritChance;

                double advantage = _rules.ElementAdvantage(actorStats.Element, defenderStats.Element);
                var inputs = new DamageInputs(
                    shot.ImpactPoint, _combatants[i].Position,
                    shotBaseDamage, shotBlastRadius,
                    actorStats, defenderStats,
                    _rules.Tuning, _rules.ModeMultiplier, advantage,
                    isCrit, isMiss);

                DamageResult result = DamageCalculator.Compute(inputs);
                damage = result.FinalDamage;
                isCrit = result.IsCrit;
                isMiss = result.IsMiss;
            }

            _combatants[i] = _combatants[i] with { Hp = hpBefore - damage };
            results[i]     = new CombatantTurnResult(damage, hpBefore, _combatants[i].Hp)
            {
                IsCrit = isCrit,
                IsMiss = isMiss,
            };
        }

        var turnEvent = new TurnEvent(_turnNumber, actorIdx, fire, shot.ImpactPoint, results)
        {
            ItemUse = itemUse,
        };
        _log.Add(turnEvent);

        // Remove newly-defeated combatants from the turn-order pool.
        for (int k = _living.Count - 1; k >= 0; k--)
            if (_combatants[_living[k]].IsDefeated)
                _living.RemoveAt(k);

        _turnNumber++;

        // Evaluate win condition after updating the living list.
        var aliveTeams = new HashSet<int>();
        foreach (int i in _living) aliveTeams.Add(_teamIds[i]);

        if (aliveTeams.Count == 0)
        {
            FinishMatch(MatchOutcome.Draw, null);
        }
        else if (aliveTeams.Count == 1)
        {
            int winnerTeam = -1;
            foreach (int t in aliveTeams) winnerTeam = t;
            FinishMatch(winnerTeam == 0 ? MatchOutcome.Team0Wins : MatchOutcome.Team1Wins, winnerTeam);
        }
        else if (_turnNumber >= SimConstants.MaxTurnsPerMatch)
        {
            FinishMatch(MatchOutcome.MaxTurnsReached, null);
        }
        else
        {
            _currentActorIndex = _policy.NextActor(_living);
        }

        return turnEvent;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves and applies one item for the actor, mutating inventory (and HP for a heal) and
    /// reporting the outcome. On any rejection the inventory and HP are left unchanged and the
    /// shot parameters (<paramref name="physics"/>/<paramref name="baseDamage"/>/<paramref name="blastRadius"/>)
    /// are untouched, so the turn proceeds fire-only.
    /// </summary>
    private TurnItemUse ApplyItem(
        string itemId, int actorIdx,
        ref ShellPhysics physics, ref double baseDamage, ref double blastRadius)
    {
        // No catalog, or unknown id: nothing to resolve — reject (kind unknown).
        if (_itemCatalog is null || !_itemCatalog.TryGet(itemId, out ItemDefinition def))
            return new TurnItemUse(itemId, null, Applied: false, HpRestored: 0.0, GrantedBallId: null);

        // Not held / insufficient: Consume reports failure and leaves the inventory unchanged.
        ItemUseOutcome consumed = _inventories[actorIdx].Consume(itemId);
        if (!consumed.Success)
            return new TurnItemUse(itemId, def.Effect.Kind, Applied: false, HpRestored: 0.0, GrantedBallId: null);

        switch (def.Effect.Kind)
        {
            case ItemEffectKind.RestoreHp:
            {
                double before = _combatants[actorIdx].Hp;
                double after  = ItemEffects.ResolveRestoredHp(before, _combatants[actorIdx].Stats.MaxHp, def.Effect.Amount);

                _inventories[actorIdx] = consumed.Inventory;
                _combatants[actorIdx]  = _combatants[actorIdx] with { Hp = after };
                return new TurnItemUse(itemId, ItemEffectKind.RestoreHp, Applied: true, after - before, GrantedBallId: null);
            }

            case ItemEffectKind.GrantBall:
            {
                // A dangling ball ref is a content error (the load-time guard is
                // ItemCatalog.ValidateBallReferences); reject cleanly rather than throw mid-match.
                if (_ballCatalog is null ||
                    def.Effect.BallId is null ||
                    !_ballCatalog.TryGet(def.Effect.BallId, out BallDefinition ball))
                    return new TurnItemUse(itemId, ItemEffectKind.GrantBall, Applied: false, HpRestored: 0.0, GrantedBallId: null);

                _inventories[actorIdx] = consumed.Inventory;
                physics     = ball.Physics;
                baseDamage  = ball.BaseDamage;
                blastRadius = ball.BlastRadius;
                return new TurnItemUse(itemId, ItemEffectKind.GrantBall, Applied: true, HpRestored: 0.0, ball.Id);
            }

            default:
                return new TurnItemUse(itemId, def.Effect.Kind, Applied: false, HpRestored: 0.0, GrantedBallId: null);
        }
    }

    private void FinishMatch(MatchOutcome outcome, int? winningTeamId)
    {
        _isOver = true;
        _result = new MatchResult(outcome, winningTeamId, _turnNumber, _log);
    }

    private IReadOnlyList<Combatant> BuildTeammates(int actorIdx, int actorTeam)
    {
        var list = new List<Combatant>();
        foreach (int i in _living)
            if (i != actorIdx && _teamIds[i] == actorTeam)
                list.Add(_combatants[i]);
        return list;
    }

    private IReadOnlyList<Combatant> BuildEnemies(int actorTeam)
    {
        var list = new List<Combatant>();
        foreach (int i in _living)
            if (_teamIds[i] != actorTeam)
                list.Add(_combatants[i]);
        return list;
    }
}
