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
    
        private static string ClipsFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-rig-clips", "manifest.json"));

        [Test]
        public void PerFrameMode_WritesEveryFrame_AndSharedFramingFilmsTwoClipsTheSame()
        {
            ImportAssetTool.Import(ClipsFixture, Root + "/clips");
            string fbx = Root + "/clips/handoff-rig-clips.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var bend = AnimContactSheetTool.FindClip(fbx, "bend");
            var twist = AnimContactSheetTool.FindClip(fbx, "twist");
            Assert.IsNotNull(bend);
            Assert.IsNotNull(twist);
            string a = Path.GetFullPath("Temp/contact_frames_bend"), b = Path.GetFullPath("Temp/contact_frames_twist");
            foreach (var d in new[] { a, b }) if (Directory.Exists(d)) Directory.Delete(d, true);
            int framesA = Mathf.RoundToInt(bend.length * 30) + 1;
            AnimContactSheetTool.Render(model, bend, framesA, 90f, 96, null, a, new[] { twist });
            AnimContactSheetTool.Render(model, twist, Mathf.RoundToInt(twist.length * 30) + 1, 90f, 96, null, b, new[] { bend });
            var files = Directory.GetFiles(a, "f_*.png").OrderBy(f => f).ToArray();
            Assert.AreEqual(framesA, files.Length);
            StringAssert.Contains("_t0.000", Path.GetFileName(files[0]));
            // Both clips start at the rest pose: with one shared camera their first frames are the same picture.
            var first = Directory.GetFiles(b, "f_0000_*.png").Single();
            CollectionAssert.AreEqual(File.ReadAllBytes(files[0]), File.ReadAllBytes(first), "same camera for raw | stylised");
        }
    }
}
