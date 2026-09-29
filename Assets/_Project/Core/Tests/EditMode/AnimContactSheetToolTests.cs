using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class AnimContactSheetToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__contact_test__";
        private const string OutPath = "Temp/contact_sheet_test.png";

        private static string RigFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-rig", "manifest.json"));

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        // The sheet rendered skinned meshes with stale skinning: only the root bone's movement showed, so a shot's
        // raised arms stayed at the hips while the bone table said they were above the head. The fixture's root does not
        // move, so a stale render makes every frame identical. The clip bends 0 -> 0.8 rad -> 0: compare the first frame
        // with the middle one (the bend's peak).
        [Test]
        public void SkinnedMeshFollowsTheClip()
        {
            ImportAssetTool.Import(RigFixture, Root + "/rig");
            string glb = Root + "/rig/handoff-rig.glb";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
            var clip = AssetDatabase.LoadAllAssetsAtPath(glb).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

            AnimContactSheetTool.Render(model, clip, 3, 90f, 128, OutPath);  // the bend is about X: seen from the side

            var sheet = new Texture2D(2, 2);
            sheet.LoadImage(File.ReadAllBytes(OutPath));
            bool Shape(int x, int y)
            {
                var c = sheet.GetPixel(x, y);
                return Mathf.Abs(c.r - 0.16f) + Mathf.Abs(c.g - 0.16f) + Mathf.Abs(c.b - 0.18f) > 0.1f;
            }
            int both = 0, either = 0;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    bool a = Shape(x, y), b = Shape(x + 128, y);
                    if (a && b) both++;
                    if (a || b) either++;
                }
            Object.DestroyImmediate(sheet);
            Assert.Greater(either, 50, "nothing rendered");
            Assert.Less((float)both / either, 0.9f, "first and middle frame look the same: the mesh did not follow the bones");
        }
    }
}
