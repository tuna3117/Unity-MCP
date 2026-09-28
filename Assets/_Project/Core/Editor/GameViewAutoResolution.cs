using Project.Editor.AITools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Editor
{
    /// <summary>
    /// Keeps the Game view at the open game's phone resolution (from its <see cref="GameProfile"/>), so previews and
    /// screenshots match the device. Runs when a scene is opened and after every editor start / domain reload; only
    /// touches Game views that are already open and logs only when it actually changes the size.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewAutoResolution
    {
        static GameViewAutoResolution()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => { if (mode == OpenSceneMode.Single) ApplyFor(scene.path); };
            EditorApplication.delayCall += () => ApplyFor(SceneManager.GetActiveScene().path);
        }

        public static void ApplyFor(string scenePath)
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var profile = GameProfile.ForAsset(scenePath);
            if (profile == null) return;
            var r = profile.GameViewResolution;
            if (GameViewPortrait.TrySet(r.x, r.y, false, out string message))
                AIToolsCommon.Log($"{profile.GameName}: {message}");
        }
    }
}
