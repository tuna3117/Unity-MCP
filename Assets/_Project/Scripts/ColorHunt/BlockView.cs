using Project.Basket.Visual;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block's visual: rounded box tinted per instance (MaterialPropertyBlock), a bold hp label, and the
    /// small animation state the game drives every frame (squash, flash, shiver, dimming).
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public Block Block { get; private set; }
        public int Cell;
        /// <summary>Home position (board-local) and the animated one.</summary>
        public Vector2 Home;
        public Vector2 Pos;
        public float Scale = 1f;
        public float Flash;
        public float Squash;
        public float Shiver;
        public bool Dim;
        public bool Exploding;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private TextMesh _label;
        private TextMesh _shadow;
        private ColorHuntSettings _s;
        private Color _base;
        private float _labelRed;

        public void Init(ColorHuntSettings s, Mesh mesh, Material material, Font font, Material textMat, Material shadowMat)
        {
            _s = s;
            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _renderer.receiveShadows = true;
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(s.BlockSize, s.BlockSize, s.BlockDepth);
            _mpb = new MaterialPropertyBlock();

            _shadow = MakeLabel("Shadow", font, shadowMat, new Vector3(0.03f, -0.035f, -s.BlockDepth * 0.5f - 0.012f), new Color(0.08f, 0.1f, 0.2f, 0.85f));
            _label = MakeLabel("Label", font, textMat, new Vector3(0f, 0f, -s.BlockDepth * 0.5f - 0.02f), Color.white);
        }

        private TextMesh MakeLabel(string name, Font font, Material mat, Vector3 localPos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 96;
            tm.characterSize = 0.062f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return tm;
        }

        public void SetBlock(Block b, int cell)
        {
            Block = b;
            Cell = cell;
            _base = _s.PaletteColor(b.Color);
            _label.font.RequestCharactersInTexture(b.Hp.ToString(), _label.fontSize, _label.fontStyle);
            _label.text = b.Hp.ToString();
            _shadow.text = _label.text;
            Apply();
        }

        private void OnEnable() { Font.textureRebuilt += OnFontRebuilt; }
        private void OnDisable() { Font.textureRebuilt -= OnFontRebuilt; }

        /// <summary>The dynamic font atlas was rebuilt: TextMesh keeps stale glyph UVs until its text is reassigned.</summary>
        private void OnFontRebuilt(Font f)
        {
            if (_label == null || f != _label.font) return;
            string t = _label.text;
            _label.text = string.Empty; _label.text = t;
            _shadow.text = string.Empty; _shadow.text = t;
        }

        public void FlashRedLabel() => _labelRed = 1f;

        /// <summary>Advance the transient state and write the transform/material.</summary>
        public void Tick(float dt, float rotationCompensation)
        {
            Flash = Mathf.Max(0f, Flash - dt * 4.5f);
            Squash = Mathf.Max(0f, Squash - dt * 6f);
            Shiver = Mathf.Max(0f, Shiver - dt);
            _labelRed = Mathf.Max(0f, _labelRed - dt * 3f);
            float shiverX = Shiver > 0f ? Mathf.Sin((_s.InsufficientShake - Shiver) * 68f) * 0.075f * (Shiver / _s.InsufficientShake) : 0f;
            transform.localPosition = new Vector3(Pos.x + shiverX, Pos.y, 0f);
            float sq = 1f - 0.12f * Squash;
            transform.localScale = new Vector3(Scale * (2f - sq) * 0.5f + Scale * 0.5f, Scale * sq, Scale);
            // Labels stay upright while the board rotates.
            var q = Quaternion.Euler(0f, 0f, rotationCompensation);
            _label.transform.localRotation = q;
            _shadow.transform.localRotation = q;
            Apply();
        }

        private void Apply()
        {
            var c = Color.Lerp(_base, Color.white, Mathf.Min(1f, Flash));
            if (Dim) c = Color.Lerp(c, new Color(0.55f, 0.55f, 0.58f), _s.DimAmount) * 0.72f;
            c.a = 1f;
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(EmissionId, Color.white * (Flash * 1.6f));
            _renderer.SetPropertyBlock(_mpb);
            var lc = Color.Lerp(Color.white, _s.RedColor, _labelRed);
            if (Dim) lc = Color.Lerp(lc, new Color(0.8f, 0.8f, 0.85f), 0.5f);
            _label.color = lc;
        }
    }
}
