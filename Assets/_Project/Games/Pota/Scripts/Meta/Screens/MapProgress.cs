using Project.ColorHunt;
using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// M6, pure: the map after a win, second by second. The scroll centres the passed node (MapScroll), 0.3 s later it turns green
    /// (NodeGreen, 1.15 overshoot) and gets its medal (MedalStamp, 1.6 → 1); the path's dots light one by one (DotStep) while the
    /// "Sen" pin hops three times to the new node (PinHop, squash on landing); the new node unlocks (lock shrinks 0.15 s, the number
    /// pops 0 → 1.3 → 1 over UnlockPop, a ring and eight stars) as the scroll centres it (0.4 s). Feel off = everything done at t = 0.
    /// </summary>
    public class MapProgress
    {
        public struct Frame
        {
            public float Scroll;          // 0 = a node below the previous, 1 = the previous node centred, 2 = the new node centred
            public float Green, GreenScale, Medal;
            public int DotsLit;
            public float Pin, PinHeight, PinSquash;
            public float Lock, Number, Ring, Stars;
            public bool Unlocked;
        }

        public const float Settle = 0.3f, ScrollBack = 0.4f, LockShrink = 0.15f, NumberDelay = 0.1f, Squash = 0.15f, HopHeight = 14f;

        public readonly MetaFeelTuning Feel;
        public readonly int Dots;

        public MapProgress(MetaFeelTuning feel, int dots) { Feel = feel; Dots = dots; }

        public float ScrollEnd => Feel.T(Feel.MapScroll);
        public float GreenAt => ScrollEnd + Feel.T(Settle);
        public float DotsAt => GreenAt + Feel.T(Feel.NodeGreen);
        public float PinAt => DotsAt;
        public float UnlockAt => PinAt + Feel.T(Feel.PinHop);
        public float Done => UnlockAt + Feel.T(Mathf.Max(ScrollBack, NumberDelay + Feel.UnlockPop));

        public Frame Stage(float t) => Feel.Enabled ? Timed(t) : End();

        private Frame End() => new Frame
        {
            Scroll = 2f, Green = 1f, GreenScale = 1f, Medal = 1f, DotsLit = Dots, Pin = 1f, PinHeight = 0f, PinSquash = 1f,
            Lock = 0f, Number = 1f, Ring = 0f, Stars = 0f, Unlocked = true,
        };

        private Frame Timed(float t)
        {
            var f = new Frame();
            float u1 = StudioCurves.EaseOutCubic(Mathf.Clamp01(t / Mathf.Max(0.01f, Feel.MapScroll)));
            float u2 = PotaLook.Smoothstep(Mathf.Clamp01((t - UnlockAt) / ScrollBack));
            f.Scroll = t < UnlockAt ? u1 : 1f + u2;
            float ug = Mathf.Clamp01((t - GreenAt) / Mathf.Max(0.01f, Feel.NodeGreen));
            f.Green = t < GreenAt ? 0f : ug;
            f.GreenScale = 1f + 0.15f * Mathf.Sin(f.Green * Mathf.PI);
            float um = Mathf.Clamp01((t - GreenAt) / Mathf.Max(0.01f, Feel.MedalStamp));
            f.Medal = t < GreenAt ? 0f : Mathf.Lerp(1.6f, 1f, StudioCurves.EaseOutCubic(um));
            f.DotsLit = t < DotsAt ? 0 : Mathf.Min(Dots, Mathf.FloorToInt((t - DotsAt) / Mathf.Max(0.001f, Feel.DotStep)) + 1);
            float up = Mathf.Clamp01((t - PinAt) / Mathf.Max(0.01f, Feel.PinHop));
            f.Pin = t < PinAt ? 0f : up;
            f.PinHeight = t < PinAt || up >= 1f ? 0f : Mathf.Abs(Mathf.Sin(up * 3f * Mathf.PI)) * HopHeight;
            float landed = t - (PinAt + Feel.PinHop);
            f.PinSquash = landed < 0f ? 1f : landed < Squash ? Mathf.Lerp(0.9f, 1f, landed / Squash) : 1f;
            f.Lock = t < UnlockAt ? 1f : 1f - Mathf.Clamp01((t - UnlockAt) / LockShrink);
            float un = Mathf.Clamp01((t - UnlockAt - NumberDelay) / Mathf.Max(0.01f, Feel.UnlockPop));
            f.Number = t < UnlockAt + NumberDelay ? 0f : un < 0.4f ? Mathf.Lerp(0f, 1.3f, StudioCurves.EaseOutCubic(un / 0.4f)) : Mathf.Lerp(1.3f, 1f, PotaLook.Smoothstep((un - 0.4f) / 0.6f));
            float ur = (t - UnlockAt) / ScrollBack;
            f.Ring = t < UnlockAt || ur >= 1f ? 0f : ur;
            f.Stars = f.Ring;
            f.Unlocked = t >= UnlockAt;
            return f;
        }
    }
}
