using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Meta;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Brings Pota's menu/reward UI images (style guide §12) from the read-only Pota checkout into Games/Pota/Art/Meta/ as sprites, and
    /// the Fredoka font (OFL, Google Fonts) as two static instances (SemiBold 600, Bold 700, made with fonttools) into Art/Fonts/; creates
    /// or updates Settings/MetaSettings.asset and wires everything. Re-running keeps GUIDs (files are overwritten in place).
    /// </summary>
    [McpForUnityTool("ai_potameta_import", Description =
        "Imports Pota's menu UI images (public/assets) into Art/Meta, instances the Fredoka font (600/700) into Art/Fonts, and wires Settings/MetaSettings.asset. " +
        "{\"pota\": \"~/projects/Pota\" (default), \"fonts\": true (default; false = skip the download)}")]
    public static class PotaMetaImportTool
    {
        public const string MetaFolder = "Assets/_Project/Games/Pota/Art/Meta";
        public const string FontFolder = "Assets/_Project/Games/Pota/Art/Fonts";
        public const string FredokaUrl = "https://github.com/google/fonts/raw/main/ofl/fredoka/Fredoka%5Bwdth%2Cwght%5D.ttf";
        public const string FredokaLicenseUrl = "https://github.com/google/fonts/raw/main/ofl/fredoka/OFL.txt";

        /// <summary>Image file name (without extension) → how MetaSettings refers to it.</summary>
        public static readonly string[] Images =
        {
            "anaekran-1-gunduz", "anaekran-2-gunbatimi", "anaekran-3-gece", "anaekran-4-sokak", "sehir", "acilis",
            "kahraman", "kahraman-maske", "para", "kalp", "kupa", "disli", "sepet", "top", "para-yigini",
            "lig-caylak", "lig-amator", "lig-profesyonel", "lig-allstar", "lig-sampiyonlar",
            "madalya-bronz", "madalya-gumus", "madalya-altin",
            "esya-forma", "esya-ayakkabi", "esya-sapka", "esya-corap", "esya-bileklik",
            "market", "ev-ikon", "harita-ikon", "kat-giysi", "kat-top", "kat-guc",
            "guc-altin", "guc-cephane", "guc-devam", "giysi-forma", "giysi-sort", "giysi-ayakkabi", "giysi-sac",
        };

        public static object HandleCommand(JObject p)
        {
            string pota = AIToolsCommon.GetString(p, "pota", "~/projects/Pota");
            bool fonts = AIToolsCommon.GetBool(p, "fonts", true);
            var report = Import(pota, fonts);
            return new { success = true, report };
        }

        [MenuItem("AI Tools/Pota/Import Meta UI Assets")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", Import("~/projects/Pota", true)));

        public static List<string> Import(string pota, bool fonts)
        {
            var report = new List<string>();
            string root = pota.StartsWith("~") ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), pota.Substring(2)) : pota;
            string assets = Path.Combine(root, "public", "assets");
            AIToolsCommon.EnsureFolder(MetaFolder);
            int copied = 0;
            foreach (var name in Images)
            {
                string src = File.Exists(Path.Combine(assets, name + ".png")) ? Path.Combine(assets, name + ".png")
                           : File.Exists(Path.Combine(assets, name + ".jpg")) ? Path.Combine(assets, name + ".jpg") : null;
                if (src == null) { report.Add($"missing {name}"); continue; }
                string dst = Path.Combine(MetaFolder, Path.GetFileName(src));
                File.Copy(src, dst, true);
                copied++;
            }
            report.Add($"{copied}/{Images.Length} images copied to {MetaFolder}");
            AssetDatabase.Refresh();
            foreach (var name in Images)
            {
                string path = AssetPathOf(name);
                if (path == null) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool big = name.StartsWith("anaekran") || name == "sehir" || name == "acilis";
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = !big;
                imp.maxTextureSize = big ? 4096 : 512;            // 1536×2752 stays full size (2048 shrank it to 1143 wide)
                imp.textureCompression = TextureImporterCompression.Compressed;
                if (path.EndsWith("kahraman-maske.png")) { imp.isReadable = true; imp.textureCompression = TextureImporterCompression.Uncompressed; }   // luminance mask, read at runtime (MarketScreen tints the jersey)
                imp.SaveAndReimport();
            }
            if (fonts) report.AddRange(ImportFredoka());
            report.AddRange(Wire());
            AssetDatabase.SaveAssets();
            return report;
        }

        private static string AssetPathOf(string name)
        {
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                string p = $"{MetaFolder}/{name}{ext}";
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>Downloads the variable Fredoka and writes static 600 / 700 instances with fonttools (uv), plus the OFL text.</summary>
        private static List<string> ImportFredoka()
        {
            var report = new List<string>();
            AIToolsCommon.EnsureFolder(FontFolder);
            string tmp = Path.Combine(Path.GetTempPath(), "Fredoka-var.ttf");
            if (!File.Exists($"{FontFolder}/Fredoka-SemiBold.ttf") || !File.Exists($"{FontFolder}/Fredoka-Bold.ttf"))
            {
                if (!Run("/usr/bin/curl", $"-sL --max-time 60 -o \"{tmp}\" \"{FredokaUrl}\"", out string err) || !File.Exists(tmp))
                { report.Add("Fredoka download failed: " + err); return report; }
                string uv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "uv");
                string script = "from fontTools.ttLib import TTFont\nfrom fontTools.varLib import instancer\nimport sys\nsrc, out, w = sys.argv[1], sys.argv[2], float(sys.argv[3])\n" +
                                "g = instancer.instantiateVariableFont(TTFont(src), {'wght': w, 'wdth': 100})\ng.save(out)\n";
                string py = Path.Combine(Path.GetTempPath(), "fredoka_instance.py");
                File.WriteAllText(py, script);
                foreach (var (w, name) in new[] { (600, "SemiBold"), (700, "Bold") })
                {
                    string outPath = Path.GetFullPath($"{FontFolder}/Fredoka-{name}.ttf");
                    if (!Run(uv, $"run --with fonttools python \"{py}\" \"{tmp}\" \"{outPath}\" {w}", out err)) report.Add($"Fredoka {name}: {err}");
                }
                Run("/usr/bin/curl", $"-sL --max-time 60 -o \"{Path.GetFullPath(FontFolder + "/Fredoka-OFL.txt")}\" \"{FredokaLicenseUrl}\"", out _);
                AssetDatabase.Refresh();
            }
            report.Add($"fonts: semibold={File.Exists($"{FontFolder}/Fredoka-SemiBold.ttf")} bold={File.Exists($"{FontFolder}/Fredoka-Bold.ttf")}");
            return report;
        }

        private static bool Run(string file, string args, out string error)
        {
            error = "";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(120000);
                    if (proc.ExitCode != 0) { error = stderr.Trim(); return false; }
                    return true;
                }
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        private static Sprite S(string name)
        {
            string path = AssetPathOf(name);
            return path != null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
        }

        private static List<string> Wire()
        {
            var report = new List<string>();
            var s = AssetDatabase.LoadAssetAtPath<MetaSettings>(MetaSettings.AssetPath);
            if (s == null)
            {
                AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Settings");
                s = ScriptableObject.CreateInstance<MetaSettings>();
                AssetDatabase.CreateAsset(s, MetaSettings.AssetPath);
                report.Add("created " + MetaSettings.AssetPath);
            }
            s.Lilita = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/LilitaOne-Regular.ttf");
            s.FredokaSemiBold = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/Fredoka-SemiBold.ttf");
            s.FredokaBold = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/Fredoka-Bold.ttf");
            s.MenuBackgrounds = new[] { S("anaekran-1-gunduz"), S("anaekran-2-gunbatimi"), S("anaekran-3-gece"), S("anaekran-4-sokak") };
            s.MapBackground = S("sehir"); s.LoadingBackground = S("acilis");
            s.Hero = S("kahraman"); s.HeroMask = S("kahraman-maske");
            s.Coin = S("para"); s.Heart = S("kalp"); s.Trophy = S("kupa"); s.Gear = S("disli"); s.Basket = S("sepet"); s.Ball = S("top"); s.CoinPile = S("para-yigini");
            s.LeagueBadges = new[] { S("lig-caylak"), S("lig-amator"), S("lig-profesyonel"), S("lig-allstar"), S("lig-sampiyonlar") };
            s.Medals = new[] { S("madalya-bronz"), S("madalya-gumus"), S("madalya-altin") };
            s.Items = new[] { S("esya-forma"), S("esya-ayakkabi"), S("esya-sapka"), S("esya-corap"), S("esya-bileklik") };
            s.TabIcons = new[] { S("market"), S("ev-ikon"), S("harita-ikon") };
            s.CategoryIcons = new[] { S("kat-giysi"), S("kat-top"), S("kat-guc"), S("para-yigini") };
            s.Boosters = new[] { S("guc-altin"), S("guc-cephane"), S("guc-devam") };
            s.Clothes = new[] { S("giysi-forma"), S("giysi-sort"), S("giysi-ayakkabi"), S("giysi-sac") };
            EditorUtility.SetDirty(s);
            int missing = 0;
            foreach (var sp in s.MenuBackgrounds) if (sp == null) missing++;
            foreach (var sp in s.LeagueBadges) if (sp == null) missing++;
            foreach (var sp in s.Medals) if (sp == null) missing++;
            report.Add($"settings wired; missing sprites in the main groups: {missing}; fonts lilita={s.Lilita != null} fredoka={s.FredokaSemiBold != null && s.FredokaBold != null}");
            return report;
        }
    }
}
