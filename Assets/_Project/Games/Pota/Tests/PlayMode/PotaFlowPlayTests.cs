using System;
using System.Collections;
using NUnit.Framework;
using Project.Basket;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Phase = Project.Flow.PotaFlowDirector.Phase;

namespace Project.Tests
{
    /// <summary>PotaFlow scene as loaded from disk: basket round end → transition → playable board → retry → basket.</summary>
    public class PotaFlowPlayTests
    {
        private const float Step = 1f / 60f;

        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator Load(Action<PotaFlowDirector> ready)
        {
            SceneManager.LoadScene("PotaFlow");
            yield return null;
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            Assert.IsNotNull(d, "PotaFlowDirector missing");
            yield return WaitUntil(() => d.Hoop != null && d.Game.BoardRoot != null && d.Round.Column != null, 5f);
            Assert.IsNotNull(d.Game.BoardRoot, "board not built");
            Assert.AreEqual(Phase.Basket, d.Current);
            ready(d);
        }

        private static void RunTransition(PotaFlowDirector d)
        {
            int guard = 0;
            while (d.Current == Phase.Transition && guard++ < 1000) d.Advance(Step);
        }

        private static Button ButtonAt(ColorHuntHud hud, string path) => hud.transform.Find(path).GetComponent<Button>();

        [UnityTest]
        public IEnumerator Transition_EndsOnAPlayableBoard_WithTheBasketCountAsAmmo()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            float t0 = Time.realtimeSinceStartup;
            Assert.IsTrue(d.BeginTransition(158));
            Assert.AreEqual(Phase.Transition, d.Current);
            Assert.IsFalse(d.Shot.InputEnabled, "basket input off during the transition");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 3.0f, "spec: board playable within 3 s");
            yield return null;
            Assert.AreEqual(ColorHuntGame.State.Playing, d.Game.Current);
            Assert.AreEqual(158, d.Game.Board.Ammo);
            Assert.AreEqual(158, d.Game.DisplayedAmmo);
            Assert.AreEqual("158", d.Hud.transform.Find("AmmoPanel/AmmoWindow/Ammo").GetComponent<Text>().text);
            Assert.IsTrue(d.BoardInput.enabled, "board input on");
            Assert.IsFalse(d.Shot.InputEnabled, "basket input off");
            Assert.IsFalse(d.RoundCam.enabled);
            Assert.IsTrue(d.BoardCam.enabled);
            Assert.AreEqual(d.Bin.RestingCapacity, d.Stream.ActiveCount, "a basket full of balls beside the hero");
            Assert.AreEqual(d.Bin.RestingCapacity, d.Bin.Pile);
            Assert.AreEqual(0, d.Hoop.Pile, "the giant hoop's net stays empty");
            var hero = d.Round.Column.Player.transform;
            var toBin = d.Bin.transform.position - hero.position; toBin.y = 0f;
            Assert.Less(Vector3.Angle(hero.forward, toBin), 20f, "the hero ends up facing his basket");
            Assert.IsFalse(d.Badge.Visible);
            for (int i = 0; i < d.Game.Board.Cells.Length; i++) Assert.AreEqual(1f, d.Game.ViewAt(i).Scale, 1e-5f, $"cell {i}");
            Assert.Less(Vector3.Distance(d.Cam.transform.position, d.BoardCam.FitFor(d.S.BoardFov, d.Cam.aspect)), 0.3f);
            Assert.AreEqual("Tekrar at", d.Hud.transform.Find("ResultPanel/Card/Retry/Label").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator Skip_FinishesWithinASecond()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(40);
            for (int i = 0; i < 5; i++) yield return null;
            float t0 = Time.realtimeSinceStartup;
            d.Skip();
            Assert.IsTrue(d.Skipping);
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 2f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 1.0f);
            Assert.AreEqual(40, d.Game.DisplayedAmmo);
        }

        [UnityTest]
        public IEnumerator HeldSkipTouch_DoesNotReachTheBoard()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            bool held = true;
            int presses = 1;
            d.PointerHeld = () => held;
            d.PointerPressedThisFrame = () => presses-- > 0;
            d.BeginTransition(20);
            yield return null;
            Assert.IsTrue(d.Skipping, "a tap during the transition skips");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 2f);
            yield return null;
            yield return null;
            Assert.IsFalse(d.BoardInput.enabled, "board input must wait until the finger lifts");
            held = false;
            yield return null;
            Assert.IsTrue(d.BoardInput.enabled);
        }

        [UnityTest]
        public IEnumerator Counter_ReachesN_ForEdgeCounts()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            foreach (int n in new[] { 1, 3, 1000 })
            {
                Assert.IsTrue(d.BeginTransition(n), $"N={n} did not start");
                int prev = 0;
                int guard = 0;
                while (d.Current == Phase.Transition && guard++ < 1000)
                {
                    d.Advance(Step);
                    Assert.GreaterOrEqual(d.Game.DisplayedAmmo, prev, $"N={n}: counter went down");
                    prev = d.Game.DisplayedAmmo;
                }
                Assert.AreEqual(Phase.Puzzle, d.Current);
                Assert.AreEqual(n, d.Game.Board.Ammo);
                Assert.AreEqual(n, d.Game.DisplayedAmmo);
                Assert.IsTrue(d.ReturnToBasket());
                guard = 0;
                while (d.Current == Phase.Return && guard++ < 1000) d.Advance(Step);
                Assert.AreEqual(Phase.Basket, d.Current);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ZeroBaskets_LoseThenRetryFliesBack()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            Assert.IsTrue(d.BeginTransition(0));
            yield return WaitUntil(() => d.Current == Phase.Result, 8f);
            Assert.AreEqual(Phase.Result, d.Current, "a board with 0 ammo ends at once");
            ButtonAt(d.Hud, "ResultPanel/Card/Retry").onClick.Invoke();
            Assert.AreEqual(Phase.Return, d.Current, "\"Tekrar at\" flies back");
            yield return WaitUntil(() => d.Current == Phase.Basket, 3f);
            Assert.AreEqual(Phase.Basket, d.Current);
            yield return null;
            Assert.AreEqual(BasketRound.State.Aim, d.Round.Current);
            Assert.IsTrue(d.Shot.InputEnabled);
            Assert.IsFalse(d.BoardInput.enabled);
            Assert.IsTrue(d.RoundCam.enabled);
            Assert.Less(Vector3.Distance(d.Cam.transform.position, d.Round.Settings.AimCamPos), 0.6f);
            Assert.AreEqual(d.Round.Settings.Fov, d.Cam.fieldOfView, 0.01f);
        }

        [UnityTest]
        public IEnumerator LeftoverSlowMotion_DoesNotSlowTheTransition()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.TimeCtl.SlowMo(0.05f, 10f);
            yield return null;
            Assert.AreEqual(0.05f, Time.timeScale, 1e-4f);
            float t0 = Time.realtimeSinceStartup;
            d.BeginTransition(20);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "the transition starts at normal time");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 4f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 3.0f);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }

        [UnityTest]
        public IEnumerator SecondRoundEnd_AndHudTaps_DuringTransition_AreIgnored()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(30);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(d.BeginTransition(99), "a second round end must be ignored");
            Assert.IsFalse(d.ReturnToBasket(), "no return during the transition");
            ButtonAt(d.Hud, "RotateRight").onClick.Invoke();
            Assert.AreEqual(0, d.Game.Rotations, "the staged board cannot rotate");
            Assert.AreEqual("locked", d.Game.Tap(0), "the staged board cannot be tapped");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(30, d.Game.Board.Ammo);
        }

        [UnityTest]
        public IEnumerator AspectChange_MidFlight_StillLandsOnTheFit()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            try
            {
                d.BeginTransition(20);
                for (int i = 0; i < 40; i++) d.Advance(Step);
                d.Cam.aspect = 16f / 9f;
                RunTransition(d);
                var want = d.BoardCam.FitFor(d.S.BoardFov, d.Cam.aspect);
                Assert.Less(Vector3.Distance(d.Cam.transform.position, want), 0.3f);
                yield return null;
                Assert.Less(Vector3.Distance(d.Cam.transform.position, want), 0.3f, "no jump when ColorHuntCamera takes over");
            }
            finally { d.Cam.ResetAspect(); }
        }

        [UnityTest]
        public IEnumerator CandidateB_HasNoStream_AndTheSameCount()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.CandidateOverride = FlowCandidate.Simple;
            Assert.IsTrue(d.BeginTransition(158));
            int maxActive = 0, guard = 0;
            while (d.Current == Phase.Transition && guard++ < 1000)
            {
                d.Advance(Step);
                maxActive = Mathf.Max(maxActive, d.Stream.ActiveCount);
            }
            Assert.AreEqual(0, maxActive, "B has no ball stream");
            Assert.AreEqual(158, d.Game.DisplayedAmmo);
        }

        [UnityTest]
        public IEnumerator Reveal_GrowsFromTheCentre()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.CandidateOverride = FlowCandidate.Stream;      // A's ring reveal / early ammo panel (the default is Stamp since 2026-10-02)
            d.BeginTransition(10);
            d.Advance(1.80f);
            int n = d.Game.Board.Size, centre = -1;
            for (int i = 0; i < d.Game.Board.Cells.Length; i++)
                if (ColorHuntRules.Row(n, i) == n / 2 && ColorHuntRules.Col(n, i) == n / 2) centre = i;
            Assert.Greater(d.Game.ViewAt(centre).Scale, 0.2f, "centre ring is popping at 1.80 s");
            Assert.Less(d.Game.ViewAt(0).Scale, 0.01f, "corner ring has not started");
        }

        [UnityTest]
        public IEnumerator AmmoPanel_CountsWhileBallsLand_BeforeTheBoardArrives()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.CandidateOverride = FlowCandidate.Stream;      // A's ring reveal / early ammo panel (the default is Stamp since 2026-10-02)
            var ammo = (RectTransform)d.Hud.transform.Find("AmmoPanel");
            var targets = (RectTransform)d.Hud.transform.Find("TargetPanel");
            d.Hud.SetIntro(1f);
            Vector2 ammoHome = ammo.anchoredPosition, targetsHome = targets.anchoredPosition;
            d.BeginTransition(158);
            d.Advance(1.40f);
            Assert.Less(Vector2.Distance(ammo.anchoredPosition, ammoHome), 1f, "ammo panel is in while balls land");
            Assert.Greater(targets.anchoredPosition.y, targetsHome.y + 100f, "targets wait for the board");
            Assert.That(d.Game.DisplayedAmmo, Is.InRange(1, 157), "the count is running");
            RunTransition(d);
            Assert.AreEqual(0f, d.Hud.transform.Find("Hint").GetComponent<Text>().color.a, 1e-4f, "hint hidden in PotaFlow");
        }

        [UnityTest]
        public IEnumerator FirstTransition_BuildsNoBlocks_OnItsFirstFrame()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            yield return null;
            yield return null;
            int before = d.Game.GetComponentsInChildren<BlockView>(true).Length;
            Assert.GreaterOrEqual(before, d.Game.S.Size * d.Game.S.Size, "block views are prewarmed during the basket round");
            d.BeginTransition(158);
            Assert.AreEqual(before, d.Game.GetComponentsInChildren<BlockView>(true).Length, "the transition's first frame created blocks");
        }

        [UnityTest]
        public IEnumerator Transition_AllocatesAlmostNothingPerFrame()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(158);
            for (int i = 0; i < 5; i++) d.Advance(Step);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 140; i++) d.Advance(Step);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Less(bytes, 8192, $"{bytes} bytes over 140 transition frames");
        }

        [UnityTest]
        public IEnumerator RealBasketRound_StartsTheTransition_WithItsCount()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.Round.LoadLevel(1);
            yield return WaitUntil(() => d.Round.Current == BasketRound.State.Aim, 3f);
            for (int i = 0; i < 10; i++) yield return null;
            d.Round.ShootProgrammatic(0f, 0.6f);
            yield return WaitUntil(() => d.Current != Phase.Basket, 45f);
            Assert.AreNotEqual(Phase.Basket, d.Current, "round end did not start the transition");
            Assert.AreEqual(d.Round.BasketCount, d.Count);
        }

        [UnityTest]
        public IEnumerator Puzzle_HasPotasLook_AndTheBasketKeepsItsOwn()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            Assert.IsTrue(d.Cam.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.AreEqual(0f, d.Look.Post.weight, "basket phase: no puzzle post");
            yield return null;
            var stack = UnityEngine.Rendering.VolumeManager.instance.stack;
            Assert.AreEqual(0f, stack.GetComponent<Vignette>().intensity.value, 1e-4f, "URP's default profile must not reach the basket");
            Assert.AreEqual(0f, stack.GetComponent<Bloom>().intensity.value, 1e-4f);
            Assert.AreEqual(TonemappingMode.None, stack.GetComponent<Tonemapping>().mode.value);
            Assert.AreEqual(1.2f, d.Game.BoardRoot.localScale.x, 1e-4f);
            Assert.IsNotNull(d.Stage.Reflection);
            d.BeginTransition(158);
            RunTransition(d);
            Assert.AreEqual(1f, d.Look.Post.weight, 1e-4f);
            Assert.AreEqual(1f, d.Look.Tone.weight);
            Assert.AreEqual(38f, d.Cam.fieldOfView, 1e-3f);
            Assert.Less(Vector3.Distance(d.Stage.Hoop.RimCenter, d.Game.transform.position + new Vector3(0f, -5.6f, -2.2f)), 1e-3f);
        }

        [UnityTest]
        public IEnumerator StudioLook_TransitionEndsOnAPlayableBoard_AndTheBurstIsTheLooks()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.Game.SetLook(PuzzleLook.Shatter);
            Assert.IsTrue(d.BeginTransition(158));
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            yield return null;
            Assert.AreEqual(ColorHuntGame.State.Playing, d.Game.Current);
            Assert.IsFalse(d.Game.Intro, "PotaFlow keeps its own reveal");
            var shatter = d.Game.Style as ShatterBurst;
            Assert.IsNotNull(shatter);
            Assert.AreEqual("broken", d.Game.Tap(ColorHuntHint.Best(d.Game.Board)));
            yield return WaitUntil(() => shatter.ActivePieces > 0, 1.5f);
            Assert.Greater(shatter.ActivePieces, 0, "the pieces fly in the giant hoop's space too");
        }

        // ---------- 2026-10-02 candidates ----------

        [UnityTest]
        public IEnumerator Candidates123_EndOnAPlayableBoard_WithTheCountAsAmmo()
        {
            foreach (var c in new[] { FlowCandidate.BigShot, FlowCandidate.Dive, FlowCandidate.Stamp })
            {
                PotaFlowDirector d = null;
                yield return Load(x => d = x);
                d.CandidateOverride = c;
                Assert.IsTrue(d.BeginTransition(158), c.ToString());
                int maxNet = 0, guard = 0;
                bool speedSeen = false, letterSeen = false;
                while (d.Current == Phase.Transition && guard++ < 2000)
                {
                    d.Advance(Step);
                    maxNet = Mathf.Max(maxNet, d.NetStream.ActiveCount);
                    speedSeen |= d.Fx.SpeedLinesShown;
                    letterSeen |= d.Fx.LetterShown;
                    yield return null;
                }
                Assert.AreEqual(Phase.Puzzle, d.Current, c.ToString());
                Assert.LessOrEqual(d.Clock, 3.5f, $"{c}: ready within 3.5 s");
                yield return null;
                Assert.AreEqual(ColorHuntGame.State.Playing, d.Game.Current);
                Assert.AreEqual(158, d.Game.Board.Ammo);
                Assert.AreEqual(158, d.Game.DisplayedAmmo);
                Assert.AreEqual("158", d.Hud.transform.Find("AmmoPanel/AmmoWindow/Ammo").GetComponent<Text>().text);
                Assert.IsFalse(d.Badge.Visible, $"{c}: badge merged");
                Assert.IsFalse(d.Fx.SpeedLinesShown);
                Assert.IsFalse(d.Fx.LetterShown);
                for (int i = 0; i < d.Game.Board.Cells.Length; i++)
                {
                    var v = d.Game.ViewAt(i);
                    Assert.AreEqual(1f, v.Scale, 1e-5f, $"{c} cell {i}");
                    Assert.Less(Vector2.Distance(v.Pos, v.Home), 1e-4f, $"{c} cell {i} home");
                }
                Assert.Less(Vector3.Distance(d.Cam.transform.position, d.BoardCam.FitFor(d.S.BoardFov, d.Cam.aspect)), 0.3f, c.ToString());
                switch (c)
                {
                    case FlowCandidate.BigShot:
                        Assert.AreEqual(d.Bin.RestingCapacity, maxNet, "the whole basket flew to the hoop");
                        Assert.AreEqual(GiantHoop.MaxPile, d.Hoop.Pile, "the net holds its pile");
                        Assert.AreEqual(0, d.Stream.ActiveCount, "the basket emptied");
                        var hero = d.Round.Column.Player.transform;
                        var toHoop = d.Hoop.RimCenter - hero.position; toHoop.y = 0f;
                        Assert.Less(Vector3.Angle(hero.forward, toHoop), 20f, "the hero faces the giant hoop");
                        break;
                    case FlowCandidate.Dive:
                        Assert.IsTrue(speedSeen, "speed lines during the chase");
                        Assert.IsTrue(letterSeen, "SWISH! at the rim");
                        Assert.AreEqual(0, maxNet);
                        Assert.AreEqual(d.Bin.RestingCapacity, d.Bin.Pile);
                        break;
                    case FlowCandidate.Stamp:
                        Assert.AreEqual(0, maxNet);
                        Assert.AreEqual(d.Bin.RestingCapacity, d.Bin.Pile);
                        Assert.AreEqual(0, d.Hoop.Pile);
                        break;
                }
                d.CandidateOverride = null;
            }
        }

        [UnityTest]
        public IEnumerator Candidates123_SkipFinishesQuickly()
        {
            foreach (var c in new[] { FlowCandidate.BigShot, FlowCandidate.Dive, FlowCandidate.Stamp })
            {
                PotaFlowDirector d = null;
                yield return Load(x => d = x);
                d.CandidateOverride = c;
                d.BeginTransition(40);
                for (int i = 0; i < 5; i++) yield return null;
                float t0 = Time.realtimeSinceStartup;
                d.Skip();
                yield return WaitUntil(() => d.Current == Phase.Puzzle, 2f);
                Assert.AreEqual(Phase.Puzzle, d.Current, c.ToString());
                Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 1.0f, c.ToString());
                Assert.AreEqual(40, d.Game.DisplayedAmmo, c.ToString());
                d.CandidateOverride = null;
            }
        }

        [UnityTest]
        public IEnumerator Stamp_BlocksHangAboveTheFrame_ThenLandHome()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.CandidateOverride = FlowCandidate.Stamp;
            d.BeginTransition(10);
            var s = d.S.Timing.Stamp;
            d.Advance(s.RainStart + 0.01f);
            int n = d.Game.Board.Size;
            int topLeft = 0, topRight = n - 1;
            Assert.Greater(d.Game.ViewAt(topLeft).Pos.y - d.Game.ViewAt(topLeft).Home.y, n * 0.9f, "column 0 hangs high");
            Assert.AreEqual(1f, d.Game.ViewAt(topLeft).Scale, 1e-5f, "column 0 is visible");
            Assert.Less(d.Game.ViewAt(topRight).Scale, 0.01f, "the last column has not started");
            d.Advance(s.RainTime);
            Assert.Less(Mathf.Abs(d.Game.ViewAt(topLeft).Pos.y - d.Game.ViewAt(topLeft).Home.y), 1e-3f, "column 0 landed");
            Assert.Greater(d.Game.ViewAt(topLeft).Squash, 0.3f, "landing squash");
            d.CandidateOverride = null;
        }
    }
}
