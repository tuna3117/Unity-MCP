using System.Collections;
using NUnit.Framework;
using Project.Juice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class TimeScaleControllerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Clear_EndsSlowMotionNow_AndItStaysEnded()
        {
            _go = new GameObject("TimeCtl");
            var ctl = _go.AddComponent<TimeScaleController>();
            yield return null;                       // Start records the base fixed step
            float baseFixed = Time.fixedDeltaTime;
            ctl.SlowMo(0.2f, 10f);
            ctl.HitStop(10f);
            Assert.AreEqual(0.2f, Time.timeScale, 1e-4f);
            ctl.Clear();
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "Clear must restore normal time immediately");
            Assert.AreEqual(baseFixed, Time.fixedDeltaTime, 1e-6f);
            yield return null;                       // Update must not bring the old timers back
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Dip_KeepsTheSmallerScale_AndTheLaterEnd()
        {
            _go = new GameObject("TimeCtl");
            var ctl = _go.AddComponent<TimeScaleController>();
            yield return null;
            ctl.Dip(0.12f, 0.05f);                   // tap: renkAviFaz.ts HIS.hitStop
            Assert.AreEqual(0.12f, Time.timeScale, 1e-4f);
            ctl.Dip(0.06f, 0.2f);                    // chain step arriving during the tap's dip
            Assert.AreEqual(0.06f, Time.timeScale, 1e-4f, "smaller scale wins (zamanDusur)");
            ctl.Dip(0.12f, 0.01f);
            Assert.AreEqual(0.06f, Time.timeScale, 1e-4f, "a weaker, shorter dip changes nothing");
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.3f) yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "time always comes back");
            ctl.Dip(0.12f, 0.05f);
            Assert.AreEqual(0.12f, Time.timeScale, 1e-4f, "an expired dip does not keep its old scale");
        }
    }
}
