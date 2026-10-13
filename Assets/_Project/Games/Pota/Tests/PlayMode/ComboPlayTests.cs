using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// The combo in the Specials scene as saved (Pota spec 2026-10-13 §3; Unity Task 7, rulings R-9, R-10): every combo scenario taps,
    /// merges, fires once and settles under the Court look in both choreographies and both pass looks — a pass combo's flights all leave
    /// and all land (three fanned for Pas+Pas, the carried block hanging under the ball for a carry), nothing waits on a landing that never
    /// comes; B's hit-stop comes before its slow motion, A has none; the merge lasts its MergeTime before the plan; the lean moves the
    /// resting pairs toward each other only when it is on. Any logged error or exception fails these tests.
    /// </summary>
    public class ComboPlayTests
    {
        private static readonly SpecialScenario[] Combos =
        {
            SpecialScenario.ComboCross, SpecialScenario.ComboThickCross, SpecialScenario.ComboBigBomb,
            SpecialScenario.ComboPassLine, SpecialScenario.ComboPassBomb, SpecialScenario.ComboPassPass,
        };

        private static IEnumerator LoadCourt(Action<SpecialsDemo, CourtSpecials> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished && !demo.Game.Busy, 20f);   // the start-up scenario
            demo.SetLook(SpecialLook.Court);
            var court = demo.Game.Specials as CourtSpecials;
            Assert.IsNotNull(court, "the Court look fell back to Pota: run ai_studio_import");
            Assert.IsTrue(court.ComboReady, "the combo's letterings, toon ring and chunks are wired (ai_studio_import)");
            ready(demo, court);
        }

        private static int Flights(SpecialScenario s)
        {
            var r = ColorHuntRules.Break(ColorHuntRules.FromPreset(SpecialScenarios.Board(s)), SpecialScenarios.Taps(s)[0]);
            return r.Steps[0].Passes.Count;
        }

        [UnityTest]
        public IEnumerator EveryCombo_MergesFiresOnceFliesAndSettles([Values] ComboLook look, [Values] PassLook pass)
        {
            SpecialsDemo demo = null;
            CourtSpecials court = null;
            yield return LoadCourt((d, c) => { demo = d; court = c; });
            var game = demo.Game;
            demo.SetComboLook(look);
            demo.SetPassLook(pass);
            int merges = 0, combos = 0, hits = 0;
            void Merged(ComboKind k) => merges++;
            void Fired(ComboKind k) => combos++;
            void Hit(int cell, int target) => hits++;
            game.ComboMerging += Merged;
            game.ComboFired += Fired;
            game.PassLanded += Hit;
            try
            {
                foreach (var s in Combos)
                {
                    merges = combos = hits = 0;
                    int flights = Flights(s), most = 0, carrying = 0, chunks = 0, rings = 0, letters = 0;
                    float spread = -1f;
                    bool done = false;
                    void Done() => done = true;
                    demo.ScenarioFinished += Done;
                    demo.Play(s, true);
                    float t = 0f;
                    while (!done && t < 20f)
                    {
                        int now = game.PassFlights.InFlight;
                        if (now == 3 && spread < 0f)
                        {
                            float lo = float.MaxValue, hi = float.MinValue;
                            foreach (var f in game.PassFlights.Flights) { lo = Mathf.Min(lo, f.pos.x); hi = Mathf.Max(hi, f.pos.x); }
                            spread = hi - lo;
                        }
                        foreach (var f in game.PassFlights.Flights)
                        {
                            if (f.carried == null) continue;
                            carrying = Mathf.Max(carrying, 1);
                            Assert.AreEqual(f.pos.y - game.S.Combo.CarryDown, f.carried.Pos.y, 1e-4f, $"{s}: the carried block hangs under the ball");
                            Assert.AreEqual(f.pos.x, f.carried.Pos.x, 1e-4f, $"{s}: right under it");
                            Assert.AreEqual(f.lift, f.carried.Lift, 1e-4f, $"{s}: at the ball's height");
                            Assert.AreEqual(game.S.Combo.CarryScale, f.carried.Scale, 1e-4f, $"{s}: shrunk");
                            Assert.IsTrue(f.carried.gameObject.activeSelf, $"{s}: visible");
                        }
                        most = Mathf.Max(most, now);
                        chunks = Mathf.Max(chunks, court.ChunksLive);
                        rings = Mathf.Max(rings, court.ToonRingsLive);
                        letters = Mathf.Max(letters, court.LettersLive);
                        Assert.LessOrEqual(game.Specials.LiveCount, CourtSpecials.Cap, $"{s}: the Court look's own pieces");
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    demo.ScenarioFinished -= Done;
                    string what = $"{look}/{pass}/{s}";
                    Assert.IsTrue(done, what + " never finished");
                    Assert.AreEqual(1, merges, what + ": one merge");
                    Assert.AreEqual(1, combos, what + ": one combo moment");
                    Assert.AreEqual(flights, hits, what + ": every flight landed (one hit each)");
                    Assert.AreEqual(flights, most, what + ": every flight of the centre left at once");
                    if (s == SpecialScenario.ComboPassPass) Assert.AreEqual(2f * game.S.Combo.PassFan, spread, 0.02f, what + ": the three passes fan out");
                    bool carry = s == SpecialScenario.ComboPassLine || s == SpecialScenario.ComboPassBomb;
                    Assert.AreEqual(carry ? 1 : 0, carrying, what + ": the carried special hangs under the ball");
                    Assert.Greater(chunks, 0, what + ": chunks flew");
                    Assert.Greater(rings, 0, what + ": the toon ring");
                    Assert.Greater(letters, 0, what + ": the combo's word");
                    Assert.IsFalse(game.Busy, what + ": busy");
                    Assert.AreEqual(0, game.PassFlights.InFlight, what + ": a flight left in the air");
                    for (int i = 0; i < game.Board.Cells.Length; i++)
                    {
                        var v = game.ViewAt(i);
                        Assert.IsNotNull(v, $"{what}: no view at {i}");
                        Assert.IsTrue(v.gameObject.activeSelf, $"{what}: view {i} hidden");
                        Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
                        Assert.AreEqual(0f, v.Lift, $"{what}: view {i} still lifted");
                        Assert.AreEqual(1f, v.Scale, 1e-4f, $"{what}: view {i} still grown");
                    }
                    yield return StudioLookPlayTests.WaitUntil(() => game.Specials.LiveCount == 0, 4f);
                    Assert.AreEqual(0, game.Specials.LiveCount, what + ": a Court effect never ended");
                }
            }
            finally
            {
                game.ComboMerging -= Merged;
                game.ComboFired -= Fired;
                game.PassLanded -= Hit;
            }
        }

        /// <summary>R-10: B dips (×HitStopTapScale) for its hit-stop, THEN runs ×0.5 for its slow motion — never both at once; A never slows.</summary>
        [UnityTest]
        public IEnumerator HitStopThenSlowMotion_InB_NeverInA([Values] ComboLook look)
        {
            SpecialsDemo demo = null;
            yield return LoadCourt((d, c) => demo = d);
            var game = demo.Game;
            demo.SetComboLook(look);
            var tune = look == ComboLook.B ? game.S.ComboLookB : game.S.ComboLookA;
            float mergeAt = -1f, firedAt = -1f;
            void Merged(ComboKind k) => mergeAt = Time.unscaledTime;
            void Fired(ComboKind k) => firedAt = Time.unscaledTime;
            game.ComboMerging += Merged;
            game.ComboFired += Fired;
            var samples = new List<(float t, float scale)>();
            var mergeFrames = new List<float>();                                 // the game's clamped dt of every frame of the merge
            try
            {
                demo.Play(SpecialScenario.ComboCross, true);
                float t = 0f;
                int phase = 0;                                                    // 0 before the merge, 1 merging, 2 fired
                while (!demo.Finished && t < 10f)
                {
                    if (phase == 0 && mergeAt >= 0f) phase = 1;
                    if (phase == 1)
                    {
                        mergeFrames.Add(Mathf.Min(Time.deltaTime, 0.05f));        // the dt the game took this frame (it clamps at 0.05)
                        if (firedAt >= 0f) phase = 2;
                    }
                    if (firedAt >= 0f) samples.Add((Time.unscaledTime - firedAt, Time.timeScale));
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                game.ComboMerging -= Merged;
                game.ComboFired -= Fired;
            }
            Assert.GreaterOrEqual(mergeAt, 0f, "merged");
            Assert.GreaterOrEqual(firedAt, 0f, "fired");
            // The merge (game time, outside the plan) runs from its frame through the frame it ends in, when the combo fires.
            float merged = 0f;
            foreach (float dt in mergeFrames) merged += dt;
            Assert.GreaterOrEqual(merged, tune.MergeTime - 1e-4f, "the combo fired before its merge ended");
            Assert.Less(merged - mergeFrames[mergeFrames.Count - 1], tune.MergeTime, "the combo waited a frame past its merge");
            float slow = game.S.ComboLookB.SlowScale;
            int dips = 0, slows = 0;
            foreach (var (at, scale) in samples)
            {
                bool isSlow = Mathf.Abs(scale - slow) < 0.01f;
                if (Mathf.Abs(scale - game.S.HitStopTapScale) < 0.01f) dips++;
                if (look == ComboLook.A) { Assert.IsFalse(isSlow, $"A slowed at {at:0.000} s"); continue; }
                if (at < tune.HitStop - 0.02f) Assert.IsFalse(isSlow, $"B slowed during its hit-stop ({at:0.000} s)");
                if (isSlow)
                {
                    slows++;
                    Assert.Less(at, tune.HitStop + tune.SlowTime + 0.05f, $"B slow at {at:0.000} s");
                }
            }
            Assert.Greater(dips, 0, "the hit-stop dipped");
            if (look == ComboLook.B) Assert.Greater(slows, 0, "B's slow motion ran after the hit-stop");
        }

        [UnityTest]
        public IEnumerator Lean_MovesThePairsTowardEachOther_OnlyWhenOn([Values] bool on)
        {
            SpecialsDemo demo = null;
            yield return LoadCourt((d, c) => demo = d);
            var game = demo.Game;
            demo.SetComboLean(on);
            demo.Play(SpecialScenario.ComboLean, true);
            yield return null;
            float right = 0f, up = 0f, away = 0f;
            float t = 0f;
            while (t < 1.2f)
            {
                var a = game.ViewAt(8);                                       // LineH 8 leans right, toward 9
                var b = game.ViewAt(32);                                      // bomb 32 leans up, toward 25
                right = Mathf.Max(right, a.Pos.x - a.Home.x);
                up = Mathf.Max(up, b.Pos.y - b.Home.y);
                away = Mathf.Max(away, Mathf.Max(a.Home.x - a.Pos.x, b.Home.y - b.Pos.y));
                for (int i = 0; i < game.Board.Cells.Length; i++)
                    if (i != 8 && i != 9 && i != 25 && i != 32 && i != 36 && i != 37)
                        Assert.AreEqual(game.ViewAt(i).Home, game.ViewAt(i).Pos, $"cell {i} has no partner: it never leans");
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(0f, away, 1e-5f, "never away from the partner");
            if (on)
            {
                Assert.AreEqual(game.S.Combo.LeanAmount, right, 0.012f, "the line leans LeanAmount toward its partner");
                Assert.AreEqual(game.S.Combo.LeanAmount, up, 0.012f, "the lower bomb leans up toward the upper");
            }
            else
            {
                Assert.AreEqual(0f, right, 1e-5f, "no lean");
                Assert.AreEqual(0f, up, 1e-5f, "no lean");
            }
        }
    }
}
