using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>A "Şeker patlaması" in the Color Hunt scene (spec §2 A).</summary>
    public class CandyPlayTests
    {
        private static int Live(CandyBurst candy, string system) =>
            candy.transform.Find(system).GetComponent<ParticleSystem>().particleCount;

        [UnityTest]
        public IEnumerator Tap_PuffsFlashesAndThrowsChunks_WithinBudget()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = game.Style as CandyBurst;
            Assert.IsNotNull(candy, "the Candy style is ready (ai_studio_import ran)");
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            int puff = 0, flash = 0, chunks = 0, most = 0;
            float t = 0f;
            while (t < 1.5f)
            {
                puff = Mathf.Max(puff, Live(candy, "CandyPuff"));
                flash = Mathf.Max(flash, Live(candy, "CandyFlash"));
                chunks = Mathf.Max(chunks, Live(candy, "CandyChunks"));
                most = Mathf.Max(most, candy.LiveCount);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(puff, 0);
            Assert.Greater(flash, 0);
            Assert.GreaterOrEqual(chunks, 6);
            Assert.LessOrEqual(most, BurstBudget.Max);
        }

        [UnityTest]
        public IEnumerator ChainPeakFive_SaysMuhtesem_GlowsSlowsAndThrowsConfetti()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = (CandyBurst)game.Style;
            candy.ChainPeak(5, Vector2.zero, PotaLook.Tones[0]);
            yield return null;
            yield return null;
            Assert.AreEqual("Muhteşem!", candy.Hud.LastWord);
            Assert.Greater(candy.Hud.EdgeAlpha, 0f);
            Assert.Less(Time.timeScale, 0.99f, "0.25 s slow motion");
            Assert.Greater(game.Fx.LiveCount, 0, "confetti");
            Assert.Greater(Live(candy, "CandyRings"), 0);
        }

        [UnityTest]
        public IEnumerator TwelveBurstsAndAChainAtOnce_StayUnderTheCap()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = (CandyBurst)game.Style;
            for (int i = 0; i < 12; i++) candy.StepBurst(game.ViewAt(i), PotaLook.Tones[i % 5], 6);
            candy.ChainPeak(6, Vector2.zero, PotaLook.Tones[1]);
            int most = 0;
            float t = 0f;
            while (t < 1f) { most = Mathf.Max(most, candy.LiveCount); t += Time.unscaledDeltaTime; yield return null; }
            Assert.LessOrEqual(most, BurstBudget.Max);
        }


        [UnityTest]
        public IEnumerator Words_HaveANavyOutline_AndASoftDropShadow()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = (CandyBurst)game.Style;
            var words = candy.Hud.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            Assert.Greater(words.Length, 0);
            foreach (var w in words)
            {
                var outline = w.GetComponent<UnityEngine.UI.Outline>();
                Assert.AreEqual(PotaLook.HudNavy, outline.effectColor, "the ink outline");
                UnityEngine.UI.Shadow drop = null;
                foreach (var s in w.GetComponents<UnityEngine.UI.Shadow>()) if (s.GetType() == typeof(UnityEngine.UI.Shadow)) drop = s;
                Assert.IsNotNull(drop);
                Assert.AreEqual(0.35f, drop.effectColor.a, 1e-4f, "the soft drop shadow");
            }
        }
    }
}
