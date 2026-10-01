using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    /// <summary>The Pota look assets are in the project, imported the way Pota uses them, and wired into the settings asset.</summary>
    public class PotaLookAssetsTests
    {
        private const string Look = "Assets/_Project/Games/Pota/Art/PotaLook/";
        private const string Hint = "run Tools/potalook/run_export.sh <scratch>, then execute_custom_tool ai_potalook_import";

        private static ColorHuntSettings Settings() =>
            AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");

        [Test]
        public void LabelAtlas_IsPotasAtlas_Mipmapped_Uncompressed()
        {
            var imp = AssetImporter.GetAtPath(Look + "label-atlas.png") as TextureImporter;
            Assert.IsNotNull(imp, Hint);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Look + "label-atlas.png");
            Assert.AreEqual(2048, tex.width);
            Assert.AreEqual(2048, tex.height);
            Assert.IsTrue(imp.mipmapEnabled, "Pota: generateMipmaps, LinearMipmapLinear");
            Assert.AreEqual(FilterMode.Trilinear, imp.filterMode);
            Assert.AreEqual(4, imp.anisoLevel);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression);
        }

        [Test]
        public void LabelAtlas_HasInkInTheNumberAndItemCells()
        {
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Look + "label-atlas.png")), Hint);
            try
            {
                Assert.Greater(OpaqueFraction(tex, 6), 0.05f, "cell 6 = number 7");
                Assert.Greater(OpaqueFraction(tex, 98), 0.05f, "cell 98 = number 99");
                for (int i = 0; i < 5; i++) Assert.Greater(OpaqueFraction(tex, 100 + i), 0.2f, $"item cell {100 + i}");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>Share of pixels with alpha &gt; 0.5 in atlas cell c (Pota: x = c mod 11 · 186, y from the TOP).</summary>
        private static float OpaqueFraction(Texture2D tex, int c)
        {
            int x0 = c % 11 * 186, yTop = c / 11 * 186, n = 0;
            for (int y = 0; y < 186; y++)
                for (int x = 0; x < 186; x++)
                    if (tex.GetPixel(x0 + x, 2047 - (yTop + y)).a > 0.5f) n++;
            return n / (186f * 186f);
        }

        [Test]
        public void ItemStickers_AreThe256Set()
        {
            foreach (var name in new[] { "forma", "ayakkabi", "sapka", "corap", "bileklik" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/Games/Pota/Art/esya-{name}.png");
                Assert.IsNotNull(tex, name);
                Assert.AreEqual(256, tex.width, $"{name}: the old 09-23 set was 217 wide");
                Assert.AreEqual(256, tex.height, name);
            }
        }

        [Test]
        public void Settings_PointAtThePotaLookAssets()
        {
            var s = Settings();
            Assert.IsNotNull(s.LabelAtlas, Hint);
            Assert.IsNotNull(s.StarSprite);
            Assert.IsNotNull(s.TrailSprite);
            Assert.IsNotNull(s.SoftDisc);
            Assert.IsNotNull(s.SoftRing);
            Assert.IsNotNull(s.Backboard);
            Assert.IsNotNull(s.SalvoBall);
            Assert.AreEqual("ball-skin", s.SalvoBall.GetTexture("_BaseMap").name);
            Assert.IsNotNull(s.PuzzleProfile);
            Assert.AreEqual("LilitaOne-Regular", s.Font.name);
            foreach (var c in new[] { s.SfxBreak, s.SfxSalvo, s.SfxDrop, s.SfxRotate, s.SfxMinus, s.SfxWin, s.SfxLose, s.SfxSelect }) Assert.IsNotNull(c);
            Assert.AreEqual(11, s.SfxCombo.Length, "ses.ts MERDIVEN has 11 steps");
            Assert.AreEqual(3, s.SfxHit.Length);
            foreach (var c in s.SfxCombo) Assert.IsNotNull(c);
            foreach (var c in s.SfxHit) Assert.IsNotNull(c);
        }

        [Test]
        public void PuzzleProfile_HasPotasPost()
        {
            var p = Settings().PuzzleProfile;
            Assert.IsNotNull(p, Hint);
            Assert.IsTrue(p.TryGet(out Tonemapping tm));
            Assert.AreEqual(TonemappingMode.Neutral, tm.mode.value);
            Assert.IsTrue(p.TryGet(out ColorAdjustments ca));
            Assert.AreEqual(Mathf.Log(1.12f, 2f), ca.postExposure.value, 1e-3f, "efekt.ts toneMappingExposure 1.12");
            Assert.IsTrue(p.TryGet(out Bloom bloom));
            Assert.AreEqual(Mathf.Pow(0.86f, 1f / 2.2f), bloom.threshold.value, 1e-3f, "linear 0.86; URP's threshold is gamma");
            Assert.IsTrue(p.TryGet(out Vignette v));
            Assert.AreEqual(0.32f, v.intensity.value, 1e-4f);
            Assert.AreEqual(0.55f, v.smoothness.value, 1e-4f);
        }

        [Test]
        public void RoomLayer_Exists() => Assert.GreaterOrEqual(LayerMask.NameToLayer("PotaRoom"), 8, Hint);

        [Test]
        public void RoomReflection_IsABakedSpecularCubemap()
        {
            var s = Settings();
            Assert.IsNotNull(s.RoomReflection, Hint);
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.RoomReflection));
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            Assert.AreEqual(TextureImporterShape.TextureCube, ts.textureShape);
            Assert.AreEqual(TextureImporterCubemapConvolution.Specular, ts.cubemapConvolution);
        }


        [Test]
        public void SelectClip_IsPotasDokunMp3_ThroughTheRoom()
        {
            var clip = Settings().SfxSelect;
            Assert.IsNotNull(clip, Hint);
            Assert.AreEqual(1.068f + 0.35f, clip.length, 0.03f, "public/assets/ses/dokun.mp3 (1.068 s) + the 0.35 s room tail, not the 0.4 s synth fallback");
        }

        [Test]
        public void LabelAtlas_CarriesPotasSpecialStickers_Payli()
        {
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Look + "label-atlas.png")), Hint);
            try
            {
                foreach (var k in new[] { SpecialKind.LineV, SpecialKind.Bomb, SpecialKind.Golden })
                {
                    int c = LabelAtlas.SpecialCell(k);
                    Assert.Greater(OpaqueFraction(tex, c), 0.3f, $"{k}: cell {c} holds ozel-*-a.png (ozelleriGiydir)");
                    Assert.AreEqual(0f, EdgeAlpha(tex, c, 10), $"{k}: \"payli\" draws into the middle 88 %, the rim stays clear");
                }
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>Largest alpha in the outer <paramref name="px"/> pixels of atlas cell c (y from the TOP, like OpaqueFraction).</summary>
        private static float EdgeAlpha(Texture2D tex, int c, int px)
        {
            int x0 = c % 11 * 186, yTop = c / 11 * 186;
            float max = 0f;
            for (int y = 0; y < 186; y++)
                for (int x = 0; x < 186; x++)
                    if (x < px || y < px || x >= 186 - px || y >= 186 - px)
                        max = Mathf.Max(max, tex.GetPixel(x0 + x, 2047 - (yTop + y)).a);
            return max;
        }

        [Test]
        public void SpecialClips_ArePotasSamples_ThroughTheRoom()
        {
            var s = Settings();
            // ffprobe durations of ~/projects/Pota/public/assets/ses/<name>.mp3 (2026-10-01); the export adds the 0.35 s room tail.
            var expected = new (AudioClip clip, string name, float mp3)[]
            {
                (s.SfxBomb, "bomba", 3.0186f), (s.SfxLine, "cizgi", 1.5325f), (s.SfxGoldenBorn, "altin-dogum", 1.7647f),
                (s.SfxGoldenLand, "altin-inis", 2.9954f), (s.SfxSlam, "smac-vurus", 4.9280f), (s.SfxSwish, "file-swish", 1.6951f),
                (s.SfxCrowd, "kalabalik", 4.0171f),
            };
            foreach (var (clip, name, mp3) in expected)
            {
                Assert.IsNotNull(clip, $"{name}: {Hint}");
                Assert.AreEqual(name, clip.name);
                Assert.AreEqual(mp3 + 0.35f, clip.length, 0.03f, $"public/assets/ses/{name}.mp3 + the room tail, not a synth fallback");
            }
        }

        [Test]
        public void Settings_SpecialBallsAreOff_AndLookPota_InTodaysLevel()
        {
            var s = Settings();
            Assert.AreEqual(Features.None, s.Specials, "bölüm 20 ships without specials (spec §1.1)");
            Assert.AreEqual(SpecialLook.Pota, s.SpecialLook);
            Assert.AreEqual(s.Specials, s.ToDesign().Features, "ToDesign carries the switches to Create");
            var fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                Assert.AreEqual(Features.None, fresh.Specials);
                Assert.AreEqual(SpecialLook.Pota, fresh.SpecialLook);
            }
            finally { Object.DestroyImmediate(fresh); }
        }
    }
}
