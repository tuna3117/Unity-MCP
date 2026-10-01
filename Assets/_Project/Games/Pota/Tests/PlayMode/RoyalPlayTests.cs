using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// 1 · Royal in the Specials demo scene as saved (spec 2026-10-01 §2; Review Focus 2: restart mid-rocket). "Without errors" is the
    /// Test Framework's own rule: any Debug.LogError or exception fails the test.
    /// </summary>
    public class RoyalPlayTests
    {
        /// <summary>A real RoyalSpecials behind a counter: every Gather's arrived and every flight's callbacks are recorded.</summary>
        private sealed class CountingRoyal : ISpecialStyle
        {
            public readonly RoyalSpecials Inner;
            /// <summary>Per Gather call: how many times its arrived came.</summary>
            public readonly List<int> Arrivals = new List<int>();
            /// <summary>Per GoldenFlight call: "g" for passedGate, "l" for landed, in call order.</summary>
            public readonly List<string> Flights = new List<string>();
            /// <summary>While on, a gathered block's arrival is kept in <see cref="Held"/> instead of reaching the game (a slow style).</summary>
            public bool HoldArrivals;
            public readonly List<Action> Held = new List<Action>();

            public CountingRoyal(RoyalSpecials inner) { Inner = inner; }

            public bool Ready => Inner.Ready;
            public int LiveCount => Inner.LiveCount;
            public SpecialTiming Timing => Inner.Timing;
            public bool GathersBirthGroup => Inner.GathersBirthGroup;
            public bool ConsumesSpecialView => Inner.ConsumesSpecialView;
            public void Build(ColorHuntGame game) => Inner.Build(game);
            public void Activate(bool on) => Inner.Activate(on);
            public void Clear() => Inner.Clear();
            public void Idle(BlockView v, float dt) => Inner.Idle(v, dt);
            public void Birth(BlockView v, SpecialKind kind) => Inner.Birth(v, kind);
            public void TriggerStep(int specials, Vector2 centre) => Inner.TriggerStep(specials, centre);
            public void Fire(SpecialFire fire) => Inner.Fire(fire);

            public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
            {
                int id = Arrivals.Count;
                Arrivals.Add(0);
                Inner.Gather(v, to, duration, x =>
                {
                    Arrivals[id]++;
                    if (HoldArrivals) Held.Add(() => arrived(x));
                    else arrived(x);
                });
            }

            public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
            {
                int id = Flights.Count;
                Flights.Add("");
                Inner.GoldenFlight(route, () => { Flights[id] += "g"; passedGate(); }, () => { Flights[id] += "l"; landed(); });
            }
        }

        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene (Task 6)");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, demo.Game.Current, "the start scenario was not staged");
            ready(demo);
        }

        /// <summary>Registers a counting Royal before the look is first resolved (the scene starts on Pota) and switches to it.</summary>
        private static CountingRoyal UseCounting(SpecialsDemo demo)
        {
            var game = demo.Game;
            var go = new GameObject("RoyalSpecials");
            go.transform.SetParent(game.transform, false);
            var counting = new CountingRoyal(go.AddComponent<RoyalSpecials>());
            game.UseSpecialStyle(SpecialLook.Royal, counting);
            demo.SetLook(SpecialLook.Royal);
            Assert.AreSame(counting, game.Specials, "Royal is ready (Task 9's ai_studio_import ran) and did not fall back to Pota");
            return counting;
        }

        /// <summary>Plays <paramref name="s"/>'s script to its end; <paramref name="perFrame"/> runs every frame (budget checks, maxima).</summary>
        private static IEnumerator PlayToTheEnd(SpecialsDemo demo, SpecialScenario s, Action perFrame, float timeout = 25f)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            float t = 0f;
            while (!done && t < timeout)
            {
                perFrame?.Invoke();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished under Royal");
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
            }
        }

        private static void AssertBudget(RoyalSpecials royal, ColorHuntGame game, string what)
        {
            Assert.LessOrEqual(royal.LiveCount, RoyalSpecials.Cap, $"{what}: Royal's own particles and pieces");
            Assert.LessOrEqual(royal.LiveCount + game.Fx.LiveCount, BurstBudget.Max, $"{what}: with Pota's burst pools");
        }

        [UnityTest]
        public IEnumerator EveryScenario_PlaysToTheEnd_UnderRoyal_WithinTheBudget()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready (Task 9's ai_studio_import ran) and did not fall back to Pota");
            Assert.AreEqual(RoyalSpecials.TimingOf(game.S.Royal), game.Specials.Timing);
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                int gathers = 0, rockets = 0, copies = 0, pieces = 0, coins = 0, tokens = 0, slowFrames = 0;
                float flash = 0f;
                yield return PlayToTheEnd(demo, s, () =>
                {
                    AssertBudget(royal, game, s.ToString());
                    gathers = Mathf.Max(gathers, royal.GatherCount);
                    rockets = Mathf.Max(rockets, royal.RocketsLive);
                    copies = Mathf.Max(copies, royal.ProxiesLive);
                    pieces = Mathf.Max(pieces, royal.PiecesLive);
                    coins = Mathf.Max(coins, royal.CoinsLive);
                    tokens = Mathf.Max(tokens, royal.TokensLive);
                    flash = Mathf.Max(flash, royal.ScreenFlash);
                    if (Mathf.Abs(Time.timeScale - game.S.Royal.ComboSlowScale) < 0.01f) slowFrames++;   // dips are ×0.06/×0.12, never 0.5
                });
                Assert.IsFalse(game.Busy, s.ToString());
                Assert.AreEqual(0, game.GoldenInFlight, s.ToString());
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{s}: golden balls in the hoop");
                AssertViewsShowTheBoard(game, $"Royal/{s}");
                switch (s)
                {
                    case SpecialScenario.Birth:
                        Assert.Greater(gathers, 0, "Birth: the group travels into the birth cell");
                        break;
                    case SpecialScenario.Line:
                        Assert.GreaterOrEqual(rockets, 2, "Line: the ball splits into two rockets");
                        break;
                    case SpecialScenario.Bomb:
                        Assert.Greater(copies, 0, "Bomb: the wind-up draws the block's copy");
                        Assert.GreaterOrEqual(pieces, 8, "Bomb: eight shell pieces");
                        break;
                    case SpecialScenario.Chain:
                        Assert.Greater(rockets, 0, "Chain: rockets");
                        Assert.Greater(pieces, 0, "Chain: the bomb went");
                        Assert.Greater(slowFrames, 0, "Chain: 2+ specials → ×0.5 slow motion");
                        Assert.Greater(flash, 0f, "Chain: 2+ specials → white screen flash");
                        break;
                    case SpecialScenario.Golden:
                        Assert.Greater(tokens, 0, "Golden: the coin token flew");
                        Assert.GreaterOrEqual(coins, Mathf.Min(8, game.S.Royal.LandCoins), "Golden: coins burst from the rim");
                        break;
                }
            }
        }

        [UnityTest]
        public IEnumerator Restart_MidRocket_LeavesNothing()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready");
            demo.Play(SpecialScenario.Line, true);
            yield return StudioLookPlayTests.WaitUntil(() => royal.RocketsLive > 0 && royal.LiveCount > royal.RocketsLive * RoyalSpecials.PerRocket, 8f);
            Assert.Greater(royal.RocketsLive, 0, "caught mid-rocket");
            Assert.IsTrue(game.Busy, "the trigger step runs");

            demo.Play(SpecialScenario.Line, false);                        // "Tekrar" without auto: a fresh board staged over the rockets
            int bursts = 0;
            void Count(int cell) => bursts++;
            game.CellBurst += Count;
            Assert.AreEqual(0, royal.LiveCount, "Clear dropped every rocket, jet, puff, spark and flash");
            Assert.AreEqual(0, royal.RocketsLive);
            Assert.AreEqual(0f, royal.ScreenFlash);
            Assert.IsFalse(game.Busy, "the lock is released at once");
            Assert.AreEqual(0, game.GoldenInFlight);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);   // the old rockets would have crossed the frame by now
            game.CellBurst -= Count;
            Assert.AreEqual(0, bursts, "no fire of the old plan reached the new board");
            Assert.AreEqual(0, royal.LiveCount, "nothing came back (the Line board's two line balls idle without particles)");
            Assert.AreEqual(SpecialKind.LineH, game.Board.Cells[22].Special);
            Assert.AreEqual(SpecialKind.LineV, game.Board.Cells[19].Special);
            AssertViewsShowTheBoard(game, "after the restart");
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsTrue(v.gameObject.activeSelf, $"cell {i}: a consumed view of the old board must not linger hidden");
                Assert.AreEqual(1f, v.Scale, $"cell {i}: no pop or breath left on a fresh view");
            }

            Assert.AreEqual("broken", game.Tap(22), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
            AssertViewsShowTheBoard(game, "after the replay");
        }

        [UnityTest]
        public IEnumerator Gather_CallsArrivedOncePerGroupBlock()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            var counting = UseCounting(demo);
            // Every exploded group block of every birth in the Birth script (the rules' own answer). A salvo's step 0 is its own block
            // (depth 0, no gathering) and never bears a special.
            int expected = 0;
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Birth));
            foreach (int cell in SpecialScenarios.Taps(SpecialScenario.Birth))
            {
                var r = ColorHuntRules.Break(b, cell);
                Assert.IsEmpty(r.Steps[0].Born, $"tap {cell}: step 0 is the salvo's block");
                for (int k = 1; k < r.Steps.Count; k++)
                {
                    var step = r.Steps[k];
                    var exploded = new HashSet<int>(step.Exploded);
                    var seen = new HashSet<int>();
                    foreach (var born in step.Born)
                        if (born.Group != null)
                            foreach (int g in born.Group)
                                if (exploded.Contains(g) && seen.Add(g)) expected++;
                }
                b = r.Board;
            }
            Assert.Greater(expected, 0, "the Birth script gathers groups");
            yield return PlayToTheEnd(demo, SpecialScenario.Birth, () => AssertBudget(counting.Inner, game, "Birth"));
            Assert.AreEqual(expected, counting.Arrivals.Count, "one Gather per exploded group block");
            CollectionAssert.AreEqual(Enumerable.Repeat(1, counting.Arrivals.Count).ToList(), counting.Arrivals, "arrived exactly once each");
            Assert.AreEqual(0, counting.Inner.GatherCount, "none left travelling");
            AssertViewsShowTheBoard(game, "after the births");
        }

        [UnityTest]
        public IEnumerator Golden_LandsOnce_WithACoinBurst()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            var counting = UseCounting(demo);
            int coins = 0, tokens = 0;
            yield return PlayToTheEnd(demo, SpecialScenario.Golden, () =>
            {
                AssertBudget(counting.Inner, game, "Golden");
                coins = Mathf.Max(coins, counting.Inner.CoinsLive);
                tokens = Mathf.Max(tokens, counting.Inner.TokensLive);
            });
            CollectionAssert.AreEqual(new[] { "gl", "gl" }, counting.Flights, "each ball: through its gate once, then landed once");
            Assert.AreEqual(2, game.GoldenLanded);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(2, tokens, "both balls flew as coins, 0.12 s apart");
            Assert.GreaterOrEqual(coins, Mathf.Min(8, game.S.Royal.LandCoins), "8–12 spinning coins at the rim");
            CollectionAssert.IsEmpty(game.Gates.Shown, "both rings closed as their balls passed");
            yield return StudioLookPlayTests.WaitUntil(() => counting.Inner.CoinsLive == 0, 3f);
            Assert.AreEqual(0, counting.Inner.CoinsLive, "the coins fade out");
            Assert.AreEqual(2, game.GoldenLanded, "nothing landed twice");
        }

        /// <summary>Every CellBurst a tap on <paramref name="cell"/> raises: each step's exploded cells (step 0 of a salvo is the tapped block).</summary>
        private static List<int> ExpectedBursts(Board b, int cell)
        {
            var r = ColorHuntRules.Break(b, cell);
            Assert.IsTrue(r.Broken, $"tap {cell}");
            var all = new List<int>();
            foreach (var step in r.Steps) all.AddRange(step.Exploded);
            return all;
        }

        [UnityTest]
        public IEnumerator LookSwitch_MidGoldenFlight_LandsEachBallOnce()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready");
            demo.Play(SpecialScenario.Golden, false);
            Assert.AreEqual("broken", game.Tap(SpecialScenarios.Taps(SpecialScenario.Golden)[0]));
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0 && royal.TokensLive > 0, 6f);
            Assert.AreEqual(2, game.GoldenInFlight, "both golden balls of the basket step fly as coins");
            demo.SetLook(SpecialLook.Pota);
            Assert.AreEqual(0, game.GoldenInFlight, "landed on the switch");
            Assert.AreEqual(2, game.GoldenLanded, "each counted once");
            CollectionAssert.IsEmpty(game.Gates.Shown, "both rings closed");
            Assert.AreEqual(0, royal.LiveCount, "Royal dropped its coins, glitter and stars");
            Assert.AreEqual(0, royal.TokensLive);
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy, "the lock is released");
            yield return StudioLookPlayTests.WaitUntil(() => false, 1.5f);   // Royal's coins would have reached the rim by now
            Assert.AreEqual(2, game.GoldenLanded, "the old style called nothing back");
            Assert.AreEqual(0, game.GoldenInFlight);
        }

        [UnityTest]
        public IEnumerator LookSwitch_MidGather_BurstsEachGatheredBlockOnce()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready");
            demo.Play(SpecialScenario.Birth, false);
            int tap = SpecialScenarios.Taps(SpecialScenario.Birth)[0];
            var expected = ExpectedBursts(game.Board, tap);
            var bursts = new List<int>();
            game.CellBurst += bursts.Add;
            Assert.AreEqual("broken", game.Tap(tap));
            yield return StudioLookPlayTests.WaitUntil(() => royal.GatherCount > 0, 6f);
            Assert.Greater(royal.GatherCount, 0, "caught while the group travels into its birth cell");
            demo.SetLook(SpecialLook.Pota);
            Assert.AreEqual(0, royal.GatherCount, "the old style dropped its gathers");
            Assert.AreEqual(0, royal.LiveCount);
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy && game.ItemsInFlight == 0, 8f);
            game.CellBurst -= bursts.Add;
            Assert.IsFalse(game.Busy, "no gathered block is left holding the board");
            CollectionAssert.AreEquivalent(expected, bursts, "every exploded block burst once — the gathered ones at the switch");
            AssertViewsShowTheBoard(game, "after the switch");
            foreach (var t in game.Board.Targets)
                Assert.AreEqual(Mathf.Max(0, t.Remaining), game.ShownTarget(t.Color), $"colour {t.Color}: the HUD shows the rules' count");
        }

        [UnityTest]
        public IEnumerator HeldGather_KeepsTheBoardBusy_AndALookSwitchCompletesItOnce()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            var counting = UseCounting(demo);
            counting.HoldArrivals = true;                                    // a style slower than the step: its blocks arrive late
            demo.Play(SpecialScenario.Birth, false);
            int tap = SpecialScenarios.Taps(SpecialScenario.Birth)[0];
            var expected = ExpectedBursts(game.Board, tap);
            var bursts = new List<int>();
            game.CellBurst += bursts.Add;
            Assert.AreEqual("broken", game.Tap(tap));
            yield return StudioLookPlayTests.WaitUntil(() => false, 3f);     // every step of the tap has dropped by now
            Assert.Greater(counting.Held.Count, 0, "the birth groups reached their cells and wait to be let in");
            Assert.AreEqual(0, counting.Inner.GatherCount, "Royal's own travel is over");
            AssertViewsShowTheBoard(game, "the steps played on");
            Assert.IsTrue(game.Busy, "a gathered block still on its way keeps the board busy");
            Assert.AreEqual("locked", game.Tap(0));
            Assert.AreEqual(expected.Count - counting.Held.Count, bursts.Count, "the held blocks have not burst yet");
            demo.SetLook(SpecialLook.Pota);                                  // the switch settles them: counted and burst now
            Assert.IsFalse(game.Busy, "nothing is left on its way");
            foreach (var late in counting.Held) late();                      // the old style's arrivals come in after all
            game.CellBurst -= bursts.Add;
            CollectionAssert.AreEquivalent(expected, bursts, "each gathered block burst exactly once");
            yield return StudioLookPlayTests.WaitUntil(() => game.ItemsInFlight == 0, 3f);
            foreach (var t in game.Board.Targets)
                Assert.AreEqual(Mathf.Max(0, t.Remaining), game.ShownTarget(t.Color), $"colour {t.Color}: counted once");
            AssertViewsShowTheBoard(game, "after the switch");
        }
    }
}
