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
    }
}
