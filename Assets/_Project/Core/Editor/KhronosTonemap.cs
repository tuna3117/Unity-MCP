using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Editor
{
    /// <summary>
    /// Makes "Neutral" tonemapping in this project Khronos PBR Neutral — three.js' NeutralToneMapping — instead of URP's Hable-style
    /// curve (1.0 → 0.87 instead of 0.63; below 0.76 it is nearly the identity), so looks ported from three keep their tones.
    /// Every renderer gets a copy of URP's PostProcessData whose uberPostPS is Core/Shaders/UberPostKhronos.shader, and every pipeline
    /// asset grades in LDR (the project's setting): there the tonemap runs in the uber shader, per pixel. HDR grading bakes the tonemap
    /// into a 32³ colour LUT, and Khronos — every channel scaled by the brightest — cannot live in one (1.0 came out 0.82, 4.0 0.94).
    /// Idempotent.
    /// </summary>
    public static class KhronosTonemap
    {
        public const string DataPath = "Assets/Settings/KhronosPostProcessData.asset";
        public const string ShaderPath = "Assets/_Project/Core/Shaders/UberPostKhronos.shader";
        private const string UrpDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("AI Tools/Rendering/Use Khronos Neutral Tonemapping")]
        public static void Menu() => Debug.Log(Apply());

        public static string Apply()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) return "Khronos tonemap: " + ShaderPath + " missing";
            var urp = AssetDatabase.LoadAssetAtPath<PostProcessData>(UrpDataPath);
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(DataPath);
            bool changed = data == null;
            if (data == null)
            {
                data = Object.Instantiate(urp);
                data.shaders.uberPostPS = shader;
                AssetDatabase.CreateAsset(data, DataPath);           // set up before it is written (CLAUDE.md lesson)
            }
            else if (data.shaders.uberPostPS != shader || data.shaders.lutBuilderHdrPS != urp.shaders.lutBuilderHdrPS)
            {
                data.shaders.uberPostPS = shader;
                data.shaders.lutBuilderHdrPS = urp.shaders.lutBuilderHdrPS;
                EditorUtility.SetDirty(data);
                changed = true;
            }

            int pipelines = 0, renderers = 0;
            foreach (var rp in Pipelines())
            {
                if (rp.colorGradingMode != ColorGradingMode.LowDynamicRange)
                {
                    rp.colorGradingMode = ColorGradingMode.LowDynamicRange;
                    EditorUtility.SetDirty(rp);
                    pipelines++;
                }
                foreach (var rd in rp.rendererDataList)
                {
                    if (rd is not UniversalRendererData urd) continue;
                    if (urd.postProcessData != data)
                    {
                        urd.postProcessData = data;
                        EditorUtility.SetDirty(urd);
                        renderers++;
                    }
                    else if (!changed) continue;
                    urd.SetDirty();                                      // rebuilds the renderer's passes with the new shaders
                }
            }
            AssetDatabase.SaveAssets();
            return $"Khronos Neutral tonemapping: {renderers} renderer(s) rewired, {pipelines} pipeline(s) switched to LDR grading";
        }

        private static IEnumerable<UniversalRenderPipelineAsset> Pipelines()
        {
            var seen = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d && seen.Add(d)) yield return d;
            for (int q = 0; q < QualitySettings.count; q++)
                if (QualitySettings.GetRenderPipelineAssetAt(q) is UniversalRenderPipelineAsset a && seen.Add(a)) yield return a;
        }
    }
}
