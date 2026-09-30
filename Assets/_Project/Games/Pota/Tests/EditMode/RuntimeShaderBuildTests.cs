using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tests
{
    /// <summary>
    /// A player build carries only the shaders some built asset references (or GraphicsSettings' Always Included list). A runtime
    /// <c>Shader.Find</c> of anything else returns null on the device, and <c>new Material(null)</c> throws — the puzzle then never deals.
    /// </summary>
    public class RuntimeShaderBuildTests
    {
        /// <summary>Shader names the runtime looks up: <c>Shader.Find("…")</c> and the fallback of <c>RuntimeMaterial.From(template, "…")</c>.</summary>
        private static readonly Regex Find = new Regex("(?:Shader\\.Find\\(|RuntimeMaterial\\.From\\([^,()]+,\\s*)\"([^\"]+)\"");

        private static IEnumerable<string> RuntimeShaderNames()
        {
            foreach (var root in new[] { "Assets/_Project/Games/Pota/Scripts", "Assets/_Project/Core/Scripts" })
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                    foreach (Match m in Find.Matches(File.ReadAllText(file)))
                        yield return m.Groups[1].Value;
        }

        /// <summary>Shaders in a Pota build: dependencies of the profile's scenes and of the render pipeline, plus Always Included.</summary>
        private static HashSet<string> BuiltShaderNames()
        {
            var roots = new List<string>();
            var profile = GameProfile.Find("Pota");
            Assert.IsNotNull(profile, "Games/Pota/PotaProfile.asset");
            foreach (var scene in profile.Scenes) if (scene != null) roots.Add(AssetDatabase.GetAssetPath(scene));
            var pipelines = new List<Object> { GraphicsSettings.defaultRenderPipeline };
            for (int q = 0; q < QualitySettings.count; q++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(q));
            foreach (var rp in pipelines) if (rp != null) roots.Add(AssetDatabase.GetAssetPath(rp));
            foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineGlobalSettings")) roots.Add(AssetDatabase.GUIDToAssetPath(guid));

            var names = new HashSet<string>();
            foreach (var path in AssetDatabase.GetDependencies(roots.ToArray(), true))
                if (!path.EndsWith(".unity"))                                  // scene files are not loadable as assets; their shaders are deps
                    foreach (var shader in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Shader>())
                        names.Add(shader.name);
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var always = graphics.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < always.arraySize; i++)
                if (always.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) names.Add(s.name);
            return names;
        }

        [Test]
        public void EveryShaderTheRuntimeFinds_IsInAPotaBuild()
        {
            var built = BuiltShaderNames();
            var missing = RuntimeShaderNames().Distinct().Where(n => !built.Contains(n)).OrderBy(n => n).ToList();
            Assert.IsEmpty(missing, "Shader.Find names no built asset references (stripped from the player): " + string.Join(", ", missing));
        }
    }
}
