using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Turns an auto-rigged character (Mixamo bone names, e.g. from Uthana create-character or Mixamo) into a game-ready
    /// Humanoid prefab: T-poses the arms, builds a Humanoid Avatar, creates an AnimatorController from a clip folder and
    /// saves a prefab with an Animator. Works for glb (glTFast) and fbx sources.
    /// </summary>
    [McpForUnityTool("ai_character_setup", Description =
        "Build a Humanoid player prefab from a rigged model (Mixamo bone names) and a folder of Humanoid clips. " +
        "model_path: rigged glb/fbx; anim_folder (default Assets/_Project/Art/Pota/Anim); clips: map state->clip asset name " +
        "(default Idle=player_idle2, Run=player_run, Shoot=player_shoot2, Dunk=player_dunk2); prefab_path (default Assets/_Project/Prefabs/PlayerCharacter.prefab); " +
        "assign_settings=true also sets BasketSettings.PlayerPrefab.")]
    public static class CharacterSetupTool
    {
        public class Parameters
        {
            [ToolParameter("Rigged model asset path (glb/fbx with mixamorig:* bones)")] public string model_path { get; set; }
            [ToolParameter("Folder with the Humanoid animation fbx files", Required = false)] public string anim_folder { get; set; }
            [ToolParameter("State name -> clip name map, e.g. {\"Idle\":\"player_idle2\",\"Shoot\":\"player_shoot2\"}", Required = false)] public JObject clips { get; set; }
            [ToolParameter("Prefab output path", Required = false)] public string prefab_path { get; set; }
            [ToolParameter("Also assign BasketSettings.PlayerPrefab (default true)", Required = false)] public bool? assign_settings { get; set; }
        }

        public const string DefaultAnimFolder = "Assets/_Project/Art/Pota/Anim";
        public const string DefaultPrefabPath = "Assets/_Project/Prefabs/PlayerCharacter.prefab";
        public const string ControllerFolder = "Assets/_Project/Anim";

        private static readonly Dictionary<string, string> DefaultClips = new Dictionary<string, string>
        {
            { "Idle", "player_idle2" }, { "Run", "player_run" }, { "Shoot", "player_shoot2" }, { "Dunk", "player_dunk2" },
        };

        public static object HandleCommand(JObject p)
        {
            string modelPath = AIToolsCommon.GetString(p, "model_path");
            if (string.IsNullOrEmpty(modelPath)) return new ErrorResponse("'model_path' is required.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return new ErrorResponse($"No model at '{modelPath}'.");

            string animFolder = AIToolsCommon.GetString(p, "anim_folder", DefaultAnimFolder);
            string prefabPath = AIToolsCommon.GetString(p, "prefab_path", DefaultPrefabPath);
            bool assign = AIToolsCommon.GetBool(p, "assign_settings", true);
            var clipMap = new Dictionary<string, string>(DefaultClips);
            if (p?["clips"] is JObject cj)
                foreach (var kv in cj) clipMap[kv.Key] = kv.Value.ToString();

            var report = new List<string>();
            var prefab = Build(model, animFolder, clipMap, prefabPath, assign, report);
            string msg = string.Join("\n", report);
            AIToolsCommon.Log(msg);
            if (prefab == null) return new ErrorResponse(msg);
            return new SuccessResponse($"Character prefab built: {prefabPath}", new { prefab = prefabPath, steps = report });
        }

        public static GameObject Build(GameObject model, string animFolder, Dictionary<string, string> clipMap, string prefabPath, bool assignSettings, List<string> report)
        {
            string modelPath = AssetDatabase.GetAssetPath(model);
            string baseName = Path.GetFileNameWithoutExtension(modelPath);
            string modelFolder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');

            // 1. Instance + T-pose (the auto-rig is usually an A-pose; Unity's Humanoid retargeting expects a T-pose reference).
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = Path.GetFileNameWithoutExtension(prefabPath);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            var bones = inst.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
            if (!bones.ContainsKey("mixamorig:Hips")) { report.Add("no mixamorig:Hips bone found"); Object.DestroyImmediate(inst); return null; }
            TPoseArm(bones, "Left", Vector3.left, report);
            TPoseArm(bones, "Right", Vector3.right, report);
            GroundFeet(inst, report);

            // 2. Humanoid avatar.
            var desc = new HumanDescription
            {
                human = HumanBones(bones, report),
                skeleton = inst.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                {
                    name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(inst, desc);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                report.Add($"avatar build failed: valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");
                Object.DestroyImmediate(inst); return null;
            }
            avatar.name = baseName + "Avatar";
            string avatarPath = $"{modelFolder}/{baseName}.avatar.asset";
            AssetDatabase.DeleteAsset(avatarPath); // CreateAsset over a live asset leaves a broken (non-human) avatar behind
            AssetDatabase.CreateAsset(avatar, avatarPath);
            AssetDatabase.SaveAssets();
            avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            report.Add($"avatar reloaded: valid={avatar.isValid} human={avatar.isHuman}");
            report.Add($"avatar -> {avatarPath} (human bones {desc.human.Length})");

            // 3. Animator controller.
            AIToolsCommon.EnsureFolder(ControllerFolder);
            string controllerPath = $"{ControllerFolder}/{inst.name}.controller";
            var controller = BuildController(controllerPath, animFolder, clipMap, report);

            // 4. Animator + prefab.
            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

            AIToolsCommon.EnsureFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            Object.DestroyImmediate(inst);
            report.Add($"prefab -> {prefabPath}");

            if (assignSettings)
            {
                var settings = AssetDatabase.LoadAssetAtPath<BasketSettings>(BasketSceneTool.SettingsPath);
                if (settings != null)
                {
                    settings.PlayerPrefab = prefab;
                    EditorUtility.SetDirty(settings);
                    report.Add("BasketSettings.PlayerPrefab assigned");
                }
                else report.Add("BasketSettings not found, prefab not assigned");
            }
            AssetDatabase.SaveAssets();
            // The prefab can be imported before the freshly created avatar/controller are: reimport so the in-memory
            // prefab resolves both references (they were correct on disk but null in memory otherwise).
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var check = prefab.GetComponent<Animator>();
            report.Add($"prefab animator: avatar={(check.avatar != null ? check.avatar.name : "null")} human={check.isHuman} controller={(check.runtimeAnimatorController != null ? check.runtimeAnimatorController.name : "null")}");
            return prefab;
        }

        /// <summary>
        /// Lifts the skeleton so the lowest mesh point sits at the root's y = 0. Unity's Humanoid retargeting measures
        /// the hips height from the root; a rig whose hips sit at the root (feet below it) folds the legs up.
        /// </summary>
        private static void GroundFeet(GameObject inst, List<string> report)
        {
            float minY = float.MaxValue;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var b = smr.sharedMesh.bounds;
                foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.min.z) })
                    minY = Mathf.Min(minY, inst.transform.InverseTransformPoint(smr.transform.TransformPoint(corner)).y);
            }
            if (minY == float.MaxValue) { report.Add("no skinned mesh, feet not grounded"); return; }
            int moved = 0;
            foreach (Transform child in inst.transform)
            {
                if (child.GetComponent<Renderer>() != null) continue; // bones only; the skinned mesh follows its bones
                child.localPosition += Vector3.up * -minY;
                moved++;
            }
            report.Add($"feet grounded: skeleton lifted by {-minY:0.000} ({moved} root bones)");
        }

        /// <summary>Rotates upper arm and forearm so the arm points straight along `side` (T-pose), keeping the mesh skinned.</summary>
        private static void TPoseArm(Dictionary<string, Transform> bones, string side, Vector3 dir, List<string> report)
        {
            if (!bones.TryGetValue($"mixamorig:{side}Arm", out var upper) || !bones.TryGetValue($"mixamorig:{side}ForeArm", out var fore) || !bones.TryGetValue($"mixamorig:{side}Hand", out var hand))
            { report.Add($"{side} arm bones missing, no T-pose"); return; }
            var before = Vector3.Angle(fore.position - upper.position, dir);
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, dir) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, dir) * fore.rotation;
            report.Add($"{side} arm T-posed ({before:0}° -> {Vector3.Angle(fore.position - upper.position, dir):0}°)");
        }

        private static HumanBone[] HumanBones(Dictionary<string, Transform> bones, List<string> report)
        {
            var map = new Dictionary<string, string>
            {
                { "Hips", "Hips" }, { "Spine", "Spine" }, { "Spine1", "Chest" }, { "Spine2", "UpperChest" }, { "Neck", "Neck" }, { "Head", "Head" },
            };
            foreach (var side in new[] { "Left", "Right" })
            {
                map[$"{side}Shoulder"] = $"{side}Shoulder";
                map[$"{side}Arm"] = $"{side}UpperArm";
                map[$"{side}ForeArm"] = $"{side}LowerArm";
                map[$"{side}Hand"] = $"{side}Hand";
                map[$"{side}UpLeg"] = $"{side}UpperLeg";
                map[$"{side}Leg"] = $"{side}LowerLeg";
                map[$"{side}Foot"] = $"{side}Foot";
                map[$"{side}ToeBase"] = $"{side}Toes";
                var fingers = new Dictionary<string, string> { { "Thumb", "Thumb" }, { "Index", "Index" }, { "Middle", "Middle" }, { "Ring", "Ring" }, { "Pinky", "Little" } };
                var joints = new[] { "Proximal", "Intermediate", "Distal" };
                foreach (var f in fingers)
                    for (int i = 0; i < 3; i++)
                        map[$"{side}Hand{f.Key}{i + 1}"] = $"{side} {f.Value} {joints[i]}";
            }
            var valid = new HashSet<string>(HumanTrait.BoneName);
            var list = new List<HumanBone>();
            int missing = 0;
            foreach (var kv in map)
            {
                if (!valid.Contains(kv.Value)) { report.Add($"unknown human bone name {kv.Value}"); continue; }
                if (!bones.ContainsKey("mixamorig:" + kv.Key)) { missing++; continue; }
                var hb = new HumanBone { humanName = kv.Value, boneName = "mixamorig:" + kv.Key, limit = new HumanLimit { useDefaultValues = true } };
                list.Add(hb);
            }
            if (missing > 0) report.Add($"{missing} optional bones not present in the rig");
            return list.ToArray();
        }

        private static AnimatorController BuildController(string path, string animFolder, Dictionary<string, string> clipMap, List<string> report)
        {
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Run", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dunk", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (var kv in clipMap)
            {
                var clip = FindClip(animFolder, kv.Value);
                if (clip == null) { report.Add($"clip '{kv.Value}' not found in {animFolder}"); continue; }
                var st = sm.AddState(kv.Key);
                st.motion = clip;
                states[kv.Key] = st;
                report.Add($"state {kv.Key} <- {clip.name} ({clip.length:0.00}s, loop={clip.isLooping})");
            }
            if (states.TryGetValue("Idle", out var idle)) sm.defaultState = idle;
            if (idle != null && states.TryGetValue("Run", out var run))
            {
                var toRun = idle.AddTransition(run); toRun.hasExitTime = false; toRun.duration = 0.15f; toRun.AddCondition(AnimatorConditionMode.If, 0, "Run");
                var toIdle = run.AddTransition(idle); toIdle.hasExitTime = false; toIdle.duration = 0.15f; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Run");
            }
            foreach (var trig in new[] { "Shoot", "Dunk" })
            {
                if (idle == null || !states.TryGetValue(trig, out var st)) continue;
                var any = sm.AddAnyStateTransition(st); any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false; any.AddCondition(AnimatorConditionMode.If, 0, trig);
                var back = st.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.2f;
            }
            AssetDatabase.SaveAssets();
            report.Add($"controller -> {path}");
            return controller;
        }

        private static AnimationClip FindClip(string folder, string clipName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is AnimationClip c && c.name == clipName) return c;
            }
            return null;
        }

        [MenuItem("AI Tools/Character/Build Player Prefab From Selected Rig")]
        private static void MenuBuild()
        {
            var go = Selection.activeObject as GameObject;
            if (go == null) { Debug.LogWarning(AIToolsCommon.LogPrefix + "Select a rigged model asset first."); return; }
            var r = HandleCommand(new JObject { ["model_path"] = AssetDatabase.GetAssetPath(go) });
            Debug.Log(AIToolsCommon.LogPrefix + r);
        }
    }
}
