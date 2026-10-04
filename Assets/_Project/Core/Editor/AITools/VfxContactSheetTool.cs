using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.VFX;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Renders an effect prefab at fixed moments of its life into one PNG (top row on a dark, bottom row on a light background)
    /// without Play Mode: particles are simulated to each moment, MeshBursts evaluated, so the sheet is the same every run.
    /// Returns the visible pixel count per moment (dark row) — an empty frame before the end means the effect is broken.
    /// </summary>
    [McpForUnityTool("ai_vfx_contact_sheet", Description =
        "Render an effect prefab at evenly spaced moments of its life into one PNG (dark row + light row), no Play Mode. " +
        "prefab: asset path; frames (default 8); size per frame px (default 256); out (default Temp/mcp_out/<prefab>_sheet.png). " +
        "Returns duration and visible pixels per frame.")]
    public static class VfxContactSheetTool
    {
        public class Parameters
        {
            [ToolParameter("Effect prefab asset path")] public string prefab { get; set; }
            [ToolParameter("Moments to draw, first ≈ 0, last = end of the effect (default 8)", Required = false)] public int? frames { get; set; }
            [ToolParameter("Frame size in pixels (default 256)", Required = false)] public int? size { get; set; }
            [ToolParameter("Output PNG path (default Temp/mcp_out/<prefab>_sheet.png)", Required = false)] public string @out { get; set; }
        }

        public class Result
        {
            public string png;
            public float duration;
            public float[] times;
            public int[] visible;
        }

        private static readonly Color Dark = new Color(0.12f, 0.13f, 0.16f, 1f), Light = new Color(0.78f, 0.8f, 0.84f, 1f);

        public static object HandleCommand(JObject p)
        {
            string path = AIToolsCommon.GetString(p, "prefab");
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return new ErrorResponse($"No prefab at '{path}'.");
            string outPath = AIToolsCommon.GetString(p, "out") ?? $"Temp/mcp_out/{prefab.name}_sheet.png";
            var r = Render(prefab, Mathf.Clamp(AIToolsCommon.GetInt(p, "frames", 8), 2, 24), Mathf.Clamp(AIToolsCommon.GetInt(p, "size", 256), 32, 1024), outPath);
            string msg = $"{prefab.name}: {r.times.Length} frames over {r.duration:0.00} s -> {r.png} (visible px {string.Join(", ", r.visible)})";
            AIToolsCommon.Log("VFX contact sheet: " + msg);
            return new SuccessResponse(msg, r);
        }

        public static Result Render(GameObject prefab, int frames, int size, string outPath)
        {
            var r = new Result();
            var root = new GameObject("__vfxsheet_root") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(-5000f, 5000f, -5000f);
            var inst = Object.Instantiate(prefab, root.transform, false);
            inst.hideFlags = HideFlags.HideAndDontSave;
            inst.SetActive(true);
            var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
            var bursts = inst.GetComponentsInChildren<MeshBurst>(true);
            foreach (var ps in systems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 1;
            }
            r.duration = Duration(systems, bursts);
            r.times = Enumerable.Range(0, frames).Select(i => Mathf.Max(0.02f, r.duration * i / (frames - 1f))).ToArray();
            r.times[frames - 1] = r.duration;

            var lightGo = new GameObject("__vfxsheet_light") { hideFlags = HideFlags.HideAndDontSave };
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var camGo = new GameObject("__vfxsheet_cam") { hideFlags = HideFlags.HideAndDontSave };
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var sheet = new Texture2D(size * frames, size * 2, TextureFormat.RGB24, false);
            var frame = new Texture2D(size, size, TextureFormat.RGB24, false);
            r.visible = new int[frames];
            try
            {
                // Frame the camera on everything the effect covers over its life, then keep that framing.
                // Particle renderer bounds are conservative (a 1 m cloud filled a tenth of the frame): use the particles
                // themselves, and mesh renderer bounds for everything else.
                Bounds? all = null;
                void Add(Bounds b) { if (all == null) all = b; else { var a = all.Value; a.Encapsulate(b); all = a; } }
                var buffer = new ParticleSystem.Particle[256];
                foreach (float t in r.times)
                {
                    Seek(systems, bursts, t);
                    foreach (var ps in systems)
                    {
                        int n = ps.GetParticles(buffer);
                        bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
                        for (int k = 0; k < n; k++)
                        {
                            Vector3 s3 = buffer[k].GetCurrentSize3D(ps);
                            float extent = Mathf.Max(s3.x, s3.y, s3.z) * 1.15f;   // pop overshoot
                            Vector3 pos = world ? buffer[k].position : ps.transform.TransformPoint(buffer[k].position);
                            Add(new Bounds(pos, Vector3.one * extent));
                        }
                    }
                    foreach (var rend in inst.GetComponentsInChildren<MeshRenderer>(true))
                        if (rend.bounds.size.sqrMagnitude > 0f) Add(rend.bounds);
                }
                var bounds = all ?? new Bounds(root.transform.position, Vector3.one);
                float radius = Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z)) * 1.15f;
                float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + bounds.extents.z;
                camGo.transform.position = bounds.center + new Vector3(0f, 0.25f, -1f).normalized * dist;
                camGo.transform.LookAt(bounds.center);

                // The empty background as the camera actually writes it (colour space, tonemapping): a pixel is visible
                // when it differs from that.
                cam.backgroundColor = Dark;
                inst.SetActive(false);
                cam.Render();
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                var bg = frame.GetPixel(0, 0);
                inst.SetActive(true);
                foreach (var ps in systems)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = 1;
                }
                Color32 bg32 = bg;
                for (int row = 0; row < 2; row++)
                {
                    cam.backgroundColor = row == 0 ? Dark : Light;
                    for (int i = 0; i < frames; i++)
                    {
                        Seek(systems, bursts, r.times[i]);
                        cam.Render();
                        RenderTexture.active = rt;
                        frame.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                        frame.Apply();
                        RenderTexture.active = null;
                        var px = frame.GetPixels32();
                        if (row == 0) r.visible[i] = px.Count(c => Mathf.Abs(c.r - bg32.r) + Mathf.Abs(c.g - bg32.g) + Mathf.Abs(c.b - bg32.b) > 12);
                        // Texture rows start at the bottom: the dark row goes on top.
                        sheet.SetPixels32(i * size, (1 - row) * size, size, size, px);
                    }
                }
                sheet.Apply();
                string full = Path.GetFullPath(outPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, sheet.EncodeToPNG());
                r.png = full;
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(frame);
                Object.DestroyImmediate(root);
            }
            return r;
        }

        /// <summary>How long the effect lasts: the longest particle (start delay + lifetime) or MeshBurst.</summary>
        public static float Duration(IEnumerable<ParticleSystem> systems, IEnumerable<MeshBurst> bursts)
        {
            float d = 0f;
            foreach (var ps in systems)
            {
                var main = ps.main;
                float emitEnd = main.loop ? 0f : ps.emission.burstCount > 0 ? 0f : main.duration;
                d = Mathf.Max(d, main.startDelay.constantMax + emitEnd + main.startLifetime.constantMax);
            }
            foreach (var b in bursts) d = Mathf.Max(d, b.Lifetime);
            return d > 0f ? d : 1f;
        }

        private static void Seek(ParticleSystem[] systems, MeshBurst[] bursts, float t)
        {
            foreach (var ps in systems)
                if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                    ps.Simulate(t, true, true, false);   // top-level systems; withChildren covers sub-systems
            foreach (var b in bursts) b.Evaluate(t);
        }
    }
}
