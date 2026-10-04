using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Tests
{
    public class VfxContactSheetToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__vfxsheet_test__";

        private static string FixtureDir(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures", name));

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        private static string Effect(string fixture)
        {
            string folder = ImportAssetTool.Import(Path.Combine(FixtureDir(fixture), "manifest.json"), Root + "/" + fixture).folder;
            return VfxFromDeliveryTool.Build(folder, new VfxFromDeliveryTool.Options { outFolder = Root + "/out", library = Root + "/out/Lib.asset" }).prefab;
        }

        private static string Png(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "vfxsheet-" + name + ".png"));

        [Test]
        public void SheetEffect_IsVisibleWhileItPlays_AndGoneAtTheEnd()
        {
            string prefab = Effect("handoff-flipbook");
            int before = SceneManager.GetActiveScene().rootCount;
            var r = VfxContactSheetTool.Render(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), 6, 128, Png("flip"));
            Assert.AreEqual(before, SceneManager.GetActiveScene().rootCount, "nothing left in the scene");
            Assert.IsTrue(File.Exists(r.png));
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(r.png));
            Assert.AreEqual(6 * 128, tex.width);
            Assert.AreEqual(2 * 128, tex.height, "dark and light rows");
            Object.DestroyImmediate(tex);
            Assert.AreEqual(0.35f, r.duration, 1e-4f);
            Assert.IsTrue(r.visible.Take(5).All(v => v > 0), string.Join(",", r.visible));
            Assert.AreEqual(0, r.visible[5], "a one-shot effect is over at its end");
        }

        [Test]
        public void MeshBurst_IsVisible_ThenShrinksAway()
        {
            string prefab = Effect("handoff-pieces");
            var r = VfxContactSheetTool.Render(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), 5, 128, Png("pieces"));
            Assert.Greater(r.visible[0], 0);
            Assert.Greater(r.visible[2], 0);
            Assert.AreEqual(0, r.visible[4], string.Join(",", r.visible));
        }
    }
}
