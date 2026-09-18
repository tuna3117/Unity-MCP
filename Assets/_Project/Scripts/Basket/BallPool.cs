using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Project.Basket
{
    /// <summary>Pool of <see cref="Ball"/>s with the simulation caps from <see cref="BasketSettings"/>.</summary>
    public class BallPool : MonoBehaviour
    {
        public BasketSettings Settings;

        private ObjectPool<Ball> _pool;
        private readonly List<Ball> _active = new List<Ball>();
        private int _layerFlight, _layerDrop, _nextId;
        private Material _visual;
        private Transform _root;

        public IReadOnlyList<Ball> Active => _active;
        public int ActiveCount => _active.Count;

        public void Initialize(BasketSettings settings, int flightLayer, int dropLayer)
        {
            Settings = settings;
            _layerFlight = flightLayer;
            _layerDrop = dropLayer;
            _root = new GameObject("Balls").transform;
            _root.SetParent(transform, false);
            _visual = Settings.BallVisualMaterial != null
                ? Settings.BallVisualMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.94f, 0.48f, 0.16f) };
            _pool = new ObjectPool<Ball>(Create, null, OnRelease, b => { if (b != null) Destroy(b.gameObject); },
                collectionCheck: false, defaultCapacity: 64, maxSize: Settings.MaxBalls);
        }

        public int CountSimulated()
        {
            int n = 0;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Phase != BallPhase.Frozen) n++;
            return n;
        }

        public bool CanSimulateMore => _active.Count < Settings.MaxBalls && CountSimulated() < Settings.MaxSimulated;

        public Ball Get()
        {
            var b = _pool.Get();
            b.Id = _nextId++;
            _active.Add(b);
            return b;
        }

        public void Release(Ball b)
        {
            if (_active.Remove(b)) _pool.Release(b);
        }

        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) _pool.Release(_active[i]);
            _active.Clear();
        }

        private Ball Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            go.transform.SetParent(_root, false);
            go.transform.localScale = Vector3.one * (Settings.BallRadius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _visual;
            go.AddComponent<Rigidbody>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.emitting = false;
            trail.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(1f, 0.9f, 0.6f, 0.6f) };
            var ball = go.AddComponent<Ball>();
            ball.Configure(Settings, _layerFlight, _layerDrop);
            go.SetActive(false);
            return ball;
        }

        private void OnRelease(Ball b)
        {
            b.Body.isKinematic = false;
            b.Body.linearVelocity = Vector3.zero;
            b.gameObject.SetActive(false);
        }
    }
}
