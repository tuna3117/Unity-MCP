using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>The Pota look: exactly what the game drew before the seam (ColorHuntFx puff, hot points and burst layers).</summary>
    public sealed class PotaBurstStyle : IBurstStyle
    {
        private ColorHuntGame _game;
        private CandyHud _hud;

        /// <summary>F4: the chain word under the Pota look — from Feel.WordFromDepth, only with the feel layer on; null otherwise.</summary>
        public static string Word(ColorHuntSettings s, int depth) =>
            s != null && s.Feel != null && s.Feel.Enabled && depth >= s.Feel.WordFromDepth ? CandyHud.WordFor(depth) : null;

        public CandyHud Hud => _hud;

        public bool Ready => true;
        public bool ShowsChainNumber => true;
        public int LiveCount => _game != null && _game.Fx != null ? _game.Fx.LiveCount : 0;

        public void Build(ColorHuntGame game) => _game = game;
        public void Activate(bool on) { }

        public void TapBurst(BlockView v, Color tone)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            fx.Puff(v.Pos, 1.15f);
            fx.Particles(v.Pos, tone, _game.S.PointsBase + 4, 2.6f, 0.12f);
            fx.Burst(v.Pos, tone, 0);
        }

        public void StepBurst(BlockView v, Color tone, int depth)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            fx.Puff(v.Pos, 1f + depth * 0.12f);
            fx.Particles(v.Pos, tone, PotaFx.PointCount(_game.S, depth), 2.2f + depth * 0.3f, 0.1f);
            fx.Burst(v.Pos, tone, depth);
        }

        /// <summary>F4 (feel layer): the chain word, screen-edge glow, and at 5+ the confetti and a 0.25 s ×0.5 slow motion (A's rule).</summary>
        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (_game == null || Word(_game.S, depth) == null) return;
            if (_hud == null) _hud = CandyHud.Attach(_game);
            if (_hud == null) return;
            _hud.Word(depth, _game.BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.7f, -PotaLook.LabelZ)));
            _hud.EdgeGlow(tone, depth);
            if (depth < 5) return;
            _game.Fx?.Confetti(_game.Board.Size, _game.S.ConfettiCount / 2);
            _game.Juice?.TimeCtl?.SlowMo(0.5f, 0.25f);
        }

        public void Clear() => _hud?.Clear();   // the game clears ColorHuntFx itself (Prepare)
    }

    /// <summary>A look whose style is not written yet (or a future enum value): never ready, so the game plays Pota's burst.</summary>
    public sealed class NoStyle : IBurstStyle
    {
        public bool Ready => false;
        public bool ShowsChainNumber => true;
        public int LiveCount => 0;
        public void Build(ColorHuntGame game) { }
        public void Activate(bool on) { }
        public void TapBurst(BlockView v, Color tone) { }
        public void StepBurst(BlockView v, Color tone, int depth) { }
        public void ChainPeak(int depth, Vector2 at, Color tone) { }
        public void Clear() { }
    }
}
