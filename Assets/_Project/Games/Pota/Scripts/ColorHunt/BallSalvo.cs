using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's salvo (renkAviFaz.ts salvoBaslat / toplariIlerlet / topVardi, yapbozSahne.ts cizToplar, patlamaEfekt.ts cizIzler): up to 24
    /// skinned balls (r 0.19) leave the strip under the frame over the first 55 % of the salvo; each flies 42 % of it on a smoothstep
    /// path bowing sin(πo)·0.9 toward the camera, swells 0.7 → 1.2 → 0.7, spins 14 rad/s on three axes and drags a soft #ffa040
    /// ribbon over its last 10 positions (one every 0.14 cells). Positions are board cells: balls and ribbons are children of the board.
    /// </summary>
    public class BallSalvo : MonoBehaviour
    {
        public ColorHuntSettings S;
        public Material BallMaterial;
        [Tooltip("The board the balls fly over (ColorHuntGame sets its BoardRoot).")]
        public Transform Board;

        private class Ball
        {
            public Transform T;
            public LineRenderer Trail;
            public Vector2 From, To;
            public float Delay, Flight, Clock, Spin, Scale;
            public readonly List<Vector3> Points = new List<Vector3>(PotaLook.TrailPoints);
        }

        private readonly List<Ball> _active = new List<Ball>();
        private readonly Stack<Ball> _pool = new Stack<Ball>();
        private readonly System.Random _rng = new System.Random();
        private Action<Vector2> _onArrive;
        private Material _trailMat;

        public bool Busy => _active.Count > 0;

        public void Fire(Vector2 source, Vector2 target, int cost, float duration, Action<Vector2> onArrive)
        {
            _onArrive = onArrive;
            int n = Mathf.Max(1, Mathf.Min(S.SalvoMaxBalls, cost));
            float launch = duration * S.SalvoLaunchFraction, flight = duration * S.SalvoFlightFraction;
            for (int i = 0; i < n; i++)
            {
                float o = n == 1 ? 0f : (float)i / (n - 1);
                var b = _pool.Count > 0 ? _pool.Pop() : Create();
                b.From = new Vector2(source.x + (R() - 0.5f) * S.SalvoSpread, source.y);
                b.To = new Vector2(target.x + (R() - 0.5f) * S.SalvoScatter, target.y + (R() - 0.5f) * S.SalvoScatter);
                b.Delay = o * launch;
                b.Flight = flight;
                b.Clock = 0f;
                b.Spin = R() * 6f;
                b.Scale = 1f;
                b.Points.Clear();
                b.Trail.positionCount = 0;
                b.T.gameObject.SetActive(false);
                b.Trail.gameObject.SetActive(true);
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
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                b.Clock += dt;
                float o = (b.Clock - b.Delay) / b.Flight;
                if (o < 0f) continue;
                if (o >= 1f)
                {
                    _active.RemoveAt(i);
                    Recycle(b);
                    _onArrive?.Invoke(b.To);
                    continue;
                }
                if (!b.T.gameObject.activeSelf) b.T.gameObject.SetActive(true);
                var p = PotaLook.SalvoPoint(b.From, b.To, o);
                b.Scale = PotaLook.SalvoScale(o);
                b.Spin += dt * PotaLook.SalvoSpin;
                b.T.localPosition = Local(p);
                b.T.localScale = Vector3.one * (0.38f * b.Scale);                  // sphere r 0.19 · olcek
                b.T.localRotation = Quaternion.Euler(new Vector3(b.Spin, b.Spin * 0.6f, b.Spin * 0.3f) * Mathf.Rad2Deg);
                if (b.Points.Count == 0 || Vector2.Distance(p, b.Points[0]) >= PotaLook.TrailSpacing)
                {
                    b.Points.Insert(0, p);
                    if (b.Points.Count > PotaLook.TrailPoints) b.Points.RemoveAt(b.Points.Count - 1);
                }
                DrawTrail(b);
            }
        }

        private static Vector3 Local(Vector3 pota) => new Vector3(pota.x, pota.y, -(PotaLook.FrontZ + 0.2f + pota.z));   // cizToplar z

        /// <summary>cizIzler: width 0.34·max(0.3, scale)·(1 − age)^0.6, brightness 1.35·(1 − age)^0.8, #ffa040, izDokusu.</summary>
        private void DrawTrail(Ball b)
        {
            int n = b.Points.Count;
            if (n < 2) { b.Trail.positionCount = 0; return; }
            b.Trail.positionCount = n;
            for (int i = 0; i < n; i++) b.Trail.SetPosition(i, Local(b.Points[i]));
            b.Trail.widthMultiplier = PotaLook.TrailWidth * Mathf.Max(0.3f, b.Scale) * (Board != null ? Board.lossyScale.x : 1f);
        }

        private float R() => (float)_rng.NextDouble();

        private void Recycle(Ball b)
        {
            b.T.gameObject.SetActive(false);
            b.Trail.positionCount = 0;
            b.Trail.gameObject.SetActive(false);
            _pool.Push(b);
        }

        private Ball Create()
        {
            var parent = Board != null ? Board : transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            if (BallMaterial != null) r.sharedMaterial = BallMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(parent, false);
            var line = trailGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 1;
            line.sharedMaterial = TrailMaterial();
            var width = new AnimationCurve();
            var alphas = new GradientAlphaKey[8];
            for (int i = 0; i <= 10; i++) width.AddKey(i / 10f, Mathf.Pow(1f - i / 10f, 0.6f));
            for (int i = 0; i < 8; i++) alphas[i] = new GradientAlphaKey(Mathf.Pow(1f - i / 7f, 0.8f), i / 7f);
            line.widthCurve = width;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(PotaLook.TrailColor, 0f), new GradientColorKey(PotaLook.TrailColor, 1f) }, alphas);
            line.colorGradient = g;
            line.positionCount = 0;
            trailGo.SetActive(false);
            return new Ball { T = go.transform, Trail = line };
        }

        private Material TrailMaterial()
        {
            if (_trailMat != null) return _trailMat;
            _trailMat = RuntimeMaterial.From(S.AdditiveTemplate, "Project/PotaAdditive", "PotaTrail");
            _trailMat.SetTexture("_BaseMap", S.TrailSprite != null ? S.TrailSprite : Texture2D.whiteTexture);
            _trailMat.SetFloat("_Intensity", PotaLook.TrailBrightness);
            _trailMat.SetFloat("_SwapUV", 1f);
            return _trailMat;
        }
    }
}
