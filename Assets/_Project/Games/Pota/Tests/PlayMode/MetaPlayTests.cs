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
    }
}
