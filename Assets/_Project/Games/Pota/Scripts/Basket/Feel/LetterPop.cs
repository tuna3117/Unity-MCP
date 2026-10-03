using Project.ColorHunt;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Basket
{
    /// <summary>
    /// A Blender lettering still (2:1 sheet, e.g. SWISH! / KAÇTI!) on a camera-facing quad: pops 0 → 1.2 → 1 in 0.12 s, holds, fades
    /// over the last third of its life. Real time. Shared by MissFeel and SwishFeel.
    /// </summary>
    public class LetterPop
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly MeshRenderer _r;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private Camera _cam;
        private float _t = -1f, _life, _width, _wobble;
        private Vector3 _at;

        public bool Shown => _t >= 0f;

        public LetterPop(Transform parent, string name, Material template, Texture sheet, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _r = go.AddComponent<MeshRenderer>();
            var m = RuntimeMaterial.From(template, "Project/PotaLabel", name);
            if (sheet != null) m.SetTexture("_BaseMap", sheet);
            _r.sharedMaterial = m;
            _r.shadowCastingMode = ShadowCastingMode.Off;
            _r.receiveShadows = false;
            _r.sortingOrder = order;
            _r.enabled = false;
        }

        /// <summary>Shows the word centred on <paramref name="world"/>, <paramref name="width"/> metres wide, for <paramref name="life"/> seconds.</summary>
        public void Show(Vector3 world, float width, float life, Camera cam, float wobbleDeg = 0f)
        {
            _at = world;
            _width = width;
            _life = Mathf.Max(0.2f, life);
            _cam = cam;
            _wobble = wobbleDeg;
            _t = 0f;
            _r.enabled = true;
            Place();
        }

        public void Hide()
        {
            _t = -1f;
            _r.enabled = false;
        }

        public void Tick(float dt)
        {
            if (_t < 0f) return;
            _t += dt;
            if (_t >= _life) { Hide(); return; }
            Place();
        }

        private void Place()
        {
            var t = _r.transform;
            t.position = _at;
            if (_cam != null) t.rotation = Quaternion.LookRotation(t.position - _cam.transform.position, _cam.transform.up);
            float s = FeelCurves.RibbonScale(_t / 0.12f) * (_t < 0.12f ? 1.0435f : 1f);     // 0 → 1.2 → 1 over 0.12 s (RibbonScale peaks at 1.15)
            float wob = _wobble > 0f ? Mathf.Sin(_t * 26f) * _wobble * Mathf.Max(0f, 1f - _t / 0.5f) : 0f;
            t.rotation *= Quaternion.Euler(0f, 0f, wob);
            t.localScale = new Vector3(_width * s, _width * 0.5f * s, 1f);
            float fadeFrom = _life * 0.66f;
            float a = _t < fadeFrom ? 1f : 1f - (_t - fadeFrom) / (_life - fadeFrom);
            _r.GetPropertyBlock(_mpb);
            _mpb.SetVector(ColorId, new Vector4(1f, 1f, 1f, a));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
