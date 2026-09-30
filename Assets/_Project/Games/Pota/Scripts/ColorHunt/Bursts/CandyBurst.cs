using Project.Rendering;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// A "Şeker patlaması" (spec §2 A): the block turns into an inked cartoon puff (Blender flipbook), an eight-ray white flash,
    /// 6–10 fat outlined candy chunks thrown in arcs, twinkling stars. Each chain step adds an inked ring wave, a word
    /// (İyi!, Süper!, Harika!, Muhteşem!), a screen-edge glow in the chain's colour and a stronger camera kick; 5+ adds confetti and a
    /// 0.25 s ×0.5 slow motion. World-space particles on game time; the words and the glow are on the HUD (CandyHud).
    /// </summary>
    public class CandyBurst : MonoBehaviour, IBurstStyle
    {
        public const int PuffMax = 24, FlashMax = 16, ChunkMax = 120, TwinkleMax = 64, RingMax = 16;

        private ColorHuntGame _game;
        private ParticleSystem _puff, _flash, _chunks, _twinkles, _rings;
        private CandyHud _hud;
        private readonly System.Random _rng = new System.Random(11);

        public bool Ready { get; private set; }
        public bool ShowsChainNumber => false;
        public CandyHud Hud => _hud;
        public int LiveCount => StudioParticles.Count(_puff, _flash, _chunks, _twinkles, _rings)
                                + (_game != null && _game.Fx != null ? _game.Fx.LiveCount : 0);

        public static bool AssetsReady(ColorHuntSettings s) =>
            s != null && s.CandyPuff != null && s.CandyRing != null && s.StarSprite != null;

        public void Build(ColorHuntGame game)
        {
            _game = game;
            Ready = AssetsReady(game.S);
            if (!Ready) return;
            var s = game.S;
            _puff = StudioParticles.System(transform, "CandyPuff", StudioParticles.Flipbook(s, s.CandyPuff), ParticleSystemRenderMode.Billboard, PuffMax, 6);
            StudioParticles.Sheet(_puff, 4, 4);
            _flash = StudioParticles.System(transform, "CandyFlash", StudioParticles.Additive(s, StudioTextures.RayStar(), 1.6f), ParticleSystemRenderMode.Billboard, FlashMax, 8);
            StudioParticles.SizeOverLife(_flash, u => Mathf.Lerp(0.4f, 1.15f, StudioCurves.EaseOutCubic(Mathf.Min(1f, u * 1.6f))));
            StudioParticles.AlphaOverLife(_flash, u => 1f - u);
            _chunks = StudioParticles.System(transform, "CandyChunks", RuntimeMaterial.From(s.CandyShardTemplate, "Project/CandyShard", "CandyShard"),
                ParticleSystemRenderMode.Mesh, ChunkMax, 7, true);
            _chunks.GetComponent<ParticleSystemRenderer>().mesh = StudioTextures.CandyChunk();
            StudioParticles.SizeOverLife(_chunks, u => u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f);
            _twinkles = StudioParticles.System(transform, "CandyTwinkles", StudioParticles.Additive(s, s.StarSprite, 2f), ParticleSystemRenderMode.Billboard, TwinkleMax, 9);
            StudioParticles.SizeOverLife(_twinkles, u => Mathf.Sin(u * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(u * 25f)));
            _rings = StudioParticles.System(transform, "CandyRings", StudioParticles.Flipbook(s, s.CandyRing), ParticleSystemRenderMode.Billboard, RingMax, 5);
            StudioParticles.Sheet(_rings, 4, 4);
            _hud = CandyHud.Attach(game);
            Activate(false);
        }

        public void Activate(bool on)
        {
            gameObject.SetActive(on);
            if (_hud != null) _hud.gameObject.SetActive(on);
        }

        public void TapBurst(BlockView v, Color tone) => Pop(v.Pos, tone, 0);
        public void StepBurst(BlockView v, Color tone, int depth) => Pop(v.Pos, tone, depth);

        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (!Ready) return;
            float s = Scale;
            StudioParticles.Emit(_rings, World(at, PotaLook.FrontZ + 0.2f), (2.4f + 0.35f * depth) * s, 0.4f, Color.Lerp(Color.white, tone, 0.2f));
            _hud?.Word(depth, World(at + new Vector2(0f, 0.7f), PotaLook.LabelZ));
            _hud?.EdgeGlow(tone, depth);
            _game.Juice?.Shake?.Add(StudioCurves.CandyShake(depth));
            if (depth < 5) return;
            _game.Fx?.Confetti(_game.Board.Size, _game.S.ConfettiCount / 2);
            _game.Juice?.TimeCtl?.SlowMo(0.5f, 0.25f);
        }

        public void Clear()
        {
            foreach (var ps in new[] { _puff, _flash, _chunks, _twinkles, _rings }) if (ps != null) ps.Clear();
            _hud?.Clear();
        }

        private float Scale => _game.BoardRoot.lossyScale.x;
        private Vector3 World(Vector2 pos, float z) => _game.BoardRoot.TransformPoint(new Vector3(pos.x, pos.y, -z));
        private Vector3 Dir(Vector3 boardDir) => _game.BoardRoot.TransformVector(boardDir);
        private float R() => (float)_rng.NextDouble();

        private void Pop(Vector2 pos, Color tone, int depth)
        {
            if (!Ready) return;
            float s = Scale;
            Vector3 at = World(pos, PotaLook.FrontZ + 0.25f);
            StudioParticles.Emit(_puff, at, (2.2f + 0.1f * depth) * s, 0.35f, Color.Lerp(Color.white, tone, 0.25f), Vector3.zero, R() * 360f);
            // a random twist per flash: neighbours bursting together must not line their rays up into a lattice
            StudioParticles.Emit(_flash, World(pos, PotaLook.FrontZ + 0.35f), (2.1f + 0.15f * depth) * s, 0.12f, Color.white, Vector3.zero, R() * 45f);
            StudioParticles.Gravity(_chunks, 12f * s);
            var lin = tone.linear;
            int chunks = Mathf.Min(10, 6 + depth + (depth == 0 ? 2 : 0));
            for (int i = 0; i < chunks; i++)
            {
                float a = (i + R() * 0.6f) / chunks * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 0.35f, -(0.3f + 0.5f * R()));
                var size = new Vector3(0.26f + 0.14f * R(), 0.18f + 0.1f * R(), 0.22f + 0.1f * R()) * s;
                var spin = new Vector3((R() - 0.5f) * 720f, (R() - 0.5f) * 720f, (R() - 0.5f) * 720f);
                StudioParticles.Emit3D(_chunks, at, Dir(dir) * (3.2f + 2f * R()), size, new Vector3(R() * 360f, R() * 360f, R() * 360f),
                    spin, 0.6f + 0.3f * R(), lin);
            }
            int twinkles = Mathf.Min(8, 4 + depth + (depth == 0 ? 2 : 0));
            for (int i = 0; i < twinkles; i++)
            {
                var off = new Vector2((R() - 0.5f) * 1.4f, (R() - 0.5f) * 1.4f);
                StudioParticles.Emit(_twinkles, World(pos + off, PotaLook.FrontZ + 0.4f), (0.18f + 0.14f * R()) * s, 0.35f + 0.25f * R(),
                    new Color(1f, 0.95f, 0.75f, 1f), Dir(new Vector3(off.x, off.y + 0.5f, 0f)) * 0.8f, R() * 360f);
            }
        }
    }
}
