using System;
using System.Collections;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// The basket round state machine: one swipe → N balls → hoop column → cage count.
    /// Raises events consumed by the HUD, camera and juice layers.
    /// </summary>
    public class BasketRound : MonoBehaviour
    {
        public enum State { Aim, Flight, Drop, Settle, Done }

        public BasketSettings Settings;
        public BallPool Pool;
        public int Level = 1;

        public State Current { get; private set; }
        public HoopLayout Layout { get; private set; }
        public HoopColumnBuilder.Result Column { get; private set; }
        public int BasketCount { get; private set; }
        public int TopPasses { get; private set; }
        public bool Missed { get; private set; }
        public int TotalSpawned { get; private set; }
        public int VirtualCount { get; private set; }
        public float Elapsed { get; private set; }
        public int InFlight { get; private set; }
        public int BallsToThrow { get; private set; }
        public AimPoint LastAim { get; private set; }
        public Ball FirstBall { get; private set; }
        public int LayerFlight { get; private set; }
        public int LayerDrop { get; private set; }
        public int LayerHoop { get; private set; }
        public int LayerSlab { get; private set; }

        public event Action<AimPoint> ShotFired;
        public event Action<Hoop, Ball, bool> HoopPassed;   // (hoop, ball, isFirstTopSwish)
        public event Action<Hoop, Ball> RimHit;
        public event Action<Ball> BallLanded;
        public event Action<int> RoundEnded;
        public event Action MissedDeclared;
        public event Action<State> StateChanged;

        private float _prevFixedDelta;
        private Coroutine _spawner;
        private readonly System.Random _rng = new System.Random();

        private void Awake()
        {
            LayerFlight = ResolveLayer("BallFlight");
            LayerDrop = ResolveLayer("BallDrop");
            LayerHoop = ResolveLayer("Hoop");
            LayerSlab = ResolveLayer("Slab");
            if (LayerFlight >= 0 && LayerSlab >= 0) Physics.IgnoreLayerCollision(LayerFlight, LayerSlab, true);
            if (Pool == null) Pool = GetComponent<BallPool>();
            if (Pool == null) Pool = gameObject.AddComponent<BallPool>();
            Pool.Initialize(Settings, Mathf.Max(0, LayerFlight), Mathf.Max(0, LayerDrop));
            _prevFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / 120f;
        }

        private static int ResolveLayer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0) Debug.LogError($"[BasketRound] Layer '{name}' is missing. Run AI Tools/Basket/Build Basket Scene.");
            return layer;
        }

        private void OnDestroy()
        {
            if (_prevFixedDelta > 0f) Time.fixedDeltaTime = _prevFixedDelta;
        }

        private void Start()
        {
            if (Column == null) LoadLevel(Level);
        }

        public void LoadLevel(int level)
        {
            Level = LevelRules.ClampLevel(level);
            if (_spawner != null) { StopCoroutine(_spawner); _spawner = null; }
            Pool.ReleaseAll();
            if (Column != null)
            {
                foreach (var h in Column.Hoops) { h.Passed -= OnHoopPassed; h.RimHit -= OnRimHit; }
                Column.Cage.Entered -= OnBallEntered;
                Destroy(Column.Root.gameObject);
            }
            Layout = LevelRules.Build(Level);
            Column = HoopColumnBuilder.Build(transform, Layout, Settings, Mathf.Max(0, LayerHoop), Mathf.Max(0, LayerSlab));
            foreach (var h in Column.Hoops) { h.Passed += OnHoopPassed; h.RimHit += OnRimHit; }
            Column.Cage.Entered += OnBallEntered;
            BasketCount = 0; TopPasses = 0; Missed = false; TotalSpawned = 0; VirtualCount = 0; Elapsed = 0f; InFlight = 0;
            BallsToThrow = Layout.BallCount; FirstBall = null;
            SetState(State.Aim);
        }

        public void ResetRound() => LoadLevel(Level);

        public void Shoot(AimPoint aim)
        {
            if (Current != State.Aim) return;
            LastAim = aim;
            SetState(State.Flight);
            ShotFired?.Invoke(aim);
            _spawner = StartCoroutine(SpawnBalls(aim));
        }

        public void ShootProgrammatic(float x, float power) => Shoot(AimMapper.FromXPower(x, power));

        private IEnumerator SpawnBalls(AimPoint aim)
        {
            int n = Layout.BallCount;
            for (int i = 0; i < n; i++)
            {
                float jx = ((float)_rng.NextDouble() - 0.5f) * 2f * Settings.AimJitter;
                var target = new Vector3(aim.X + jx, aim.Y, 0f);
                var v = TrajectorySolver.LaunchVelocity(Settings.LaunchStart, target, aim.FlightTime, Settings.FlightGravity);
                var b = Pool.Get();
                b.Launch(Settings.LaunchStart, v);
                TotalSpawned++;
                if (i == 0) FirstBall = b;
                BallsToThrow = n - i - 1;
                yield return new WaitForSeconds(Settings.BallSpacing);
            }
            _spawner = null;
        }

        private void OnHoopPassed(Hoop hoop, Ball ball)
        {
            bool firstTopSwish = false;
            if (hoop.Spec.IsTop)
            {
                TopPasses++;
                firstTopSwish = TopPasses == 1;
            }
            if (hoop.Spec.Kind == HoopEffectKind.Multiply)
            {
                for (int k = 1; k < hoop.Spec.Amount; k++) SpawnClone(ball, hoop);
            }
            else
            {
                for (int k = 0; k < hoop.Spec.Amount; k++) SpawnAdd(hoop);
            }
            HoopPassed?.Invoke(hoop, ball, firstTopSwish);
        }

        private void SpawnClone(Ball source, Hoop hoop)
        {
            if (!Pool.CanSimulateMore) { VirtualCount++; BasketCount++; return; }
            var v = source.Body.linearVelocity;
            v.x += ((float)_rng.NextDouble() - 0.5f) * Settings.CloneScatter;
            v.y *= 0.8f + (float)_rng.NextDouble() * 0.3f;
            v.z *= 0.5f;
            var p = source.transform.position;
            p.x += ((float)_rng.NextDouble() - 0.5f) * 0.05f;
            var b = Pool.Get();
            b.Drop(p, v, hoop);
            TotalSpawned++;
        }

        private void SpawnAdd(Hoop hoop)
        {
            if (!Pool.CanSimulateMore) { VirtualCount++; BasketCount++; return; }
            var p = new Vector3(hoop.Center.x + ((float)_rng.NextDouble() - 0.5f) * 0.1f, hoop.Center.y - 0.15f, 0f);
            var b = Pool.Get();
            b.Drop(p, new Vector3(0f, -Settings.AddDropSpeed, 0f), hoop);
            TotalSpawned++;
        }

        private void OnBallEntered(Ball ball)
        {
            BasketCount++;
            BallLanded?.Invoke(ball);
        }

        private void OnRimHit(Hoop hoop, Ball ball) => RimHit?.Invoke(hoop, ball);

        private void FixedUpdate()
        {
            if (Current == State.Aim || Current == State.Done || Column == null) return;
            float dt = Time.fixedDeltaTime;
            Elapsed += dt;
            var balls = Pool.Active;
            int inFlight = 0, active = 0;
            bool anyDropping = false;
            float highestY = float.MinValue;
            float killY = Column.Cage.FloorY - 5f;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var b = balls[i];
                if (b.Phase == BallPhase.Flight) inFlight++;
                if (b.Phase == BallPhase.Drop) anyDropping = true;
                if (b.Counted) continue;
                if (b.transform.position.y < killY) { Pool.Release(b); continue; } // lost outside the column
                active++;
                if (b.transform.position.y > highestY) highestY = b.transform.position.y;
                UpdateStuck(b, dt);
            }
            InFlight = inFlight;
            Column.Cage.Track(balls);
            FreezeSleepers();

            if (Current == State.Flight && anyDropping) SetState(State.Drop);

            if (!Missed && _spawner == null && TotalSpawned > 0 &&
                RoundRules.IsMissed(inFlight, TopPasses, highestY == float.MinValue ? -999f : highestY, Layout.TopRimY))
            {
                Missed = true;
                MissedDeclared?.Invoke();
            }

            if (Current == State.Drop && _spawner == null && RoundRules.FewLeft(active, TotalSpawned)) SetState(State.Settle);

            if (_spawner == null && RoundRules.ShouldEnd(active, TotalSpawned, Column.Cage.MaxSpeed, Time.time - Column.Cage.LastEntryTime, Elapsed, Settings.RoundTimeout))
                EndRound();
        }

        private void UpdateStuck(Ball b, float dt)
        {
            if (b.Phase != BallPhase.Drop) return;
            float y = b.transform.position.y;
            if (y < b.LowestY - 0.04f) { b.LowestY = y; b.StuckTimer = 0f; }
            else b.StuckTimer += dt;
            float speed = b.Body.linearVelocity.magnitude;
            b.SlowTimer = speed < Settings.SlowSpeed ? b.SlowTimer + dt : 0f;

            if (b.StuckTimer >= Settings.NudgeSeconds && b.StuckTimer < Settings.NudgeSeconds + dt * 1.5f)
                b.Body.AddForce(new Vector3(((float)_rng.NextDouble() - 0.5f) * 1.2f, 0.4f, 0f), ForceMode.VelocityChange);

            if (b.StuckTimer >= Settings.StuckSeconds || b.SlowTimer >= Settings.SlowSeconds)
            {
                float x = Mathf.Clamp(b.transform.position.x, -Column.Cage.Width * 0.4f, Column.Cage.Width * 0.4f);
                b.Teleport(new Vector3(x, Column.Cage.TopY + 0.3f, 0f), new Vector3(0f, -1f, 0f));
            }
        }

        private void FreezeSleepers()
        {
            var balls = Pool.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var b = balls[i];
                if (b.Counted && b.Phase == BallPhase.Piled && b.Body.IsSleeping()) b.Freeze();
            }
        }

        private void EndRound()
        {
            var balls = Pool.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var b = balls[i];
                if (b.Counted) continue;
                float x = Mathf.Clamp(b.transform.position.x, -Column.Cage.Width * 0.4f, Column.Cage.Width * 0.4f);
                b.Teleport(new Vector3(x, Column.Cage.TopY + 0.3f, 0f), new Vector3(0f, -2f, 0f));
                b.MarkPiled();
                BasketCount++;
            }
            SetState(State.Done);
            RoundEnded?.Invoke(BasketCount);
        }

        public Vector3 MeanActivePosition
        {
            get
            {
                var balls = Pool.Active;
                var sum = Vector3.zero; int n = 0;
                for (int i = 0; i < balls.Count; i++)
                {
                    if (balls[i].Counted) continue;
                    sum += balls[i].transform.position; n++;
                }
                if (n == 0) return new Vector3(0f, Column != null ? Column.Cage.TopY : 0f, 0f);
                return sum / n;
            }
        }

        private void SetState(State s)
        {
            if (Current == s) return;
            Current = s;
            StateChanged?.Invoke(s);
        }
    }
}
