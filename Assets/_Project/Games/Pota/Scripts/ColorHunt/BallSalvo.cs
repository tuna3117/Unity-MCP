using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The ball rain of a tap: pooled basketballs leave the launcher strip one after another and fly to the
    /// block on an arched smoothstep path with a trail. Arrival is reported so the block can flinch.
    /// </summary>
    public class BallSalvo : MonoBehaviour
    {
        public ColorHuntSettings S;
        public Material BallMaterial;

        private class Ball
        {
            public Transform T; public TrailRenderer Trail;
            public Vector3 From, To; public float Delay, Flight, T0; public bool Flying, Done;
        }

        private readonly List<Ball> _active = new List<Ball>();
        private readonly Stack<Ball> _pool = new Stack<Ball>();
        private System.Action<Vector3> _onArrive;
        private float _clock;

        public bool Busy => _active.Count > 0;

        public void Fire(Vector3 source, Vector3 target, int count, float duration, System.Action<Vector3> onArrive)
        {
            _onArrive = onArrive;
            int n = Mathf.Max(1, Mathf.Min(S.SalvoMaxBalls, count));
            float launch = duration * S.SalvoLaunchFraction, flight = duration * S.SalvoFlightFraction;
            for (int i = 0; i < n; i++)
            {
                float o = n == 1 ? 0f : (float)i / (n - 1);
                var b = _pool.Count > 0 ? _pool.Pop() : Create();
                b.From = source + new Vector3((Random.value - 0.5f) * S.SalvoSpread, 0f, -0.3f);
                b.To = target + new Vector3((Random.value - 0.5f) * S.SalvoScatter, (Random.value - 0.5f) * S.SalvoScatter, -S.BlockDepth * 0.5f - 0.18f);
                b.Delay = o * launch; b.Flight = flight; b.T0 = _clock; b.Flying = false; b.Done = false;
                b.T.position = b.From;
                b.T.gameObject.SetActive(false);
                _active.Add(b);
            }
        }

        public void Clear()
        {
            foreach (var b in _active) Recycle(b);
            _active.Clear();
        }

        public void Tick(float dt)
        {
            _clock += dt;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                float u = (_clock - b.T0 - b.Delay) / b.Flight;
                if (u < 0f) continue;
                if (u >= 1f)
                {
                    _active.RemoveAt(i);
                    Recycle(b);
                    _onArrive?.Invoke(b.To);
                    continue;
                }
                if (!b.Flying)
                {
                    b.Flying = true;
                    b.T.gameObject.SetActive(true);
                    b.Trail.Clear();
                }
                float e = u * u * (3f - 2f * u);
                var p = Vector3.Lerp(b.From, b.To, e);
                p.z -= Mathf.Sin(Mathf.PI * u) * 0.9f; // arc toward the viewer
                b.T.position = p;
                float sc = 0.7f + 0.5f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.15f));
                b.T.localScale = Vector3.one * (0.36f * sc);
                b.T.Rotate(0f, 0f, -720f * dt, Space.Self);
            }
        }

        private void Recycle(Ball b)
        {
            b.T.gameObject.SetActive(false);
            _pool.Push(b);
        }

        private Ball Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            if (BallMaterial != null) r.sharedMaterial = BallMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.03f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 2f); mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", Color.white);
            trail.sharedMaterial = mat;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return new Ball { T = go.transform, Trail = trail };
        }
    }
}
