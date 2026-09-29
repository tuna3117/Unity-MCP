using System;
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// Candidate A's visible balls: pooled spheres (no physics) with short trails. Positions are a pure function of the
    /// timeline time t, so a skip or a long frame cannot lose a ball: <see cref="Tick"/> raises <see cref="Landed"/> once for
    /// every ball whose rim time has passed since the previous tick. Path: cubic Bézier leaving the cage mouth straight up
    /// (a little forward), arcing over the roof and dropping straight down through the rim, then a short fall to its pile
    /// slot (the first <see cref="GiantHoop.MaxPile"/> balls) or out of sight into the net.
    /// </summary>
    public class BallStream : MonoBehaviour
    {
        public Material BallMaterial;
        public float BallRadius = 0.24f;
        [Tooltip("Path top = the higher end + this (spec: 9 m).")]
        public float ApexAbove = 9f;
        [Tooltip("Extra apex per ball, 0..this (never lower: lower paths graze the hero).")]
        public float ApexJitter = 1.2f;
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);
        public float DropTime = 0.15f;
        [Tooltip("Source spread across the cage mouth (x ± this, z ± half of it).")]
        public float Spread = 0.6f;
        public Color TrailColor = new Color(1f, 0.62f, 0.25f, 0.8f);

        public event Action<int> Launched;
        public event Action<int> Landed;
        public int ActiveCount { get; private set; }
        public int Capacity => _balls.Length;

        private Transform[] _balls = new Transform[0];
        private TrailRenderer[] _trails = new TrailRenderer[0];
        private Vector3[] _from = new Vector3[0], _entry = new Vector3[0], _axis = new Vector3[0];
        private float[] _apex = new float[0];
        private FlowTimeline _tl;
        private GiantHoop _hoop;
        private float _lastT;
        private bool _running;

        /// <summary>Creates the ball objects up front (no allocation or hitch when the transition starts).</summary>
        public void Prewarm(int count)
        {
            int old = _balls.Length;
            if (count <= old) return;
            Array.Resize(ref _balls, count);
            Array.Resize(ref _trails, count);
            Array.Resize(ref _from, count);
            Array.Resize(ref _entry, count);
            Array.Resize(ref _axis, count);
            Array.Resize(ref _apex, count);
            var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var mat = BallMaterial != null ? BallMaterial : RimMeshBuilder.UrpLit(new Color(0.93f, 0.45f, 0.13f), 0.35f);
            var trailMat = new Material(Shader.Find("Sprites/Default"));
            for (int i = old; i < count; i++)
            {
                var go = RimMeshBuilder.MeshObject($"FlowBall{i:00}", transform, mesh, mat);
                go.transform.localScale = Vector3.one * (BallRadius * 2f);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMat;
                trail.time = 0.12f;
                trail.minVertexDistance = 0.05f;
                trail.widthMultiplier = BallRadius * 1.4f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.startColor = TrailColor;
                trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
                trail.emitting = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _balls[i] = go.transform;
                _trails[i] = trail;
            }
        }

        public void Begin(FlowTimeline tl, Vector3 source, GiantHoop hoop)
        {
            Prewarm(tl.Visible);
            Clear();
            _tl = tl;
            _hoop = hoop;
            _lastT = float.NegativeInfinity;
            _running = true;
            var rim = hoop.RimCenter;
            for (int i = 0; i < tl.Visible; i++)
            {
                _from[i] = source + new Vector3((Hash(i, 1) - 0.5f) * 2f * Spread, 0f, (Hash(i, 2) - 0.5f) * Spread);
                float a = Hash(i, 3) * Mathf.PI * 2f, r = Hash(i, 4) * hoop.RimRadius * 0.35f;
                _entry[i] = rim + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                _apex[i] = Mathf.Max(_from[i].y, _entry[i].y) + ApexAbove + Hash(i, 5) * ApexJitter;
                var axis = new Vector3(Hash(i, 6) - 0.5f, Hash(i, 7) - 0.5f, Hash(i, 8) - 0.5f);
                _axis[i] = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.up;
            }
        }

        /// <summary>Places every ball for timeline time t and raises launch/landing events crossed since the last tick.</summary>
        public void Tick(float t)
        {
            if (!_running) return;
            int active = 0;
            for (int i = 0; i < _tl.Visible; i++)
            {
                float launch = _tl.LaunchTime(i), land = _tl.LandTime(i);
                if (_lastT < launch && t >= launch) Launched?.Invoke(i);
                if (_lastT < land && t >= land)
                {
                    _hoop.AddToPile();
                    Landed?.Invoke(i);
                }
                bool resting = i < GiantHoop.MaxPile;
                bool visible = t >= launch && (resting || t < land + DropTime);
                var ball = _balls[i];
                if (ball.gameObject.activeSelf != visible)
                {
                    ball.gameObject.SetActive(visible);
                    if (visible) _trails[i].Clear();
                }
                if (!visible) continue;
                active++;
                if (t < land)
                {
                    float u = (t - launch) / (land - launch);
                    ball.position = PathPoint(_from[i], _entry[i], _apex[i], LaunchKick, u);
                    _trails[i].emitting = true;
                }
                else
                {
                    float d = Mathf.Clamp01((t - land) / DropTime);
                    var target = resting ? _hoop.SlotPosition(i) : _hoop.RimCenter + Vector3.down * (_hoop.NetDepth + BallRadius);
                    ball.position = Vector3.Lerp(_entry[i], target, d * d);
                    _trails[i].emitting = false;
                }
                ball.rotation = Quaternion.AngleAxis(t * 540f, _axis[i]);
            }
            ActiveCount = active;
            _lastT = t;
        }

        public void Clear()
        {
            _running = false;
            ActiveCount = 0;
            for (int i = 0; i < _balls.Length; i++)
            {
                _trails[i].emitting = false;
                _trails[i].Clear();
                _balls[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Cubic Bézier with vertical end tangents: leaves a upward (plus kick), comes down into b; top ≈ apexY.</summary>
        public static Vector3 PathPoint(Vector3 a, Vector3 b, float apexY, Vector3 kick, float u)
        {
            u = Mathf.Clamp01(u);
            var p1 = a + new Vector3(0f, (apexY - a.y) * 4f / 3f, 0f) + kick;
            var p2 = b + new Vector3(0f, (apexY - b.y) * 4f / 3f, 0f);
            float v = 1f - u;
            return v * v * v * a + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * b;
        }

        private static float Hash(int i, int k)
        {
            unchecked
            {
                uint h = (uint)(i * 73856093) ^ (uint)(k * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }
    }
}
