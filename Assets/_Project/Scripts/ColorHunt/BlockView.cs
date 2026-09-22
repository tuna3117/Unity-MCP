using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block's visual: rounded box tinted per instance (MaterialPropertyBlock), the basket item sprite of its
    /// colour on the upper face, a bold hp label in front of and below the item (Pota 2026-09-23 layout), and the
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
        private MeshRenderer _item;
        private MaterialPropertyBlock _itemMpb;
        private ColorHuntSettings _s;
        private Color _base;
        private float _labelRed;
        private bool _hasItem;
        private float _labelBaseScale;

        public void Init(ColorHuntSettings s, Mesh mesh, Material material, Font font, Material textMat, Material shadowMat, Material itemMat)
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
            _itemMpb = new MaterialPropertyBlock();

            float face = -s.BlockDepth * 0.5f;
            var itemGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            itemGo.name = "Item";
            Destroy(itemGo.GetComponent<Collider>());
            itemGo.transform.SetParent(transform, false);
            itemGo.transform.localPosition = new Vector3(0f, 0f, face - 0.01f);
            // Unity's Quad primitive already faces −z (the camera); no rotation needed.
            _item = itemGo.GetComponent<MeshRenderer>();
            _item.sharedMaterial = itemMat;
            _item.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _item.receiveShadows = false;

            _shadow = MakeLabel("Shadow", font, shadowMat, new Vector3(0.03f, -0.035f, face - 0.022f), new Color(0.08f, 0.1f, 0.2f, 0.85f));
            _label = MakeLabel("Label", font, textMat, new Vector3(0f, 0f, face - 0.03f), Color.white);
            _labelBaseScale = _label.characterSize;
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

            var icon = _s.ItemIcon(b.Color);
            _hasItem = icon != null;
            _item.enabled = _hasItem;
            if (_hasItem)
            {
                _itemMpb.SetTexture("_BaseMap", icon);
                _item.SetPropertyBlock(_itemMpb);
                float aspect = icon.width / (float)icon.height;
                float size = _s.BlockSize * _s.ItemScale;
                _item.transform.localScale = new Vector3(size * Mathf.Min(1f, aspect), size * Mathf.Min(1f, 1f / aspect), 1f);
                _item.transform.localPosition = new Vector3(0f, _s.ItemOffsetY, _item.transform.localPosition.z);
            }
            float numberScale = _hasItem ? _s.NumberScale : 1f;
            float ny = _hasItem ? _s.NumberOffsetY : 0f;
            _label.characterSize = _labelBaseScale * numberScale;
            _shadow.characterSize = _labelBaseScale * numberScale;
            _label.transform.localPosition = new Vector3(0f, ny, _label.transform.localPosition.z);
            _shadow.transform.localPosition = new Vector3(0.03f * numberScale, ny - 0.035f * numberScale, _shadow.transform.localPosition.z);
            Apply();
        }

        public Texture2D ItemIcon => _s.ItemIcon(Block.Color);

        public void FlashRedLabel() => _labelRed = 1f;

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
            // Labels and the item stay upright while the board rotates (rotate about the block centre).
            var q = Quaternion.Euler(0f, 0f, rotationCompensation);
            RotateAbout(_label.transform, q, _label.transform.localPosition.z);
            RotateAbout(_shadow.transform, q, _shadow.transform.localPosition.z);
            if (_hasItem)
            {
                _item.transform.localRotation = q;
                var p = q * new Vector3(0f, _s.ItemOffsetY, 0f);
                _item.transform.localPosition = new Vector3(p.x, p.y, _item.transform.localPosition.z);
            }
            Apply();
        }

        private void RotateAbout(Transform t, Quaternion q, float z)
        {
            t.localRotation = q;
            float ny = _hasItem ? _s.NumberOffsetY : 0f;
            float ox = t == _shadow.transform ? 0.03f * (_hasItem ? _s.NumberScale : 1f) : 0f;
            float oy = t == _shadow.transform ? -0.035f * (_hasItem ? _s.NumberScale : 1f) : 0f;
            var p = q * new Vector3(ox, ny + oy, 0f);
            t.localPosition = new Vector3(p.x, p.y, z);
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
            if (_hasItem)
            {
                var ic = Dim ? new Color(0.48f, 0.52f, 0.58f, 1f) : Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.6f, 1f), Flash);
                _itemMpb.SetColor(BaseColorId, ic);
                _item.SetPropertyBlock(_itemMpb);
            }
        }
    }
}
