using System;
using System.Collections;
using NUnit.Framework;
using Project.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The menu/reward showcase (Scenes/Meta.unity): screens, windows and the feel moments M1–M11.</summary>
    public class MetaPlayTests
    {
        public static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        public static IEnumerator Wait(float seconds) => WaitUntil(() => false, seconds);

        public static IEnumerator Load(Action<MetaDirector> ready)
        {
            SceneManager.LoadScene("Meta");
            yield return null;
            yield return null;
            var d = Object.FindAnyObjectByType<MetaDirector>();
            Assert.IsNotNull(d, "MetaDirector missing: run ai_meta_build_scene");
            d.S.Feel.Enabled = true;
            d.ResetSave();
            yield return null;
            ready(d);
        }

        [UnityTest]
        public IEnumerator Menu_ShowsTheSampleSave()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            Assert.AreEqual(MetaDirector.Screen.Menu, d.Current);
            Assert.AreEqual("Bölüm 20", d.Menu.PlayLabel);
            Assert.AreEqual("145", d.Menu.CoinsText);
            Assert.AreEqual("11,8K", d.Menu.TrophiesText);
            Assert.AreEqual("3", d.Menu.LivesText);
            Assert.IsNotNull(d.Menu.Background.sprite);
        }

        [UnityTest]
        public IEnumerator Menu_Lives_HeroBreathes_PlayShines()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            float s0 = d.Menu.HeroScale;
            yield return Wait(1.0f);
            Assert.AreNotEqual(s0, d.Menu.HeroScale, "the hero breathes");
            bool shone = false;
            float t = 0f;
            while (t < 3f && !shone) { shone = d.Menu.ShineVisible; t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(shone, "the play button's shine swept within a period");
        }

        [UnityTest]
        public IEnumerator Tabs_SlideTheMapInAndOut()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Tabs.Select(TabBar.Tab.Map);
            yield return null;
            Assert.IsTrue(d.MapPage.Visible);
            Assert.IsTrue(d.MapPage.Moving, "it slides in");
            yield return WaitUntil(() => !d.MapPage.Moving, d.S.Feel.Slide + 0.3f);
            Assert.IsFalse(d.MapPage.Moving);
            Assert.AreEqual(0f, d.MapPage.Offset, 1f);
            Assert.AreEqual(MetaDirector.Screen.Map, d.Current);
            d.Tabs.Select(TabBar.Tab.Home);
            yield return WaitUntil(() => !d.MapPage.Visible, d.S.Feel.Slide + 0.3f);
            Assert.IsFalse(d.MapPage.Visible);
            Assert.AreEqual(MetaDirector.Screen.Menu, d.Current);
        }

        [UnityTest]
        public IEnumerator Tabs_FeelOff_SwitchInstantly()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.S.Feel.Enabled = false;
            d.Tabs.Select(TabBar.Tab.Map);
            yield return null;
            Assert.IsTrue(d.MapPage.Visible);
            Assert.IsFalse(d.MapPage.Moving, "Pota: instant");
            d.Tabs.Select(TabBar.Tab.Home);
            yield return null;
            Assert.IsFalse(d.MapPage.Visible);
            d.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Settings_OpensWithASpring_AndCloses()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.OpenSettings();
            yield return null;
            Assert.IsTrue(d.Settings.IsOpen);
            Assert.Less(d.Settings.BoxScale, 0.95f, "starts small");
            yield return WaitUntil(() => d.Settings.State == MetaWindow.Phase.Open, d.S.Feel.WindowOpen + 0.3f);
            Assert.AreEqual(1f, d.Settings.BoxScale, 1e-3f);
            Assert.AreEqual(0.62f, d.Settings.BackdropAlpha, 0.02f);
            d.Settings.Hide();
            yield return WaitUntil(() => d.Settings.State == MetaWindow.Phase.Closed, d.S.Feel.WindowClose + 0.3f);
            Assert.IsFalse(d.Settings.IsOpen);
        }

        [UnityTest]
        public IEnumerator Toast_RisesHoldsAndLeaves()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Toast.Show("Deneme");
            yield return null;
            Assert.IsTrue(d.Toast.Showing);
            Assert.AreEqual("Deneme", d.Toast.Message);
            yield return WaitUntil(() => !d.Toast.Showing, d.S.Feel.ToastIn + d.S.Feel.ToastHold + d.S.Feel.ToastOut + 0.5f);
            Assert.IsFalse(d.Toast.Showing);
        }

        [UnityTest]
        public IEnumerator Button_PressSinks_ReleaseSprings()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            var b = d.Menu.Play;
            b.OnPointerDown(null);
            yield return Wait(d.S.Feel.Press + 0.05f);
            Assert.AreEqual(0.94f, b.Scale, 0.01f);
            b.OnPointerUp(null);
            float most = 0f, t = 0f;
            while (t < d.S.Feel.Release + 0.1f) { most = Mathf.Max(most, b.Scale); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 1.02f);
            Assert.AreEqual(1f, b.Scale, 1e-3f);
        }

        // ---------- Task 3: level, win, lose windows ----------

        [UnityTest]
        public IEnumerator LevelWindow_OpensFromPlay_ShowsGoalsAmmoReward()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Menu.Play.Tap();
            yield return null;
            Assert.AreEqual(MetaDirector.Screen.Level, d.Current);
            Assert.IsTrue(d.Level.Window.IsOpen);
            Assert.AreEqual("Bölüm 20", d.Level.Window.Title.text);
            Assert.AreEqual("Oyna · 158 top", d.Level.Play.Label.text);
            yield return WaitUntil(() => d.Level.Window.State == MetaWindow.Phase.Open, 1f);
            d.Level.Window.Hide();
            yield return WaitUntil(() => d.Level.Window.State == MetaWindow.Phase.Closed, 1f);
            Assert.AreEqual(MetaDirector.Screen.Menu, d.Current);
        }

        [UnityTest]
        public IEnumerator Win_CountsTheScore_EscalatesTheMedal_RollsCoins_PulsesContinue()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Win.Tap();
            yield return null;
            Assert.AreEqual(MetaDirector.Screen.Win, d.Current);
            var w = d.Win;
            Assert.AreEqual("Bölüm 20 bitti", w.Window.Title.text);
            float f0 = d.S.Feel.ScoreAt + d.S.Feel.ScoreTime + 0.3f;
            yield return Wait(f0);
            Assert.AreEqual(w.Sequence.Score.ToString(), w.ScoreText, "the count-up reached the total");
            Assert.AreEqual(w.Sequence.FinalMedal, w.MedalShown, "the medal the score earns");
            yield return WaitUntil(() => !d.Coins.Running && w.Elapsed > d.S.Feel.ContinueAt + 0.2f, 4f);
            Assert.AreEqual(d.S.Save.Coins + w.Sequence.CoinsGained, d.Menu.Coins.Value, "the strip's coins rolled up by the reward");
            Assert.IsTrue(w.ContinuePulsing);
            Assert.AreEqual(d.S.Save.Coins + w.Sequence.CoinsGained, d.Save.Coins);
        }

        [UnityTest]
        public IEnumerator Win_FeelOff_IsPotasInstantWindow()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.S.Feel.Enabled = false;
            d.Dev.Win.Tap();
            yield return null;
            yield return null;
            var w = d.Win;
            Assert.AreEqual(w.Sequence.Score.ToString(), w.ScoreText, "no count-up");
            Assert.AreEqual(w.Sequence.FinalMedal, w.MedalShown);
            Assert.IsFalse(w.ContinuePulsing);
            Assert.AreEqual(d.S.Save.Coins + w.Sequence.CoinsGained, d.Menu.Coins.Value, "coins credited at once");
            d.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Win_FeelTurnedOffMidSequence_LandsOnTheFinalState()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Win.Tap();
            yield return Wait(0.6f);
            d.S.Feel.Enabled = false;
            yield return null;
            yield return null;
            var w = d.Win;
            Assert.AreEqual(w.Sequence.Score.ToString(), w.ScoreText);
            Assert.AreEqual(w.Sequence.FinalMedal, w.MedalShown);
            Assert.IsFalse(d.Coins.Running, "no coins left in the air");
            Assert.AreEqual(d.S.Save.Coins + w.Sequence.CoinsGained, d.Menu.Coins.Value);
            d.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Lose_ShakesIn_StampsTheRemaining_TagsANearMiss_PulsesTheOrangeButton()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Lose.Tap();
            yield return null;
            Assert.AreEqual(MetaDirector.Screen.Lose, d.Current);
            var l = d.Lose;
            Assert.AreEqual("Cephane bitti", l.Window.Title.text);
            Assert.AreEqual("3", l.RemainingText);
            Assert.IsTrue(l.NearMissShown, "3 left is a near miss");
            yield return Wait(0.6f);
            Assert.Greater(l.ShakePeak, 1f, "the box shook");
            Assert.AreEqual(0f, l.ShakeOffset, 1e-3f, "and settled");
            yield return Wait(d.S.Feel.LosePulseAt);
            Assert.IsTrue(l.Recommended.Pulsing);
            Assert.AreEqual(d.S.Save.Lives - 1, d.Save.Lives);
        }

        // ---------- Task 4: map progression ----------

        private static Vector2 SenYeri(MetaDirector d, int n)
        {
            int last = d.S.MapLevels;
            return MapLayout.SenYeri(MapLayout.Node(n, MapScreen.Width, last), n > 1 ? MapLayout.Node(n - 1, MapScreen.Width, last) : (Vector2?)null,
                n < last ? MapLayout.Node(n + 1, MapScreen.Width, last) : (Vector2?)null, MapScreen.Width);
        }

        [UnityTest]
        public IEnumerator Map_ShowsTheSave_CurrentNodePulses_PinBesideIt()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Tabs.Select(TabBar.Tab.Map);
            yield return WaitUntil(() => !d.MapPage.Moving, 1f);
            Assert.AreEqual(MapScreen.NodeState.Passed, d.Map.StateOf(19));
            Assert.AreEqual(MapScreen.NodeState.Current, d.Map.StateOf(20));
            Assert.AreEqual(MapScreen.NodeState.Locked, d.Map.StateOf(21));
            var pin = d.Map.PinPosition; var expected = SenYeri(d, 20);
            Assert.AreEqual(expected.x, pin.x, 0.5f, "the pin stands beside its node (Pota senYeri)");
            Assert.AreEqual(expected.y, pin.y, 0.5f);
            float s0 = d.Map.NodeScale(20);
            yield return Wait(0.5f);
            Assert.AreNotEqual(s0, d.Map.NodeScale(20), "the current node breathes (Pota nabiz)");
        }

        [UnityTest]
        public IEnumerator Map_AfterDevam_ProgressesToTheNewNode()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Win.Tap();
            yield return WaitUntil(() => d.Win.Elapsed > d.S.Feel.ContinueAt, 4f);
            d.Win.Continue.Tap();
            yield return WaitUntil(() => d.Current == MetaDirector.Screen.Map, 1f);
            Assert.AreEqual(MetaDirector.Screen.Map, d.Current);
            Assert.AreEqual(TabBar.Tab.Map, d.Tabs.Current);
            Assert.IsTrue(d.Map.Progressing);
            Assert.AreEqual(MapScreen.NodeState.Locked, d.Map.StateOf(21), "still locked while it plays");
            yield return WaitUntil(() => !d.Map.Progressing, 4f);
            Assert.AreEqual(MapScreen.NodeState.Passed, d.Map.StateOf(20));
            Assert.AreEqual(MapScreen.NodeState.Current, d.Map.StateOf(21));
            Assert.AreEqual(d.LastWin.MedalIndex, d.Map.MedalOf(20));
            var pin = d.Map.PinPosition; var expected = SenYeri(d, 21);
            Assert.AreEqual(expected.x, pin.x, 0.5f, "the pin landed beside the new node");
            Assert.AreEqual(expected.y, pin.y, 0.5f);
            Assert.AreEqual(21, d.Map.CentredNode, "the scroll ends on the new node");
            Assert.AreEqual(21, d.Save.Level);
        }

        [UnityTest]
        public IEnumerator Map_FeelOff_DevamReturnsToTheMenu()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.S.Feel.Enabled = false;
            d.Dev.Win.Tap();
            yield return null;
            d.Win.Continue.Tap();
            yield return null;
            yield return null;
            Assert.AreEqual(MetaDirector.Screen.Menu, d.Current, "Pota: Devam goes home");
            Assert.AreEqual(MapScreen.NodeState.Current, d.Map.StateOf(21), "the map is already up to date");
            d.S.Feel.Enabled = true;
        }

        // ---------- Task 5: promotion, bonus, market ----------

        [UnityTest]
        public IEnumerator Promotion_NewBadgeOvershoots_OldBadgeLeaves()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Promotion.Tap();
            yield return null;
            Assert.AreEqual(MetaDirector.Screen.Promotion, d.Current);
            var p = d.Promotion;
            Assert.AreEqual("Yeni lig!", p.Window.Title.text);
            Assert.AreEqual("Amatör", p.LeagueName);
            float peak = 0f, t = 0f;
            while (t < 1.2f) { peak = Mathf.Max(peak, p.NewBadgeScale); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(peak, 1.15f, "the new badge overshoots");
            Assert.AreEqual(1f, p.NewBadgeScale, 0.02f, "and settles");
            Assert.IsFalse(p.OldBadgeVisible);
            Assert.AreEqual(1, d.S.LeagueOf(d.Save.Trophies));
            p.Continue.Tap();
            yield return WaitUntil(() => d.Current != MetaDirector.Screen.Promotion, 1f);
            Assert.AreEqual("12,4K", d.Menu.TrophiesText);
        }

        [UnityTest]
        public IEnumerator League_SenRowSlides_ToTheNewLeague()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.OpenLeague(0);
            yield return null;
            Assert.IsTrue(d.League.Window.IsOpen);
            Assert.AreEqual("Çaylak", d.League.Rows[4].Name.text, "Çaylak at the bottom, Şampiyonlar on top");
            Assert.AreEqual(0, d.League.SenRow);
            d.League.Window.Hide();
            yield return WaitUntil(() => d.League.Window.State == MetaWindow.Phase.Closed, 1f);
            d.Dev.Promotion.Tap();
            yield return null;
            d.Promotion.Continue.Tap();
            yield return WaitUntil(() => d.Current != MetaDirector.Screen.Promotion, 1f);
            d.OpenLeague(0);
            yield return null;
            Assert.IsTrue(d.League.SenMoving, "the SEN pill slides from the old row");
            yield return WaitUntil(() => !d.League.SenMoving, 1f);
            Assert.AreEqual(1, d.League.SenRow);
        }

        [UnityTest]
        public IEnumerator Bonus_ReelsStopInOrder_WinCellsGlow_CoinsFlyToTheCard()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Dev.Bonus.Tap();
            yield return null;
            var b = d.Bonus;
            Assert.AreEqual(MetaDirector.Screen.Bonus, d.Current);
            Assert.AreEqual("BONUS OYNA · 3", b.Play.Label.text);
            b.Play.Tap();
            yield return null;
            Assert.IsTrue(b.Spinning);
            int stoppedBefore = 0;
            yield return WaitUntil(() => b.StoppedColumns >= 3, 3f);
            stoppedBefore = b.StoppedColumns;
            Assert.IsTrue(b.Spinning, "the right columns still spin");
            Assert.GreaterOrEqual(stoppedBefore, 3);
            yield return WaitUntil(() => !b.Spinning, 3f);
            Assert.AreEqual("BONUS OYNA · 2", b.Play.Label.text);
            Assert.Greater(b.Winnings.Coins, 0, "the showcase seed pays on the first spin");
            Assert.Greater(b.GlowingCells, 0, "the winning cells glow");
            Assert.IsTrue(b.CoinsFlew, "coins flew to the result card");
            yield return WaitUntil(() => !d.Coins.Running, 3f);
            StringAssert.Contains(b.Winnings.Coins.ToString(), b.ResultText);
            Assert.AreEqual(d.S.Save.Coins, d.Menu.Coins.Value, "winnings wait for the level (Pota)");
        }

        [UnityTest]
        public IEnumerator Market_Buy_CoinsFlyToTheChip_ChipStamped_HeroHops_InsufficientShakes()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.Tabs.Select(TabBar.Tab.Market);
            yield return WaitUntil(() => !d.MarketPage.Moving, 1f);
            var m = d.Market;
            Assert.AreEqual(MetaDirector.Screen.Market, d.Current);
            m.TapChip(1);                                        // forma-kirmizi, 120
            yield return null;
            Assert.AreEqual(1, m.Tried);
            Assert.AreEqual("Kırmızı forma · 120", m.Big.Label.text);
            m.Big.Tap();
            yield return null;
            Assert.IsTrue(m.Purchasing);
            yield return WaitUntil(() => !m.Purchasing, 3f);
            Assert.AreEqual(25, d.Save.Coins);
            Assert.AreEqual(25, d.Menu.Coins.Value);
            Assert.AreEqual("Giyili", m.ChipLabel(1));
            Assert.AreEqual("Giyili", m.Big.Label.text);
            Assert.Greater(m.HeroHopPeak, 0.5f, "the hero jumped (Pota kutla)");
            m.TapChip(4);                                        // forma-siyah, 160 > 25
            yield return null;
            m.Big.Tap();
            yield return Wait(0.5f);
            Assert.IsFalse(m.Purchasing);
            Assert.Greater(m.ShakePeak, 1f, "the big button shook");
            Assert.AreEqual(25, d.Save.Coins);
            Assert.AreEqual("160", m.ChipLabel(4));
        }

        [UnityTest]
        public IEnumerator Market_FeelOff_BuysInstantly()
        {
            MetaDirector d = null;
            yield return Load(x => d = x);
            d.S.Feel.Enabled = false;
            d.Tabs.Select(TabBar.Tab.Market);
            yield return null;
            d.Market.TapChip(2);
            yield return null;
            d.Market.Big.Tap();
            yield return null;
            yield return null;
            Assert.IsFalse(d.Market.Purchasing);
            Assert.AreEqual(25, d.Menu.Coins.Value);
            Assert.AreEqual("Giyili", d.Market.ChipLabel(2));
            d.S.Feel.Enabled = true;
        }
    }
}
