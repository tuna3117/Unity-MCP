using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Device builds from a game's <see cref="GameProfile"/>: applies its product name, bundle id, orientation and Apple
    /// team, builds its scenes to Builds/&lt;Game&gt;-&lt;platform&gt; (an Xcode project for iOS). The build blocks the editor for
    /// minutes, so 'start' only queues it and the result is written to Temp/ai_build_status.json for 'status'.
    /// </summary>
    [McpForUnityTool("ai_build", Description =
        "Device build of a game from its GameProfile (Games/<Game>/<Game>Profile.asset); the profile's name/bundle id/orientation/team " +
        "are applied for the build only. action='start' {game, scene?, platform?, development?} " +
        "queues the build (the editor is busy for minutes, the bridge may not answer meanwhile) and switches the editor back to its " +
        "previous platform afterwards; action='status' returns the last result. " +
        "platform: ios (default, writes an Xcode project) | android | mac. scene: build only that scene. Output: Builds/<Game>-<platform>[-<scene>].")]
    public static class BuildTool
    {
        public class Parameters
        {
            [ToolParameter("start | status (default status)", Required = false)] public string action { get; set; }
            [ToolParameter("Game name or GameProfile asset path, e.g. 'Pota'", Required = false)] public string game { get; set; }
            [ToolParameter("Build only this scene (name), e.g. 'ColorHunt'; default all profile scenes", Required = false)] public string scene { get; set; }
            [ToolParameter("ios (default) | android | mac", Required = false)] public string platform { get; set; }
            [ToolParameter("Development build (default true)", Required = false)] public bool? development { get; set; }
        }

        public const string StatusPath = "Temp/ai_build_status.json";

        public static object HandleCommand(JObject p)
        {
            string action = AIToolsCommon.GetString(p, "action", "status");
            if (action == "status")
                return File.Exists(StatusPath)
                    ? new SuccessResponse("last build", JObject.Parse(File.ReadAllText(StatusPath)))
                    : new SuccessResponse("no build yet");
            if (action != "start") return new ErrorResponse("action must be 'start' or 'status'.");

            var profile = GameProfile.Find(AIToolsCommon.GetString(p, "game"));
            if (profile == null) return new ErrorResponse($"No GameProfile '{AIToolsCommon.GetString(p, "game")}'. Known: {string.Join(", ", GameProfile.All().Select(g => g.GameName))}");
            string platform = AIToolsCommon.GetString(p, "platform", "ios").ToLowerInvariant();
            if (!Targets.ContainsKey(platform)) return new ErrorResponse("platform must be ios, android or mac.");
            string scene = AIToolsCommon.GetString(p, "scene");
            var scenes = ScenesFor(profile, scene);
            if (scenes.Length == 0) return new ErrorResponse(scene == null ? "Profile has no scenes." : $"Scene '{scene}' is not in the {profile.GameName} profile.");
            bool development = AIToolsCommon.GetBool(p, "development", true);

            WriteStatus(new JObject { ["state"] = "queued", ["game"] = profile.GameName, ["platform"] = platform, ["scenes"] = new JArray(scenes) });
            // Not delayCall: an unfocused editor may not tick for a long time, while the synchronization context
            // keeps being pumped (the bridge's own commands arrive that way).
            var context = SynchronizationContext.Current;
            if (context != null) context.Post(_ => Build(profile, scenes, platform, scene, development), null);
            else EditorApplication.delayCall += () => Build(profile, scenes, platform, scene, development);
            EditorApplication.QueuePlayerLoopUpdate();
            return new SuccessResponse($"Build of {profile.GameName} for {platform} queued ({scenes.Length} scene(s)). " +
                                       "The editor is busy for a few minutes; poll action='status'.");
        }

        [MenuItem("AI Tools/Build/iOS Build of the Open Game")]
        public static void BuildOpenGameMenu()
        {
            var profile = GameProfile.ForAsset(EditorSceneManager.GetActiveScene().path);
            if (profile == null) { AIToolsCommon.Log("Build: the open scene belongs to no GameProfile."); return; }
            Build(profile, ScenesFor(profile, null), "ios", null, true);
        }

        private static readonly Dictionary<string, BuildTarget> Targets = new Dictionary<string, BuildTarget>
        {
            { "ios", BuildTarget.iOS }, { "android", BuildTarget.Android }, { "mac", BuildTarget.StandaloneOSX },
        };

        private static string[] ScenesFor(GameProfile profile, string onlyScene) =>
            profile.Scenes.Where(s => s != null)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => onlyScene == null || Path.GetFileNameWithoutExtension(path) == onlyScene)
                .ToArray();

        public static BuildReport Build(GameProfile profile, string[] scenes, string platform, string onlyScene, bool development)
        {
            // Read everything from the profile up front: the build reloads assets, and afterwards this instance may be
            // a dead object whose asset path is empty.
            string game = profile.GameName;
            var target = Targets[platform];
            var group = BuildPipeline.GetBuildTargetGroup(target);
            string output = $"Builds/{game}-{platform}" + (onlyScene != null ? "-" + onlyScene : "") +
                            (platform == "android" ? ".apk" : platform == "mac" ? ".app" : "");
            var status = new JObject { ["game"] = game, ["platform"] = platform, ["scenes"] = new JArray(scenes), ["output"] = output };
            var previous = EditorUserBuildSettings.activeBuildTarget;
            Action restorePlayerSettings = null;
            try
            {
                if (previous != target)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                restorePlayerSettings = ApplyPlayerSettings(profile, target, group, status);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    targetGroup = group,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                });
                var s = report.summary;
                status["state"] = "done";
                status["result"] = s.result.ToString();
                status["seconds"] = Math.Round(s.totalTime.TotalSeconds);
                status["size_mb"] = Math.Round(s.totalSize / 1048576.0, 1);
                status["errors"] = new JArray(report.steps.SelectMany(step => step.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => m.content).Distinct().Take(10));
                if (target == BuildTarget.iOS) status["xcode_project"] = output + "/Unity-iPhone.xcodeproj";
                AIToolsCommon.Log($"Build {game} {platform}: {s.result} in {status["seconds"]} s -> {output}");
                return report;
            }
            catch (Exception e)
            {
                status["state"] = "failed";
                status["result"] = e.GetType().Name + ": " + e.Message;
                Debug.LogException(e);
                return null;
            }
            finally
            {
                status["finished"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                // The game's identity is baked into the build output; the workshop (a template other games are copied
                // from) keeps its own Player Settings.
                restorePlayerSettings?.Invoke();
                AssetDatabase.SaveAssets();
                // Switch back: with iOS active the Test Runner finds no PlayMode tests (checked 2026-09-28), and the
                // workshop's edit-test loop runs on the editor platform.
                bool restore = EditorUserBuildSettings.activeBuildTarget != previous;
                if (restore) status["restored_target"] = previous.ToString();
                WriteStatus(status);
                if (restore) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previous), previous);
            }
        }

        /// <summary>Applies the profile's identity to Player Settings and returns an action that puts the old values back.</summary>
        private static Action ApplyPlayerSettings(GameProfile profile, BuildTarget target, BuildTargetGroup group, JObject status)
        {
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            string oldName = PlayerSettings.productName, oldId = PlayerSettings.GetApplicationIdentifier(named);
            var oldOrientation = PlayerSettings.defaultInterfaceOrientation;
            string oldTeam = PlayerSettings.iOS.appleDeveloperTeamID;
            int oldAutoSign = AutoSigningRaw; // three-state in the asset (0 = editor default), the bool API can't restore 0

            if (!string.IsNullOrEmpty(profile.ProductName)) PlayerSettings.productName = profile.ProductName;
            if (!string.IsNullOrEmpty(profile.BundleId)) PlayerSettings.SetApplicationIdentifier(named, profile.BundleId);
            PlayerSettings.defaultInterfaceOrientation = profile.Orientation;
            if (target == BuildTarget.iOS)
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                if (!string.IsNullOrEmpty(profile.AppleTeamId)) PlayerSettings.iOS.appleDeveloperTeamID = profile.AppleTeamId;
            }
            status["product_name"] = PlayerSettings.productName;
            status["bundle_id"] = PlayerSettings.GetApplicationIdentifier(named);
            return () =>
            {
                PlayerSettings.productName = oldName;
                PlayerSettings.SetApplicationIdentifier(named, oldId);
                PlayerSettings.defaultInterfaceOrientation = oldOrientation;
                PlayerSettings.iOS.appleDeveloperTeamID = oldTeam;
                AutoSigningRaw = oldAutoSign;
            };
        }

        private static int AutoSigningRaw
        {
            get => PlayerSettingsAsset().FindProperty("appleEnableAutomaticSigning").intValue;
            set
            {
                var so = PlayerSettingsAsset();
                so.FindProperty("appleEnableAutomaticSigning").intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static SerializedObject PlayerSettingsAsset() =>
            new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));

        private static void WriteStatus(JObject status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
            File.WriteAllText(StatusPath, status.ToString());
        }
    }
}
