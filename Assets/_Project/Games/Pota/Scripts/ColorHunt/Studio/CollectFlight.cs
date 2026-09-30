using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// The studio sticker flight (every look but Pota, spec §3.1). The sticker launches from the burst (0.9 → 1.4 → 1.2, a ±12° wobble,
    /// a short hang), flies an accelerating curve to its target icon trailing sparkles; departures leave 0.05 s apart so a chain reads as
    /// a stream fanning left and right. On arrival the icon squashes, a star burst and a ring pop, a rising "topla" plays and the game
    /// lowers the count; the count rolls (old number up and out, new one in, white → gold); at zero the ✓ is stamped and the panel glows
    /// green. Real time: hit-stops do not freeze the HUD. A new round drops everything without arrivals (the game's flight generation
    /// already ignores late callbacks).
    /// </summary>
    public class CollectFlight : MonoBehaviour
    {
        private class Flyer
        {
            public RectTransform T;
            public Image Img;
            public Vector2 From, To;
            public float Side, Start, NextSpark;
            public int Color;
            public Action Done;
        }

        private class Spark
        {
            public RectTransform T;
            public Image Img;
            public Vector2 V;
            public float Age, Life, Spin;
        }

        private class RowFx
        {
            public float Squash = -1f, Ring = -1f, Roll = -1f, Stamp = -1f;
            public Image RingImg;
            public Text Old;
            public Vector2 CountHome;
            public Color Final;
        }

        private const int SparkMax = 64;
        private ColorHuntHud _hud;
        private readonly List<Flyer> _flying = new List<Flyer>();
        private readonly Stack<Flyer> _flyerPool = new Stack<Flyer>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly Stack<Spark> _sparkPool = new Stack<Spark>();
        private readonly Dictionary<int, RowFx> _rows = new Dictionary<int, RowFx>();
        private Image _glow;
        private float _glowT = -1f;
        private float _lastDeparture = -10f, _lastArrival = -10f;
        private int _stream, _streak;
        private Sprite _star, _ring;

        public int Flying => _flying.Count;
        public int Arrived { get; private set; }
        public int LastStreak => _streak;
        public IEnumerable<float> Departures { get { foreach (var f in _flying) yield return f.Start; } }

        public bool Animating(int color) =>
            _rows.TryGetValue(color, out var fx) && (fx.Squash >= 0f || fx.Roll >= 0f || fx.Stamp >= 0f);

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var s = hud.S;
            if (s != null && s.StarSprite != null) _star = Sprite.Create(s.StarSprite, new Rect(0, 0, s.StarSprite.width, s.StarSprite.height), new Vector2(0.5f, 0.5f));
            if (s != null && s.SoftRing != null) _ring = Sprite.Create(s.SoftRing, new Rect(0, 0, s.SoftRing.width, s.SoftRing.height), new Vector2(0.5f, 0.5f));
        }

        public bool Fly(int color, Vector3 world, Action onArrive)
        {
            if (_hud == null || !_hud.TryTarget(color, out var icon, out _, out _) || icon.sprite == null) return false;
            if (!_hud.CanvasPoint(world, out var from)) return false;
            float now = Time.unscaledTime;
            if (now - _lastDeparture > StudioCurves.StreamGap) _stream = 0;
            float start = Mathf.Max(now, _lastDeparture + StudioCurves.StaggerTime);
            _lastDeparture = start;
            var f = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewFlyer();
            f.From = from;
            f.To = _hud.CanvasPointOf(icon.rectTransform);
            // the bow flips (or straightens) rather than leave the screen
            Vector2 lifted = from + new Vector2(0f, StudioCurves.LaunchLiftCss * ColorHuntHud.Css);
            f.Side = StudioCurves.FitSide(lifted, f.To, StudioCurves.Side(_stream++), ((RectTransform)_hud.transform).rect.size * 0.5f, 60f * ColorHuntHud.Css);
            f.Start = start;
            f.NextSpark = 0f;
            f.Color = color;
            f.Done = onArrive;
            f.Img.sprite = icon.sprite;
            f.T.anchoredPosition = from;
            f.T.localScale = Vector3.one * StudioCurves.LaunchStart;
            f.T.localRotation = Quaternion.identity;
            f.T.gameObject.SetActive(false);
            _flying.Add(f);
            return true;
        }

        /// <summary>The HUD lowered a target's count from <paramref name="old"/>: roll it; <paramref name="done"/> = it reached zero.</summary>
        public void CountChanged(int color, string old, bool done)
        {
            var fx = Row(color);
            if (fx == null) return;
            fx.Old.text = old;
            fx.Old.enabled = true;
            fx.Final = done ? PotaLook.HudGreen : PotaLook.HudGold;
            fx.Roll = 0f;
        }

        public void StampTick(int color)
        {
            var fx = Row(color);
            if (fx == null || !_hud.TryTarget(color, out _, out _, out var tick)) return;
            fx.Stamp = 0f;
            _glowT = 0f;
            Vector2 at = _hud.CanvasPointOf(tick.rectTransform);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f;
                EmitSpark(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (220f * ColorHuntHud.Css), 0.4f, 30f);
            }
            Sounds()?.TargetDone();
        }

        public void ClearAll()
        {
            foreach (var f in _flying) { f.T.gameObject.SetActive(false); _flyerPool.Push(f); }
            _flying.Clear();
            foreach (var s in _sparks) { s.T.gameObject.SetActive(false); _sparkPool.Push(s); }
            _sparks.Clear();
            _rows.Clear();                 // their texts and rings are children of the rows BuildTargets destroys
            _glowT = -1f;
            if (_glow != null) _glow.enabled = false;
            _stream = 0;
        }

        private void Update()
        {
            if (_hud == null) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                float t = now - f.Start;
                if (t < 0f) continue;
                f.T.gameObject.SetActive(true);
                Vector2 lifted = f.From + new Vector2(0f, StudioCurves.LaunchLiftCss * ColorHuntHud.Css);
                if (t < StudioCurves.LaunchTime)
                {
                    float u = t / StudioCurves.LaunchTime;
                    f.T.anchoredPosition = Vector2.Lerp(f.From, lifted, StudioCurves.EaseOutCubic(u));
                    f.T.localScale = Vector3.one * StudioCurves.LaunchScale(u);
                    f.T.localRotation = Quaternion.Euler(0f, 0f, StudioCurves.LaunchWobble(u));
                    continue;
                }
                float v = (t - StudioCurves.LaunchTime - StudioCurves.HangTime) / StudioCurves.TravelTime;
                if (v < 0f)
                {
                    f.T.anchoredPosition = lifted;
                    f.T.localScale = Vector3.one * StudioCurves.LaunchEnd;
                    f.T.localRotation = Quaternion.identity;
                    continue;
                }
                if (v >= 1f)
                {
                    _flying.RemoveAt(i);
                    Arrive(f);
                    continue;
                }
                var p = StudioCurves.FlightPoint(lifted, f.To, f.Side, v);
                f.T.anchoredPosition = p;
                f.T.localScale = Vector3.one * StudioCurves.FlightScale(v);
                f.T.localRotation = Quaternion.Euler(0f, 0f, StudioCurves.FlightTilt(f.Side, v));
                if (now >= f.NextSpark)
                {
                    f.NextSpark = now + StudioCurves.SparkEvery;
                    EmitSpark(p, UnityEngine.Random.insideUnitCircle * (40f * ColorHuntHud.Css), 0.25f, 20f);
                }
            }
            TickSparks(dt);
            TickRows(dt);
        }

        private void Arrive(Flyer f)
        {
            f.T.gameObject.SetActive(false);
            _flyerPool.Push(f);
            float now = Time.unscaledTime;
            _streak = now - _lastArrival < StudioCurves.StreakWindow ? _streak + 1 : 0;
            _lastArrival = now;
            Arrived++;
            var fx = Row(f.Color);
            if (fx != null) { fx.Squash = 0f; fx.Ring = 0f; }
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.3f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // stars start at the icon's rim so they burst out of it, not over it
                EmitSpark(f.To + dir * (20f * ColorHuntHud.Css), dir * (180f * ColorHuntHud.Css), 0.3f, 26f);
            }
            Sounds()?.Collect(_streak);
            f.Done?.Invoke();
        }

        private void TickRows(float dt)
        {
            foreach (var kv in _rows)
            {
                if (!_hud.TryTarget(kv.Key, out var icon, out var count, out var tick)) continue;
                var fx = kv.Value;
                if (fx.Squash >= 0f)
                {
                    fx.Squash += dt / StudioCurves.SquashTime;
                    var s = StudioCurves.ArrivalSquash(fx.Squash);
                    icon.rectTransform.localScale = new Vector3(s.x, s.y, 1f);
                    if (fx.Squash >= 1f) { fx.Squash = -1f; icon.rectTransform.localScale = Vector3.one; }
                }
                if (fx.Ring >= 0f && fx.RingImg != null)
                {
                    fx.Ring += dt / StudioCurves.RingTime;
                    fx.RingImg.enabled = fx.Ring < 1f;
                    fx.RingImg.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition;
                    fx.RingImg.rectTransform.localScale = Vector3.one * StudioCurves.RingScale(fx.Ring);
                    fx.RingImg.color = new Color(1f, 0.93f, 0.6f, StudioCurves.RingAlpha(fx.Ring));
                    if (fx.Ring >= 1f) fx.Ring = -1f;
                }
                if (fx.Roll >= 0f)
                {
                    fx.Roll += dt / StudioCurves.RollTime;
                    float u = Mathf.Min(1f, fx.Roll);
                    var r = StudioCurves.CounterRoll(u);
                    // 0.6 of the text height keeps the rolling numbers inside the panel
                    float h = count.rectTransform.rect.height * 0.6f;
                    count.rectTransform.anchoredPosition = fx.CountHome + new Vector2(0f, r.y * h);
                    fx.Old.rectTransform.anchoredPosition = fx.CountHome + new Vector2(0f, r.x * h);
                    fx.Old.color = new Color(PotaLook.HudGold.r, PotaLook.HudGold.g, PotaLook.HudGold.b, 1f - u);
                    count.color = Color.Lerp(fx.Final, Color.white, StudioCurves.CounterFlash(u));
                    if (fx.Roll >= 1f)
                    {
                        fx.Roll = -1f;
                        fx.Old.enabled = false;
                        count.rectTransform.anchoredPosition = fx.CountHome;
                        count.color = fx.Final;
                    }
                }
                if (fx.Stamp >= 0f)
                {
                    fx.Stamp += dt / StudioCurves.StampTime;
                    float u = Mathf.Min(1f, fx.Stamp);
                    tick.rectTransform.localScale = Vector3.one * StudioCurves.Stamp(u);
                    var c = tick.color;
                    c.a = StudioCurves.StampAlpha(u);
                    tick.color = c;
                    if (fx.Stamp >= 1f) { fx.Stamp = -1f; tick.rectTransform.localScale = Vector3.one; }
                }
            }
            if (_glowT >= 0f && _glow != null)
            {
                _glowT += dt / StudioCurves.GlowTime;
                _glow.enabled = _glowT < 1f;
                _glow.color = new Color(PotaLook.HudGreen.r, PotaLook.HudGreen.g, PotaLook.HudGreen.b, StudioCurves.GlowAlpha(_glowT));
                if (_glowT >= 1f) _glowT = -1f;
            }
        }

        private void TickSparks(float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                float u = s.Age / s.Life;
                if (u >= 1f) { s.T.gameObject.SetActive(false); _sparks.RemoveAt(i); _sparkPool.Push(s); continue; }
                s.T.anchoredPosition += s.V * dt;
                s.V *= Mathf.Max(0f, 1f - 3f * dt);
                s.T.localScale = Vector3.one * (1f - u);
                s.T.localRotation = Quaternion.Euler(0f, 0f, s.Spin * s.Age);
                s.Img.color = new Color(1f, 0.95f, 0.7f, 1f - u);
            }
        }

        private RowFx Row(int color)
        {
            if (_rows.TryGetValue(color, out var fx)) return fx;
            if (_hud == null || !_hud.TryTarget(color, out var icon, out var count, out _)) return null;
            fx = new RowFx { CountHome = count.rectTransform.anchoredPosition, Final = PotaLook.HudGold };
            fx.Old = Instantiate(count, count.transform.parent);
            fx.Old.name = "CountOld";
            fx.Old.enabled = false;
            if (_ring != null)
            {
                var go = new GameObject("ArrivalRing", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(icon.transform.parent, false);
                fx.RingImg = go.GetComponent<Image>();
                fx.RingImg.sprite = _ring;
                fx.RingImg.raycastTarget = false;
                fx.RingImg.enabled = false;
                var rt = fx.RingImg.rectTransform;
                rt.anchorMin = rt.anchorMax = icon.rectTransform.anchorMin;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = icon.rectTransform.sizeDelta * 1.6f;
            }
            if (_glow == null && _hud.TargetPanel != null)
            {
                var go = new GameObject("TargetGlow", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_hud.TargetPanel, false);
                _glow = go.GetComponent<Image>();
                _glow.sprite = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(18f * ColorHuntHud.Css), Color.clear, Color.clear,
                    Color.white, Mathf.RoundToInt(5f * ColorHuntHud.Css), 0f, 0);
                _glow.type = _glow.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                _glow.raycastTarget = false;
                _glow.enabled = false;
                var rt = _glow.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.one * (-6f * ColorHuntHud.Css);
                rt.offsetMax = Vector2.one * (6f * ColorHuntHud.Css);
            }
            _rows[color] = fx;
            return fx;
        }

        private Flyer NewFlyer()
        {
            var go = new GameObject("StudioFlyer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_hud.FlyersRoot, false);
            var f = new Flyer { T = (RectTransform)go.transform, Img = go.GetComponent<Image>() };
            f.T.anchorMin = f.T.anchorMax = new Vector2(0.5f, 0.5f);
            f.T.sizeDelta = Vector2.one * (56f * ColorHuntHud.Css);
            f.Img.preserveAspect = true;
            f.Img.raycastTarget = false;
            return f;
        }

        private void EmitSpark(Vector2 at, Vector2 velocity, float life, float sizeCss)
        {
            if (_star == null || _sparks.Count >= SparkMax) return;
            Spark s;
            if (_sparkPool.Count > 0) s = _sparkPool.Pop();
            else
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_hud.FlyersRoot, false);
                s = new Spark { T = (RectTransform)go.transform, Img = go.GetComponent<Image>() };
                s.T.anchorMin = s.T.anchorMax = new Vector2(0.5f, 0.5f);
                s.Img.sprite = _star;
                s.Img.raycastTarget = false;
            }
            s.T.anchoredPosition = at;
            s.T.sizeDelta = Vector2.one * (sizeCss * ColorHuntHud.Css);
            s.V = velocity;
            s.Age = 0f;
            s.Life = life;
            s.Spin = UnityEngine.Random.Range(-360f, 360f);
            s.T.gameObject.SetActive(true);
            _sparks.Add(s);
        }

        private PotaSounds Sounds() => _hud.Game != null && _hud.Game.Juice != null ? _hud.Game.Juice.Sounds : null;
    }
}
