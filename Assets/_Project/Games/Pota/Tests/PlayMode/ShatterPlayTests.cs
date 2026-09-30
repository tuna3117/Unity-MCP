using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
            while (shatter.ActivePieces > 0 && t < 8f)           // the hint's longest chain keeps breaking blocks for ~3 s
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

        [UnityTest]
        public IEnumerator Tap_SendsAShockRing()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            yield return StudioLookPlayTests.WaitUntil(() => shatter.ShockLive > 0, 1.5f);
            Assert.Greater(shatter.ShockLive, 0);
        }


        [UnityTest]
        public IEnumerator ShockRing_SortsBeforeTheLabelsAndEffects()
        {
            // the ring redraws the opaque-only scene inside its band: drawn later it would erase labels, smoke and sparks there
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            int ring = shatter.transform.Find("ShatterShock").GetComponent<ParticleSystemRenderer>().sortingOrder;
            int labels = 0;
            foreach (var v in game.BoardRoot.GetComponentsInChildren<BlockView>())
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 2500) { Assert.Less(ring, r.sortingOrder, r.name); labels++; }
            Assert.Greater(labels, 0);
            foreach (var ps in new[] { "ShatterSmoke", "ShatterSparks" })
                Assert.Less(ring, shatter.transform.Find(ps).GetComponent<ParticleSystemRenderer>().sortingOrder, ps);
        }

        [UnityTest]
        public IEnumerator OpaqueTexture_IsOnOnlyWhileARingLives()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var data = game.Cam.GetUniversalAdditionalCameraData();
            var shatter = (ShatterBurst)game.Style;
            Assert.AreEqual(CameraOverrideOption.UsePipelineSettings, data.requiresColorOption, "idle: no extra full-screen copy");
            shatter.TapBurst(game.ViewAt(0), PotaLook.Tones[0]);
            Assert.AreEqual(CameraOverrideOption.On, data.requiresColorOption, "a ring is alive");
            yield return StudioLookPlayTests.WaitUntil(() => shatter.ShockLive == 0, 2f);
            yield return null;
            Assert.AreEqual(CameraOverrideOption.UsePipelineSettings, data.requiresColorOption, "back off with the last ring");
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(CameraOverrideOption.UsePipelineSettings, data.requiresColorOption);
        }

        [UnityTest]
        public IEnumerator Pieces_AreChildrenOfTheGameRoot_AndStayInTheFrameWhileTheBoardTurns()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < 3; i++) shatter.TapBurst(game.ViewAt(i), PotaLook.Tones[i]);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            float t = 0f;
            while (t < 1f)
            {
                foreach (Transform piece in shatter.transform)
                {
                    if (!piece.gameObject.activeSelf || piece.GetComponent<MeshFilter>() == null) continue;
                    Assert.AreSame(game.transform, piece.parent.parent, "not carried by the board");
                    var p = game.transform.InverseTransformPoint(piece.position);
                    Assert.LessOrEqual(Mathf.Abs(p.x), ShatterDebris.InnerHalf + 1e-3f);
                    Assert.LessOrEqual(Mathf.Abs(p.y), ShatterDebris.InnerHalf + 1e-3f);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
