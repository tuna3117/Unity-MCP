using UnityEngine;

namespace Project.Rendering
{
    /// <summary>
    /// Materials made at runtime from a template asset. A player build carries only the shaders some built asset references, so a bare
    /// <c>Shader.Find</c> of anything else returns null on the device and <c>new Material(null)</c> throws. A template referenced from a
    /// settings asset keeps the shader — and the keyword variant the template uses — in the build. Without a template (settings made in
    /// memory, tests) the shader is looked up by name, which works in the Editor.
    /// </summary>
    public static class RuntimeMaterial
    {
        public static Material From(Material template, string shaderName, string name = null)
        {
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) Debug.LogError($"RuntimeMaterial: shader '{shaderName}' is not in this build and no template was set.");
                m = new Material(shader);
            }
            if (name != null) m.name = name;
            return m;
        }
    }
}
