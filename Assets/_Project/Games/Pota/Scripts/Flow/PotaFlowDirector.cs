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
        [Header("Look")]
        public PuzzleLighting Look;
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
        public PuzzleStage Stage { get; private set; }
        /// <summary>The hero's basket on the terrace: candidate A's balls land here.</summary>
        public BallBin Bin { get; private set; }
        public BallStream Stream { get; private set; }
        public FlowBadge Badge { get; private set; }
        public event Action<Phase> PhaseChanged;

        private CamPose _p0, _p1, _back;
        private Vector3 _source;
        private Vector2 _cageViewport;
        private float _fogStart0, _fogEnd0, _skipSpeed, _lastT, _camU;
        private bool _skipping, _heroShown, _badgeMerged, _revealSounded, _built, _prewarmed;
        private Quaternion _heroRot0 = Quaternion.identity;
        private int _shownCount;
        private Canvas _basketCanvas, _boardCanvas;

        private void Start()
        {
            BoardCam.Anchor = Game.transform.position;
            _basketCanvas = BasketHud != null ? BasketHud.GetComponent<Canvas>() : null;
            _boardCanvas = Hud.GetComponent<Canvas>();
            if (BasketHud != null) BasketHud.ShowResultPanel = false;
            Hud.RetryOverride = () => ReturnToBasket();
            Hud.SetRetryLabel("Tekrar at");
            Hud.ShowHint = false;          // the giant hoop's net sits where the how-to hint goes
            Hud.SetIntro(0f);

            var stageGo = new GameObject("Stage");
            stageGo.transform.SetParent(transform, false);
            Stage = stageGo.AddComponent<PuzzleStage>();
            Stage.BuildOnStart = false;
            Stage.Game = Game;
            Stage.GroundBelow = Game.transform.position.y;                      // the roof is world y = 0
            Stage.CourtSize = S.CourtSize;
            Stage.CourtNear = Game.transform.position.z - S.CourtStartZ;
            Stage.CourtColor = S.CourtColor;
            Stage.CourtLineColor = S.CourtLineColor;
            Stage.BallRadius = S.BallRadius;
            Stage.Build();
            Hoop = Stage.Hoop;

            var binGo = new GameObject("BallBin");
            binGo.transform.SetParent(transform, false);
            binGo.transform.position = S.BinPosition;
            Bin = binGo.AddComponent<BallBin>();
            Bin.BallRadius = S.BallRadius;
            Bin.Build();

            var streamGo = new GameObject("BallStream");
            streamGo.transform.SetParent(transform, false);
            Stream = streamGo.AddComponent<BallStream>();
            Stream.BallMaterial = Game.S.BallMaterial;
            Stream.BallRadius = S.BallRadius;
            Stream.ApexAbove = S.ApexAbove;
            Stream.LaunchKick = S.LaunchKick;
            Stream.EndLean = S.BinEndLean;
            Stream.Prewarm(S.Timing.MaxVisible);
            Stream.Launched += OnLaunched;
            Stream.Landed += OnLanded;

            Badge = FlowBadge.Create(transform, Game.S.Font, Game.S.GoldColor, Game.S.NavyColor);

            Round.RoundEnded += OnRoundEnded;
            Game.RoundEnded += OnBoardEnded;
            _fogStart0 = RenderSettings.fogStartDistance;
            _fogEnd0 = RenderSettings.fogEndDistance;
            _built = true;
            Look?.Blend(0f);
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
            Prewarm();
            Count = Mathf.Max(0, count);
            TimeCtl?.Clear();
            Timeline = new FlowTimeline(S.Timing, Count, ActiveCandidate);
            var look = RoundCam != null ? RoundCam.LookPoint : Cam.transform.position + Cam.transform.forward * 10f;
            _p0 = new CamPose(Cam.transform.position, look, Cam.fieldOfView);
            _p1 = new CamPose(S.MidPos, S.MidLook, S.MidFov);
            var cage = Round.Column.Cage;
            _source = new Vector3(0f, cage.TopY + 0.3f, cage.Depth * 0.5f + 0.6f);
            var cv = Cam.WorldToViewportPoint(_source + Vector3.up * 1.2f);   // off-screen (e.g. from the aim pose): start mid-low
            _cageViewport = cv.z > 0f ? new Vector2(Mathf.Clamp01(cv.x), Mathf.Clamp(cv.y, 0.1f, 0.9f)) : new Vector2(0.5f, 0.35f);

            Game.Prepare(Count);
            Game.ShowAmmo(0);
            _shownCount = 0;
            BoardReveal.Apply(Game, Timeline, 0f);
            Hoop.ClearPile();
            Hoop.SetGlow(0f);
            Bin.Clear();
            if (Timeline.Visible > 0) Stream.Begin(Timeline, _source, Bin);
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
            Bin.Clear();
            Badge.Hide();
            Clock = 0f;
            EnterPhase(Phase.Return);
            return true;
        }

        // The first Prepare builds every block view (GameObject, mesh, labels, collider): doing it on the transition's first
        // frame hitches. Stage one board once, out of sight, during the basket round; later Prepares reuse the pooled views.
        private void Prewarm()
        {
            if (_prewarmed || Game.BoardRoot == null) return;
            _prewarmed = true;
            Game.Prepare(Mathf.Max(1, Game.S.Ammo));
            BoardReveal.Apply(Game, new FlowTimeline(S.Timing, 1, FlowCandidate.Simple), 0f);
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (!_prewarmed) Prewarm();
            // The basket stands with the hero: hidden with him and the terrace while the camera dives past them.
            var hero = Round.Column != null ? Round.Column.Player : null;
            bool binShown = hero != null && hero.gameObject.activeInHierarchy;
            if (Bin.gameObject.activeSelf != binShown) Bin.gameObject.SetActive(binShown);
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
            _camU = tl.CameraProgress(t, CameraFlight.Split(_p0, _p1, p2));
            var pose = CameraFlight.Evaluate(_p0, _p1, p2, _camU, S.LookLead);
            float settle = tl.SettleAt(t);
            if (settle >= 0f) pose = CameraFlight.Settle(p2, settle, S.SettleAmount);
            if (t < T.HighlightEnd) pose.Fov -= S.FovPunch * Mathf.Sin(Mathf.PI * t / T.HighlightEnd);
            ApplyPose(pose);

            float f = _camU;
            RenderSettings.fogStartDistance = Mathf.Lerp(_fogStart0, S.BoardFogStart, f);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd0, S.BoardFogEnd, f);
            Look?.Blend(f);
            if (_basketCanvas != null) _basketCanvas.enabled = t < T.CameraStart;

            var player = Round.Column.Player;
            if (player != null)
            {
                if (!_heroShown && t >= T.HeroRevealAt) { _heroShown = true; player.Reveal(); }
                if (_heroShown) player.transform.rotation = Quaternion.Slerp(_heroRot0, FacingBin(player.transform.position), tl.HeroTurn(t));
            }

            Stream.Tick(t);
            Hoop.Tick(Mathf.Max(0f, t - _lastT));
            Bin.Tick(Mathf.Max(0f, t - _lastT));
            _lastT = t;
            int c = tl.CountAt(t);
            if (c != _shownCount) { _shownCount = c; Game.ShowAmmo(c); }

            PlaceBadge(t);
            BoardReveal.Apply(Game, tl, t);
            float glow = tl.FrameGlow(t);
            Hoop.SetGlow(glow);
            if (!_revealSounded && t >= T.RevealStart) { _revealSounded = true; Sfx?.Play("multiply"); }
            Hud.SetIntro(tl.AmmoProgress(t), tl.HudProgress(t));
        }

        private void PlaceBadge(float t)
        {
            if (_badgeMerged) return;
            var tl = Timeline;
            bool stream = ActiveCandidate == FlowCandidate.Stream;
            float dock = tl.BadgeDock(t), fly = tl.BadgeFly(t);
            var dockPt = new Vector2(0.5f, stream ? 0.84f : 0.62f);
            var p = Vector2.Lerp(Vector2.Lerp(_cageViewport, dockPt, dock), Hud.AmmoViewportPoint, fly);
            float flying = stream ? 0.72f : 1.15f + 0.12f * Mathf.Sin(Mathf.PI * _camU);
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
            Hud.SetIntro(1f);
            Badge.Hide();
            RenderSettings.fogStartDistance = S.BoardFogStart;
            RenderSettings.fogEndDistance = S.BoardFogEnd;
            Look?.Blend(1f);
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
            Look?.Blend(1f - u);
            Hud.SetIntro(1f - Mathf.Clamp01(t / 0.3f));
        }

        private void FinishReturn()
        {
            ApplyPose(AimPose());
            RenderSettings.fogStartDistance = _fogStart0;
            RenderSettings.fogEndDistance = _fogEnd0;
            Look?.Blend(0f);
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

        private Quaternion FacingBin(Vector3 heroPos)
        {
            var d = Bin.transform.position - heroPos;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d, Vector3.up) : _heroRot0;
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

        private void OnLaunched(int i)
        {
            if (i % 3 == 0) Sfx?.Play("shot", 1.15f, 0.35f);
        }

        private void OnLanded(int i) => Sfx?.Play("land", Mathf.Min(1.25f, 1f + 0.01f * i), 0.55f);
    }
}
