using System.Collections.Generic;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Editor.AITools
{
    /// <summary>
    /// F7 board clip install (spec 2026-10-03): names the PotaBoard layer, takes it out of the PC and Mobile renderers' opaque and
    /// transparent passes and adds two RenderObjects features that draw it stencil-tested (Equal BoardClip.StencilRef), after the
    /// opaques and after the transparents. Idempotent: features named "BoardClip Opaque/Transparent" are recognised and kept.
    /// Changes Assets/Settings/PC_Renderer.asset, Mobile_Renderer.asset and ProjectSettings/TagManager.asset.
    /// </summary>
    [McpForUnityTool("ai_install_board_clip", Description = "Installs the Color Hunt board clip: the PotaBoard layer, two stencil-tested RenderObjects features on the PC and Mobile renderers, the stencil mask material template. Idempotent.")]
    public static class BoardClipInstaller
    {
        public const string OpaqueName = "BoardClip Opaque", TransparentName = "BoardClip Transparent";
        public static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        public static object HandleCommand(JObject p)
        {
            var report = InstallAll();
            return new { success = true, report };
        }

        [MenuItem("AI Tools/Rendering/Install Board Clip")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", InstallAll()));

        public static List<string> InstallAll()
        {
            var report = new List<string>();
            int layer = PotaLookImportTool.EnsureLayer(BoardClip.LayerName);
            report.Add($"layer {BoardClip.LayerName} = {layer}");
            foreach (var path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) { report.Add($"{path}: not a UniversalRendererData"); continue; }
                int added = Install(data, layer);
                report.Add($"{path}: {added} feature(s) added");
            }
            PotaMaterialTemplates.Ensure();
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Installs into one renderer; returns how many features were added (0 when already installed).</summary>
        public static int Install(UniversalRendererData data, int layer)
        {
            int bit = 1 << layer;
            if ((data.opaqueLayerMask.value & bit) != 0) data.opaqueLayerMask = data.opaqueLayerMask.value & ~bit;
            if ((data.transparentLayerMask.value & bit) != 0) data.transparentLayerMask = data.transparentLayerMask.value & ~bit;
            int added = 0;
            if (Find(data, OpaqueName) == null) { Add(data, OpaqueName, RenderQueueType.Opaque, RenderPassEvent.AfterRenderingOpaques, bit); added++; }
            // the board's transparents (labels) go BEFORE the default transparents, so burst debris, confetti and the selection ring
            // (Default layer, in front of the board) still draw over them (review 2026-10-03)
            if (Find(data, TransparentName) == null) { Add(data, TransparentName, RenderQueueType.Transparent, RenderPassEvent.BeforeRenderingTransparents, bit); added++; }
            foreach (var f in data.rendererFeatures)
            {
                if (!(f is RenderObjects ro)) continue;
                if (f.name == OpaqueName) { ro.settings.filterSettings.LayerMask = bit; ro.settings.Event = RenderPassEvent.AfterRenderingOpaques; }
                if (f.name == TransparentName) { ro.settings.filterSettings.LayerMask = bit; ro.settings.Event = RenderPassEvent.BeforeRenderingTransparents; }
            }
            data.SetDirty();
            EditorUtility.SetDirty(data);
            return added;
        }

        private static ScriptableRendererFeature Find(UniversalRendererData data, string name)
        {
            foreach (var f in data.rendererFeatures) if (f != null && f.name == name) return f;
            return null;
        }

        private static void Add(UniversalRendererData data, string name, RenderQueueType queue, RenderPassEvent evt, int layerBit)
        {
            var feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = name;
            feature.settings.passTag = name;
            feature.settings.Event = evt;
            feature.settings.filterSettings.RenderQueueType = queue;
            feature.settings.filterSettings.LayerMask = layerBit;
            feature.settings.stencilSettings.overrideStencilState = true;
            feature.settings.stencilSettings.stencilReference = BoardClip.StencilRef;
            feature.settings.stencilSettings.stencilCompareFunction = CompareFunction.Equal;
            feature.settings.stencilSettings.passOperation = StencilOp.Keep;
            feature.settings.stencilSettings.failOperation = StencilOp.Keep;
            feature.settings.stencilSettings.zFailOperation = StencilOp.Keep;
            if (EditorUtility.IsPersistent(data)) AssetDatabase.AddObjectToAsset(feature, data);
            // the serialized list plus the GUID map the renderer keeps beside it (ScriptableRendererDataEditor.AddComponent)
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
