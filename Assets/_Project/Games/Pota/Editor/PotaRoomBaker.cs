using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Bakes three.js' RoomEnvironment (examples/jsm/environments/RoomEnvironment.js, r180) — the room Pota's glossy blocks reflect
    /// (yapbozSahne.ts ortamUret) — into an HDR cubemap with specular convolution. The room is built far below the open scene on the
    /// PotaRoom layer (DontSave), baked by one reflection probe and destroyed. three's z is mirrored (Unity's board faces −z), so
    /// rotations about y flip sign.
    /// </summary>
    public static class PotaRoomBaker
    {
        public const string CubemapPath = PotaLookImportTool.LookFolder + "/PotaRoom.exr";
        public static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);

        private static readonly Vector3 LightPos = new Vector3(0.418f, 16.199f, 0.300f);
        private static readonly (Vector3 p, float ry, Vector3 s)[] Boxes =
        {
            (new Vector3(-10.906f, 2.009f, 1.846f), -0.195f, new Vector3(2.328f, 7.905f, 4.651f)),
            (new Vector3(-5.607f, -0.754f, -0.758f), 0.994f, new Vector3(1.970f, 1.534f, 3.955f)),
            (new Vector3(6.167f, 0.857f, 7.803f), 0.561f, new Vector3(3.927f, 6.285f, 3.687f)),
            (new Vector3(-2.017f, 0.018f, 6.124f), 0.333f, new Vector3(2.002f, 4.566f, 2.064f)),
            (new Vector3(2.291f, -0.756f, -2.621f), -0.286f, new Vector3(1.546f, 1.552f, 1.496f)),
            (new Vector3(-2.193f, -0.369f, -5.547f), 0.516f, new Vector3(3.875f, 3.487f, 2.986f)),
        };
        private static readonly (Vector3 p, Vector3 s, float e)[] Panels =
        {
            (new Vector3(-16.116f, 14.37f, 8.208f), new Vector3(0.1f, 2.428f, 2.739f), 50f),
            (new Vector3(-16.109f, 18.021f, -8.207f), new Vector3(0.1f, 2.425f, 2.751f), 50f),
            (new Vector3(14.904f, 12.198f, -1.832f), new Vector3(0.15f, 4.265f, 6.331f), 17f),
            (new Vector3(-0.462f, 8.89f, 14.520f), new Vector3(4.38f, 5.441f, 0.088f), 43f),
            (new Vector3(3.235f, 11.486f, -12.541f), new Vector3(2.5f, 2.0f, 0.1f), 20f),
            (new Vector3(0f, 20f, 0f), new Vector3(1f, 0.1f, 1f), 100f),
        };

        public static Vector3 Mirror(Vector3 three) => new Vector3(three.x, three.y, -three.z);

        /// <summary>The room around <paramref name="origin"/> (three's cube camera), every renderer on <paramref name="layer"/>; never saved.</summary>
        public static GameObject BuildRoom(Vector3 origin, int layer)
        {
            var root = new GameObject("PotaRoomBake") { hideFlags = HideFlags.DontSave, layer = layer };
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var shader = Shader.Find("Project/PotaRoom");
            var light = origin + Mirror(LightPos);
            Material Mat(float albedo, float emission, CullMode cull)
            {
                var m = new Material(shader) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_Albedo", albedo);
                m.SetFloat("_Emission", emission);
                m.SetVector("_LightPos", light);
                m.SetFloat("_LightIntensity", 900f);
                m.SetFloat("_LightRange", 28f);
                m.SetFloat("_Cull", (float)cull);
                return m;
            }
            Part(root, "Room", cube, Mat(1f, 0f, CullMode.Front), origin, new Vector3(-0.757f, 13.219f, 0.717f), 0f, new Vector3(31.713f, 28.305f, 28.591f), layer);
            var boxMat = Mat(1f, 0f, CullMode.Back);
            for (int i = 0; i < Boxes.Length; i++) Part(root, $"Box{i + 1}", cube, boxMat, origin, Boxes[i].p, Boxes[i].ry, Boxes[i].s, layer);
            for (int i = 0; i < Panels.Length; i++) Part(root, $"Light{i + 1}", cube, Mat(0f, Panels[i].e, CullMode.Back), origin, Panels[i].p, 0f, Panels[i].s, layer);
            return root;
        }

        private static void Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 origin, Vector3 threePos, float threeRotY, Vector3 scale, int layer)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = layer };
            go.transform.SetParent(root.transform, false);
            go.transform.position = origin + Mirror(threePos);
            go.transform.rotation = Quaternion.Euler(0f, -threeRotY * Mathf.Rad2Deg, 0f);
            go.transform.localScale = scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);   // a baked probe draws only these
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Builds the room, bakes <see cref="CubemapPath"/> (256², HDR, specular convolution), removes the room.</summary>
        public static Cubemap Bake(List<string> report)
        {
            int layer = PotaLookImportTool.EnsureLayer(PotaLookImportTool.RoomLayer);
            var room = BuildRoom(Origin, layer);
            var probeGo = new GameObject("PotaRoomProbe") { hideFlags = HideFlags.DontSave };
            try
            {
                probeGo.transform.position = Origin;
                var p = probeGo.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Baked;
                p.resolution = 256;
                p.hdr = true;
                p.cullingMask = 1 << layer;
                p.clearFlags = ReflectionProbeClearFlags.SolidColor;
                p.backgroundColor = Color.black;
                p.nearClipPlane = 0.05f;
                p.farClipPlane = 80f;
                if (!Lightmapping.BakeReflectionProbe(p, CubemapPath)) throw new System.InvalidOperationException("reflection probe bake failed");
            }
            finally
            {
                Object.DestroyImmediate(probeGo);
                Object.DestroyImmediate(room);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(CubemapPath);
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureShape = TextureImporterShape.TextureCube;
            ts.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            ts.mipmapEnabled = true;
            ts.filterMode = FilterMode.Trilinear;
            imp.SetTextureSettings(ts);
            imp.SaveAndReimport();
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(CubemapPath);
            report.Add($"room reflection {CubemapPath} {(cube != null ? cube.width : 0)}²");
            return cube;
        }
    }
}
