using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>Running the template tool again (every ai_potalook_import / ai_studio_import does) must not degrade a template.</summary>
    public class PotaMaterialTemplatesTests
    {
        [Test]
        public void Ensure_Twice_KeepsTheSelectionTemplateTransparent()
        {
            PotaMaterialTemplates.Ensure();
            PotaMaterialTemplates.Ensure();
            var m = AssetDatabase.LoadAssetAtPath<Material>(PotaMaterialTemplates.Folder + "/PotaSelection.mat");
            Assert.IsNotNull(m);
            Assert.AreEqual("Transparent", m.GetTag("RenderType", false), "URP's transparent tag");
            Assert.IsFalse(m.GetShaderPassEnabled("ShadowCaster"), "a transparent ring casts no shadow");
            Assert.AreEqual(0.9f, m.GetColor("_BaseColor").a, 1e-3f);
        }

        [Test]
        public void Ensure_WiresTheToonTemplate()
        {
            PotaMaterialTemplates.Ensure();
            var m = AssetDatabase.LoadAssetAtPath<Material>(PotaMaterialTemplates.Folder + "/PotaToon.mat");
            Assert.IsNotNull(m);
            Assert.AreEqual("Project/Toon", m.shader.name);
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");
            Assert.AreEqual(m, s.ToonTemplate, "ColorHuntSettings.ToonTemplate keeps Project/Toon in a Pota build");
        }
    }
}
