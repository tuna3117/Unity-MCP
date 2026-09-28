using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    /// <summary>
    /// What the workshop needs to know about a game beyond its own code: the phone resolution its scenes are previewed at
    /// and how a device build of it is named and signed. One per game at Games/&lt;Game&gt;/&lt;Game&gt;Profile.asset; the folder
    /// the asset sits in is the game's folder.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Game Profile", fileName = "GameProfile")]
    public class GameProfile : ScriptableObject
    {
        [Tooltip("Game view size while one of this game's scenes is open")]
        public Vector2Int GameViewResolution = new Vector2Int(1080, 1920);

        [Header("Device build")]
        public string ProductName = "";
        [Tooltip("Keep it different from the store app so a test build does not replace it on the phone")]
        public string BundleId = "";
        [Tooltip("Apple team for automatic signing; empty = choose the team in Xcode > Signing")]
        public string AppleTeamId = "";
        public UIOrientation Orientation = UIOrientation.Portrait;
        [Tooltip("Scenes in build order; the first one opens when the app starts")]
        public SceneAsset[] Scenes = new SceneAsset[0];

        public string Folder
        {
            get
            {
                string path = AssetDatabase.GetAssetPath(this);
                return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path)?.Replace('\\', '/');
            }
        }
        public string GameName => Folder == null ? name : Path.GetFileName(Folder);

        public static GameProfile[] All() =>
            AssetDatabase.FindAssets("t:" + nameof(GameProfile))
                .Select(g => AssetDatabase.LoadAssetAtPath<GameProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .ToArray();

        /// <summary>The profile of the game whose folder contains <paramref name="assetPath"/>, or null.</summary>
        public static GameProfile ForAsset(string assetPath) =>
            string.IsNullOrEmpty(assetPath) ? null : All().FirstOrDefault(p => p.Folder != null && assetPath.StartsWith(p.Folder + "/"));

        /// <summary>Looks a profile up by game name ("Pota") or asset path.</summary>
        public static GameProfile Find(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;
            var direct = AssetDatabase.LoadAssetAtPath<GameProfile>(nameOrPath);
            if (direct != null) return direct;
            return All().FirstOrDefault(p => string.Equals(p.GameName, nameOrPath, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
