using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>B "Gerçek kırılma" in the Color Hunt scene (spec §2 B).</summary>
    public class ShatterPlayTests
    {
        private static void AssertInsideTheFrame(ShatterBurst shatter)
        {
            foreach (var p in shatter.PiecePositions)
            {
                Assert.LessOrEqual(Mathf.Abs(p.x), ShatterDebris.InnerHalf + 1e-3f, "x inside the frame");
                Assert.LessOrEqual(Mathf.Abs(p.y), ShatterDebris.InnerHalf + 1e-3f, "y inside the frame");
                Assert.LessOrEqual(p.z, ShatterDebris.GlassZ + 1e-3f, "in front of the glass");
            }
        }

        [UnityTest]
        public IEnumerator Tap_BreaksTheBlockIntoPieces_ThatStayInTheFrame_AndVanish()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = game.Style as ShatterBurst;
            Assert.IsNotNull(shatter, "the Shatter style is ready (ai_studio_import ran)");
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            yield return StudioLookPlayTests.WaitUntil(() => shatter.ActivePieces > 0, 1.5f);
            Assert.Greater(shatter.ActivePieces, 0);
            float t = 0f;
            while (shatter.ActivePieces > 0 && t < 4f)
            {
                AssertInsideTheFrame(shatter);
                Assert.LessOrEqual(shatter.LiveCount, BurstBudget.Max);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(0, shatter.ActivePieces, "every piece shrank away");
        }

        [UnityTest]
        public IEnumerator Rotate_WhilePiecesFall_TheyStayInTheFrame()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < 3; i++) shatter.TapBurst(game.ViewAt(i), PotaLook.Tones[i]);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            float t = 0f;
            while (t < 1f) { AssertInsideTheFrame(shatter); t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Clear_ReturnsEveryPiece()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < 3; i++) shatter.TapBurst(game.ViewAt(i), PotaLook.Tones[i]);
            Assert.Greater(shatter.ActivePieces, 0);
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(0, shatter.ActivePieces);
        }

        [UnityTest]
        public IEnumerator EveryBlockAtOnce_StaysUnderTheCap()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < game.Board.Cells.Length; i++) shatter.StepBurst(game.ViewAt(i), PotaLook.Tones[i % 5], 5);
            Assert.LessOrEqual(shatter.ActivePieces, ShatterBurst.PieceMax);
            float t = 0f;
            while (t < 1f) { Assert.LessOrEqual(shatter.LiveCount, BurstBudget.Max); t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator ChainOfFour_SlowsTime_AndPushesTheCamera()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            game.Style.ChainPeak(4, Vector2.zero, PotaLook.Tones[0]);
            yield return null;
            yield return null;
            Assert.Less(Time.timeScale, 0.99f);
            Assert.IsTrue(game.Cam.GetComponent<ColorHuntCamera>().Pushing);
        }
    }
}
