using System;
using Project.Basket;
using Project.ColorHunt;
using Project.Juice;
using Project.VFX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Flow
{
    /// <summary>
    /// PotaFlow's state machine: Basket → Transition → Puzzle → Result → (Return) → Basket. Owns the one camera during the
    /// transition and the return (RoundCamera / ColorHuntCamera drive it in their phases), gates both inputs per phase,
    /// hands the basket count to the board as ammo, and plays the transition from a pure <see cref="FlowTimeline"/>.
    /// Transition time is real time: a slow-motion left over from the basket round cannot slow it.
    /// </summary>
    public class PotaFlowDirector : MonoBehaviour
    {
        public enum Phase { Basket, Transition, Puzzle, Result, Return }

        public PotaFlowSettings S;
        [Header("Basket")]
        public BasketRound Round;
        public ShotInput Shot;
        public RoundCamera RoundCam;
        public BasketHud BasketHud;
        [Header("Board")]
        public ColorHuntGame Game;
        public ColorHuntInput BoardInput;
        public ColorHuntCamera BoardCam;
        public ColorHuntHud Hud;
        [Header("Shared")]
        public Camera Cam;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public SynthSfx Sfx;

        [Tooltip("≥ 0: use this basket count instead of the round's (recording, tests).")]
        public int ForcedCount = -1;
        [Tooltip("> 0: advance the clock by exactly this much per frame (frame-exact recording).")]
        public float FixedStep;

        /// <summary>Pointer state (tests replace these to simulate a new or held touch).</summary>
        public Func<bool> PointerPressedThisFrame = () => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        public Func<bool> PointerHeld = () => Pointer.current != null && Pointer.current.press.isPressed;

        /// <summary>Runtime override of the settings' candidate (record both without touching the asset).</summary>
        public FlowCandidate? CandidateOverride { get; set; }
        public FlowCandidate ActiveCandidate => CandidateOverride ?? S.Candidate;

        public Phase Current { get; private set; } = Phase.Basket;
        public float Clock { get; private set; }
        public int Count { get; private set; }
        public bool Skipping => _skipping;
        public FlowTimeline Timeline { get; private set; }
        public GiantHoop Hoop { get; private set; }
        public BallStream Stream { get; private set; }
        public FlowBadge Badge { get; private set; }
        public event Action<Phase> PhaseChanged;

        private CamPose _p0, _p1, _back;
        private Vector3 _source;
        private Vector2 _cageScreen;
        private float _fogStart0, _fogEnd0, _skipSpeed, _lastT;
        private bool _skipping, _heroShown, _badgeMerged, _revealSounded, _built;
        private Quaternion _heroRot0 = Quaternion.identity;
        private int _shownCount;
        private Canvas _basketCanvas, _boardCanvas;
        private Material _boardFrameMat;

        private void Start()
        {
            BoardCam.Anchor = Game.transform.position;
            _basketCanvas = BasketHud != null ? BasketHud.GetComponent<Canvas>() : null;
            _boardCanvas = Hud.GetComponent<Canvas>();
            if (BasketHud != null) BasketHud.ShowResultPanel = false;
            Hud.RetryOverride = () => ReturnToBasket();
            Hud.SetRetryLabel("Tekrar at");
            Hud.SetIntro(0f);

            CourtBuilder.Build(transform, S);
            var hoopGo = new GameObject("GiantHoop");
            hoopGo.transform.SetParent(Game.transform, false);
            Hoop = hoopGo.AddComponent<GiantHoop>();
            Hoop.BallRadius = S.BallRadius;
            Hoop.Build(Game.S.Size);

            var streamGo = new GameObject("BallStream");
            streamGo.transform.SetParent(transform, false);
            Stream = streamGo.AddComponent<BallStream>();
            Stream.BallMaterial = Game.S.BallMaterial;
            Stream.BallRadius = S.BallRadius;
            Stream.ApexAbove = S.ApexAbove;
            Stream.LaunchKick = S.LaunchKick;
            Stream.Prewarm(S.Timing.MaxVisible);
            Stream.Launched += OnLaunched;
            Stream.Landed += OnLanded;

            Badge = FlowBadge.Create(transform, Game.S.Font, Game.S.GoldColor, Game.S.NavyColor);

            Round.RoundEnded += OnRoundEnded;
            Game.RoundEnded += OnBoardEnded;
            _fogStart0 = RenderSettings.fogStartDistance;
            _fogEnd0 = RenderSettings.fogEndDistance;
            _built = true;
            EnterPhase(Phase.Basket);
        }

        private void OnDestroy()
        {
            if (Round != null) Round.RoundEnded -= OnRoundEnded;
            if (Game != null) Game.RoundEnded -= OnBoardEnded;
            if (Stream != null) { Stream.Launched -= OnLaunched; Stream.Landed -= OnLanded; }
        }

        private void OnRoundEnded(int count) => BeginTransition(ForcedCount >= 0 ? ForcedCount : count);

        private void OnBoardEnded(bool won, string reason)
        {
            if (Current == Phase.Puzzle) EnterPhase(Phase.Result);
        }

        /// <summary>Starts the basket → board transition with <paramref name="count"/> baskets. False if not in the Basket phase.</summary>
        public bool BeginTransition(int count)
        {
            if (!_built || Current != Phase.Basket || Round.Column == null) return false;
            if (Game.BoardRoot == null) { Debug.LogWarning("[PotaFlow] board not built yet"); return false; }
            Count = Mathf.Max(0, count);
            TimeCtl?.Clear();
            Timeline = new FlowTimeline(S.Timing, Count, ActiveCandidate);
            var look = RoundCam != null ? RoundCam.LookPoint : Cam.transform.position + Cam.transform.forward * 10f;
            _p0 = new CamPose(Cam.transform.position, look, Cam.fieldOfView);
            _p1 = new CamPose(S.MidPos, S.MidLook, S.MidFov);
            var cage = Round.Column.Cage;
            _source = new Vector3(0f, cage.TopY + 0.3f, cage.Depth * 0.5f + 0.6f);
            _cageScreen = Cam.WorldToScreenPoint(_source + Vector3.up * 1.2f);

            Game.Prepare(Count);
            Game.ShowAmmo(0);
            _shownCount = 0;
            BoardReveal.Apply(Game, Timeline, 0f);
            Hoop.ClearPile();
            Hoop.SetGlow(0f);
            if (Timeline.Visible > 0) Stream.Begin(Timeline, _source, Hoop);
            else Stream.Clear();
            Badge.Show(Count);
            Hud.HideResult();
            Hud.SetIntro(0f);

            var player = Round.Column.Player;
            _heroRot0 = player != null ? player.transform.rotation : Quaternion.identity;
            _heroShown = _skipping = _badgeMerged = _revealSounded = false;
            Clock = 0f;
            _lastT = 0f;
            EnterPhase(Phase.Transition);
            Sfx?.Play("swish");
            Shake?.Kick(0.18f);
            VfxService.Play("pickup", _source);
            ApplyTransition(0f);
            return true;
        }

        /// <summary>Plays the rest of the transition fast (≤ MaxSkipSeconds).</summary>
        public void Skip()
        {
            if (Current != Phase.Transition || _skipping) return;
            _skipping = true;
            _skipSpeed = Timeline.SkipSpeed(Clock);
        }

        /// <summary>"Tekrar at": fly back to the basket aim pose and restart the basket round. False unless on the board.</summary>
        public bool ReturnToBasket()
        {
            if (Current != Phase.Puzzle && Current != Phase.Result) return false;
            TimeCtl?.Clear();
            Hud.HideResult();
            _back = new CamPose(Cam.transform.position, Cam.transform.position + Cam.transform.forward * 20f, Cam.fieldOfView);
            Stream.Clear();
            Hoop.ClearPile();
            Badge.Hide();
            Clock = 0f;
            EnterPhase(Phase.Return);
            return true;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (Current == Phase.Transition && PointerPressedThisFrame()) Skip();
            Advance(FixedStep > 0f ? FixedStep : Mathf.Min(Time.unscaledDeltaTime, 0.25f));
        }

        /// <summary>Advances the running phase by dt real seconds (LateUpdate calls this; tests call it directly).</summary>
        public void Advance(float dt)
        {
            switch (Current)
            {
                case Phase.Transition:
                    Clock += dt * (_skipping ? _skipSpeed : 1f);
                    ApplyTransition(Mathf.Min(Clock, Timeline.Ready));
                    if (Clock >= Timeline.Ready) FinishTransition();
                    break;
                case Phase.Puzzle:
                    if (!BoardInput.enabled && !PointerHeld()) BoardInput.enabled = true;
                    break;
                case Phase.Return:
                    Clock += dt;
                    ApplyReturn(Mathf.Min(Clock, S.Timing.ReturnTime));
                    if (Clock >= S.Timing.ReturnTime) FinishReturn();
                    break;
            }
        }

        private void ApplyTransition(float t)
        {
            var tl = Timeline;
            var T = S.Timing;

            var p2 = BoardPose();
            var pose = CameraFlight.Evaluate(_p0, _p1, p2, tl.CameraProgress(t));
            float settle = tl.SettleAt(t);
            if (settle >= 0f) pose = CameraFlight.Settle(p2, settle, S.SettleAmount);
            if (t < T.HighlightEnd) pose.Fov -= S.FovPunch * Mathf.Sin(Mathf.PI * t / T.HighlightEnd);
            ApplyPose(pose);

            float f = tl.CameraProgress(t);
            RenderSettings.fogStartDistance = Mathf.Lerp(_fogStart0, S.BoardFogStart, f);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd0, S.BoardFogEnd, f);
            if (_basketCanvas != null) _basketCanvas.enabled = t < T.CameraStart;

            var player = Round.Column.Player;
            if (player != null)
            {
                if (!_heroShown && t >= T.HeroStart) { _heroShown = true; player.Reveal(); }
                if (_heroShown) player.transform.rotation = _heroRot0 * Quaternion.Euler(0f, S.HeroTurnDegrees * tl.HeroTurn(t), 0f);
            }

            Stream.Tick(t);
            Hoop.Tick(Mathf.Max(0f, t - _lastT));
            _lastT = t;
            int c = tl.CountAt(t);
            if (c != _shownCount) { _shownCount = c; Game.ShowAmmo(c); }

            PlaceBadge(t);
            BoardReveal.Apply(Game, tl, t);
            float glow = tl.FrameGlow(t);
            Hoop.SetGlow(glow);
            GlowBoardFrame(glow);
            if (!_revealSounded && t >= T.RevealStart) { _revealSounded = true; Sfx?.Play("multiply"); }
            Hud.SetIntro(tl.HudProgress(t));
        }

        private void PlaceBadge(float t)
        {
            if (_badgeMerged) return;
            var tl = Timeline;
            bool stream = ActiveCandidate == FlowCandidate.Stream;
            float dock = tl.BadgeDock(t), fly = tl.BadgeFly(t);
            var dockPt = new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * (stream ? 0.84f : 0.62f));
            var p = Vector2.Lerp(Vector2.Lerp(_cageScreen, dockPt, dock), Hud.AmmoScreenPoint, fly);
            float flying = stream ? 0.72f : 1.15f + 0.12f * Mathf.Sin(Mathf.PI * tl.CameraProgress(t));
            float scale = t < S.Timing.HighlightEnd ? tl.HighlightScale(t) : Mathf.Lerp(1f, flying, dock) * Mathf.Lerp(1f, 0.45f, fly);
            float alpha = 1f - Mathf.Clamp01((fly - 0.7f) / 0.3f);
            Badge.Place(p, scale, alpha);
            if (fly >= 1f)
            {
                _badgeMerged = true;
                Badge.Hide();
                Hud.PopAmmo();
            }
        }

        private void FinishTransition()
        {
            BoardReveal.Finish(Game);
            Hoop.SetGlow(0f);
            GlowBoardFrame(0f);
            Hud.SetIntro(1f);
            Badge.Hide();
            RenderSettings.fogStartDistance = S.BoardFogStart;
            RenderSettings.fogEndDistance = S.BoardFogEnd;
            Cam.fieldOfView = S.BoardFov;
            Game.Begin();
            Hud.PopAmmo();
            Sfx?.Play("add");
            EnterPhase(Phase.Puzzle);
        }

        private void ApplyReturn(float t)
        {
            float u = FlowTimeline.Smoother(Mathf.Clamp01(t / S.Timing.ReturnTime));
            ApplyPose(CameraFlight.Evaluate(_back, _p1, AimPose(), u));
            RenderSettings.fogStartDistance = Mathf.Lerp(S.BoardFogStart, _fogStart0, u);
            RenderSettings.fogEndDistance = Mathf.Lerp(S.BoardFogEnd, _fogEnd0, u);
            Hud.SetIntro(1f - Mathf.Clamp01(t / 0.3f));
        }

        private void FinishReturn()
        {
            ApplyPose(AimPose());
            RenderSettings.fogStartDistance = _fogStart0;
            RenderSettings.fogEndDistance = _fogEnd0;
            var player = Round.Column != null ? Round.Column.Player : null;
            if (player != null) player.transform.rotation = _heroRot0;
            Hud.SetIntro(0f);
            EnterPhase(Phase.Basket);        // RoundCamera resumes from the aim pose it finds
            Round.ResetRound();
        }

        private void EnterPhase(Phase p)
        {
            Current = p;
            bool basket = p == Phase.Basket;
            if (Shot != null) Shot.InputEnabled = basket;
            if (RoundCam != null) RoundCam.enabled = basket;
            if (BoardInput != null) BoardInput.enabled = false;      // Puzzle turns it on once the finger is up
            if (BoardCam != null) BoardCam.enabled = p == Phase.Puzzle || p == Phase.Result;
            if (_basketCanvas != null) _basketCanvas.enabled = basket || (p == Phase.Transition && Clock < S.Timing.CameraStart);
            if (_boardCanvas != null) _boardCanvas.enabled = !basket;
            if (basket) Badge.Hide();
            PhaseChanged?.Invoke(p);
        }

        private CamPose BoardPose()
        {
            var pos = BoardCam.FitFor(S.BoardFov, Cam.aspect);
            return new CamPose(pos, pos + Vector3.forward * 20f, S.BoardFov);
        }

        private CamPose AimPose()
        {
            var bs = Round.Settings;
            return new CamPose(bs.AimCamPos, bs.AimLookAt, bs.Fov);
        }

        private void ApplyPose(CamPose p)
        {
            Cam.transform.position = p.Pos + (Shake != null ? Shake.Offset : Vector3.zero);
            Cam.transform.rotation = Quaternion.LookRotation(p.Look - p.Pos, Vector3.up);
            Cam.fieldOfView = p.Fov;
        }

        private void GlowBoardFrame(float g)
        {
            if (_boardFrameMat == null)
            {
                var bar = Game.transform.Find("Scenery/FrameTop");
                var r = bar != null ? bar.GetComponent<Renderer>() : null;
                if (r == null) return;
                _boardFrameMat = r.sharedMaterial;          // runtime material shared by the four frame bars
                _boardFrameMat.EnableKeyword("_EMISSION");
            }
            _boardFrameMat.SetColor("_EmissionColor", Game.S.FrameColor * (1.8f * g));
        }

        private void OnLaunched(int i)
        {
            if (i % 3 == 0) Sfx?.Play("shot", 1.15f, 0.35f);
        }

        private void OnLanded(int i) => Sfx?.Play("land", Mathf.Min(1.25f, 1f + 0.01f * i), 0.55f);
    }
}
