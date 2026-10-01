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
    /// <summary>A staged board with all four specials and two gates, in the Color Hunt scene as saved (Pota look).</summary>
    public class SpecialBoardPlayTests
    {
        private static readonly int[] GateCols = { 1, 5 };

        /// <summary>7×7: LineV (1, 1), LineH (2, 4), Bomb (3, 2), Golden (4, 5) — no golden ball on the bottom row (nothing reaches the basket, also after a turn).</summary>
        private static readonly Dictionary<int, SpecialKind> Specials = new Dictionary<int, SpecialKind>
        {
            { 8, SpecialKind.LineV }, { 18, SpecialKind.LineH }, { 23, SpecialKind.Bomb }, { 33, SpecialKind.Golden },
        };

        public static Board SpecialBoard(ColorHuntSettings s)
        {
            var b = ColorHuntRules.Create(s.ToDesign(), s.Ammo, 7u);
            foreach (var kv in Specials) b.Cells[kv.Key] = Block.MakeSpecial(kv.Value);
            b.Gates.AddRange(GateCols);
            b.Features = Features.All;
            return b;
        }

        private static List<Transform> ActiveRings(GateRings g)
        {
            var list = new List<Transform>();
            foreach (Transform t in g.transform) if (t.gameObject.activeSelf) list.Add(t);
            return list;
        }

        private static int SpecialViews(ColorHuntGame game)
        {
            int n = 0;
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                if (v != null && v.Block.IsSpecial) n++;
            }
            return n;
        }

        [UnityTest]
        public IEnumerator StagedSpecials_WearTheirStickers_AndTheGatesStayOnTheFrame()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(7, game.S.Size, "the level this test is laid out for");

            // Stage: a copy, the same events as Prepare, staged until Begin.
            var board = SpecialBoard(game.S);
            int started = 0;
            void Count() => started++;
            game.RoundStarted += Count;
            game.Stage(board);
            game.RoundStarted -= Count;
            Assert.AreEqual(1, started, "Stage raises RoundStarted like Prepare");
            Assert.AreNotSame(board, game.Board, "Stage deals a copy");
            Assert.AreEqual(ColorHuntGame.State.Idle, game.Current, "staged until Begin");
            Assert.AreEqual(0u, game.Seed);
            game.Begin();
            yield return null;

            // Views: tone, sticker, no number, never dimmed; LineH turned.
            Assert.AreEqual(4, SpecialViews(game));
            foreach (var kv in Specials)
            {
                var v = game.ViewAt(kv.Key);
                Assert.AreEqual(kv.Value, v.Block.Special, $"cell {kv.Key}");
                Assert.AreEqual(PotaLook.SpecialTone(kv.Value), v.Tone);
                Assert.IsFalse(v.transform.Find("Number").GetComponent<MeshRenderer>().enabled, $"{kv.Value}: no number");
                Assert.IsFalse(v.Dim, $"{kv.Value}: never dimmed");
                var mpb = new MaterialPropertyBlock();
                v.transform.Find("Item").GetComponent<Renderer>().GetPropertyBlock(mpb);
                Assert.AreEqual(LabelAtlas.UvRect(LabelAtlas.SpecialCell(kv.Value)), mpb.GetVector("_UvRect"), $"{kv.Value}: sticker");
            }
            Assert.AreEqual(90f, game.ViewAt(18).transform.Find("Item").localEulerAngles.z, 1e-3f, "LineH: the line sticker turned 90°");

            // The golden ball pulses (0 … 0.16 over 2π/5 s of game time); the bomb does not.
            var golden = game.ViewAt(33);
            float most = 0f, least = 1f, clock = 0f, wall = 0f;
            while (clock < 1.4f && wall < 6f)
            {
                most = Mathf.Max(most, golden.Flash);
                least = Mathf.Min(least, golden.Flash);
                clock += Mathf.Min(Time.deltaTime, 0.05f);
                wall += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(most, 0.14f, "altinNabiz reaches 0.16");
            Assert.Less(least, 0.03f, "and falls to 0");
            Assert.AreEqual(0f, game.ViewAt(23).Flash, 1e-6f, "only golden balls pulse");

            // Gates: two rings on the game root at columns 1 and 5, on the frame's bottom edge.
            Assert.AreSame(game.transform, game.Gates.transform.parent, "on the game root, not the board");
            CollectionAssert.AreEqual(GateCols, game.Gates.Shown);
            var rings = ActiveRings(game.Gates);
            Assert.AreEqual(2, rings.Count);
            for (int i = 0; i < rings.Count; i++)
                Assert.Less(Vector3.Distance(game.Gates.RingLocal(GateCols[i]), rings[i].localPosition), 1e-5f, $"ring {i}");
            Assert.AreEqual(-2.4f, rings[0].localPosition.x, 1e-4f, "column 1 of 7, s = 1.2");
            Assert.AreEqual(-4.69f, rings[0].localPosition.y, 1e-4f, "−(4.4 + 0.35) + 0.05·1.2");

            // Flash at a gate draws through ColorHuntFx.
            Assert.AreEqual(0, game.Fx.LiveCount, "Stage cleared the effects");
            game.Gates.Flash(GateCols[0]);
            yield return null;
            Assert.Greater(game.Fx.LiveCount, 0, "the gate flash drew");

            // The board turns; the rings stay where they are.
            var before = rings.ConvertAll(r => r.position);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            yield return StudioLookPlayTests.WaitUntil(() => Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity) > 30f, 2f);
            Assert.Greater(Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity), 30f, "caught mid-turn");
            for (int i = 0; i < rings.Count; i++)
                Assert.Less(Vector3.Distance(before[i], rings[i].position), 1e-4f, $"ring {i} turned with the board");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 3f);
            Assert.IsFalse(game.Busy, "the turn never settled");
            for (int i = 0; i < rings.Count; i++)
            {
                Assert.IsTrue(rings[i].gameObject.activeSelf, $"ring {i} still shown");
                Assert.Less(Vector3.Distance(before[i], rings[i].position), 1e-4f, $"ring {i} moved");
            }
            Assert.AreEqual(4, SpecialViews(game), "the specials turned with the board");

            // A normal deal hides the gates and has no specials.
            game.Prepare(game.S.Ammo, 7, null);
            Assert.AreEqual(0, game.Gates.Shown.Count, "a normal deal has no gates");
            Assert.AreEqual(0, ActiveRings(game.Gates).Count);
            Assert.AreEqual(0, SpecialViews(game));
        }
    }
}
