using System.Reflection;
using NUnit.Framework;
using Project.Basket;
using UnityEngine;

namespace Project.Tests
{
    public class BasketHudTests
    {
        [Test]
        public void RoundEnd_OpensTheResultPanel_OnlyWhenAllowed()
        {
            var root = new GameObject("UI");
            try
            {
                var hud = BasketHud.Create(root.transform);
                var onEnded = typeof(BasketHud).GetMethod("OnEnded", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(onEnded, "BasketHud.OnEnded(int) not found");
                hud.ResultPanel.SetActive(false);
                hud.ShowResultPanel = false;
                onEnded.Invoke(hud, new object[] { 42 });
                Assert.IsFalse(hud.ResultPanel.activeSelf, "PotaFlow: the basket result panel must stay closed");
                hud.ShowResultPanel = true;
                onEnded.Invoke(hud, new object[] { 42 });
                Assert.IsTrue(hud.ResultPanel.activeSelf, "Basket scene: the panel opens as before");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
