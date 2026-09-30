using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.ColorHunt
{
    /// <summary>
    /// B "Gerçek kırılma" (spec §2 B): the block breaks into the Blender-fractured pillow's pieces, lit like the blocks (tone, room
    /// reflection) with the sticker on their front faces; they fly out and toward the camera, spin, bounce off the frame's inner edges and
    /// the glass, and shrink away in 0.8–1.2 s. Lit smoke (flipbook) and streaking sparks go with it and Pota's point light flashes
    /// stronger. Chains of 4+ get a 0.3 s ×0.4 slow motion and a short camera push toward the step. Pieces are pooled GameObjects in the
    /// game root (the hoop's space), so a board rotation does not carry them.
    /// </summary>
    public class ShatterBurst : MonoBehaviour, IBurstStyle
    {
        public const int PieceMax = 160, SmokeMax = 24, SparkMax = 96, ShockMax = 16;

        /// <summary>BlockView's label placement in the pieces' face UV (0..1 across the 0.9 m face): centre xy, size zw.</summary>
        public static readonly Vector4 ItemPlace = new Vector4(0.5f, 0.5f + PotaLook.ItemOffsetY / PotaLook.Block,
            PotaLook.LabelQuad * PotaLook.ItemScale / PotaLook.Block, PotaLook.LabelQuad * PotaLook.ItemScale / PotaLook.Block);
        public static readonly Vector4 NumberPlace = new Vector4(0.5f + PotaLook.NumberOffset.x / PotaLook.Block,
            0.5f + PotaLook.NumberOffset.y / PotaLook.Block,
            PotaLook.LabelQuad * PotaLook.NumberScale / PotaLook.Block, PotaLook.LabelQuad * PotaLook.NumberScale / PotaLook.Block);

        /// <summary>The fractured model imports with the workshop's front (−Y) at Unity +z; the board's blocks face the camera at −z.
        /// A half turn about y shows the sticker side to the camera and keeps its u running left to right.</summary>
        public static readonly Quaternion Mount = Quaternion.Euler(0f, 180f, 0f);

        private class Piece { public Transform T; public MeshFilter F; public MeshRenderer R; public Debris D; public float Radius; }
        private struct Part { public Mesh Mesh; public Vector3 Centre; public float Radius; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ItemRectId = Shader.PropertyToID("_ItemRect");
        private static readonly int NumRectId = Shader.PropertyToID("_NumRect");

        private ColorHuntGame _game;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Piece> _live = new List<Piece>();
        private readonly Stack<Piece> _pool = new Stack<Piece>();
        private Material _pieceMat;
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _smoke, _sparks, _shock;
        private readonly System.Random _rng = new System.Random(17);

        public bool Ready { get; private set; }
        public bool ShowsChainNumber => true;
        public int ActivePieces => _live.Count;
        public int ShockLive => _shock != null ? _shock.particleCount : 0;
        public IEnumerable<Vector3> PiecePositions { get { foreach (var p in _live) yield return p.D.P; } }
        public int LiveCount => _live.Count + StudioParticles.Count(_smoke, _sparks, _shock)
                                + (_game != null && _game.Fx != null ? _game.Fx.LiveCount : 0);

        /// <summary>A usable fractured block: 4+ child meshes, each carrying the front-flag UV set (UV1).</summary>
        public static bool PiecesReady(GameObject model)
        {
            if (model == null) return false;
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length < 4) return false;
            foreach (var f in filters)
            {
                var m = f.sharedMesh;
                if (m == null || m.uv2 == null || m.uv2.Length != m.vertexCount) return false;
            }
            return true;
        }

        public void Build(ColorHuntGame game)
        {
            _game = game;
            var s = game.S;
            Ready = s != null && s.ShatterSmoke != null && s.LabelAtlas != null && s.SoftDisc != null && PiecesReady(s.ShatterPieces);
            if (!Ready) return;
            var root = s.ShatterPieces.transform;
            foreach (var f in s.ShatterPieces.GetComponentsInChildren<MeshFilter>(true))
                _parts.Add(new Part { Mesh = f.sharedMesh, Centre = Mount * root.InverseTransformPoint(f.transform.position), Radius = f.sharedMesh.bounds.extents.magnitude });
            _pieceMat = RuntimeMaterial.From(s.PieceTemplate, "Project/PotaPiece", "PotaPiece");
            _pieceMat.SetTexture("_LabelAtlas", s.LabelAtlas);
            _pieceMat.SetVector("_ItemPlace", ItemPlace);
            _pieceMat.SetVector("_NumPlace", NumberPlace);
            _mpb = new MaterialPropertyBlock();
            _smoke = StudioParticles.System(transform, "ShatterSmoke", StudioParticles.Flipbook(s, s.ShatterSmoke), ParticleSystemRenderMode.Billboard, SmokeMax, 6);
            StudioParticles.Sheet(_smoke, 4, 4);
            _sparks = StudioParticles.System(transform, "ShatterSparks", StudioParticles.Additive(s, s.SoftDisc, 2f), ParticleSystemRenderMode.Stretch, SparkMax, 9);
            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.06f;
            sr.lengthScale = 1.2f;
            StudioParticles.SizeOverLife(_sparks, u => 1f - u);
            if (s.ShockRing != null)
            {
                var shock = RuntimeMaterial.From(s.ShockTemplate, "Project/PotaShockRing", "PotaShockRing");
                shock.SetTexture("_BaseMap", s.ShockRing);
                _shock = StudioParticles.System(transform, "ShatterShock", shock, ParticleSystemRenderMode.Billboard, ShockMax, 10);
                StudioParticles.Sheet(_shock, 4, 4);
                StudioParticles.AlphaOverLife(_shock, u => 1f - u * u);
            }
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on);
            var data = _game != null && _game.Cam != null ? _game.Cam.GetUniversalAdditionalCameraData() : null;
            if (data != null) data.requiresColorOption = on && _shock != null ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
        }

        public void TapBurst(BlockView v, Color tone) => Shatter(v, tone, 0);
        public void StepBurst(BlockView v, Color tone, int depth) => Shatter(v, tone, depth);

        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (!Ready) return;
            float bs = _game.BoardRoot.lossyScale.x;
            if (_shock != null)
                StudioParticles.Emit(_shock, _game.BoardRoot.TransformPoint(new Vector3(at.x, at.y, -(PotaLook.FrontZ + 0.2f))), (3.2f + 0.4f * depth) * bs, 0.45f, Color.white);
            if (depth < 4) return;
            _game.Juice?.TimeCtl?.SlowMo(0.4f, 0.3f);
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.Push(_game.BoardRoot.TransformPoint(new Vector3(at.x, at.y, -PotaLook.FrontZ)), 0.18f, 0.6f);
        }

        public void Clear()
        {
            foreach (var p in _live) Recycle(p);
            _live.Clear();
            if (_smoke != null) _smoke.Clear();
            if (_sparks != null) _sparks.Clear();
            if (_shock != null) _shock.Clear();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);          // game time: dips and slow motion hold the pieces too
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                ShatterDebris.Step(ref p.D, dt, p.Radius);
                if (p.D.Age >= p.D.Life) { Recycle(p); _live.RemoveAt(i); continue; }
                Place(p);
            }
        }

        private void Shatter(BlockView v, Color tone, int depth)
        {
            if (!Ready) return;
            var board = _game.BoardRoot;
            float bs = board.lossyScale.x;
            float s = bs * Mathf.Max(0.5f, v.transform.localScale.x);                 // the swollen block breaks at its size
            Vector3 centre = transform.InverseTransformPoint(board.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, 0f)));
            Vector4 item = LabelAtlas.UvRect(LabelAtlas.ItemCell(v.Block.Color));
            Vector4 number = LabelAtlas.UvRect(LabelAtlas.NumberCell(v.Block.Hp));
            Color lin = PotaLook.BlockColor(tone, 0f, false);
            foreach (var part in _parts)
            {
                if (_live.Count >= PieceMax) break;
                var p = _pool.Count > 0 ? _pool.Pop() : NewPiece();
                p.F.sharedMesh = part.Mesh;
                p.Radius = part.Radius * s;
                p.D = ShatterDebris.Launch(centre, part.Centre, s, _rng);
                ShatterDebris.Step(ref p.D, 0f, p.Radius);                       // an edge block's piece starts inside the frame
                _mpb.SetVector(BaseColorId, lin);
                _mpb.SetVector(ItemRectId, item);
                _mpb.SetVector(NumRectId, number);
                p.R.SetPropertyBlock(_mpb);
                Place(p);
                p.T.gameObject.SetActive(true);
                _live.Add(p);
            }
            Vector3 at = board.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -(PotaLook.FrontZ + 0.2f)));
            var smokeTint = Color.Lerp(Color.white, tone, 0.35f);
            for (int k = 0; k < 2; k++)
            {
                var jitter = new Vector3((R() - 0.5f) * 0.3f, (R() - 0.5f) * 0.3f, 0f) * bs;
                StudioParticles.Emit(_smoke, at + jitter, (2.2f + 0.3f * k + 0.1f * depth) * bs, 0.9f, smokeTint, new Vector3(0f, 0.5f * bs, 0f), R() * 360f);
            }
            if (_shock != null && depth == 0) StudioParticles.Emit(_shock, at, 3.2f * bs, 0.4f, Color.white);
            StudioParticles.Gravity(_sparks, 8f * bs);
            int sparks = Mathf.Min(14, 8 + depth);
            for (int k = 0; k < sparks; k++)
            {
                float a = R() * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 0.3f, -(0.2f + 0.6f * R()));
                StudioParticles.Emit(_sparks, at, 0.12f * bs, 0.35f + 0.2f * R(), new Color(1f, 0.85f, 0.55f, 1f), board.TransformVector(dir) * (6f + 4f * R()));
            }
            _game.Fx?.Glow(v.Pos, tone.linear, _game.S.FlashSize * 0.6f, 0f, _game.S.LightFlashIntensity * 1.8f, _game.S.LightFlashTime * 1.5f);
        }

        private void Place(Piece p)
        {
            p.T.localPosition = p.D.P;
            p.T.localRotation = p.D.R * Mount;
            p.T.localScale = Vector3.one * (p.D.Size * ShatterDebris.Shrink(p.D.Age, p.D.Life));
        }

        private void Recycle(Piece p)
        {
            p.T.gameObject.SetActive(false);
            _pool.Push(p);
        }

        private Piece NewPiece()
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(transform, false);
            var p = new Piece { T = go.transform, F = go.AddComponent<MeshFilter>(), R = go.AddComponent<MeshRenderer>() };
            p.R.sharedMaterial = _pieceMat;
            p.R.shadowCastingMode = ShadowCastingMode.On;
            p.R.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return p;
        }

        private float R() => (float)_rng.NextDouble();
    }
}
