using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The in-game feel layer under the Pota look (spec 2026-10-03): F2 ammo tension, …</summary>
    public class FeelPlayTests
    {
        private static IEnumerator Wait(float seconds) => StudioLookPlayTests.WaitUntil(() => false, seconds);

        /// <summary>The Pota look with the feel layer on, the same deal with ammo one short of twice the cheapest block.</summary>
        private static IEnumerator LoadLowAmmo(System.Action<ColorHuntGame, ColorHuntHud> ready)
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.S.Feel.Enabled = true;
            game.Prepare(158, 7, null);
            int cheapest = ColorHuntRules.Cheapest(game.Board);
            game.Prepare(2 * cheapest - 1, 7, null);
            game.Begin();
            yield return null;
            yield return null;
            ready(game, hud);
        }

        [UnityTest]
        public IEnumerator Tension_BreathesRedAtTheEdge_UnderThePotaLook()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            Assert.IsTrue(hud.AmmoCounter.Pulsing, "the counter pulses under the Pota look too");
            Assert.IsTrue(hud.Tension.Active);
            float most = 0f, t = 0f;
            while (t < 0.7f) { most = Mathf.Max(most, hud.Tension.EdgeAlpha); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 0.1f, "the edge breath reached its peak within a period");
        }

        [UnityTest]
        public IEnumerator Tension_StopsWhenTheRoundEnds()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            game.Prepare(0, 7, null);
            game.Begin();                                   // no affordable move: the round ends at once
            yield return null;
            yield return null;
            Assert.AreNotEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.IsFalse(hud.Tension.Active);
            Assert.AreEqual(0f, hud.Tension.EdgeAlpha, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Tension_ClearsTheFrameAfterFeelIsTurnedOff()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            yield return Wait(0.3f);
            Assert.IsTrue(hud.Tension.Active);
            game.S.Feel.Enabled = false;
            yield return null;
            Assert.IsFalse(hud.Tension.Active);
            Assert.AreEqual(0f, hud.Tension.EdgeAlpha, 1e-4f);
            Assert.IsFalse(hud.AmmoCounter.Pulsing, "the Pota look without feel keeps today's gold counter");
            game.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Intro_UnderThePotaLook_TapsWait_RibbonAndGoalsPlay_ReadyUnder1_8s()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.NewRound();
            yield return null;
            yield return null;
            Assert.IsTrue(game.Intro, "the feel layer's intro runs under the Pota look");
            Assert.IsTrue(game.Busy, "taps wait");
            Assert.IsFalse(hud.Intro.Done);
            Assert.IsTrue(hud.Intro.RibbonVisible, "the BÖLÜM ribbon is up");
            Assert.AreEqual("BÖLÜM " + game.S.LevelNumber, hud.Intro.Title);
            int n = game.Board.Size;
            var top = game.ViewAt(ColorHuntRules.Index(n, 0, 0));
            Assert.AreEqual(0f, top.Scale, 1e-5f, "blocks wait for the ribbon and the goals");
            float t = 0f;
            while (game.Intro && t < 1.8f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(game.Intro, "ready within 1.8 s");
            Assert.AreEqual(1f, top.Scale, 1e-5f);
            Assert.IsTrue(hud.Intro.Done);
            Assert.AreEqual(game.Board.Targets.Length, hud.Intro.GoalsArrived, "every goal chip flew to its row");
            Assert.IsFalse(hud.Intro.RibbonVisible);
        }

        [UnityTest]
        public IEnumerator Intro_Skip_FinishesWithin0_35s()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.NewRound();
            yield return Wait(0.2f);
            Assert.IsTrue(game.Intro);
            game.SkipIntro();
            float t = 0f;
            while (game.Intro && t < 0.35f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(game.Intro, "a tap compresses the rest of the intro");
            Assert.IsTrue(hud.Intro.Done);
        }

        [UnityTest]
        public IEnumerator Landing_BouncesDirectionally_UnderTheFeelLayer()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int cell = ColorHuntHint.Best(game.Board);
            Assert.AreEqual("broken", game.Tap(cell));
            bool bounced = false;
            float t = 0f;
            while (t < 3f && !bounced)
            {
                for (int i = 0; i < game.Board.Cells.Length; i++)
                {
                    var v = game.ViewAt(i);
                    if (v != null && v.Bounce >= 0f && v.transform.localScale.x > v.transform.localScale.y * 1.1f) bounced = true;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(bounced, "a landing block was wider than tall");
        }

        /// <summary>A one-red-block level: tapping any affordable red block wins. Returns that cell.</summary>
        private static int StageOneRedTarget(ColorHuntGame game, int ammo)
        {
            var design = game.S.ToDesign();
            design.Targets = new[] { (0, 1) };
            game.Prepare(ammo, 7, design);
            game.Begin();
            for (int i = 0; i < game.Board.Cells.Length; i++)
                if (game.Board.Cells[i].Color == 0 && game.Board.Cells[i].Hp <= game.Board.Ammo) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator Finale_WithLeftoverAmmo_FliesBallsIntoBlocks_CoinsByPotasFormula_ThenTheRoundEnds()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int cell = StageOneRedTarget(game, 140);
            Assume.That(cell >= 0, "an affordable red block");
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = won;
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 6f);
            Assert.AreEqual(ColorHuntGame.State.Ended, game.Current);
            int remaining = game.Board.Ammo;
            Assume.That(remaining >= 10, "enough ammo left for coins");
            yield return null;
            Assert.IsTrue(hud.Finale.Running, "the finale starts at the win");
            Assert.IsFalse(ended, "the result waits for the finale");
            float t = 0f;
            while (hud.Finale.Running && t < 2.5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(hud.Finale.Running, "the finale is over within the cap");
            Assert.LessOrEqual(t, game.S.Feel.FinaleMax + 0.3f);
            Assert.AreEqual(FeelCurves.LeftoverCoins(remaining), hud.Finale.CoinsShown);
            Assert.GreaterOrEqual(hud.Finale.Flights, game.S.Feel.FinaleMinFlights);
            Assert.LessOrEqual(hud.Finale.Flights, game.S.Feel.FinaleMaxFlights);
            Assert.AreEqual(0, game.DisplayedAmmo, "the counter rolled down to zero");
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.WinWait + 0.6f);
            Assert.IsTrue(ended, "RoundEnded follows the finale and the disperse");
        }

        [UnityTest]
        public IEnumerator Finale_WithNoAmmoLeft_TakesNoFlights_ShowsNoBadge_AndEndsAtOnce()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int probe = StageOneRedTarget(game, 158);
            Assume.That(probe >= 0);
            int cell = StageOneRedTarget(game, game.Board.Cells[probe].Hp);   // the same deal with exactly that block's cost
            Assume.That(cell == probe, "the cheapest-covered red block is the same cell");
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = won;
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 6f);
            Assert.AreEqual(0, game.Board.Ammo);
            yield return null;
            Assert.AreEqual(0, hud.Finale.Flights);
            Assert.IsFalse(hud.Finale.BadgeVisible);
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.WinWait + 0.4f);
            Assert.IsTrue(ended);
        }

        /// <summary>A round that ends at once with nothing affordable: ammo 0 and the given red target.</summary>
        private static void StageInstantLoss(ColorHuntGame game, int redTarget)
        {
            var design = game.S.ToDesign();
            design.Targets = new[] { (0, redTarget) };
            game.Prepare(0, 7, design);
            game.Begin();
        }

        [UnityTest]
        public IEnumerator Lose_DimsTheBoard_PullsTheCameraBack_LabelsTheRemainingGoal_NearMissUnderFour()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = true;
            StageInstantLoss(game, 3);
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 2f);
            yield return null;
            Assert.IsTrue(hud.Lose.Playing);
            Assert.IsTrue(hud.Lose.NearMiss, "3 left reads as a near miss");
            Assert.AreEqual("3 kaldı", hud.Lose.RemainingLabel(0));
            yield return Wait(game.S.Feel.LoseDimTime + 0.1f);
            Assert.Less(game.ViewAt(0).Fade, game.S.Feel.LoseDim + 0.05f, "blocks faded to the lose dim");
            var cam = game.Cam.GetComponent<ColorHuntCamera>();
            Assert.Greater(cam.BackOffset, game.S.Feel.LoseCamBack * 0.8f, "the camera pulled back");
            Assert.IsFalse(ended, "the card waits for the moment");
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.Feel.LoseHold + 0.5f);
            Assert.IsTrue(ended, "the card comes after the hold");
        }

        [UnityTest]
        public IEnumerator Lose_WithManyGoalsLeft_IsNotANearMiss()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            StageInstantLoss(game, 22);
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 2f);
            yield return null;
            Assert.IsTrue(hud.Lose.Playing);
            Assert.IsFalse(hud.Lose.NearMiss);
            Assert.AreEqual("22 kaldı", hud.Lose.RemainingLabel(0));
        }

        [UnityTest]
        public IEnumerator Lose_WithFeelOff_KeepsTodaysBoardAndWait()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.S.Feel.Enabled = false;
            game.NewRound();
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 3f);
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = true;
            StageInstantLoss(game, 3);
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 2f);
            yield return Wait(0.3f);
            Assert.IsFalse(hud.Lose.Playing);
            Assert.AreEqual(1f, game.ViewAt(0).Fade, 1e-4f);
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.LoseWait + 0.4f);
            Assert.IsTrue(ended, "today's LoseWait");
            game.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Lose_TurningFeelOffMidMoment_RestoresTheBoard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            StageInstantLoss(game, 3);
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 2f);
            yield return Wait(game.S.Feel.LoseDimTime + 0.1f);
            Assert.Less(game.ViewAt(0).Fade, 0.9f);
            game.S.Feel.Enabled = false;
            yield return null;
            yield return null;
            Assert.IsFalse(hud.Lose.Playing);
            Assert.AreEqual(1f, game.ViewAt(0).Fade, 1e-4f);
            Assert.AreEqual(0f, game.Cam.GetComponent<ColorHuntCamera>().BackOffset, 1e-4f);
            game.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator BoardClip_PutsBlocksOnTheBoardLayer_AndTheMaskTurnsWithTheBoard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            var clip = game.GetComponentInChildren<BoardClip>();
            Assert.IsNotNull(clip, "the scene tool put a BoardClip on the game");
            Assert.IsTrue(BoardClip.Installed(), "the renderer features are installed");
            int layer = LayerMask.NameToLayer(BoardClip.LayerName);
            Assert.GreaterOrEqual(layer, 0);
            var v = game.ViewAt(0);
            Assert.AreEqual(layer, v.gameObject.layer);
            foreach (var r in v.GetComponentsInChildren<Renderer>()) Assert.AreEqual(layer, r.gameObject.layer, r.name);
            Assert.AreNotEqual(layer, clip.Mask.gameObject.layer, "the mask is drawn by the default pass (it writes the stencil the board's passes test)");
            Assert.AreEqual(BoardClip.MaskLocalSize(game.Board.Size).x, clip.Mask.localScale.x, 1e-3f);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            yield return Wait(0.2f);
            Assert.AreNotEqual(0f, game.BoardRoot.localRotation.eulerAngles.z, "mid-turn");
            Assert.AreEqual(game.BoardRoot.rotation.eulerAngles.z, clip.Mask.rotation.eulerAngles.z, 1e-3f, "the mask turns with the board");
        }

        [UnityTest]
        public IEnumerator SkyLife_CloudsDrift_BirdsFlapAndPass_CityStands()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            var sky = Object.FindAnyObjectByType<Project.Flow.SkyLife>();
            Assert.IsNotNull(sky, "the puzzle stage built a SkyLife");
            Assert.AreEqual(6, sky.CloudCount);
            Assert.IsNotNull(sky.City, "the city band stands behind the roof");
            var cloud = sky.Cloud(0);
            var p0 = cloud.position;
            yield return Wait(0.5f);
            Assert.Greater(Vector3.Distance(p0, cloud.position), 0.05f, "clouds drift");
            foreach (var r in cloud.GetComponentsInChildren<Renderer>()) Assert.AreEqual("Project/Toon", r.sharedMaterial.shader.name);
            Assert.IsTrue(sky.BirdsFlapping, "the flap clip plays");
            sky.StartBirdPass();
            yield return null;
            var b0 = sky.Birds.position;
            yield return Wait(0.3f);
            Assert.Greater(Vector3.Distance(b0, sky.Birds.position), 0.5f, "the flock crosses the sky");
            float boardTop = game.transform.position.y + PotaLook.PanoSize * 0.5f;
            for (int i = 0; i < sky.CloudCount; i++)
                Assert.Greater(Vector3.Distance(sky.Cloud(i).position, game.transform.position), 60f, "clouds stay far from the board");
            Assert.Less(sky.City.position.y, game.transform.position.y - 5f, "the city's bases sit below the roof");
        }

        [UnityTest]
        public IEnumerator SkyLife_IsBuiltOnceInPotaFlow()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("PotaFlow");
            yield return null;
            yield return null;
            var skies = Object.FindObjectsByType<Project.Flow.SkyLife>(FindObjectsSortMode.None);
            Assert.AreEqual(1, skies.Length, "the basket environment and the puzzle stage share one sky");
        }

        [UnityTest]
        public IEnumerator AffordableCount_CountsBlocksTheAmmoCovers()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            int expected = 0;
            foreach (var c in game.Board.Cells) if (c.Hp <= game.Board.Ammo) expected++;
            Assert.AreEqual(expected, game.AffordableCount);
            Assert.Greater(expected, 0);
        }
    }
}
