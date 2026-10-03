using System;
using System.Collections.Generic;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>How many coin sprites a gain or spend sends (M3): 8 up to 12, never the amount itself; 0 for nothing.</summary>
    public static class CoinPlan
    {
        public static int Count(int amount) => amount <= 0 ? 0 : Mathf.Clamp(amount / 5, 8, 12);
    }

    /// <summary>
    /// M3: coins burst from a source point (fan, CoinBurst), then fly one after another (CoinStagger) along a bow to a target
    /// (CoinFlight); every arrival calls <c>onEach</c> (the counter steps), the last one <c>onDone</c>. Canvas space of the HUD root;
    /// pooled; real time. Feel off = no flight, <c>onEach</c> fires count times at once and <c>onDone</c> follows.
    /// </summary>
    public class CoinFlight : MonoBehaviour
    {
        private class Coin { public RectTransform T; public Vector2 From, Burst, To; public float Side, Start; public bool Flying, Done; }

        private MetaFeelTuning _feel;
        private Sprite _sprite;
        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<Coin> _pool = new Stack<Coin>();
        private float _t = -1f;
        private Action _onEach, _onDone;
        private int _arrived, _count;

        public bool Running => _t >= 0f;
        public int Flying => _coins.Count;

        public static CoinFlight Create(Transform canvasRoot, Sprite coin, MetaFeelTuning feel)
        {
            var go = new GameObject("CoinFlight", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var f = go.AddComponent<CoinFlight>();
            f._feel = feel;
            f._sprite = coin;
            return f;
        }

        /// <summary>Sends CoinPlan.Count(amount) coins from <paramref name="from"/> to <paramref name="to"/> (canvas points, centre origin).</summary>
        public void Fly(Vector2 from, Vector2 to, int amount, Action onEach, Action onDone)
        {
            Clear();
            _count = CoinPlan.Count(amount);
            _onEach = onEach; _onDone = onDone;
            _arrived = 0;
            if (_count == 0 || _feel == null || !_feel.Enabled)
            {
                for (int i = 0; i < _count; i++) onEach?.Invoke();
                onDone?.Invoke();
                return;
            }
            transform.SetAsLastSibling();
            _t = 0f;
            float css = MetaStyle.Css;
            for (int i = 0; i < _count; i++)
            {
                var c = _pool.Count > 0 ? _pool.Pop() : New();
                float a = Mathf.Lerp(-70f, 70f, (i + 0.5f) / _count) * Mathf.Deg2Rad;
                float r = (36f + 14f * ((i * 7) % 3)) * css;
                c.From = from;
                c.Burst = from + new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r * 0.8f + 10f * css);
                c.To = to;
                c.Side = StudioCurves.Side(i) * 0.5f;
                c.Start = _feel.CoinBurst + i * _feel.CoinStagger;
                c.Flying = c.Done = false;
                c.T.anchoredPosition = from;
                c.T.localScale = Vector3.one * 0.6f;
                c.T.gameObject.SetActive(true);
                _coins.Add(c);
            }
        }

        public void Clear()
        {
            foreach (var c in _coins) Hide(c);
            _coins.Clear();
            _t = -1f;
        }

        private Coin New()
        {
            var go = new GameObject("Coin", typeof(RectTransform), typeof(Image), typeof(Shadow));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (26f * MetaStyle.Css);
            var img = go.GetComponent<Image>();
            img.sprite = _sprite; img.preserveAspect = true; img.raycastTarget = false;
            var sh = go.GetComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(0f, -2f * MetaStyle.Css);
            return new Coin { T = rt };
        }

        private void Hide(Coin c) { c.T.gameObject.SetActive(false); _pool.Push(c); }

        private void Update()
        {
            if (_t < 0f) return;
            if (!_feel.Enabled) { int left = _count - _arrived; Clear(); for (int i = 0; i < left; i++) _onEach?.Invoke(); _onDone?.Invoke(); return; }
            _t += Time.unscaledDeltaTime;
            float burst = Mathf.Max(0.01f, _feel.CoinBurst), flight = Mathf.Max(0.01f, _feel.CoinFlight);
            bool busy = false;
            foreach (var c in _coins)
            {
                if (c.Done) continue;
                busy = true;
                if (_t < c.Start)
                {
                    float ub = Mathf.Clamp01(_t / burst);
                    c.T.anchoredPosition = Vector2.Lerp(c.From, c.Burst, StudioCurves.EaseOutCubic(ub));
                    c.T.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, ub);
                    continue;
                }
                float u = Mathf.Clamp01((_t - c.Start) / flight);
                c.T.anchoredPosition = StudioCurves.FlightPoint(c.Burst, c.To, c.Side, u);
                c.T.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, u);
                c.T.localRotation = Quaternion.Euler(0f, 0f, u * 360f);
                if (u >= 1f) { c.Done = true; Hide(c); _arrived++; _onEach?.Invoke(); }
            }
            if (!busy)
            {
                _coins.Clear();
                _t = -1f;
                _onDone?.Invoke();
            }
        }
    }
}
