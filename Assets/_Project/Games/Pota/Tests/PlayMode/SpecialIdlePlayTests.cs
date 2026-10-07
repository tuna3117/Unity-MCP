using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The calm idles A–E (spec 2026-10-07) in the Specials scene: each runs on the Bekleme board without errors and draws only inside its cell.</summary>
    public class SpecialIdlePlayTests
    {
        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(SpecialIdle.Look, demo.Game.SpecialIdle, "the scene starts with the look's own idle");
            ready(demo);
        }

        private static IEnumerator StageIdleBoard(SpecialsDemo demo, SpecialLook look)
        {
            demo.SetLook(look);
            demo.Play(SpecialScenario.Idle, true);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished && !demo.Game.Busy, 5f);
            Assert.IsTrue(demo.Finished, "Bekleme taps nothing: it finishes at once");
        }

        private struct Seen { public int MostVisible, LeastVisible; public float Breath, Spin, GoldenSpin, GoldenBreath, Bob; }

        /// <summary>Watches the resting specials for <paramref name="seconds"/> of game time; every drawn overlay must stay inside its ball's cell.</summary>
        private static IEnumerator Watch(ColorHuntGame game, float seconds, Action<Seen> done)
        {
            var seen = new Seen { LeastVisible = int.MaxValue };
            float t = 0f;
            var endOfFrame = new WaitForEndOfFrame();                  // after LateUpdate: the overlays sit on the views' final transforms
            while (t < seconds)
            {
                yield return endOfFrame;
                t += Time.deltaTime;
                var fx = game.GetComponentInChildren<SpecialIdleFx>();
                int visible = fx != null ? fx.VisibleCount : 0;
                seen.MostVisible = Mathf.Max(seen.MostVisible, visible);
                seen.LeastVisible = Mathf.Min(seen.LeastVisible, visible);
                foreach (var v in game.Views)
                {
                    if (!v.Block.IsSpecial) continue;
                    bool golden = v.Block.Special == SpecialKind.Golden;
                    float breath = Mathf.Abs(v.IdleScale - 1f), spin = Mathf.Abs(v.StickerSpin);
                    if (golden) { seen.GoldenSpin = Mathf.Max(seen.GoldenSpin, spin); seen.GoldenBreath = Mathf.Max(seen.GoldenBreath, breath); }
                    else seen.Spin = Mathf.Max(seen.Spin, spin);
                    seen.Breath = Mathf.Max(seen.Breath, breath);
                    seen.Bob = Mathf.Max(seen.Bob, Mathf.Abs(v.Bob));
                }
                if (fx != null) AssertInsideTheirCells(game, fx);
            }
            done(seen);
        }

        private static void AssertInsideTheirCells(ColorHuntGame game, SpecialIdleFx fx)
        {
            float cell = game.BoardRoot.lossyScale.x;
            foreach (var r in fx.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                BlockView owner = null;
                float best = float.MaxValue;
                foreach (var v in game.Views)
                {
                    if (!v.Block.IsSpecial) continue;
                    float d = ((Vector2)(r.bounds.center - v.transform.position)).sqrMagnitude;
                    if (d < best) { best = d; owner = v; }
                }
                Assert.IsNotNull(owner, "an overlay without a special");
                var c = owner.transform.position;
                var b = r.bounds;
                float reach = 0.505f * cell;
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(b.min.x - c.x), Mathf.Abs(b.max.x - c.x)), reach, $"{r.sharedMaterial.name} spills sideways out of cell {owner.Cell}");
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(b.min.y - c.y), Mathf.Abs(b.max.y - c.y)), reach, $"{r.sharedMaterial.name} spills up or down out of cell {owner.Cell}");
            }
        }

        [UnityTest]
        public IEnumerator EveryIdle_RunsThreeSeconds_OnTheBeklemeBoard([Values] SpecialIdle idle)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            yield return StageIdleBoard(demo, SpecialLook.Royal);
            demo.SetIdle(idle);
            Assert.AreEqual(idle, game.SpecialIdle);
            yield return null;
            StringAssert.Contains(SpecialsDemo.IdleTitle(idle), demo.LabelText);
            Seen seen = default;
            yield return Watch(game, 3f, s => seen = s);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.IsFalse(game.Busy, "an idle never locks the board");
            switch (idle)
            {
                case SpecialIdle.Look:
                    Assert.IsNull(game.GetComponentInChildren<SpecialIdleFx>(), "Look never builds the calm idles");
                    Assert.AreEqual(0f, seen.Breath, "Look: no calm breath");
                    break;
                case SpecialIdle.Breath:
                    Assert.That(seen.Breath, Is.InRange(0.012f, 0.0201f), "A: ±2 %");
                    Assert.AreEqual(0, seen.MostVisible, "A draws nothing");
                    Assert.AreEqual(0f, seen.Spin);
                    break;
                case SpecialIdle.Glint:
                    Assert.Greater(seen.MostVisible, 0, "B: a sweep within 3 s (the first comes within 2.75 s)");
                    Assert.LessOrEqual(seen.MostVisible, 4, "one streak per ball at most");
                    Assert.AreEqual(0, seen.LeastVisible, "and nothing in between");
                    Assert.AreEqual(0f, seen.Breath);
                    break;
                case SpecialIdle.Rim:
                    Assert.AreEqual(4, seen.MostVisible, "C: a ring on each of the four");
                    Assert.AreEqual(0f, seen.Breath);
                    break;
                case SpecialIdle.Twinkle:
                    Assert.Greater(seen.MostVisible, 0, "D: a star within 3 s (the first comes within 2.05 s)");
                    Assert.LessOrEqual(seen.MostVisible, 4);
                    Assert.AreEqual(0, seen.LeastVisible, "nothing in between");
                    break;
                case SpecialIdle.Sway:
                    Assert.That(seen.Spin, Is.InRange(2f, 5.01f), "E: the sticker rocks ±5°");
                    Assert.LessOrEqual(seen.Breath, 0.0101f, "and breathes 1 %");
                    Assert.AreEqual(0f, seen.GoldenSpin, "the golden ball stays still");
                    Assert.AreEqual(0f, seen.GoldenBreath);
                    Assert.AreEqual(0, seen.MostVisible);
                    break;
            }
            Assert.AreEqual(0f, seen.Bob, "no hop in any of them (Royal does not hop either)");
        }

        [UnityTest]
        public IEnumerator Switching_PutsBackWhatTheOldIdleLeft_UnderEveryLook()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            yield return StageIdleBoard(demo, SpecialLook.Court);
            Seen seen = default;
            yield return Watch(game, 1.2f, s => seen = s);
            Assert.Greater(seen.Bob, 0f, "Court's own idle dribbles under Look");
            GameObject.Find("IdleRim").GetComponent<Button>().onClick.Invoke();     // the demo's third row
            Assert.AreEqual(SpecialIdle.Rim, game.SpecialIdle);
            foreach (var v in game.Views)
                if (v.Block.IsSpecial) { Assert.AreEqual(0f, v.Bob, "the dribble's hop is put back"); Assert.AreEqual(1f, v.Scale); }
            yield return Watch(game, 1f, s => seen = s);
            Assert.AreEqual(0f, seen.Bob, "and stays down under C");
            Assert.AreEqual(4, seen.MostVisible);
            demo.SetLook(SpecialLook.Royal);                                         // a look switch keeps the calm idle
            yield return Watch(game, 0.6f, s => seen = s);
            Assert.AreEqual(4, seen.MostVisible);
            GameObject.Find("IdleSway").GetComponent<Button>().onClick.Invoke();
            yield return Watch(game, 1.2f, s => seen = s);
            Assert.Greater(seen.Spin, 1f);
            GameObject.Find("IdleLook").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialIdle.Look, game.SpecialIdle);
            foreach (var v in game.Views)
                if (v.Block.IsSpecial) { Assert.AreEqual(1f, v.IdleScale, "E's breath is gone"); Assert.AreEqual(0f, v.StickerSpin, "E's rock is gone"); }
            yield return Watch(game, 0.6f, s => seen = s);
            Assert.AreEqual(0, seen.MostVisible, "no calm overlay under Look");
            Assert.AreEqual(0f, seen.Breath, "no calm breath under Look");
            // a scenario played while a calm idle is on: the specials fire and burst as usual, the newborn ones take the idle
            demo.SetIdle(SpecialIdle.Rim);
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(SpecialScenario.Birth, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, 25f);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, "Birth finished under C");
            yield return Watch(game, 0.5f, s => seen = s);
            Assert.AreEqual(4, seen.MostVisible, "the four newborn specials ring");
        }
    }
}
