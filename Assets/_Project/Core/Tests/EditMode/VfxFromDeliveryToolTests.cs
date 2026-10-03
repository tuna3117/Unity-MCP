using System;
using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using Project.VFX;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class VfxFromDeliveryToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__vfxkit_test__";
        private const string Out = Root + "/out";
        private const string Lib = Root + "/out/TestVfxLibrary.asset";

        private static string FixtureDir(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures", name));

        private static VfxFromDeliveryTool.Options Opts(string id = null) =>
            new VfxFromDeliveryTool.Options { id = id, outFolder = Out, library = Lib };

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        /// <summary>Imports a copy of the flipbook fixture whose manifest text went through <paramref name="edit"/>.</summary>
        private static string Flipbook(string name, Func<string, string> edit = null, Action<string> sheet = null)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "vfxkit-" + name));
            Directory.CreateDirectory(dir);
            string src = FixtureDir("handoff-flipbook");
            File.Copy(Path.Combine(src, "fixture-flipbook.png"), Path.Combine(dir, "fixture-flipbook.png"), true);
            sheet?.Invoke(Path.Combine(dir, "fixture-flipbook.png"));
            string text = File.ReadAllText(Path.Combine(src, "manifest.json"));
            File.WriteAllText(Path.Combine(dir, "manifest.json"), edit == null ? text : edit(text));
            return ImportAssetTool.Import(Path.Combine(dir, "manifest.json"), Root + "/" + name).folder;
        }

        private static ParticleSystem RootSystem(string prefab) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent<ParticleSystem>();

        [Test]
        public void Flipbook_BecomesAOneBurstSheetEffect_InTheLibrary()
        {
            var r = VfxFromDeliveryTool.Build(Flipbook("flip"), Opts());
            Assert.AreEqual("fixture_flipbook", r.id, "vfx-/-: id from the delivery");
            var ps = RootSystem(r.prefab);
            Assert.IsNotNull(ps);
            Assert.AreEqual(0.35f, ps.main.startLifetime.constant, 1e-4f);
            var tsa = ps.textureSheetAnimation;
            Assert.IsTrue(tsa.enabled);
            Assert.AreEqual(2, tsa.numTilesX);
            Assert.AreEqual(2, tsa.numTilesY);
            Assert.AreEqual(1f, tsa.frameOverTimeMultiplier, 1e-3f, "Unity keeps it just under 1 (0.9999) so the last frame never wraps");
            Assert.AreEqual(1, tsa.cycleCount);
            Assert.AreEqual(1, ps.emission.GetBurst(0).count.constant);
            var mat = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Assert.AreEqual(FlipbookMaterial.ShaderName, mat.shader.name);
            Assert.AreEqual(Root + "/flip/fixture-flipbook.png", AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab).GetComponent<VfxInstance>(), "VfxService adds it");
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(Lib);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<VfxDefinition>(r.definition), lib.Find("fixture_flipbook"));
            Assert.IsNotNull(lib.Find("hit"), "a new game library starts with the Core effects");
        }

        [Test]
        public void FewerFramesThanCells_NeverShowsTheEmptyCells()
        {
            var r = VfxFromDeliveryTool.Build(Flipbook("three", m => m.Replace("\"frames\": 4", "\"frames\": 3")), Opts());
            Assert.AreEqual(0.75f, RootSystem(r.prefab).textureSheetAnimation.frameOverTimeMultiplier, 1e-4f);
        }

        [Test]
        public void ALoopingSheet_CyclesFourTimes()
        {
            var r = VfxFromDeliveryTool.Build(Flipbook("loop", m => m.Replace("\"linear\": false", "\"linear\": false, \"loop\": true")), Opts());
            var ps = RootSystem(r.prefab);
            Assert.AreEqual(4, ps.textureSheetAnimation.cycleCount);
            Assert.AreEqual(0.35f * 4, ps.main.startLifetime.constant, 1e-4f);
        }

        [Test]
        public void Rebuild_KeepsGuids_AndOneLibraryEntry()
        {
            string folder = Flipbook("again");
            var a = VfxFromDeliveryTool.Build(folder, Opts());
            string[] guids = { AssetDatabase.AssetPathToGUID(a.prefab), AssetDatabase.AssetPathToGUID(a.material), AssetDatabase.AssetPathToGUID(a.definition) };
            int count = AssetDatabase.LoadAssetAtPath<VfxLibrary>(Lib).effects.Count;
            var b = VfxFromDeliveryTool.Build(folder, Opts());
            CollectionAssert.AreEqual(guids, new[] { AssetDatabase.AssetPathToGUID(b.prefab), AssetDatabase.AssetPathToGUID(b.material), AssetDatabase.AssetPathToGUID(b.definition) });
            Assert.AreEqual(count, AssetDatabase.LoadAssetAtPath<VfxLibrary>(Lib).effects.Count);
        }

        [Test]
        public void AStill_KeepsTheSheetAspect_AndPops()
        {
            string folder = Flipbook("still",
                m => m.Replace("\"cols\": 2, \"rows\": 2, \"frames\": 4, \"frame_px\": 64,", "\"cols\": 1, \"rows\": 1, \"frames\": 1, \"frame_px\": 64, \"frame_h\": 32,"),
                path =>
                {
                    var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                });
            var ps = RootSystem(VfxFromDeliveryTool.Build(folder, Opts()).prefab);
            Assert.IsTrue(ps.main.startSize3D);
            Assert.AreEqual(2f, ps.main.startSizeX.constant / ps.main.startSizeY.constant, 1e-4f);
            Assert.IsFalse(ps.textureSheetAnimation.enabled);
            Assert.IsTrue(ps.sizeOverLifetime.enabled);
            Assert.Greater(ps.sizeOverLifetime.size.curve.Evaluate(0.15f), 1.05f, "pop overshoot");
        }

        [Test]
        public void ADataSheet_IsRefused_WithoutWritingAnything()
        {
            string folder = Flipbook("data", m => m.Replace("\"linear\": false", "\"linear\": true"));
            var e = Assert.Throws<ArgumentException>(() => VfxFromDeliveryTool.Build(folder, Opts()));
            StringAssert.Contains("data", e.Message);
            Assert.IsFalse(AssetDatabase.IsValidFolder(Out));
        }

        [Test]
        public void AFolderWithoutAManifest_IsRefused()
        {
            AIToolsCommon.EnsureFolder(Root + "/empty");
            Assert.Throws<ArgumentException>(() => VfxFromDeliveryTool.Build(Root + "/empty", Opts()));
        }

        [Test]
        public void ASingleMesh_BecomesMeshParticles()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "vfxkit-single-mesh"));
            Directory.CreateDirectory(dir);
            File.Copy(Path.Combine(FixtureDir("handoff-cube"), "handoff-cube.glb"), Path.Combine(dir, "handoff-cube.glb"), true);
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                File.ReadAllText(Path.Combine(FixtureDir("handoff-cube"), "manifest.json")).Replace("\"kind\": \"prop\"", "\"kind\": \"vfx-mesh\""));
            string folder = ImportAssetTool.Import(Path.Combine(dir, "manifest.json"), Root + "/cube").folder;
            var r = VfxFromDeliveryTool.Build(folder, Opts());
            var ps = RootSystem(r.prefab);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Mesh, renderer.renderMode);
            Assert.IsNotNull(renderer.mesh);
            Assert.AreEqual(12, ps.emission.GetBurst(0).count.constant);
            Assert.IsTrue(ps.main.startRotation3D);
        }

        [Test]
        public void AMultiPieceMesh_BecomesAMeshBurst()
        {
            string folder = ImportAssetTool.Import(Path.Combine(FixtureDir("handoff-pieces"), "manifest.json"), Root + "/pieces").folder;
            var r = VfxFromDeliveryTool.Build(folder, Opts("pieces"));
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab);
            var burst = root.GetComponent<MeshBurst>();
            Assert.IsNotNull(burst);
            Assert.AreEqual(2, burst.PieceCount);
            Assert.IsNull(root.GetComponent<ParticleSystem>());
            Assert.AreEqual("pieces", r.id);
        }
    }
}
