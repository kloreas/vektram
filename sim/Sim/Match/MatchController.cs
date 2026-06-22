using System;
using System.Collections.Generic;
using Sim.Core;
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
    public MatchController(
        IProjectileSimulator     projectileSim,
        IReadOnlyList<Combatant> combatants,
        IReadOnlyList<int>       teamIds,
        MatchOptions             options,
        ITerrainQuery            terrain,
        WorldEnvironment         environment,
        uint                     seed,
        CombatRules?             rules = null)
    {
        int n = combatants.Count;

        _projectileSim = projectileSim;
        _options       = options;
        _terrain       = terrain;
        _environment   = environment;
        _seed          = seed;
        _rules         = rules ?? CombatRules.Default;
        _rng           = new SimRandom(seed);

        _teamIds = new int[n];
        for (int i = 0; i < n; i++) _teamIds[i] = teamIds[i];

        _combatants = new Combatant[n];
        for (int i = 0; i < n; i++) _combatants[i] = combatants[i];

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

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="action"/> for the current actor: simulates the projectile,
    /// computes blast damage for all combatants, updates their HP, removes any newly-defeated
    /// combatants from the turn order, and evaluates the win condition.
    /// </summary>
    /// <param name="action">The shot the acting combatant fires this turn.</param>
    /// <returns>The full record of this turn.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsOver"/> is already true.</exception>
    public TurnEvent ResolveTurn(FireAction action)
    {
        if (_isOver)
            throw new InvalidOperationException(
                "ResolveTurn called after the match is over. Check IsOver before calling ResolveTurn.");

        int actorIdx  = _currentActorIndex;
        int actorTeam = _teamIds[actorIdx];
        int n         = _combatants.Length;

        var cmd        = new FireCommand(_combatants[actorIdx].Position, action.AngleDegrees, action.Speed, _seed);
        var shot       = _projectileSim.Simulate(cmd, _environment, _terrain);
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
                    action.Weapon.BaseDamage, action.Weapon.BlastRadius,
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

        var turnEvent = new TurnEvent(_turnNumber, actorIdx, action, shot.ImpactPoint, results);
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
