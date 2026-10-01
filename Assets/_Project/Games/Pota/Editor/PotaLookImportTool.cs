using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports what Tools/potalook/run_export.sh brought from the Pota copy: import settings (Pota's filtering), the salvo ball
    /// material, the puzzle post profile, the "PotaRoom" layer (room reflection replica), and wires it all into ColorHuntSettings.
    /// Idempotent: run it again after a new export.
    /// </summary>
    [McpForUnityTool("ai_potalook_import", Description =
        "Import the Pota look export (Art/PotaLook, Audio/PotaLook, item stickers, Lilita One): texture/audio import settings, " +
        "PotaBall.mat, Settings/PotaPuzzleProfile.asset (Neutral ×1.12, bloom, vignette), the PotaRoom layer, ColorHuntSettings wiring " +
        "(including the special-ball samples: bomba, cizgi, altin-dogum, altin-inis, smac-vurus, file-swish, kalabalik).")]
    public static class PotaLookImportTool
    {
        public const string LookFolder = "Assets/_Project/Games/Pota/Art/PotaLook";
        public const string AudioFolder = "Assets/_Project/Games/Pota/Audio/PotaLook";
        public const string FontPath = "Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf";
        public const string ProfilePath = "Assets/_Project/Games/Pota/Settings/PotaPuzzleProfile.asset";
        public const string BallMaterialPath = LookFolder + "/PotaBall.mat";
        public const string RoomLayer = "PotaRoom";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            var report = Import();
            string msg = string.Join(" | ", report);
            AIToolsCommon.Log(msg);
            return new SuccessResponse("Pota look imported.", new { steps = report });
        }

        [MenuItem("AI Tools/Color Hunt/Import Pota Look")]
        public static void ImportMenu() => AIToolsCommon.Log(string.Join(" | ", Import()));

        public static List<string> Import()
        {
            var report = new List<string>();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture(LookFolder + "/label-atlas.png", mip: true, trilinear: true, aniso: 4, maxSize: 2048, uncompressed: true);
            foreach (var n in new[] { "star", "soft-disc", "soft-ring", "trail" })
                Texture($"{LookFolder}/{n}.png", mip: true, trilinear: false, aniso: 1, maxSize: 128, uncompressed: false);
            Texture(LookFolder + "/ball-skin.png", mip: true, trilinear: true, aniso: 4, maxSize: 512, uncompressed: false, repeatU: true);
            Texture(LookFolder + "/pano-a.png", mip: true, trilinear: true, aniso: 4, maxSize: 1024, uncompressed: false);
            foreach (var n in ColorHuntSceneTool.ItemNames)
                Texture($"Assets/_Project/Games/Pota/Art/esya-{n}.png", mip: true, trilinear: true, aniso: 4, maxSize: 256, uncompressed: false);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var st = imp.defaultSampleSettings;
                st.loadType = AudioClipLoadType.DecompressOnLoad;
                st.compressionFormat = AudioCompressionFormat.PCM;
                imp.defaultSampleSettings = st;
                imp.SaveAndReimport();
            }
            report.Add($"layer {RoomLayer} = {EnsureLayer(RoomLayer)}");
            report.Add(KhronosTonemap.Apply());                          // three's NeutralToneMapping for the puzzle profile's "Neutral"
            report.AddRange(PotaMaterialTemplates.Ensure());

            var room = PotaRoomBaker.Bake(report);
            var s = ColorHuntSceneTool.EnsureSettings(report);
            s.LabelAtlas = Load<Texture2D>(LookFolder + "/label-atlas.png");
            s.StarSprite = Load<Texture2D>(LookFolder + "/star.png");
            s.TrailSprite = Load<Texture2D>(LookFolder + "/trail.png");
            s.SoftDisc = Load<Texture2D>(LookFolder + "/soft-disc.png");
            s.SoftRing = Load<Texture2D>(LookFolder + "/soft-ring.png");
            s.Backboard = Load<Texture2D>(LookFolder + "/pano-a.png");
            s.Font = Load<Font>(FontPath);
            s.Palette = (Color[])PotaLook.Tones.Clone();
            s.RotateTime = 0.42f;   // RENK_AVI_HIS.donus (the asset still had 0.34)
            s.RoomReflection = room;
            s.SalvoBall = BallMaterial(Load<Texture2D>(LookFolder + "/ball-skin.png"));
            s.PuzzleProfile = Profile();
            s.SfxBreak = Clip("blok-pop"); s.SfxSalvo = Clip("salvo"); s.SfxDrop = Clip("dustu"); s.SfxRotate = Clip("dondur");
            s.SfxMinus = Clip("eksi"); s.SfxWin = Clip("kazandi"); s.SfxLose = Clip("kaybetti");
            s.SfxCombo = new AudioClip[11];
            for (int d = 0; d < 11; d++) s.SfxCombo[d] = Clip($"kombo-{d}");
            s.SfxHit = new[] { Clip("carpma-0"), Clip("carpma-1"), Clip("carpma-2") };
            s.SfxSelect = Clip("dokun");
            // Special balls (2026-10-01): ses.ts ORNEK patlama / cizgi / ozelDogdu / altinInis; the Court look's dunk hit, swish and crowd.
            s.SfxBomb = Clip("bomba"); s.SfxLine = Clip("cizgi"); s.SfxGoldenBorn = Clip("altin-dogum"); s.SfxGoldenLand = Clip("altin-inis");
            s.SfxSlam = Clip("smac-vurus"); s.SfxSwish = Clip("file-swish"); s.SfxCrowd = Clip("kalabalik");
            int specials = System.Array.FindAll(new[] { s.SfxBomb, s.SfxLine, s.SfxGoldenBorn, s.SfxGoldenLand, s.SfxSlam, s.SfxSwish, s.SfxCrowd },
                c => c != null).Length;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            report.Add($"settings: atlas={s.LabelAtlas != null} palette=pota font={(s.Font != null ? s.Font.name : "-")} profile={s.PuzzleProfile != null} " +
                       $"combo={System.Array.FindAll(s.SfxCombo, c => c != null).Length}/11 hit={System.Array.FindAll(s.SfxHit, c => c != null).Length}/3 " +
                       $"specials={specials}/7");
            return report;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static AudioClip Clip(string name) => Load<AudioClip>($"{AudioFolder}/{name}.wav");

        private static void Texture(string path, bool mip, bool trilinear, int aniso, int maxSize, bool uncompressed, bool repeatU = false)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[ai_potalook_import] missing {path}"); return; }
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = mip;
            imp.filterMode = trilinear ? FilterMode.Trilinear : FilterMode.Bilinear;
            imp.anisoLevel = aniso;
            imp.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = maxSize;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        private static Material BallMaterial(Texture2D skin)
        {
            var m = Load<Material>(BallMaterialPath);
            bool create = m == null;
            if (create) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PotaBall" };
            m.SetTexture("_BaseMap", skin);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 1f - 0.55f);                 // MeshStandard roughness 0.55
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color32(0x2a, 0x12, 0x00, 0xff));   // emissive #2a1200 (sRGB, like three)
            if (create) AssetDatabase.CreateAsset(m, BallMaterialPath);    // fully set up before it is written (CLAUDE.md lesson)
            else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>efekt.ts SonIslem → URP: Neutral tonemapping, exposure log2(1.12), bloom, vignette (tuned side by side in Task 11).</summary>
        private static VolumeProfile Profile()
        {
            var p = Load<VolumeProfile>(ProfilePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, ProfilePath);
            }
            var tm = Get<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>(p);
            ca.postExposure.Override(Mathf.Log(1.12f, 2f));
            var bloom = Get<Bloom>(p);
            bloom.threshold.Override(Mathf.Pow(0.86f, 1f / 2.2f));      // UnrealBloom threshold 0.86 is linear; URP's is gamma
            bloom.intensity.Override(0.28f);                              // UnrealBloom strength 0.28 (different kernel: tuned by eye)
            bloom.scatter.Override(0.45f);                                // UnrealBloom radius 0.45
            bloom.downscale.Override(BloomDownscaleMode.Half);            // half resolution like Pota
            var v = Get<Vignette>(p);
            v.intensity.Override(0.32f);
            v.smoothness.Override(0.55f);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T c)) return c;
            c = p.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        /// <summary>Names a free user layer (ProjectSettings/TagManager.asset) unless it exists. Returns its index.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.InvalidOperationException($"no free layer for {name}");
        }
    }
}
