using System;
using System.Collections;
using UnityEngine;
using Sim.Ai;
using Sim.Core;
using Sim.Match;
using Sim.Projectile;
using Sim.Terrain;

namespace Vektram.Client.Core
{
    /// <summary>
    /// Runs a bot-vs-bot match through the sim core and animates the turn log on screen.
    /// Attach to any empty GameObject and press Play — all visuals are created from code.
    /// No prefabs, no manual scene setup, no interactivity.
    /// </summary>
    public sealed class MatchReplayView : MonoBehaviour
    {
        // ── Sim parameters ────────────────────────────────────────────────────────
        private const uint   MatchSeed        = 42u;
        private const double TeamAX           = 5.0;   // metres, sim space
        private const double TeamBX           = 55.0;
        private const double StartHp          = 100.0;

        // ── Replay pacing ─────────────────────────────────────────────────────────
        // One WaitForSeconds per trajectory tick mirrors the sim's 60 Hz fixed step.
        private const float TrajectoryTickSecs = 1f / 60f;
        private const float PauseAfterShot     = 0.45f;

        // ── Runtime state ─────────────────────────────────────────────────────────
        private Vec2D[]              _rosterPositions;   // roster index → sim-space position
        private double[]             _currentHp;         // roster index → live HP
        private IProjectileSimulator _projectileSim;
        private WorldEnvironment     _matchEnv;
        private MatchResult          _matchResult;

        private SpriteRenderer[]     _combatantViews;    // roster index → disc sprite
        private Transform            _projectileDot;

        private string _statusText = "Initialising…";
        private int    _replayTurn = -1;
        private bool   _done       = false;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            // Pre-fill so OnGUI never reads unset fields
            _rosterPositions = new[] { new Vec2D(TeamAX, 0.0), new Vec2D(TeamBX, 0.0) };
            _currentHp       = new[] { StartHp, StartHp };

            ConfigureCamera();
            BuildScene();
            StartCoroutine(RunAndReplay());
        }

        private void OnGUI()
        {
            // HP labels — world position projected to screen
            var cam = Camera.main;
            if (cam != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var worldPos = new Vector3((float)_rosterPositions[i].X, 3.8f, 0f);
                    var screen   = cam.WorldToScreenPoint(worldPos);
                    // GUI y = 0 is top; WorldToScreen y = 0 is bottom
                    var r   = new Rect(screen.x - 55f, Screen.height - screen.y - 20f, 110f, 38f);
                    string team = i == 0 ? "A  Hard" : "B  Med";
                    double hp   = Math.Max(0.0, _currentHp[i]);
                    GUI.Label(r, $"Team {team}\n{hp:0} HP");
                }
            }

            // Status bar at top-centre
            string header = (!_done && _replayTurn >= 0)
                ? $"Turn {_replayTurn + 1}  —  {_statusText}"
                : _statusText;
            GUI.Label(new Rect(Screen.width * 0.5f - 160f, 6f, 320f, 26f), header);
        }

        // ── Match execution & replay ──────────────────────────────────────────────

        private IEnumerator RunAndReplay()
        {
            _statusText = "Building match…";
            yield return null; // let one frame render before the synchronous sim.Run

            _projectileSim = new ProjectileSimulator();
            var matchSim   = new MatchSimulator(_projectileSim);
            var weapon     = new Weapon(50.0, 80.0, 8.0);
            _matchEnv      = new WorldEnvironment(SimConstants.DefaultGravity, 2.0); // light rightward wind
            var terrain    = FlatTerrain.Ground;
            var opts       = new MatchOptions(FriendlyFire: false, SelfDamage: false);

            var entries = new[]
            {
                new CombatantEntry(
                    new Combatant(new Vec2D(TeamAX, 0.0), StartHp, CombatantStats.Default),
                    0,
                    new BotAgent(_projectileSim, weapon, BotDifficulty.Hard,   MatchSeed)),
                new CombatantEntry(
                    new Combatant(new Vec2D(TeamBX, 0.0), StartHp, CombatantStats.Default),
                    1,
                    new BotAgent(_projectileSim, weapon, BotDifficulty.Medium, MatchSeed + 1u)),
            };

            // Pure synchronous computation — fast enough to run on the main thread
            _matchResult = matchSim.Run(entries, opts, terrain, _matchEnv, MatchSeed);

            _statusText = $"Replaying {_matchResult.TurnCount} turns…";
            yield return new WaitForSeconds(0.3f);

            foreach (var turn in _matchResult.Log)
            {
                _replayTurn = turn.TurnNumber;
                yield return StartCoroutine(AnimateTurn(turn));
                SyncHpFromLog(turn);
                RefreshCombatantAlpha();
                yield return new WaitForSeconds(PauseAfterShot);
            }

            _done       = true;
            _statusText = FormatOutcome(_matchResult);
        }

        private IEnumerator AnimateTurn(TurnEvent turn)
        {
            // Re-simulate the exact shot the core fired this turn.
            // Combatants don't move in this phase, so initial positions are authoritative.
            var origin = _rosterPositions[turn.ActingCombatantIndex];
            var cmd    = new FireCommand(origin, turn.Action.AngleDegrees, turn.Action.Speed, MatchSeed);
            var shot   = _projectileSim.Simulate(cmd, _matchEnv, FlatTerrain.Ground);

            _projectileDot.gameObject.SetActive(true);

            foreach (var tick in shot.Trajectory)
            {
                _projectileDot.position = new Vector3(
                    (float)tick.Position.X,
                    (float)tick.Position.Y,
                    0f);
                yield return new WaitForSeconds(TrajectoryTickSecs);
            }

            _projectileDot.gameObject.SetActive(false);
        }

        private void SyncHpFromLog(TurnEvent turn)
        {
            for (int i = 0; i < turn.CombatantResults.Count && i < _currentHp.Length; i++)
                _currentHp[i] = turn.CombatantResults[i].HpAfter;
        }

        private void RefreshCombatantAlpha()
        {
            for (int i = 0; i < _combatantViews.Length; i++)
            {
                var c = _combatantViews[i].color;
                float a = _currentHp[i] <= 0.0 ? 0.18f : 1f;
                _combatantViews[i].color = new Color(c.r, c.g, c.b, a);
            }
        }

        // ── Camera ────────────────────────────────────────────────────────────────

        private static void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic     = true;
            cam.orthographicSize = 22f;                            // 44-unit vertical span
            cam.transform.position = new Vector3(30f, 10f, -20f); // centres on the battlefield
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.12f);
            cam.clearFlags      = CameraClearFlags.SolidColor;
        }

        // ── Visual helpers ────────────────────────────────────────────────────────

        private void BuildScene()
        {
            // Terrain strip — a scaled SpriteRenderer avoids any shader-compatibility concern
            MakeRect("Terrain",
                pos:   new Vector3(30f,           0f, 0f),
                scale: new Vector3(80f,          0.25f, 1f),
                color: new Color(0.35f, 0.65f, 0.2f),
                order: 0);

            // Combatant discs
            _combatantViews = new[]
            {
                MakeDisc("Combatant_A",
                    new Vector3((float)TeamAX, 0f, 0f),
                    new Color(0.25f, 0.45f, 1f),
                    scale: 2f, order: 5),
                MakeDisc("Combatant_B",
                    new Vector3((float)TeamBX, 0f, 0f),
                    new Color(1f, 0.3f, 0.2f),
                    scale: 2f, order: 5),
            };

            // Projectile dot — starts hidden
            var proj = MakeDisc("Projectile", Vector3.zero, Color.yellow, scale: 0.8f, order: 10);
            proj.gameObject.SetActive(false);
            _projectileDot = proj.transform;
        }

        private static SpriteRenderer MakeRect(string name, Vector3 pos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.position   = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = WhiteSquareSprite();
            sr.color        = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer MakeDisc(string name, Vector3 pos, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CircleSprite(64);
            sr.color        = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Creates a 1×1 white texture and returns a Sprite sized to 1 world unit.</summary>
        private static Sprite WhiteSquareSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Generates a soft-edged circle sprite. PPU = <paramref name="res"/> so the
        /// sprite occupies exactly 1 world unit at localScale = 1; scale up the GO for size.
        /// </summary>
        private static Sprite CircleSprite(int res)
        {
            var tex    = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float half = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float dx = x - half, dy = y - half;
                    float a  = Mathf.Clamp01(half - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, res, res),
                new Vector2(0.5f, 0.5f),
                (float)res);   // PPU = res → 1 world unit at scale 1
        }

        private static string FormatOutcome(MatchResult r) => r.Outcome switch
        {
            MatchOutcome.Team0Wins       => $"TEAM A WINS  ({r.TurnCount} turns)",
            MatchOutcome.Team1Wins       => $"TEAM B WINS  ({r.TurnCount} turns)",
            MatchOutcome.Draw            => $"DRAW  ({r.TurnCount} turns)",
            MatchOutcome.MaxTurnsReached => $"TIME LIMIT — {r.TurnCount} turns, no winner",
            _                            => r.Outcome.ToString(),
        };
    }
}
