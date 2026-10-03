using System.Collections.Generic;
using UnityEngine;

namespace Project.VFX
{
    /// <summary>
    /// Blows the mesh pieces under this object apart (a fractured block, a bomb shell, ball halves from the Blender workshop):
    /// each piece flies out from the burst centre with an upward bias, falls, spins and shrinks away at the end. Motion is a
    /// closed form of the time (p = p0 + v·t + ½·g·t²), so any moment can be drawn directly (contact sheets) and the result
    /// does not depend on the frame rate. Seeded: the same seed gives the same burst.
    /// </summary>
    public class MeshBurst : MonoBehaviour, IVfxPart
    {
        [Tooltip("Launch speed (m/s, in this object's space).")]
        [SerializeField, Min(0f)] private float speed = 2.5f;
        [Tooltip("Random speed variation per piece (0.6 = ±60 %).")]
        [SerializeField, Range(0f, 1f)] private float spread = 0.6f;
        [Tooltip("How much the launch direction leans up (0 = straight out from the centre, 1 = straight up).")]
        [SerializeField, Range(0f, 1f)] private float upBias = 0.45f;
        [SerializeField, Min(0f)] private float gravity = 9.81f;
        [Tooltip("Spin speed (degrees/s), random axis and direction per piece.")]
        [SerializeField, Min(0f)] private float spin = 540f;
        [SerializeField, Min(0.05f)] private float lifetime = 1f;
        [Tooltip("Share of the lifetime at the end during which the pieces shrink to nothing.")]
        [SerializeField, Range(0.01f, 1f)] private float shrink = 0.35f;
        [SerializeField] private int seed = 7;
        [SerializeField] private bool useUnscaledTime;

        private struct Piece
        {
            public Transform transform;
            public Vector3 restPos, restScale, velocity, spinAxis;
            public Quaternion restRot;
            public Matrix4x4 toParent;   // burst space → the piece's parent space (fixed inside the prefab)
            public float spinSpeed;
        }

        private readonly List<Piece> _pieces = new List<Piece>();
        private bool _captured, _playing;
        private float _t;

        public float Lifetime { get => lifetime; set => lifetime = Mathf.Max(0.05f, value); }
        public int Seed { get => seed; set { seed = value; _captured = false; } }
        public float Speed { get => speed; set { speed = value; _captured = false; } }
        public int PieceCount { get { Capture(); return _pieces.Count; } }
        public bool IsAlive => _playing && _t < lifetime;

        private void Awake() => Capture();

        public void Restart()
        {
            Capture();
            _t = 0f;
            _playing = true;
            Evaluate(0f);
        }

        public void Clear()
        {
            _playing = false;
            _t = 0f;
            Evaluate(0f);
        }

        /// <summary>Advances a playing burst by <paramref name="dt"/> seconds (Update does this every frame).</summary>
        public void Tick(float dt)
        {
            if (!_playing) return;
            _t += dt;
            Evaluate(_t);
            if (_t >= lifetime) _playing = false;
        }

        private void Update() => Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>Puts every piece where it is <paramref name="t"/> seconds into the burst (0 = rest pose).</summary>
        public void Evaluate(float t)
        {
            Capture();
            t = Mathf.Max(0f, t);
            Vector3 down = transform.InverseTransformDirection(Vector3.down);
            float fadeStart = lifetime * (1f - shrink);
            float scale = t <= fadeStart ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, lifetime, t));
            foreach (var p in _pieces)
            {
                if (p.transform == null) continue;
                Vector3 offset = p.velocity * t + down * (0.5f * gravity * t * t);
                p.transform.localPosition = p.restPos + p.toParent.MultiplyVector(offset);
                p.transform.localRotation = p.restRot * Quaternion.AngleAxis(p.spinSpeed * t, p.spinAxis);
                p.transform.localScale = p.restScale * scale;
            }
        }

        /// <summary>Records the rest pose and each piece's launch once (pieces = every MeshRenderer below this object).</summary>
        private void Capture()
        {
            if (_captured)
                return;
            // A re-capture (new seed) must start from the rest pose, not from wherever the last evaluation left the pieces.
            foreach (var old in _pieces)
                if (old.transform != null)
                {
                    old.transform.localPosition = old.restPos;
                    old.transform.localRotation = old.restRot;
                    old.transform.localScale = old.restScale;
                }
            _pieces.Clear();
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            var centres = new Vector3[renderers.Length];
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < renderers.Length; i++)
            {
                centres[i] = transform.InverseTransformPoint(renderers[i].bounds.center);
                centre += centres[i];
            }
            if (renderers.Length > 0) centre /= renderers.Length;
            var rng = new System.Random(seed);
            float Rand() => (float)rng.NextDouble();
            for (int i = 0; i < renderers.Length; i++)
            {
                var t = renderers[i].transform;
                Vector3 outward = centres[i] - centre;
                if (outward.sqrMagnitude < 1e-8f) outward = new Vector3(Rand() - 0.5f, 0f, Rand() - 0.5f);
                Vector3 dir = Vector3.Lerp(outward.normalized, Vector3.up, upBias).normalized;
                Vector3 axis = new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f);
                var parent = t.parent;
                _pieces.Add(new Piece
                {
                    transform = t,
                    restPos = t.localPosition,
                    restRot = t.localRotation,
                    restScale = t.localScale,
                    velocity = dir * speed * (1f + spread * (2f * Rand() - 1f)),
                    spinAxis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up,
                    spinSpeed = spin * (0.5f + Rand()) * (Rand() < 0.5f ? -1f : 1f),
                    toParent = parent == null ? transform.localToWorldMatrix
                        : parent.worldToLocalMatrix * transform.localToWorldMatrix,
                });
            }
            _captured = true;
        }

        private void OnValidate() => _captured = false;
    }
}
