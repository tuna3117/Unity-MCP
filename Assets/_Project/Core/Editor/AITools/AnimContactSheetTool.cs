using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Renders a model playing an animation clip as a contact sheet (one row of frames) so the clip can be judged
    /// from a single image without entering Play Mode. Works in Edit Mode through a PlayableGraph.
    /// </summary>
    [McpForUnityTool("ai_anim_contact_sheet", Description =
        "Render a model (fbx/glb/prefab asset path) playing an AnimationClip (asset path; defaults to the first clip inside model_path) " +
        "as a contact sheet PNG: `frames` evenly spaced samples (default 8), `angle_deg` camera yaw (0 = front, 180 = back). " +
        "Returns the written path (default Temp/mcp_out/<clip>.contact.png). clip_name picks a clip of a multi-clip model; frames_dir writes " +
        "every frame at `fps` (default 30) as f_<n>_t<seconds>.png instead of a sheet (for videos); frame_clips frames the camera over those " +
        "clips too, so two clips (raw | stylised) are filmed with the same camera. focus_bone (e.g. LeftHand, RightFoot) follows that bone " +
        "in close-up (focus_size m around it, default 0.14) to check hands and feet.")]
    public static class AnimContactSheetTool
    {
        public class Parameters
        {
            [ToolParameter("Model asset path (fbx/glb/prefab)")] public string model_path { get; set; }
            [ToolParameter("Clip asset path; default = first clip in model_path", Required = false)] public string clip_path { get; set; }
            [ToolParameter("Number of frames (default 8)", Required = false)] public int? frames { get; set; }
            [ToolParameter("Camera yaw in degrees, 0 = front (default 30)", Required = false)] public float? angle_deg { get; set; }
            [ToolParameter("Output png path (default Temp/mcp_out/<clip>.contact.png)", Required = false)] public string out_path { get; set; }
            [ToolParameter("Frame size in pixels (default 256)", Required = false)] public int? size { get; set; }
            [ToolParameter("Clip name inside clip_path/model_path (multi-clip deliveries)", Required = false)] public string clip_name { get; set; }
            [ToolParameter("Write every frame as PNG into this folder instead of a sheet", Required = false)] public string frames_dir { get; set; }
            [ToolParameter("Frames per second for frames_dir (default 30)", Required = false)] public int? fps { get; set; }
            [ToolParameter("Other clip names of the same model to include in the camera framing", Required = false)] public string[] frame_clips { get; set; }
            [ToolParameter("Close-up on this bone (name or suffix, e.g. LeftHand, RightFoot); the camera follows it", Required = false)] public string focus_bone { get; set; }
            [ToolParameter("Close-up half-size around the bone in metres (default 0.14)", Required = false)] public float? focus_size { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string modelPath = AIToolsCommon.GetString(p, "model_path");
            if (string.IsNullOrEmpty(modelPath)) return new ErrorResponse("'model_path' is required.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return new ErrorResponse($"No GameObject asset at '{modelPath}'.");

            string clipPath = AIToolsCommon.GetString(p, "clip_path", modelPath);
            string clipName = AIToolsCommon.GetString(p, "clip_name");
            AnimationClip clip = FindClip(clipPath, clipName);
            if (clip == null) return new ErrorResponse($"No AnimationClip {(clipName == null ? "" : $"'{clipName}' ")}found in '{clipPath}'.");
            var frameWith = new List<AnimationClip>();
            foreach (string other in AIToolsCommon.GetStringList(p, "frame_clips") ?? new List<string>())
                frameWith.Add(FindClip(clipPath, other) ?? throw new System.ArgumentException($"No clip '{other}' in '{clipPath}'."));
            string framesDir = AIToolsCommon.GetString(p, "frames_dir");
            int fps = Mathf.Clamp(AIToolsCommon.GetInt(p, "fps", 30), 1, 120);

            int frames = string.IsNullOrEmpty(framesDir) ? Mathf.Clamp(AIToolsCommon.GetInt(p, "frames", 8), 1, 32)
                : Mathf.Clamp(Mathf.RoundToInt(clip.length * fps) + 1, 1, 1800);
            float yaw = AIToolsCommon.GetFloat(p, "angle_deg", 30f);
            int size = Mathf.Clamp(AIToolsCommon.GetInt(p, "size", 256), 64, 1024);
            string outPath = AIToolsCommon.GetString(p, "out_path");
            if (string.IsNullOrEmpty(outPath)) outPath = $"Temp/mcp_out/{clip.name}.contact.png";

            string result = Render(model, clip, frames, yaw, size, outPath, framesDir, frameWith,
                AIToolsCommon.GetString(p, "focus_bone"), AIToolsCommon.GetFloat(p, "focus_size", 0.14f));
            AIToolsCommon.Log(result);
            return new SuccessResponse(result, new { path = outPath, clip = clip.name, length = clip.length, frames });
        }

        /// <summary>The clip named <paramref name="name"/> in an asset (null name: the first clip).</summary>
        public static AnimationClip FindClip(string assetPath, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview") && (name == null || c.name == name)) return c;
            return null;
        }

        public static string Render(GameObject model, AnimationClip clip, int frames, float yaw, int size, string outPath,
            string framesDir = null, IList<AnimationClip> frameWith = null, string focusBone = null, float focusSize = 0.14f)
        {
            bool perFrame = !string.IsNullOrEmpty(framesDir);
            var root = new GameObject("__contact_root");
            root.transform.position = new Vector3(5000f, 5000f, 5000f);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (inst == null) inst = Object.Instantiate(model);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.hideFlags = HideFlags.HideAndDontSave;
            root.hideFlags = HideFlags.HideAndDontSave;

            var animator = inst.GetComponentInChildren<Animator>();
            Transform focus = null;
            if (!string.IsNullOrEmpty(focusBone))
            {
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                    if (t.name == focusBone || t.name.EndsWith(":" + focusBone) || t.name.EndsWith(focusBone)) { focus = t; break; }
                if (focus == null) { Object.DestroyImmediate(root); throw new System.ArgumentException($"No bone '{focusBone}' in {model.name}."); }
            }
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Skinning normally updates in the player loop, which does not run between graph.Evaluate and cam.Render
            // here: the sheet showed only the root bone's motion (arms stayed down during a shot). Draw CPU-baked
            // copies of the skinned meshes instead (forceMatrixRecalculationPerRender did not help).
            var proxies = new List<(SkinnedMeshRenderer skinned, Transform proxy, Mesh mesh)>();
            foreach (var skinned in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var proxyGo = new GameObject("__contact_baked_" + skinned.name) { hideFlags = HideFlags.HideAndDontSave };
                proxyGo.transform.SetParent(root.transform, false);
                var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                proxyGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                proxyGo.AddComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
                skinned.enabled = false;
                proxies.Add((skinned, proxyGo.transform, mesh));
            }

            var lightGo = new GameObject("__contact_light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(40f, yaw - 30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; light.color = Color.white;
            light.shadows = LightShadows.None;
            lightGo.hideFlags = HideFlags.HideAndDontSave;

            var camGo = new GameObject("__contact_cam");
            camGo.transform.SetParent(root.transform, false);
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 100f;
            cam.cullingMask = ~0;

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var sheet = new Texture2D(perFrame ? size : size * frames, size, TextureFormat.RGB24, false);

            var graph = PlayableGraph.Create("__contact_graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "anim", animator);
            var playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            try
            {
                // Frame the character over every sampled pose (a jump shot's raised hands left the frame when only
                // t=0 was used), then keep that framing for all frames.
                float len = Mathf.Max(clip.length, 0.001f);
                Bounds b = default;
                bool first = true;
                // Framing: this clip, plus the clips it is compared with (same camera for raw | stylised).
                // The same sample count for every clip, so A framed with B and B framed with A get the very same camera.
                var framing = new List<(AnimationClipPlayable playable, float length, int samples)>
                    { (playable, len, frameWith != null && frameWith.Count > 0 ? 24 : Mathf.Min(frames, 48)) };
                foreach (var other in frameWith ?? new List<AnimationClip>())
                    framing.Add((AnimationClipPlayable.Create(graph, other), Mathf.Max(other.length, 0.001f), 24));
                foreach (var (fp, flen, samples) in framing)
                for (int i = 0; i < samples; i++)
                {
                    output.SetSourcePlayable(fp);
                    inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity;
                    fp.SetTime(samples == 1 ? 0f : flen * i / (samples - 1));
                    graph.Evaluate(0f);
                    foreach (var (skinned, proxy, mesh) in proxies)
                    {
                        skinned.BakeMesh(mesh, true);
                        mesh.RecalculateBounds();
                        proxy.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                    }
                    var pose = Bounds(inst);
                    if (proxies.Count > 0)
                    {
                        pose = proxies[0].proxy.GetComponent<MeshRenderer>().bounds;
                        foreach (var (_, proxy, _) in proxies) pose.Encapsulate(proxy.GetComponent<MeshRenderer>().bounds);
                    }
                    if (first) b = pose; else b.Encapsulate(pose);
                    first = false;
                }
                output.SetSourcePlayable(playable);
                float radius = Mathf.Max(b.extents.magnitude, 0.5f);
                Vector3 look = b.center;
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.95f;
                var dir = Quaternion.Euler(8f, yaw, 0f) * Vector3.forward; // a Unity character faces +Z: the camera sits in front of it
                camGo.transform.position = look + dir * dist;
                camGo.transform.LookAt(look);

                var table = new System.Text.StringBuilder("t | hips_y | head_y | lhand_y | rhand_y | lfoot_y\n");
                if (perFrame) Directory.CreateDirectory(framesDir);
                for (int i = 0; i < frames; i++)
                {
                    float t = perFrame ? Mathf.Min(len, i / (float)Mathf.Max(1, frames - 1) * len) : frames == 1 ? 0f : len * i / (frames - 1);
                    inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; // no root-motion drift between samples
                    playable.SetTime(t);
                    graph.Evaluate(0f);
                    if (animator.isHuman)
                        table.Append(t.ToString("0.00")).Append(" | ").Append(H(animator, HumanBodyBones.Hips)).Append(" | ").Append(H(animator, HumanBodyBones.Head)).Append(" | ")
                             .Append(H(animator, HumanBodyBones.LeftHand)).Append(" | ").Append(H(animator, HumanBodyBones.RightHand)).Append(" | ").Append(H(animator, HumanBodyBones.LeftFoot)).Append("\n");
                    foreach (var (skinned, proxy, mesh) in proxies)
                    {
                        skinned.BakeMesh(mesh, true);
                        mesh.RecalculateBounds();
                        proxy.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                    }
                    if (focus != null)
                    {
                        // Close-up: follow the bone (hand, foot) from the same side as the full view.
                        var dirF = Quaternion.Euler(8f, yaw, 0f) * Vector3.forward;
                        float distF = focusSize / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        camGo.transform.position = focus.position + dirF * distF;
                        camGo.transform.LookAt(focus.position);
                    }
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, size, size), perFrame ? 0 : size * i, 0);
                    if (perFrame)
                    {
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(framesDir, $"f_{i:0000}_t{t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}.png"), sheet.EncodeToPNG());
                    }
                }
                RenderTexture.active = null;
                LastTable = table.ToString();
                if (perFrame) return $"Frames '{clip.name}' ({clip.length:0.00}s, {frames} frames, yaw {yaw}) -> {framesDir}\n{LastTable}";
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
                File.WriteAllBytes(outPath, sheet.EncodeToPNG());
                return $"Contact sheet '{clip.name}' ({clip.length:0.00}s, {frames} frames, yaw {yaw}) -> {outPath}\n{LastTable}";
            }
            finally
            {
                graph.Destroy();
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(sheet);
                foreach (var (_, _, mesh) in proxies) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Per-frame bone heights (relative to the model root) of the last render, for judging a clip numerically.</summary>
        public static string LastTable;

        private static string H(Animator a, HumanBodyBones b)
        {
            var tr = a.GetBoneTransform(b);
            return tr == null ? "-" : (tr.position.y - a.transform.position.y).ToString("0.00");
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        [MenuItem("AI Tools/Animation/Contact Sheet From Selection")]
        private static void MenuFromSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning(AIToolsCommon.LogPrefix + "Select a model asset first."); return; }
            string path = AssetDatabase.GetAssetPath(go);
            var r = HandleCommand(new JObject { ["model_path"] = path });
            Debug.Log(AIToolsCommon.LogPrefix + r);
        }
    }
}
