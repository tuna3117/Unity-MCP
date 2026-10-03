using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block in Pota's "balon" look: glossy pillow in its candy tone (linear colour per instance), the item sticker (72 %, centre,
    /// +0.06) and the hp number (44 %, bottom-right) from the label atlas, and the state the game drives every frame: flash, uniform
    /// squash, shiver, swell, the labels' counter-angle during rotation, dimming when the ammo does not cover the block. A special ball
    /// (yapbozSahne.ts etiketleriYaz, `h.blok.ozel`) wears OZEL_BLOK_TONU and its sticker (atlas 105–107) centred at 82 %, the
    /// horizontal line's turned 90°; it has no number and is never dimmed.
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Mesh _quad;

        public Block Block { get; private set; }
        public int Cell;
        /// <summary>Home position (board-local cells) and the animated one.</summary>
        public Vector2 Home;
        public Vector2 Pos;
        /// <summary>Entrance multiplier (PotaFlow's reveal); 1 in play.</summary>
        public float Scale = 1f;
        public float Flash;
        public float Squash;
        public float Shiver;
        public bool Dim;
        public bool Exploding;
        /// <summary>Swell-then-burst scale while it runs (replaces the squash); −1 otherwise.</summary>
        public float Swell = -1f;
        /// <summary>Labels' angle relative to the board in degrees (Pota `donme`: rides the board, then stands up).</summary>
        public float LabelAngle;
        /// <summary>Flash floor applied after the decay this frame: the game's golden pulse (renkAviFaz.ts altinNabiz); 0 = none.</summary>
        public float Pulse;
        /// <summary>Style-driven idle offset added to the y position (board cells); 0 under the Pota look.</summary>
        public float Bob;
        /// <summary>Style-driven idle turn: degrees added to the sticker's angle; 0 under the Pota look.</summary>
        public float StickerSpin;

        /// <summary>Studio press: the finger is on this block (the game sets it only for looks other than Pota).</summary>
        public bool Pressed;
        /// <summary>Studio idle hint: z tilt in degrees.</summary>
        public float Wiggle;
        /// <summary>F6 landing bounce clock (seconds since landing); −1 = none. Scales x wide / y flat, one overshoot, then rests.</summary>
        public float Bounce = -1f;
        /// <summary>F9 lose dim: 1 = lit as usual; lower fades the body and labels toward grey at that brightness.</summary>
        public float Fade = 1f;
        /// <summary>The press factor applied this frame (1 = none).</summary>
        public float PressScale { get; private set; } = 1f;
        private float _press, _release = -1f, _releaseFrom = 1f;

        public void ResetStudio()
        {
            Pressed = false;
            Bounce = -1f;
            Fade = 1f;
            Wiggle = 0f;
            Bob = 0f;
            StickerSpin = 0f;
            _press = 0f;
            _release = -1f;
            PressScale = 1f;
        }

        private MeshRenderer _body, _item, _number;
        private MaterialPropertyBlock _bodyMpb, _itemMpb, _numberMpb;
        private ColorHuntSettings _s;

        private static Mesh Quad => _quad != null ? _quad : (_quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx"));

        /// <summary>yapbozSahne.ts renkliMat: white MeshStandard, roughness 0.16, metalness 0.02; the tone comes per block.</summary>
        public static Material BodyMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PotaBlock" };
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 1f - 0.16f);
            m.SetFloat("_Metallic", 0.02f);
            return m;
        }

        public static Material LabelMaterial(Material template, Texture atlas)
        {
            var m = RuntimeMaterial.From(template, "Project/PotaLabel", "PotaLabel");
            m.enableInstancing = true;
            if (atlas != null) m.SetTexture("_BaseMap", atlas);
            return m;
        }

        /// <summary>yapbozSahne.ts secimHalka: white, opacity 0.9, unlit.</summary>
        public static Material RingMaterial(Material template)
        {
            var m = RuntimeMaterial.From(template, "Universal Render Pipeline/Unlit", "PotaSelection");
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            return m;
        }

        public void Init(ColorHuntSettings s, Mesh body, Material bodyMat, Material labelMat)
        {
            _s = s;
            gameObject.AddComponent<MeshFilter>().sharedMesh = body;
            _body = gameObject.AddComponent<MeshRenderer>();
            _body.sharedMaterial = bodyMat;
            _body.shadowCastingMode = ShadowCastingMode.On;
            _body.receiveShadows = true;
            gameObject.AddComponent<BoxCollider>().size = Vector3.one * PotaLook.Block;
            _bodyMpb = new MaterialPropertyBlock();
            _itemMpb = new MaterialPropertyBlock();
            _numberMpb = new MaterialPropertyBlock();
            _item = Label("Item", labelMat, 2);
            _number = Label("Number", labelMat, 3);
        }

        private MeshRenderer Label(string name, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;       // Unity's quad faces −z, the camera
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingOrder = order;                                // number after item (same block overlaps)
            return r;
        }

        public void SetBlock(Block b, int cell)
        {
            Block = b;
            Cell = cell;
            int sticker = b.IsSpecial ? LabelAtlas.SpecialCell(b.Special) : LabelAtlas.ItemCell(b.Color);
            _itemMpb.SetVector(UvRectId, LabelAtlas.UvRect(sticker));
            _numberMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.NumberCell(b.Hp)));
            _number.enabled = !b.IsSpecial;                        // etiketleriYaz: özel top → rakam yok
            Apply();
        }

        public Texture2D ItemIcon => _s.ItemIcon(Block.Color);

        /// <summary>The body tone (sRGB): OZEL_BLOK_TONU for a special, the palette colour otherwise (Pota blokRengi; bursts use it too).</summary>
        public Color Tone => Block.IsSpecial ? PotaLook.SpecialTone(Block.Special) : _s.PaletteColor(Block.Color);

        /// <summary>Advance the transient state and write transform and colours (renkAviFaz.ts gorselleriIlerlet).</summary>
        public void Tick(float dt)
        {
            Flash = Mathf.Max(0f, Flash - dt * PotaLook.FlashDecay);
            if (Pulse > 0f) Flash = Mathf.Max(Flash, Pulse);      // altinNabiz: after the decay, before drawing
            Squash = Mathf.Max(0f, Squash - dt * PotaLook.SquashDecay);
            Shiver = Mathf.Max(0f, Shiver - dt);
            float t = _s.InsufficientShake;
            float shiverX = Shiver > 0f ? Mathf.Sin((t - Shiver) * 68f) * PotaLook.ShiverAmount * (Shiver / t) : 0f;
            transform.localPosition = new Vector3(Pos.x + shiverX, Pos.y + Bob, 0f);
            float own = Mathf.Max(0.001f, Swell >= 0f ? Swell : 1f - PotaLook.LandSquash * Squash);
            float press = 1f;
            if (Pressed)
            {
                _release = -1f;
                _press = Mathf.Min(1f, _press + dt / StudioCurves.PressTime);
                press = StudioCurves.PressScale(_press);
                Flash = Mathf.Max(Flash, 0.18f);                 // a soft glow under the finger
            }
            else if (_press > 0f)
            {
                if (_release < 0f) { _release = 0f; _releaseFrom = StudioCurves.PressScale(_press); }
                _release += dt / StudioCurves.ReleaseTime;
                press = StudioCurves.ReleaseScale(_release, _releaseFrom);
                if (_release >= 1f) { _press = 0f; _release = -1f; press = 1f; }
            }
            PressScale = press;
            float s = Mathf.Max(0.001f, own * Scale * press);
            var bounce = Vector2.one;
            if (Bounce >= 0f)
            {
                float time = _s != null && _s.Feel != null ? _s.Feel.LandBounceTime : 0.18f;
                bounce = FeelCurves.LandBounce(Bounce / Mathf.Max(0.01f, time));
                Bounce += dt;
                if (Bounce >= time) { Bounce = -1f; bounce = Vector2.one; }
            }
            transform.localScale = new Vector3(s * bounce.x, s * bounce.y, s * bounce.x);
            transform.localRotation = Quaternion.Euler(0f, 0f, Wiggle);
            PlaceLabels(own);
            Apply();
        }

        /// <summary>
        /// yapbozSahne.ts etiketleriYaz: the plane sits at (Z_ON + 0.03 + bulge)·max(1, scale), the quads grow with the block, the offsets
        /// do not (they are divided by the block's own scale here because the labels are its children). A special's sprite sits in the
        /// middle at ozelOlcek, cizgiYatay's turned +π/2; <see cref="StickerSpin"/> turns the sticker further.
        /// </summary>
        private void PlaceLabels(float own)
        {
            float a = LabelAngle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float z = -PotaLook.LabelZ * Mathf.Max(1f, own) / own;
            var rot = Quaternion.Euler(0f, 0f, LabelAngle);
            bool special = Block.IsSpecial;
            float ey = special ? 0f : PotaLook.ItemOffsetY;
            float turn = (Block.Special == SpecialKind.LineH ? 90f : 0f) + StickerSpin;
            var it = _item.transform;
            it.localPosition = new Vector3(-ey * sn / own, ey * cs / own, z);
            it.localRotation = Quaternion.Euler(0f, 0f, LabelAngle + turn);
            it.localScale = Vector3.one * (PotaLook.LabelQuad * (special ? PotaLook.SpecialScale : PotaLook.ItemScale));
            var o = PotaLook.NumberOffset;
            var nt = _number.transform;
            nt.localPosition = new Vector3((o.x * cs - o.y * sn) / own, (o.x * sn + o.y * cs) / own, z - 0.01f / own);
            nt.localRotation = rot;
            nt.localScale = Vector3.one * (PotaLook.LabelQuad * PotaLook.NumberScale);
        }

        /// <summary>Fade &lt; 1: toward the colour's grey, at Fade brightness (F9).</summary>
        private Color Faded(Color c)
        {
            if (Fade >= 1f) return c;
            float f = Mathf.Clamp01(Fade);
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            var grey = new Color(lum, lum, lum, c.a);
            var r = Color.Lerp(c, grey, 1f - f) * f;
            r.a = c.a;
            return r;
        }

        private void Apply()
        {
            bool dim = Dim && !Block.IsSpecial;                   // renkAviFaz.ts soluk: … && !g.blok.ozel
            _bodyMpb.SetVector(BaseColorId, Faded(PotaLook.BlockColor(Tone, Flash, dim)));
            _body.SetPropertyBlock(_bodyMpb);
            _itemMpb.SetVector(ColorId, Faded(dim ? PotaLook.ItemShort.linear : Color.white));
            _item.SetPropertyBlock(_itemMpb);
            _numberMpb.SetVector(ColorId, Faded(dim ? PotaLook.NumberShort.linear : Color.white));
            _number.SetPropertyBlock(_numberMpb);
        }
    }
}
