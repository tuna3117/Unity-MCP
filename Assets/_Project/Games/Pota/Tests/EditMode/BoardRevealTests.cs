using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class BoardRevealTests
    {
        [Test]
        public void Rings_CountOutFromTheCentre()
        {
            Assert.AreEqual(0, BoardReveal.Ring(7, 3, 3));
            Assert.AreEqual(1, BoardReveal.Ring(7, 2, 3));
            Assert.AreEqual(2, BoardReveal.Ring(7, 1, 4));
            Assert.AreEqual(3, BoardReveal.Ring(7, 0, 0));
            Assert.AreEqual(3, BoardReveal.Ring(7, 6, 2));
            Assert.AreEqual(4, BoardReveal.RingCount(7));
            Assert.AreEqual(0, BoardReveal.Ring(6, 2, 3), "even boards: the middle four are ring 0");
            Assert.AreEqual(3, BoardReveal.RingCount(6));
        }

        [Test]
        public void Badge_ShowsTheCount_ScalesAndFades()
        {
            var root = new GameObject("Flow");
            try
            {
                var badge = FlowBadge.Create(root.transform, null, Color.yellow, Color.blue);
                Assert.IsFalse(badge.Visible);
                badge.Show(158);
                Assert.IsTrue(badge.Visible);
                Assert.AreEqual("158 TOP", badge.Label);
                badge.Place(new Vector2(0.25f, 0.75f), 1.2f, 0.5f);
                var label = (RectTransform)badge.transform.Find("Label");
                Assert.AreEqual(new Vector2(0.25f, 0.75f), label.anchorMin, "placed by viewport anchors, whatever the canvas mode");
                Assert.AreEqual(label.anchorMin, label.anchorMax);
                Assert.AreEqual(Vector2.zero, label.anchoredPosition);
                Assert.AreEqual(1.2f, label.localScale.x, 1e-5f);
                Assert.AreEqual(0.5f, label.GetComponent<UnityEngine.UI.Text>().color.a, 1e-3f);
                badge.Hide();
                Assert.IsFalse(badge.Visible);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Settings_DefaultsMatchTheSpec()
        {
            var s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            try
            {
                Assert.AreEqual(FlowCandidate.Stream, s.Candidate);
                Assert.AreEqual(20, s.BasketLevel);
                Assert.AreEqual(new Vector3(0f, 8.5f, 30f), s.BoardAnchor);
                Assert.AreEqual(34f, s.BoardFov);
                Assert.AreEqual(50f, s.MidFov);
                Assert.AreEqual(60f, s.BoardFogStart);
                Assert.AreEqual(220f, s.BoardFogEnd);
                Assert.AreEqual(2.5f, s.Timing.Ready);
            }
            finally { Object.DestroyImmediate(s); }
        }
    }
}
