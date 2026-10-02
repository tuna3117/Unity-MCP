using System.Collections;
using NUnit.Framework;
using Project.Juice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>
    /// World-anchored popups land where their world point is on screen, on an overlay canvas (the phone) and on a camera canvas
    /// (a frame recorder moves the overlay canvases onto its camera: overlay UI never reaches a camera texture).
    /// </summary>
    public class PopupTextTests
    {
        private static IEnumerator ShowAtThreeQuarters(RenderMode mode, System.Action<Vector2, Vector2> check)
        {
            var camGo = new GameObject("PopupCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var go = new GameObject("PopupHost");
            try
            {
                var popups = go.AddComponent<PopupText>();                // Awake builds its overlay canvas
                popups.Cam = cam;
                var canvas = popups.Canvas;
                if (mode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1f;
                }
                yield return null;                                         // the canvas lays out in its mode
                popups.Show(cam.ViewportToWorldPoint(new Vector3(0.75f, 0.5f, 10f)), "x", Color.white, 20);
                var popup = (RectTransform)canvas.transform.GetChild(canvas.transform.childCount - 1);
                check(popup.anchoredPosition, ((RectTransform)canvas.transform).rect.size);
            }
            finally
            {
                Object.Destroy(go);
                Object.Destroy(camGo);
            }
        }

        [UnityTest]
        public IEnumerator Show_OnTheOverlayCanvas_PlacesThePopupAtItsWorldPoint()
        {
            yield return ShowAtThreeQuarters(RenderMode.ScreenSpaceOverlay, (at, size) =>
            {
                Assert.AreEqual(size.x * 0.25f, at.x, size.x * 0.02f, "a quarter of the canvas right of its centre");
                Assert.AreEqual(0f, at.y, size.y * 0.02f);
            });
        }

        [UnityTest]
        public IEnumerator Show_OnACameraCanvas_PlacesThePopupAtItsWorldPoint_AsOnTheOverlay()
        {
            yield return ShowAtThreeQuarters(RenderMode.ScreenSpaceCamera, (at, size) =>
            {
                Assert.AreEqual(size.x * 0.25f, at.x, size.x * 0.02f, "a quarter of the canvas right of its centre");
                Assert.AreEqual(0f, at.y, size.y * 0.02f);
            });
        }
    }
}
