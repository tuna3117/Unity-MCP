using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Project.Juice;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Turns an animated character delivery (Blender workshop FBX imported Generic by ai_import_asset, clips named by the
    /// manifest) into a ready prefab: root → Body (SquashStretch) → Model (Animator with a generated controller: one state per
    /// clip, the first is the default) and the Project/Toon material. Game-agnostic; Pota's Humanoid ai_character_setup stays
    /// with Pota. Spec: docs/superpowers/specs/2026-10-04-rig-animation-pipeline-design.md §5.6.
    /// </summary>
    [McpForUnityTool("ai_character_prefab", Description =
        "Make a ready prefab from an animated character delivery (fbx/glb imported by ai_import_asset): root -> Body (SquashStretch) -> " +
        "Model (Animator + generated controller, one state per clip in manifest order) + Project/Toon material. model: asset path; " +
        "prefab (default Assets/_Project/Games/<game>/Prefabs/<Id>.prefab); toon (default true); squash (default true). Rebuilding keeps GUIDs.")]
    public static class CharacterPrefabTool
    {
        public class Parameters
        {
            [ToolParameter("Animated character model asset path (fbx/glb)")] public string model { get; set; }
            [ToolParameter("Prefab path (default Assets/_Project/Games/<game>/Prefabs/<Id>.prefab)", Required = false)] public string prefab { get; set; }
            [ToolParameter("Apply the Project/Toon material (default true)", Required = false)] public bool? toon { get; set; }
            [ToolParameter("Add SquashStretch on the Body wrapper (default true)", Required = false)] public bool? squash { get; set; }
        }

        public class Result
        {
            public string prefab, controller, material;
            public List<string> states = new List<string>();
        }

        public static object HandleCommand(JObject p)
        {
            try
            {
                string model = AIToolsCommon.GetString(p, "model");
                if (string.IsNullOrEmpty(model)) return new ErrorResponse("'model' is required.");
                var r = Build(model, AIToolsCommon.GetString(p, "prefab"), AIToolsCommon.GetBool(p, "toon", true), AIToolsCommon.GetBool(p, "squash", true));
                string msg = $"Character prefab {r.prefab}: {r.states.Count} clips ({string.Join(", ", r.states)}), controller {r.controller}.";
                AIToolsCommon.Log(msg);
                return new SuccessResponse(msg, r);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is JsonException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        public static Result Build(string modelPath, string prefabPath = null, bool toon = true, bool squash = true)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new ArgumentException($"No model at '{modelPath}'.");
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string id = Path.GetFileNameWithoutExtension(modelPath);
            string pascal = string.Concat(id.Split('-', '_', ' ').Where(s => s.Length > 0).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
            var manifest = File.Exists(folder + "/manifest.json") ? JObject.Parse(File.ReadAllText(folder + "/manifest.json")) : null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                string game = manifest?.Value<string>("game");
                prefabPath = string.IsNullOrEmpty(game) ? $"{folder}/{pascal}.prefab" : $"Assets/_Project/Games/{game}/Prefabs/{pascal}.prefab";
            }
            var r = new Result { prefab = prefabPath, controller = Path.ChangeExtension(prefabPath, null).Replace('\\', '/') + ".controller" };
            AIToolsCommon.EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var order = (manifest?["clips_meta"] as JArray)?.Select(t => t.Value<string>("name")).ToList();
            clips = clips.OrderBy(c => order != null && order.Contains(c.name) ? order.IndexOf(c.name) : int.MaxValue).ThenBy(c => c.name).ToList();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(r.controller)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(r.controller);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states.ToArray()) machine.RemoveState(old.state);
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name);
                state.motion = clip;
                r.states.Add(clip.name);
            }
            if (machine.states.Length > 0) machine.defaultState = machine.states[0].state;
            EditorUtility.SetDirty(controller);

            var root = new GameObject(pascal);
            try
            {
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                if (squash) body.AddComponent<SquashStretch>();
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(body.transform, false);
                // Not "??": a missing component is a fake-null object in the editor, so ?? never adds one.
                var animator = inst.GetComponent<Animator>();
                if (animator == null) animator = inst.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            if (toon)
            {
                bool textured = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Any(m => ToonMaterialTool.BaseTexture(m) != null);
                var mat = ToonMaterialTool.Apply(prefabPath, null, textured ? modelPath : null);
                r.material = AssetDatabase.GetAssetPath(mat);
            }
            AssetDatabase.SaveAssets();
            return r;
        }
    }
}
