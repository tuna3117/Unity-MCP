using UnityEngine;
using UnityEngine.Rendering;

namespace Project.VFX
{
    /// <summary>
    /// Settings of the studio flipbook shader (<c>Project/Flipbook</c>). Materials are made at edit time by
    /// <c>ai_vfx_from_delivery</c>; at run time copy one (a player build only carries shaders some asset references).
    /// </summary>
    public static class FlipbookMaterial
    {
        public const string ShaderName = "Project/Flipbook";

        public static void SetBlend(Material m, bool additive)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        }

        public static bool IsAdditive(Material m) => m.HasProperty("_DstBlend") && (int)m.GetFloat("_DstBlend") == (int)BlendMode.One;
    }
}
