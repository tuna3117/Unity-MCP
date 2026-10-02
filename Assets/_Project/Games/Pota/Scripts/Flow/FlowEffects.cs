using System.Collections.Generic;
using Project.ColorHunt;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// The 2026-10-02 candidates' extra pictures, all from the studio assets already in ColorHuntSettings: candidate 2's
    /// screen-edge speed lines (Blender vfx-speed-lines, two mirrored strips on the camera), its swish ring and SWISH! lettering
    /// at the giant hoop's rim (Court's ring texture, vfx-letter-swish); candidate 3's stamp puff and star flash on the glass
    /// (vfx-puff-toon, Pota's star sprite). Materials clone the settings' templates (player builds keep the shaders).
    /// </summary>
    public class FlowEffects : MonoBehaviour
    {
        public const int SpeedFrames = 8, SpeedCols = 4, SpeedRows = 2;
        public const int PuffCols = 4, PuffRows = 4;
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera _cam;
        private ColorHuntSettings _s;
        private MeshRenderer _speedLeft, _speedRight, _ring, _letter;
        private ParticleSystem _puff, _star;
        private MaterialPropertyBlock _mpb;
        private readonly List<Material> _materials = new List<Material>();
        private float _speedDistance = 0.8f;

        public bool SpeedLinesShown => _speedLeft != null && _speedLeft.enabled;
        public bool LetterShown => _letter != null && _letter.enabled;
        public int PuffCount => (_puff != null ? _puff.particleCount : 0) + (_star != null ? _star.particleCount : 0);

        public static FlowEffects Create(Transform parent, ColorHuntSettings s, Camera cam)
        {
            var go = new GameObject("FlowEffects");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<FlowEffects>();
            fx.Build(s, cam);
            return fx;
        }

        private void Build(ColorHuntSettings s, Camera cam)
        {
            _s = s;
            _cam = cam;
            _mpb = new MaterialPropertyBlock();
            _speedDistance = Mathf.Max(cam.nearClipPlane * 2.5f, 0.6f);
            var speedMat = Quad(s.SpeedLines, "FlowSpeedLines");
            _speedLeft = QuadRenderer("SpeedLeft", cam.transform, speedMat, 30);
            _speedRight = QuadRenderer("SpeedRight", cam.transform, speedMat, 30);
            _ring = QuadRenderer("SwishRing", transform, Quad(CourtSpecials.RingTexture(), "FlowSwishRing"), 14);
            _letter = QuadRenderer("SwishLetter", transform, Quad(s.LetterSwish, "FlowSwishLetter"), 15);

            if (s.CandyPuff != null && s.FlipbookTemplate != null)
            {
                _puff = StudioParticles.System(transform, "StampPuff", Keep(StudioParticles.Flipbook(s, s.CandyPuff)), ParticleSystemRenderMode.Billboard, 12, 20);
                StudioParticles.Sheet(_puff, PuffCols, PuffRows);
                var main = _puff.main;
                main.useUnscaledTime = true;                        // the transition runs in real time
            }
            if (s.AdditiveTemplate != null)
            {
                _star = StudioParticles.System(transform, "StampStar", Keep(StudioParticles.Additive(s, s.StarSprite, 2f)), ParticleSystemRenderMode.Billboard, 4, 21);
                StudioParticles.SizeOverLife(_star, u => u < 0.3f ? Mathf.Lerp(0.4f, 1f, u / 0.3f) : Mathf.Lerp(1f, 0.7f, (u - 0.3f) / 0.7f));
                StudioParticles.AlphaOverLife(_star, u => 1f - u * u);
                var main = _star.main;
                main.useUnscaledTime = true;
            }
            Clear();
        }

        private Material Quad(Texture tex, string name)
        {
            var m = RuntimeMaterial.From(_s.LabelTemplate, "Project/PotaLabel", name);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            return Keep(m);
        }

        private Material Keep(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private static MeshRenderer QuadRenderer(string name, Transform parent, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        private void Draw(MeshRenderer r, Vector4 uv, float alpha)
        {
            _mpb.Clear();
            _mpb.SetVector(UvRectId, uv);
            _mpb.SetVector(ColorId, new Vector4(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            r.SetPropertyBlock(_mpb);
        }

        public static Vector4 Frame(int f, int cols, int rows)
        {
            int c = f % cols, r = f / cols;
            return new Vector4(c / (float)cols, 1f - (r + 1) / (float)rows, 1f / cols, 1f / rows);
        }

        /// <summary>
        /// Candidate 2's speed lines: two strips in front of the camera, each 45 % of the view wide from its edge, streaks
        /// pointing at the centre (the right strip is the sheet, the left its mirror). <paramref name="alpha"/> 0 hides them.
        /// </summary>
        public void SetSpeedLines(float alpha, int frame, float fov, float aspect)
        {
            bool on = alpha > 0.001f;
            _speedLeft.enabled = _speedRight.enabled = on;
            if (!on) return;
            float h = 2f * _speedDistance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), w = h * aspect;
            float stripW = w * 0.45f;
            var uv = Frame(Mathf.Clamp(frame, 0, SpeedFrames - 1), SpeedCols, SpeedRows);
            Place(_speedRight, new Vector3(w * 0.5f - stripW * 0.5f, 0f, _speedDistance), new Vector3(stripW, h, 1f));
            Place(_speedLeft, new Vector3(-w * 0.5f + stripW * 0.5f, 0f, _speedDistance), new Vector3(-stripW, h, 1f));
            Draw(_speedRight, uv, alpha);
            Draw(_speedLeft, uv, alpha);
        }

        private static void Place(MeshRenderer r, Vector3 localPos, Vector3 localScale)
        {
            r.transform.localPosition = localPos;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = localScale;
        }

        /// <summary>
        /// Candidate 2's swish at the rim: a flat ring growing R → 1.9 R and fading (<paramref name="ringU"/> 0..1, &lt; 0 off),
        /// SWISH! lettering <paramref name="letterWidth"/> wide above the rim facing −z (<paramref name="letter"/> = scale, alpha).
        /// </summary>
        public void SetSwish(Vector3 rimCenter, float rimRadius, float ringU, Vector2 letter, float letterWidth)
        {
            bool ring = ringU >= 0f && ringU < 1f;
            _ring.enabled = ring;
            if (ring)
            {
                float size = 2f * rimRadius * Mathf.Lerp(1f, 1.9f, 1f - (1f - ringU) * (1f - ringU));
                _ring.transform.position = rimCenter + Vector3.up * 0.05f;
                _ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _ring.transform.localScale = new Vector3(size, size, 1f);
                Draw(_ring, new Vector4(0f, 0f, 1f, 1f), 0.95f * Mathf.Pow(1f - ringU, 1.5f));
            }
            bool shown = letter.x > 0.001f && letter.y > 0.001f;
            _letter.enabled = shown;
            if (!shown) return;
            float w = letterWidth * letter.x;
            _letter.transform.position = rimCenter + new Vector3(0f, 1.4f + rimRadius * 0.5f, -0.3f);
            _letter.transform.rotation = Quaternion.identity;                // faces −z, the camera in front of the board
            _letter.transform.localScale = new Vector3(w, w * 0.5f, 1f);
            Draw(_letter, new Vector4(0f, 0f, 1f, 1f), letter.y);
        }

        /// <summary>Candidate 3's stamp on the glass: a few toon puffs and a star flash at <paramref name="at"/>, <paramref name="size"/> metres across.</summary>
        public void Stamp(Vector3 at, float size)
        {
            if (_puff != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI * 2f / 6f + 0.4f;
                    var off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (size * 0.22f) + Vector3.back * 0.3f;
                    StudioParticles.Emit(_puff, at + off, size * (0.55f + 0.1f * (i % 3)), 0.35f, Color.white,
                        new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (size * 0.6f), i * 60f);
                }
            }
            if (_star != null) StudioParticles.Emit(_star, at + Vector3.back * 0.4f, size * 1.3f, 0.28f, new Color(1f, 0.95f, 0.75f, 1f));
        }

        public void Clear()
        {
            if (_speedLeft != null) _speedLeft.enabled = false;
            if (_speedRight != null) _speedRight.enabled = false;
            if (_ring != null) _ring.enabled = false;
            if (_letter != null) _letter.enabled = false;
            if (_puff != null) _puff.Clear();
            if (_star != null) _star.Clear();
        }

        private void OnDestroy()
        {
            foreach (var m in _materials) if (m != null) Destroy(m);
            _materials.Clear();
        }
    }
}
