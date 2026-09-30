using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;

namespace Project.Tests
{
    public class FrameRecorderTests
    {
        private const string Dir = "Temp/test_record";

        [Test]
        public void Capture_WritesAPngOfTheRequestedSize()
        {
            var go = new GameObject("RecCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.red;
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "frame.png");
            try
            {
                using (var rec = new FrameRecorder(cam, 64, 0.5f, false)) rec.Capture(path);
                Assert.IsNull(cam.targetTexture, "camera-only mode leaves the camera as it was");
                var tex = new Texture2D(2, 2);
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)));
                Assert.AreEqual(64, tex.width);
                Assert.AreEqual(128, tex.height);
                Assert.Greater(tex.GetPixel(32, 64).r, 0.6f, "red clear colour");
                Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void WithUi_AlsoMovesCanvasesThatAreDisabledForNow()
        {
            // PotaFlow's "N TOP" badge canvas is disabled until the transition starts, after the recorder was created.
            var camGo = new GameObject("RecCam");
            var cam = camGo.AddComponent<Camera>();
            var canvasGo = new GameObject("Badge", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.enabled = false;
            try
            {
                using (new FrameRecorder(cam, 64, 1f, true))
                    Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode, "a disabled overlay canvas must be moved too");
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void WithUi_MovesOverlayCanvasesOntoTheCamera_AndBack()
        {
            var camGo = new GameObject("RecCam");
            var cam = camGo.AddComponent<Camera>();
            var canvasGo = new GameObject("Hud", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                using (new FrameRecorder(cam, 64, 1f, true, new[] { canvas }))
                {
                    Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
                    Assert.AreEqual(cam, canvas.worldCamera);
                    Assert.IsNotNull(cam.targetTexture);
                }
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(cam.targetTexture);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(camGo);
            }
        }


        [Test]
        public void WithUi_MovedCanvasesStayAboveWorldEffects_AndKeepTheirOrder()
        {
            // an overlay HUD draws over every world particle; on the camera it must still, whatever the particles' sorting order
            var camGo = new GameObject("RecCam");
            var cam = camGo.AddComponent<Camera>();
            var hudGo = new GameObject("Hud", typeof(Canvas));
            var hud = hudGo.GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder = 0;
            var cardGo = new GameObject("Card", typeof(Canvas));
            var card = cardGo.GetComponent<Canvas>();
            card.renderMode = RenderMode.ScreenSpaceOverlay;
            card.sortingOrder = 5;
            try
            {
                using (new FrameRecorder(cam, 64, 1f, true, new[] { hud, card }))
                {
                    Assert.GreaterOrEqual(hud.sortingOrder, 1000, "above the effects' orders (5–9)");
                    Assert.Greater(card.sortingOrder, hud.sortingOrder, "overlay order kept");
                }
                Assert.AreEqual(0, hud.sortingOrder);
                Assert.AreEqual(5, card.sortingOrder);
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
