using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The studio sticker flight (spec §3.1) in the Color Hunt scene.</summary>
    public class CollectFlightPlayTests
    {
        private static int Rules(ColorHuntGame game, int color)
        {
            foreach (var t in game.Board.Targets) if (t.Color == color) return t.Remaining;
            return 0;
        }

        /// <summary>An affordable block of a target colour that still counts, or −1.</summary>
        private static int TargetCell(ColorHuntGame game, out int color)
        {
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                foreach (var t in b.Targets)
                    if (t.Color == b.Cells[i].Color && t.Remaining > 0) { color = t.Color; return i; }
            }
            color = -1;
            return -1;
        }

        [UnityTest]
        public IEnumerator ThreeStickersAtOnce_LeaveFiftyMsApart_AndAllArrive()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out int color);
            Assert.GreaterOrEqual(cell, 0);
            for (int k = 0; k < 3; k++) Assert.IsTrue(hud.Collect.Fly(color, game.CellWorld(cell), null));
            var starts = new List<float>(hud.Collect.Departures);
            starts.Sort();
            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(StudioCurves.StaggerTime, starts[1] - starts[0], 1e-4f);
            Assert.AreEqual(StudioCurves.StaggerTime, starts[2] - starts[1], 1e-4f);
            int before = hud.Collect.Arrived;
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Flying == 0, 3f);
            Assert.AreEqual(before + 3, hud.Collect.Arrived);
            Assert.AreEqual(2, hud.Collect.LastStreak, "three arrivals within a second: streak 0, 1, 2");
            Assert.AreEqual("topla", game.Juice.Sounds.LastClip.name, "no tap, nothing else sounded");
        }

        [UnityTest]
        public IEnumerator LongChain_EveryStickerArrives_CountMatchesRules_Candy() => LongChain(PuzzleLook.Candy);

        [UnityTest]
        public IEnumerator LongChain_EveryStickerArrives_CountMatchesRules_Shatter() => LongChain(PuzzleLook.Shatter);

        /// <summary>Spec §1.4: the count drops once per arrival and ends on the rules, in every studio look (Pota: ColorHuntHudPlayTests).</summary>
        private static IEnumerator LongChain(PuzzleLook look)
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(look, (g, h) => { game = g; hud = h; });
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            float t = 0f;
            while ((game.Busy || game.ItemsInFlight > 0 || hud.FlyingCount > 0) && t < 10f)
            {
                foreach (var target in game.Board.Targets)
                    Assert.GreaterOrEqual(game.ShownTarget(target.Color), Rules(game, target.Color), "never below the rules");
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            foreach (var target in game.Board.Targets)
                Assert.AreEqual(Rules(game, target.Color), game.ShownTarget(target.Color), "settled: shown = rules");
            Assert.AreEqual(0, hud.FlyingCount);
        }

        [UnityTest]
        public IEnumerator NewRound_WhileStickersFly_KeepsTheNewCounts()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out _);
            Assert.GreaterOrEqual(cell, 0);
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Flying > 0, 3f);
            Assert.Greater(hud.Collect.Flying, 0, "a sticker is in the air");
            game.NewRound();
            yield return StudioLookPlayTests.WaitUntil(() => false, 1.5f);
            Assert.AreEqual(0, hud.Collect.Flying);
            foreach (var target in game.Board.Targets)
                Assert.AreEqual(Rules(game, target.Color), game.ShownTarget(target.Color));
        }

        [UnityTest]
        public IEnumerator Arrival_AnimatesTheRow()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out int color);
            Assert.AreEqual("broken", game.Tap(cell));
            int before = hud.Collect.Arrived;
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Arrived > before, 4f);
            Assert.Greater(hud.Collect.Arrived, before);
            Assert.IsTrue(hud.Collect.Animating(color), "squash / roll running on the arrival frame");
        }

        [UnityTest]
        public IEnumerator PotaLook_KeepsItsOwnFlight()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out _);
            Assert.AreEqual("broken", game.Tap(cell));
            bool flew = false;
            float t = 0f;
            while (t < 3f && !(flew && hud.FlyingCount == 0))
            {
                flew |= hud.FlyingCount > 0;
                Assert.AreEqual(0, hud.Collect.Flying, "the studio flight stays off under Pota");
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(flew);
        }


        [UnityTest]
        public IEnumerator RollingTargetCount_IsClippedToItsSlot()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out int color);
            Assert.AreEqual("broken", game.Tap(cell));
            int before = hud.Collect.Arrived;
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Arrived > before, 4f);
            Assert.IsTrue(hud.Collect.RowClipped(color), "the rolling count stays under the HEDEF label");
        }
    }
}
