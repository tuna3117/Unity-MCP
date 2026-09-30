using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>Pota's puzzle timing in the Color Hunt scene as saved.</summary>
    public class PotaLookPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadPlaying(Action<ColorHuntGame> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            ready(game);
        }

        private static int Affordable(ColorHuntGame game, int minHp)
        {
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i].Hp >= minHp && b.Cells[i].Hp <= b.Ammo) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator Salvo_FliesMinCost24Balls_OnTheBoard_ThenEnds()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            int cell = Affordable(game, 5);
            Assert.GreaterOrEqual(cell, 0);
            int cost = game.Board.Cells[cell].Hp;
            game.Tap(cell);
            yield return null;
            var balls = game.BoardRoot.GetComponentsInChildren<MeshRenderer>(true);
            int pooled = 0;
            foreach (var r in balls) if (r.name == "Ball") pooled++;
            Assert.AreEqual(Mathf.Min(24, cost), pooled, "renkAviFaz.ts: min(enCokTop, cost) balls, children of the board");
            yield return WaitUntil(() => !game.Salvo.Busy, 2f);
            Assert.IsFalse(game.Salvo.Busy, "all balls land within the 0.5 s salvo");
        }

        [UnityTest]
        public IEnumerator TappedBlock_SwellsTo1Point18_AndBurstsAtItsPeak()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            int cell = Affordable(game, 1);
            var view = game.ViewAt(cell);
            var flash = game.Fx.transform.Find("FlashWhite").GetComponent<ParticleSystem>();
            game.Tap(cell);
            yield return WaitUntil(() => view.Swell >= 0f, 3f);
            float start = Time.time, maxScale = 0f, burstAt = -1f;
            while (view.Swell >= 0f && view.gameObject.activeSelf && Time.time - start < 1f)
            {
                maxScale = Mathf.Max(maxScale, view.transform.localScale.x);
                if (burstAt < 0f && flash.particleCount > 0) burstAt = Time.time - start;
                yield return null;
            }
            Assert.That(maxScale, Is.InRange(1.1f, 1.181f), "balon.dokunusSisme 1.18");
            Assert.That(burstAt, Is.InRange(0.03f, 0.1f), "the burst waits for the peak (0.0605 s of game time; frame granularity)");
        }

        [UnityTest]
        public IEnumerator Rotation_ShrinksToFit_LabelsRideThenStandUp()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            var v = game.ViewAt(0);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            float minScale = 10f, t0 = Time.time;
            bool rodeEarly = true, stoodLate = false;
            while (game.Busy && Time.time - t0 < 2f)
            {
                float u = (Time.time - t0) / game.S.RotateTime;
                minScale = Mathf.Min(minScale, game.BoardRoot.localScale.x);
                if (u < 0.65f && game.ViewAt(0) == v) rodeEarly &= Mathf.Abs(v.LabelAngle) < 1e-3f;
                if (u > 0.85f && u < 1f && game.ViewAt(0) == v) stoodLate |= Mathf.Abs(v.LabelAngle) > 30f;
                yield return null;
            }
            Assert.Less(minScale, 1.2f * 0.85f, "donusSigdirma ≈ 0.8 at 45°");
            Assert.IsTrue(rodeEarly, "labels ride the board for the first 70 %");
            Assert.IsTrue(stoodLate, "then turn upright");
            Assert.AreEqual(1.2f, game.BoardRoot.localScale.x, 1e-4f);
            Assert.AreEqual(0f, game.ViewAt(0).LabelAngle, 1e-4f, "new views after the turn");
        }
    }
}
