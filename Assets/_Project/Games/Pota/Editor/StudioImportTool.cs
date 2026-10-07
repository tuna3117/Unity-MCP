using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports the Blender workshop's hand-offs for the studio looks (Candy A, Shatter B) and the special-ball looks (Royal 1,
    /// Court 2) through ai_import_asset and wires them into ColorHuntSettings. Idempotent: same ids keep their GUIDs.
    /// </summary>
    [McpForUnityTool("ai_studio_import", Description =
        "Import the Blender hand-offs of the studio looks (vfx-puff-toon, vfx-ring-toon, vfx-smoke-lit, vfx-shock-ring, " +
        "vfx-pillow-shatter) and the special-ball looks (vfx-fire-toon, vfx-flame-jet, vfx-dust-ring, vfx-speed-lines, " +
        "vfx-floor-crack, vfx-letter-slam/-swish/-combo, vfx-ball-halves, vfx-bomb-shell, vfx-coin, vfx-basketball), the feel and menu " +
        "props and the calm idles' stills (vfx-glint-streak, vfx-rim-ring, vfx-twinkle-star) into " +
        "Games/Pota/Art/VFX and wire them into ColorHuntSettings. Params: workshop (default ~/projects/Blender MCP).")]
    public static class StudioImportTool
    {
        public const string DefaultWorkshop = "~/projects/Blender MCP";
        public static readonly string[] Ids =
        {
            "vfx-puff-toon", "vfx-ring-toon", "vfx-smoke-lit", "vfx-shock-ring", "vfx-pillow-shatter",
            "vfx-fire-toon", "vfx-flame-jet", "vfx-dust-ring", "vfx-speed-lines", "vfx-floor-crack",
            "vfx-letter-slam", "vfx-letter-swish", "vfx-letter-combo",
            "vfx-ball-halves", "vfx-bomb-shell", "vfx-coin", "vfx-basketball",
            "vfx-letter-miss", "cloud-set", "bird-flock", "city-band",             // feel layer (spec 2026-10-03)
            "vfx-hand-pointer", "vfx-cloud-still",                                  // menu/reward layer (M11 hand, M7 clouds)
            "vfx-glint-streak", "vfx-rim-ring", "vfx-twinkle-star",                 // calm idles B, C, D (spec 2026-10-07)
        };

        public class Parameters
        {
            [ToolParameter("Blender workshop root", Required = false)]
            public string workshop { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            try
            {
                var report = Import(AIToolsCommon.GetString(p, "workshop", DefaultWorkshop));
                AIToolsCommon.Log(string.Join(" | ", report));
                return new SuccessResponse("Studio and special looks imported.", new { steps = report });
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        [MenuItem("AI Tools/Color Hunt/Import Studio Look")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", Import(DefaultWorkshop)));

        public static string Folder(string id) => $"Assets/_Project/Games/Pota/Art/VFX/{id}";
        public static string PropFolder(string id) => $"Assets/_Project/Games/Pota/Art/Props/{id}";

        public static List<string> Import(string workshop)
        {
            string root = workshop.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), workshop.Substring(2))
                : workshop;
            var report = new List<string>();
            foreach (string id in Ids)
            {
                var r = ImportAssetTool.Import(Path.Combine(root, "assets", "Pota", id, "export", "manifest.json"));
                report.Add($"{id}: {(r.matches ? "matches" : string.Join("; ", r.differences))}");
            }
            var s = ColorHuntSceneTool.EnsureSettings(report);
            s.CandyPuff = Sheet("vfx-puff-toon");
            s.CandyRing = Sheet("vfx-ring-toon");
            s.ShatterSmoke = Sheet("vfx-smoke-lit");
            s.ShockRing = Sheet("vfx-shock-ring");
            s.ShatterPieces = Model("vfx-pillow-shatter");
            s.RoyalFire = Sheet("vfx-fire-toon");
            s.FlameJet = Sheet("vfx-flame-jet");
            s.CourtDust = Sheet("vfx-dust-ring");
            s.SpeedLines = Sheet("vfx-speed-lines");
            s.FloorCrack = Sheet("vfx-floor-crack");
            s.LetterSlam = Sheet("vfx-letter-slam");
            s.LetterSwish = Sheet("vfx-letter-swish");
            s.LetterCombo = Sheet("vfx-letter-combo");
            s.RoyalHalves = Model("vfx-ball-halves");
            s.RoyalShell = Model("vfx-bomb-shell");
            s.RoyalCoin = Model("vfx-coin");
            s.CourtBall = Model("vfx-basketball");
            s.LetterMiss = Sheet("vfx-letter-miss");
            s.Clouds = Prop("cloud-set");
            s.Birds = Prop("bird-flock");
            s.BirdFlap = Clip("bird-flock", "flap");
            s.CityBand = Prop("city-band");
            report.Add(WireIdleStills(s));
            EditorUtility.SetDirty(s);
            var basket = AssetDatabase.LoadAssetAtPath<Project.Basket.BasketSettings>("Assets/_Project/Games/Pota/Settings/BasketSettings.asset");
            if (basket != null)
            {
                basket.Clouds = s.Clouds; basket.Birds = s.Birds; basket.BirdFlap = s.BirdFlap; basket.ToonTemplate = s.ToonTemplate;
                basket.LetterSwish = s.LetterSwish; basket.LetterMiss = s.LetterMiss; basket.LetterTemplate = s.LabelTemplate;
                EditorUtility.SetDirty(basket);
                report.Add("basket settings: sky props wired");
            }
            var meta = AssetDatabase.LoadAssetAtPath<Project.Meta.MetaSettings>(Project.Meta.MetaSettings.AssetPath);
            if (meta != null)
            {
                meta.HandSprite = SpriteOf("vfx-hand-pointer");
                meta.CloudSprite = SpriteOf("vfx-cloud-still");
                EditorUtility.SetDirty(meta);
                report.Add($"meta settings: hand={meta.HandSprite != null} cloud={meta.CloudSprite != null}");
            }
            AssetDatabase.SaveAssets();
            report.Add($"settings: puff={s.CandyPuff != null} ring={s.CandyRing != null} smoke={s.ShatterSmoke != null} " +
                       $"shock={s.ShockRing != null} pieces={s.ShatterPieces != null}");
            report.Add($"special settings: fire={s.RoyalFire != null} jet={s.FlameJet != null} dust={s.CourtDust != null} " +
                       $"lines={s.SpeedLines != null} crack={s.FloorCrack != null} letters={s.LetterSlam != null && s.LetterSwish != null && s.LetterCombo != null} " +
                       $"halves={s.RoyalHalves != null} shell={s.RoyalShell != null} coin={s.RoyalCoin != null} ball={s.CourtBall != null}");
            report.AddRange(PotaMaterialTemplates.Ensure());
            return report;
        }

        /// <summary>The calm idles' Blender stills (spec 2026-10-07) → ColorHuntSettings.IdleStreak / IdleRing / IdleStar.</summary>
        public static string WireIdleStills(ColorHuntSettings s)
        {
            s.IdleStreak = Sheet("vfx-glint-streak");
            s.IdleRing = Sheet("vfx-rim-ring");
            s.IdleStar = Sheet("vfx-twinkle-star");
            EditorUtility.SetDirty(s);
            return $"idle stills: streak={s.IdleStreak != null} ring={s.IdleRing != null} star={s.IdleStar != null}";
        }

        private static Texture2D Sheet(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder(id)}/{id}.png");

        /// <summary>A one-frame sheet as a UI sprite (the import leaves flipbooks as plain textures).</summary>
        private static Sprite SpriteOf(string id)
        {
            string path = $"{Folder(id)}/{id}.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject Model(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder(id)}/{id}.glb");

        private static GameObject Prop(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder(id)}/{id}.glb");

        private static AnimationClip Clip(string id, string clipName)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath($"{PropFolder(id)}/{id}.glb"))
                if (a is AnimationClip clip && clip.name == clipName) return clip;
            return null;
        }
    }
}
