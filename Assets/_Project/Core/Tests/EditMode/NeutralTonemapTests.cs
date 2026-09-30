using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    /// <summary>
    /// "Neutral" tonemapping in this project is Khronos PBR Neutral — three.js' <c>NeutralToneMapping</c> — so a look ported from three
    /// keeps its tones. URP's own Neutral is a Hable-style curve: 1.0 comes out 0.61 instead of 0.87.
    /// </summary>
    public class NeutralTonemapTests
    {
        private const int Layer = 31;
        private GameObject _cam, _quad, _volume;
        private Material _mat;
        private VolumeProfile _profile;
        private RenderTexture _rt;

        /// <summary>three.js tonemapping_pars_fragment NeutralToneMapping (exposure 1).</summary>
        public static Vector3 Khronos(Vector3 c)
        {
            const float start = 0.8f - 0.04f, desaturation = 0.15f;
            float x = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
            float offset = x < 0.08f ? x - 6.25f * x * x : 0.04f;
            c -= Vector3.one * offset;
            float peak = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
            if (peak < start) return c;
            const float d = 1f - start;
            float newPeak = 1f - d * d / (peak + d - start);
            c *= newPeak / peak;
            float g = 1f - 1f / (desaturation * (peak - newPeak) + 1f);
            return Vector3.Lerp(c, Vector3.one * newPeak, g);
        }

        [SetUp]
        public void SetUp()
        {
            _cam = new GameObject("TonemapTestCamera");
            var cam = _cam.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = true;
            cam.cullingMask = 1 << Layer;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << Layer;
            data.antialiasing = AntialiasingMode.None;
            data.dithering = false;

            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.layer = Layer;
            _quad.transform.position = new Vector3(0f, 0f, 5f);
            _quad.transform.localScale = Vector3.one * 10f;
            _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _quad.GetComponent<MeshRenderer>().sharedMaterial = _mat;

            // Over the pipeline's default profile (bloom, vignette): only the tone curve is left.
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            _profile.Add<Bloom>(true).intensity.value = 0f;
            _profile.Add<Vignette>(true).intensity.value = 0f;
            _volume = new GameObject("TonemapTestVolume") { layer = Layer };
            var volume = _volume.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = _profile;

            // A float target: URP sizes its intermediate buffer from the target, and an 8-bit one would clip the HDR input at 1.
            _rt = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            cam.targetTexture = _rt;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cam);
            Object.DestroyImmediate(_quad);
            Object.DestroyImmediate(_volume);
            Object.DestroyImmediate(_mat);
            Object.DestroyImmediate(_profile);
            _rt.Release();
            Object.DestroyImmediate(_rt);
        }

        private Vector3 Render(Vector3 linear)
        {
            var block = new MaterialPropertyBlock();                                          // a block vector: linear, no sRGB conversion
            block.SetVector("_BaseColor", new Vector4(linear.x, linear.y, linear.z, 1f));
            _quad.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            _cam.GetComponent<Camera>().Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(16, 16, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var c = tex.GetPixel(8, 8);
            Object.DestroyImmediate(tex);
            return new Vector3(c.r, c.g, c.b);
        }

        [Test]
        public void Neutral_IsKhronosPbrNeutral_LikeThreeJs()
        {
            var inputs = new[]
            {
                new Vector3(0.18f, 0.18f, 0.18f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f), new Vector3(2f, 2f, 2f),
                new Vector3(4f, 4f, 4f), new Vector3(1f, 0.8f, 0.1f), new Vector3(0.2f, 0.6f, 3f),
            };
            var report = "";
            bool ok = true;
            foreach (var input in inputs)
            {
                Vector3 got = Render(input), want = Khronos(input);
                bool close = (got - want).magnitude < 0.03f;
                ok &= close;
                report += $"\n{input:F2} → {got:F3} (Khronos {want:F3}){(close ? "" : "  ✗")}";
            }
            Assert.IsTrue(ok, "Neutral tonemapping ≠ Khronos PBR Neutral:" + report);
        }
    }
}
