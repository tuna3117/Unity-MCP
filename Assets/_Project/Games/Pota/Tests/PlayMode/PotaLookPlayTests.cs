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
    }
}
