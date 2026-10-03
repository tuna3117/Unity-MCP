using NUnit.Framework;
using Project.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tests
{
    public class FlipbookShaderTests
    {
        [Test]
        public void Shader_Compiles()
        {
            var shader = Shader.Find(FlipbookMaterial.ShaderName);
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        }

        [Test]
        public void SetBlend_SwitchesBetweenStraightAlphaAndAdditive()
        {
            var m = new Material(Shader.Find(FlipbookMaterial.ShaderName));
            try
            {
                FlipbookMaterial.SetBlend(m, additive: false);
                Assert.AreEqual((float)BlendMode.SrcAlpha, m.GetFloat("_SrcBlend"));
                Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, m.GetFloat("_DstBlend"));
                FlipbookMaterial.SetBlend(m, additive: true);
                Assert.AreEqual((float)BlendMode.One, m.GetFloat("_DstBlend"));
                Assert.IsTrue(FlipbookMaterial.IsAdditive(m));
                Assert.AreEqual(1f, m.GetFloat("_Intensity"), "HDR multiplier defaults to 1");
            }
            finally
            {
                Object.DestroyImmediate(m);
            }
        }
    }
}
