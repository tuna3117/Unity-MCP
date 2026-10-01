using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's swell-then-burst bookkeeping (renkAviFaz.ts patlamaIlerlet / tepeyiTetikle / gorselSil / sonenler): each entry swells
    /// linearly to its peak scale at 55 % of its time, then collapses; its burst runs exactly once — at the peak, or first thing when the
    /// entry is ended early (the step moves on) — and never after <see cref="Clear"/> (a new board). Loose entries (the tapped block,
    /// already out of its cell) report back when they finish so the caller can recycle them.
    /// </summary>
    public class SwellSet<T> where T : class
    {
        private class Entry { public float Time, Duration, Peak; public Action AtPeak; public bool Loose; }

        private readonly Dictionary<T, Entry> _entries = new Dictionary<T, Entry>();
        private readonly List<T> _keys = new List<T>();

        public int Count => _entries.Count;

        public bool AnyLoose
        {
            get
            {
                foreach (var e in _entries.Values) if (e.Loose) return true;
                return false;
            }
        }

        public bool Contains(T key) => _entries.ContainsKey(key);

        /// <summary>
        /// Starts (or restarts) an entry. <paramref name="elapsed"/>: seconds it has already run — a planned swell whose start fell between
        /// two frames (negative: it starts that much into the next advance).
        /// </summary>
        public void Start(T key, float duration, float peak, bool loose, Action atPeak, float elapsed = 0f) =>
            _entries[key] = new Entry { Time = elapsed, Duration = Mathf.Max(1e-4f, duration), Peak = peak, Loose = loose, AtPeak = atPeak };

        /// <summary>Advances every entry and hands (key, scale, o) to <paramref name="apply"/>; finished loose keys go to <paramref name="finishedLoose"/>.</summary>
        public void Advance(float dt, Action<T, float, float> apply, List<T> finishedLoose)
        {
            _keys.Clear();
            _keys.AddRange(_entries.Keys);
            foreach (var k in _keys)
            {
                if (!_entries.TryGetValue(k, out var e)) continue;
                e.Time += dt;
                float o = Mathf.Min(1f, e.Time / e.Duration);
                apply?.Invoke(k, PotaLook.Swell(o, e.Peak), o);
                if (o >= PotaLook.SwellPeakAt) Fire(e);
                if (o >= 1f && e.Loose)
                {
                    _entries.Remove(k);
                    finishedLoose?.Add(k);
                }
            }
        }

        /// <summary>gorselSil: the block leaves its cell; a burst still waiting runs first.</summary>
        public void End(T key)
        {
            if (!_entries.TryGetValue(key, out var e)) return;
            _entries.Remove(key);
            Fire(e);
        }

        /// <summary>A new board (kur): forget everything, run nothing.</summary>
        public void Clear() => _entries.Clear();

        private static void Fire(Entry e)
        {
            var f = e.AtPeak;
            if (f == null) return;
            e.AtPeak = null;
            f();
        }
    }
}
