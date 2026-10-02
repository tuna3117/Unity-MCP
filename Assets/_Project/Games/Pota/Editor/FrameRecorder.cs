using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Offscreen frame capture for the record tools. Camera only: the camera renders into a texture just for each capture
    /// (the Game view keeps working). With UI: the camera renders into the texture for the whole recording and the
    /// Screen Space Overlay canvases move onto it as Screen Space Camera (overlay UI never reaches a camera texture);
    /// <see cref="Dispose"/> puts everything back. Post-processing then also touches the UI: frames are for review.
    /// </summary>
    public sealed class FrameRecorder : IDisposable
    {
        private readonly Camera _cam;
        private readonly bool _includeUi;
        private readonly RenderTexture _rt;
        private readonly Texture2D _tex;
        private readonly List<Canvas> _moved = new List<Canvas>();
        private readonly List<int> _orders = new List<int>();
        /// <summary>With UI and no explicit list: look for overlay canvases created after the start at every capture.</summary>
        private readonly bool _scan;
        /// <summary>On the camera a canvas sorts with the world's transparents; this lifts it over every effect as the overlay was.</summary>
        private const int AboveWorld = 1000;

        public int Width => _rt.width;
        public int Height => _rt.height;

        public FrameRecorder(Camera cam, int width, float aspect, bool includeUi, IEnumerable<Canvas> canvases = null)
        {
            _cam = cam;
            _includeUi = includeUi;
            int w = Mathf.Max(16, width), h = Mathf.Max(16, Mathf.RoundToInt(w / Mathf.Max(0.1f, aspect)));
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "FrameRecorder" };
            _tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            if (!includeUi) return;
            _cam.targetTexture = _rt;
            _scan = canvases == null;
            Adopt(canvases ?? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include));
        }

        /// <summary>Moves the root Screen Space Overlay canvases of <paramref name="list"/> onto the camera, above the world's effects.</summary>
        private void Adopt(IEnumerable<Canvas> list)
        {
            foreach (var c in list)
            {
                if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _cam;
                c.planeDistance = _cam.nearClipPlane + 0.05f;
                _orders.Add(c.sortingOrder);
                c.sortingOrder += AboveWorld;
                _moved.Add(c);
            }
        }

        public void Capture(string path)
        {
            if (_scan) Adopt(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include));   // a canvas built since (Royal's screen flash, a popup canvas)
            var prev = _cam.targetTexture;
            _cam.targetTexture = _rt;
            if (_includeUi) Canvas.ForceUpdateCanvases();
            _cam.Render();
            if (!_includeUi) _cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _tex.Apply(false);
            RenderTexture.active = active;
            File.WriteAllBytes(path, _tex.EncodeToPNG());
        }

        public void Dispose()
        {
            for (int i = 0; i < _moved.Count; i++)
            {
                var c = _moved[i];
                if (c == null) continue;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
                c.sortingOrder = _orders[i];
            }
            _moved.Clear();
            _orders.Clear();
            if (_cam != null && _cam.targetTexture == _rt) _cam.targetTexture = null;
            _rt.Release();
            UnityEngine.Object.DestroyImmediate(_rt);
            UnityEngine.Object.DestroyImmediate(_tex);
        }

        /// <summary>
        /// The editor only steps the player loop while a Game view repaints; with the main window asleep or behind other apps
        /// nothing advances. A small floating Game view keeps it going (pair with pause + EditorApplication.Step()).
        /// </summary>
        public static EditorWindow OpenFloatingGameView(Rect position)
        {
            try
            {
                var type = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (type == null) return null;
                var w = ScriptableObject.CreateInstance(type) as EditorWindow;
                if (w == null) return null;
                w.titleContent = new GUIContent("Game (record)");
                w.ShowUtility();
                w.position = position;
                w.Repaint();
                return w;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AI Tools] floating game view: " + e.Message);
                return null;
            }
        }

        public static void CloseFloatingGameView(EditorWindow w)
        {
            try { if (w != null) w.Close(); }
            catch (Exception e) { Debug.LogWarning("[AI Tools] close floating game view: " + e.Message); }
        }
    }
}
