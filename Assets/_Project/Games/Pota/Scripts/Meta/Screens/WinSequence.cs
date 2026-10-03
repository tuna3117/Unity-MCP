using Project.ColorHunt;
using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// M4, pure: what the win window shows at second <c>t</c> after it opened — the "Harika!" stamp, the score count-up (cubic
    /// ease-out, a 1.06 hop every twelfth), the medal escalating as the count crosses the silver and gold thresholds (never back),
    /// the trophy card rolling, the league bar filling from the old value to the new (to the brim on a promotion), the coins' moment
    /// and the continue button's pulse. Feel off = Pota's window: everything at its end state at t = 0, no pulse.
    /// </summary>
    public class WinSequence
    {
        public struct Frame
        {
            public float Stamp;            // "Harika!" scale: 0 hidden → 1.3 → 1
            public int Score;              // the counter
            public float ScoreScale;       // 1.06 hops during the count-up
            public int Medal;              // -1 hidden, 0 bronze, 1 silver, 2 gold
            public float LinesAlpha;       // the score lines row
            public int TrophyGainShown;    // "Kupa +N" rolling 0 → gain
            public float Bar;              // league bar fill 0..1
            public int TrophiesShown;      // the bar's trophies (for "Sonraki lige N kupa")
            public bool BarOverflow;       // the bar hit the brim on a promotion
            public bool CoinsFlying;       // the "+15" coins have left
            public float RewardAlpha;      // the "N top arttı · ..." card
            public bool ContinuePulse;     // Devam breathes
        }

        public readonly MetaFeelTuning Feel;
        public readonly int Score, Silver, Gold, TrophiesBefore, TrophiesAfter, CoinsGained, FinalMedal;
        public readonly float BarBefore, BarAfter;
        public readonly bool Promoted;

        public const float StampTime = 0.3f, LinesAfterScore = 0.45f, RewardAfterCoins = 0.2f, LinesFade = 0.25f;

        public WinSequence(MetaSettings s, int score, int silver, int gold, int trophiesBefore, int trophiesAfter, int coins)
        {
            Feel = s.Feel;
            Score = score; Silver = silver; Gold = gold;
            TrophiesBefore = trophiesBefore; TrophiesAfter = trophiesAfter; CoinsGained = coins;
            FinalMedal = MetaRules.Medal(score, silver, gold);
            BarBefore = MetaRules.BarFill(s, trophiesBefore);
            BarAfter = MetaRules.BarFill(s, trophiesAfter);
            Promoted = s.LeagueOf(trophiesAfter) > s.LeagueOf(trophiesBefore);
        }

        /// <summary>The count-up's fraction 0..1 at <paramref name="t"/>.</summary>
        public float CountFraction(float t)
        {
            float time = Mathf.Max(0.01f, Feel.ScoreTime);
            return StudioCurves.EaseOutCubic(Mathf.Clamp01((t - Feel.ScoreAt) / time));
        }

        /// <summary>The second the medal flips to <paramref name="medal"/> (0 bronze = when the count starts); −1 when the score never reaches it.</summary>
        public float MedalFlipAt(int medal)
        {
            if (medal <= 0) return Feel.ScoreAt;
            int threshold = medal == 1 ? Silver : Gold;
            if (Score < threshold || Score <= 0) return -1f;
            float fraction = (float)threshold / Score;
            float u = 1f - Mathf.Pow(1f - fraction, 1f / 3f);           // EaseOutCubic inverted
            return Feel.ScoreAt + u * Mathf.Max(0.01f, Feel.ScoreTime);
        }

        public Frame Stage(float t) => Feel.Enabled ? Timed(t) : End();

        private Frame End() => new Frame
        {
            Stamp = 1f, Score = Score, ScoreScale = 1f, Medal = FinalMedal, LinesAlpha = 1f, TrophyGainShown = TrophiesAfter - TrophiesBefore,
            Bar = BarAfter, TrophiesShown = TrophiesAfter, BarOverflow = false, CoinsFlying = true, RewardAlpha = 1f, ContinuePulse = false,
        };

        private Frame Timed(float t)
        {
            var st = new Frame();
            // stamp
            float us = Mathf.Clamp01((t - Feel.StampAt) / StampTime);
            st.Stamp = t < Feel.StampAt ? 0f : us < 0.4f ? Mathf.Lerp(0f, 1.3f, StudioCurves.EaseOutCubic(us / 0.4f)) : Mathf.Lerp(1.3f, 1f, PotaLook.Smoothstep((us - 0.4f) / 0.6f));
            // score
            float f = CountFraction(t);
            st.Score = t < Feel.ScoreAt ? 0 : Mathf.RoundToInt(Score * f);
            float twelfths = f * 12f;
            st.ScoreScale = t < Feel.ScoreAt || f >= 1f ? (f >= 1f && t < Feel.ScoreAt + Feel.ScoreTime + 0.2f ? 1f + 0.06f * (1f - Mathf.Clamp01((t - Feel.ScoreAt - Feel.ScoreTime) / 0.2f)) : 1f)
                : 1f + 0.06f * (1f - (twelfths - Mathf.Floor(twelfths)));
            st.Medal = t < Feel.ScoreAt ? -1 : MetaRules.Medal(st.Score, Silver, Gold);
            st.LinesAlpha = Mathf.Clamp01((t - Feel.ScoreAt - LinesAfterScore) / LinesFade);
            // trophies and the bar
            float ub = StudioCurves.EaseOutCubic(Mathf.Clamp01((t - Feel.TrophyAt) / Mathf.Max(0.01f, Feel.BarTime)));
            int gain = TrophiesAfter - TrophiesBefore;
            st.TrophyGainShown = t < Feel.TrophyAt ? 0 : Mathf.RoundToInt(gain * ub);
            st.TrophiesShown = TrophiesBefore + st.TrophyGainShown;
            st.Bar = t < Feel.TrophyAt ? BarBefore : Mathf.Lerp(BarBefore, Promoted ? 1f : BarAfter, ub);
            st.BarOverflow = Promoted && ub >= 1f;
            // coins, the reward card, continue
            st.CoinsFlying = t >= Feel.CoinsAt;
            st.RewardAlpha = Mathf.Clamp01((t - Feel.CoinsAt - RewardAfterCoins) / LinesFade);
            st.ContinuePulse = t >= Feel.ContinueAt;
            return st;
        }
    }
}
