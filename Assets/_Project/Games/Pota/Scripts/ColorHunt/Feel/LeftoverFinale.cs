using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// F3 leftover finale (spec 2026-10-03): at a win the remaining balls leave the ammo panel in 6–14 flights, each bursting a live
    /// block (Pota's burst) that throws a gold coin to a "+N" badge; the badge rolls up to Pota's coin formula
    /// (min(20, floor(remaining × 0.1))), the panel counts down per launch. Then the game disperses the board as today.
    /// Real time; ≤ Feel.FinaleMax.
    /// </summary>
    public class LeftoverFinale : MonoBehaviour
    {
        private class Ball { public Transform T; public LineRenderer Trail; public Vector3 From, To, Ctrl; public float Start, Time; public int Cell, Coins; public bool Done; public readonly List<Vector3> Points = new List<Vector3>(); }
        private class Coin { public Transform T; public Vector3 From, To, Ctrl; public float Start; public bool Done; }

        private const float BadgeInTime = 0.2f, StampTime = 0.25f, RollTime = 0.14f;

        private ColorHuntHud _hud;
        private ColorHuntGame _game;
        private RectTransform _badge;
        private Text _amount, _old;
        private RectMask2D _window;
        private Vector2 _amountHome;
        private Material _ballMat, _trailMat, _coinMat;
        private Mesh _coinMesh;
        private Transform _root;
        private readonly List<Ball> _balls = new List<Ball>();
        private readonly Stack<Ball> _ballPool = new Stack<Ball>();
        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<Coin> _coinPool = new Stack<Coin>();
        private readonly System.Random _rng = new System.Random(11);
        private Action _done;
        private float _t = -1f, _badgeIn = -1f, _stamp = -1f, _roll = -1f, _endAt;
        private int _coins0, _shown, _launched, _remaining;
        private int[] _plan, _coinPlan;
        private float _gap;

        public bool Running => _t >= 0f;
        public int CoinsShown => _shown;
        public int Flights => _plan != null ? _plan.Length : 0;
        public bool BadgeVisible => _badge != null && _badge.gameObject.activeSelf;
        /// <summary>Pool accounting (the review found balls pushed twice): created once, pooled exactly once per use.</summary>
        public int BallsCreated { get; private set; }
        public int BallsPooled => _ballPool.Count;
        public int CoinsCreated { get; private set; }
        public int CoinsPooled => _coinPool.Count;

        public void Init(ColorHuntHud hud, ColorHuntGame game)
        {
            _hud = hud;
            _game = game;
            float css = ColorHuntHud.Css;
            _badge = hud.NewCounter("CoinBadge", new Vector2(0.5f, 1f), new Vector2(118f, 56f));
            hud.MakeLabel(_badge, "Label", 11f, new Vector2(0.5f, 1f), new Vector2(0f, -14f * css), new Vector2(100f * css, 14f * css), new Color(1f, 1f, 1f, 0.85f), "PARA");
            _amount = hud.MakeLabel(_badge, "Amount", 26f, new Vector2(0.5f, 1f), new Vector2(0f, -36f * css), new Vector2(100f * css, 30f * css), PotaLook.HudGold, "+0");
            _window = RollWindow.Wrap(_amount, 12f);
            _amountHome = _amount.rectTransform.anchoredPosition;
            _old = Instantiate(_amount, _amount.transform.parent);
            _old.name = "AmountOld";
            _old.enabled = false;
            _badge.gameObject.SetActive(false);
            game.RoundStarted += Reset;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.RoundStarted -= Reset;
        }

        /// <summary>Starts the finale for <paramref name="remaining"/> balls; <paramref name="done"/> runs when the last coin has landed.</summary>
        public void Play(int remaining, Action done)
        {
            Reset();
            _done = done;
            _remaining = Mathf.Max(0, remaining);
            var feel = _game.S.Feel;
            _plan = FeelCurves.FinalePlan(_remaining, feel.FinaleMinFlights, feel.FinaleMaxFlights);
            _coins0 = FeelCurves.LeftoverCoins(_remaining);
            _coinPlan = _plan.Length > 0 ? FeelCurves.FinalePlan(_coins0, _plan.Length, _plan.Length) : new int[0];
            if (_coinPlan.Length < _plan.Length) { var padded = new int[_plan.Length]; Array.Copy(_coinPlan, padded, _coinPlan.Length); _coinPlan = padded; }
            if (_plan.Length == 0) { _t = -1f; done?.Invoke(); return; }
            // the whole show stays under FinaleMax: squeeze the gap if the plan is long
            float fixedPart = 0.15f + feel.FinaleBallTime + feel.FinaleCoinTime + StampTime;
            _gap = Mathf.Min(feel.FinaleGap, Mathf.Max(0.02f, (feel.FinaleMax - fixedPart) / Mathf.Max(1, _plan.Length - 1)));
            _t = 0f;
            _launched = 0;
            _shown = 0;
            _endAt = float.PositiveInfinity;
            _hud.PopAmmo();
            if (_coins0 > 0)
            {
                float css = ColorHuntHud.Css;
                float top = Mathf.Max(PotaLook.HudTopPadCss, ColorHuntCamera.SafeTopCss()) * css;
                _badge.anchoredPosition = new Vector2(0f, -top - 90f * css);
                _badge.localScale = Vector3.zero;
                _amount.text = "+0";
                _amount.rectTransform.anchoredPosition = _amountHome;
                _badge.gameObject.SetActive(true);
                _badgeIn = 0f;
            }
            EnsureAssets();
        }

        public void Reset()
        {
            _t = -1f; _badgeIn = -1f; _stamp = -1f; _roll = -1f;
            _plan = null; _coinPlan = null;
            foreach (var b in _balls) if (!b.Done) HideBall(b);     // done ones are in the pool already
            _balls.Clear();
            foreach (var c in _coins) if (!c.Done) HideCoin(c);
            _coins.Clear();
            if (_badge != null) _badge.gameObject.SetActive(false);
            if (_old != null) _old.enabled = false;
        }

        private void EnsureAssets()
        {
            var s = _game.S;
            if (_root == null)
            {
                _root = new GameObject("FinaleFx").transform;
                _root.SetParent(_game.BoardRoot != null ? _game.BoardRoot : transform, false);
            }
            if (_ballMat == null) _ballMat = s.SalvoBall != null ? s.SalvoBall : s.BallMaterial;
            if (_trailMat == null)
            {
                _trailMat = RuntimeMaterial.From(s.AdditiveTemplate, "Project/PotaAdditive", "FinaleTrail");
                _trailMat.SetTexture("_BaseMap", s.TrailSprite != null ? s.TrailSprite : Texture2D.whiteTexture);
                _trailMat.SetFloat("_Intensity", PotaLook.TrailBrightness);
                _trailMat.SetFloat("_SwapUV", 1f);
            }
            if (_coinMesh == null && s.RoyalCoin != null)
            {
                var parts = SpecialMeshes.Parts(s.RoyalCoin);
                if (parts.Count > 0) { _coinMesh = parts[0].Mesh; _coinMat = SpecialMeshes.Toon(s, s.RoyalCoin, "FinaleCoin", 0.006f); }
            }
        }

        // ---------- geometry ----------

        /// <summary>A canvas point (centre origin) projected onto the board's front plane, in the board root's local space.</summary>
        private Vector3 BoardPointOfCanvas(Vector2 canvas, float zOffset)
        {
            var cam = _game.Cam;
            var root = _game.BoardRoot;
            var rect = ((RectTransform)_hud.transform).rect;
            var vp = new Vector3(canvas.x / rect.width + 0.5f, canvas.y / rect.height + 0.5f, 0f);
            var ray = cam.ViewportPointToRay(vp);
            var plane = new Plane(-root.forward, root.TransformPoint(new Vector3(0f, 0f, -PotaLook.FrontZ - zOffset)));
            if (!plane.Raycast(ray, out float d)) d = 10f;
            return root.InverseTransformPoint(ray.GetPoint(d));
        }

        private Vector3 CellLocal(int cell)
        {
            var v = _game.ViewAt(cell);
            var p = v != null ? v.Pos : BoardBuilder.CellPosition(_game.Board.Size, ColorHuntRules.Row(_game.Board.Size, cell), ColorHuntRules.Col(_game.Board.Size, cell));
            return new Vector3(p.x, p.y, -PotaLook.FrontZ);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float q = 1f - u;
            return q * q * a + 2f * q * u * c + u * u * b;
        }

        // ---------- update ----------

        private void Update()
        {
            if (_t < 0f || _game == null) return;
            if (!_game.Feel) { Finish(); if (_badge != null) _badge.gameObject.SetActive(false); return; }
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            var feel = _game.S.Feel;
            // launches
            while (_launched < _plan.Length && _t >= 0.15f + _launched * _gap) Launch(_launched++);
            // balls
            for (int i = 0; i < _balls.Count; i++)
            {
                var b = _balls[i];
                if (b.Done) continue;
                float u = Mathf.Clamp01((_t - b.Start) / b.Time);
                var p = Bezier(b.From, b.Ctrl, b.To, u * u * (3f - 2f * u));
                b.T.localPosition = p;
                b.T.gameObject.SetActive(true);
                b.Points.Add(p);
                if (b.Points.Count > PotaLook.TrailPoints) b.Points.RemoveAt(0);
                b.Trail.positionCount = b.Points.Count;
                for (int k = 0; k < b.Points.Count; k++) b.Trail.SetPosition(k, b.Points[b.Points.Count - 1 - k]);
                if (u >= 1f) Hit(b);
            }
            // coins
            for (int i = 0; i < _coins.Count; i++)
            {
                var c = _coins[i];
                if (c.Done) continue;
                float u = Mathf.Clamp01((_t - c.Start) / feel.FinaleCoinTime);
                if (u <= 0f) continue;
                c.T.gameObject.SetActive(true);
                c.T.localPosition = Bezier(c.From, c.Ctrl, c.To, u * u);
                c.T.localRotation = Quaternion.Euler(0f, u * 540f, 0f);
                c.T.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, u);
                if (u >= 1f) Land(c);
            }
            // badge
            if (_badgeIn >= 0f)
            {
                _badgeIn += dt;
                _badge.localScale = Vector3.one * FeelCurves.RibbonScale(_badgeIn / BadgeInTime);
                if (_badgeIn >= BadgeInTime) _badgeIn = -1f;
            }
            if (_roll >= 0f)
            {
                _roll += dt / RollTime;
                float u = Mathf.Min(1f, _roll);
                var r = StudioCurves.CounterRoll(u);
                float h = _amount.rectTransform.rect.height;
                _amount.rectTransform.anchoredPosition = _amountHome + new Vector2(0f, r.y * h);
                _old.rectTransform.anchoredPosition = _amountHome + new Vector2(0f, r.x * h);
                var oc = _old.color; oc.a = 1f - u; _old.color = oc;
                if (_roll >= 1f) { _roll = -1f; _old.enabled = false; _amount.rectTransform.anchoredPosition = _amountHome; }
            }
            if (_stamp >= 0f)
            {
                _stamp += dt;
                _badge.localScale = Vector3.one * FeelCurves.Stamp(_stamp / StampTime, 1.3f, 1f);
                if (_stamp >= StampTime) _stamp = -1f;
            }
            // end: every ball hit and every coin landed
            bool busy = _launched < _plan.Length;
            foreach (var b in _balls) busy |= !b.Done;
            foreach (var c in _coins) busy |= !c.Done;
            if (!busy && _endAt == float.PositiveInfinity) _endAt = _t + (_coins0 > 0 ? StampTime : 0.05f);
            if (_t >= _endAt) Finish();
        }

        private void Launch(int i)
        {
            int cell = _game.RandomLiveCell(_rng);
            var b = _ballPool.Count > 0 ? _ballPool.Pop() : NewBall();
            b.From = BoardPointOfCanvas(_hud.CanvasPointOf(_hud.AmmoPanel), 0.6f);
            b.To = cell >= 0 ? CellLocal(cell) : Vector3.zero;
            var mid = (b.From + b.To) * 0.5f;
            b.Ctrl = mid + new Vector3((float)(_rng.NextDouble() - 0.5) * 2.5f, 0.8f, -1.2f);
            b.Start = _t;
            b.Time = _game.S.Feel.FinaleBallTime;
            b.Cell = cell;
            b.Coins = i < _coinPlan.Length ? _coinPlan[i] : 0;
            b.Done = false;
            b.Points.Clear();
            b.Trail.positionCount = 0;
            b.T.localScale = Vector3.one * 0.38f;
            b.Trail.widthMultiplier = PotaLook.TrailWidth * (_game.BoardRoot != null ? _game.BoardRoot.lossyScale.x : 1f);
            b.Trail.gameObject.SetActive(true);
            _balls.Add(b);
            _remaining -= _plan[i];
            // the counter rolls 0.18 s per step: a new value every launch (0.08 s) stacked two rolls on top of each other in the
            // 2026-10-03 recording, so the number only drops once the last roll has landed (and snaps to 0 at the end, in Finish)
            if (_hud.AmmoCounter == null || !_hud.AmmoCounter.Rolling) { _game.ShowAmmo(Mathf.Max(0, _remaining)); _hud.PopAmmo(); }
        }

        private void Hit(Ball b)
        {
            b.Done = true;
            HideBall(b);
            if (b.Cell >= 0) _game.BurstCell(b.Cell);
            for (int k = 0; k < b.Coins; k++)
            {
                var c = _coinPool.Count > 0 ? _coinPool.Pop() : NewCoin();
                if (c == null) break;
                c.From = b.To + new Vector3(0f, 0f, -0.3f);
                c.To = BoardPointOfCanvas(_hud.CanvasPointOf(_badge), 1.4f);
                var mid = (c.From + c.To) * 0.5f;
                c.Ctrl = mid + new Vector3((float)(_rng.NextDouble() - 0.5) * 2f, 1.2f + 0.4f * k, -0.8f);
                c.Start = _t + k * 0.05f;
                c.Done = false;
                c.T.localPosition = c.From;
                _coins.Add(c);
            }
        }

        private void Land(Coin c)
        {
            c.Done = true;
            HideCoin(c);
            if (_shown >= _coins0) return;
            _old.text = _amount.text;
            _old.enabled = true;
            _shown++;
            _amount.text = "+" + _shown;
            _roll = 0f;
            if (_shown == _coins0) _stamp = 0f;
        }

        private void Finish()
        {
            var done = _done;
            _done = null;
            _t = -1f;
            foreach (var b in _balls) if (!b.Done) HideBall(b);
            _balls.Clear();
            foreach (var c in _coins) if (!c.Done) HideCoin(c);
            _coins.Clear();
            if (_badge != null && _coins0 > 0) { _amount.text = "+" + _coins0; _badge.localScale = Vector3.one; }
            if (_game != null && _game.DisplayedAmmo != 0 && _plan != null && _plan.Length > 0) _game.ShowAmmo(0);
            done?.Invoke();
        }

        // ---------- pools ----------

        private Ball NewBall()
        {
            BallsCreated++;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FinaleBall";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);
            var r = go.GetComponent<MeshRenderer>();
            if (_ballMat != null) r.sharedMaterial = _ballMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(_root, false);
            var line = trailGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 1;
            line.sharedMaterial = _trailMat;
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

        private void HideBall(Ball b)
        {
            b.T.gameObject.SetActive(false);
            b.Trail.gameObject.SetActive(false);
            _ballPool.Push(b);
        }

        private Coin NewCoin()
        {
            if (_coinMesh == null) return null;
            CoinsCreated++;
            var go = new GameObject("FinaleCoin", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_root, false);
            go.GetComponent<MeshFilter>().sharedMesh = _coinMesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _coinMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return new Coin { T = go.transform };
        }

        private void HideCoin(Coin c)
        {
            c.T.gameObject.SetActive(false);
            _coinPool.Push(c);
        }
    }
}
