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
        "the pass ball pas-topu (+ the Art/Specials/pas-topu-a.png sticker), " +
        "vfx-pillow-shatter) and the special-ball looks (vfx-fire-toon, vfx-flame-jet, vfx-dust-ring, vfx-speed-lines, " +
        "vfx-floor-crack, vfx-letter-slam/-swish/-combo, vfx-ball-halves, vfx-bomb-shell, vfx-coin, vfx-basketball), the combo's letterings " +
        "(vfx-letter-crossover/-showtime/-mega-slam/-alley-oop/-fast-break; ring and chunks: vfx-ring-toon, vfx-pillow-shatter), the feel and menu " +
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
            "pas-topu",                                                             // pass ball B Pervane (Pota spec 2026-10-11)
            "vfx-letter-crossover", "vfx-letter-showtime", "vfx-letter-mega-slam",   // combo lettering (Pota spec 2026-10-13, plan P3);
            "vfx-letter-alley-oop", "vfx-letter-fast-break",                        // the ring and the chunks are vfx-ring-toon / vfx-pillow-shatter above
        };

        /// <summary>The pass ball's sticker (Pota public/assets/ozel-pasTopu-a.png, candidate a2), copied in by hand: Pota is not a workshop.</summary>
        public const string PassSpritePath = "Assets/_Project/Games/Pota/Art/Specials/pas-topu-a.png";

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
            report.Add(WirePassBall(s));
            report.Add(WireCombo(s));
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

        /// <summary>
        /// The pass ball (Pota spec 2026-10-11): its sticker (imported like the label atlas — sRGB, mipmaps, trilinear, CLAMPED so the padded
        /// art can be drawn past its edges, uncompressed) → ColorHuntSettings.PassSprite; the Blender pass_ball (ai_import_asset, Art/VFX/pas-topu)
        /// → PassModel.
        /// </summary>
        public static string WirePassBall(ColorHuntSettings s)
        {
            if (AssetImporter.GetAtPath(PassSpritePath) is TextureImporter imp)
            {
                bool dirty = imp.textureType != TextureImporterType.Default || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp
                             || !imp.mipmapEnabled || imp.filterMode != FilterMode.Trilinear || imp.textureCompression != TextureImporterCompression.Uncompressed
                             || !imp.sRGBTexture;
                if (dirty)
                {
                    imp.textureType = TextureImporterType.Default;
                    imp.alphaIsTransparency = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.mipmapEnabled = true;
                    imp.filterMode = FilterMode.Trilinear;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.sRGBTexture = true;
                    imp.SaveAndReimport();
                }
            }
            s.PassSprite = AssetDatabase.LoadAssetAtPath<Texture2D>(PassSpritePath);
            s.PassModel = Model("pas-topu");
            EditorUtility.SetDirty(s);
            return $"pass ball: sprite={s.PassSprite != null} model={s.PassModel != null}";
        }

        /// <summary>
        /// The combo (Pota spec 2026-10-13 §3): the five letterings → ColorHuntSettings.Letter*, the toon ring (plan P2) → ComboRing, the
        /// pillow pieces (plan P6) → ComboPieces.
        /// </summary>
        public static string WireCombo(ColorHuntSettings s)
        {
            s.LetterCrossover = Sheet("vfx-letter-crossover");
            s.LetterShowtime = Sheet("vfx-letter-showtime");
            s.LetterMegaSlam = Sheet("vfx-letter-mega-slam");
            s.LetterAlleyOop = Sheet("vfx-letter-alley-oop");
            s.LetterFastBreak = Sheet("vfx-letter-fast-break");
            s.ComboRing = Sheet("vfx-ring-toon");
            s.ComboPieces = Model("vfx-pillow-shatter");
            EditorUtility.SetDirty(s);
            return $"combo: letters={s.LetterCrossover != null && s.LetterShowtime != null && s.LetterMegaSlam != null && s.LetterAlleyOop != null && s.LetterFastBreak != null} " +
                   $"ring={s.ComboRing != null} pieces={s.ComboPieces != null}";
        }

        [MenuItem("AI Tools/Color Hunt/Wire Pass Ball")]
        public static void WirePassBallMenu()
        {
            var s = ColorHuntSceneTool.EnsureSettings(new List<string>());
            AIToolsCommon.Log(WirePassBall(s));
            AssetDatabase.SaveAssets();
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
