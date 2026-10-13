using System;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The combo's two candidate choreographies (Pota spec 2026-10-13 §3, plan P4): A "Hızlı-sert" and B "Sinematik" — one choreography,
    /// two sets of numbers; Tuna picks from the Specials video, the loser is deleted (Pota Task 12).
    /// </summary>
    public enum ComboLook { A, B }

    /// <summary>
    /// One set of the combo look contract (Pota src/ozelAyar.ts OZEL_AN.kombo.a / .b, Task 8 Step 1 — the same numbers; whoever changes one
    /// changes the other in the same round). Times are game seconds unless they say real; lengths are board cells. The tooltip names the
    /// Pota key.
    /// </summary>
    [Serializable]
    public class ComboTuning
    {
        [Tooltip("birlesme: the merge pre-phase (game s), OUTSIDE the trigger plan (never compressed): the partner slides into the centre (smoothstep); the plan starts at its end.")]
        public float MergeTime;
        [Tooltip("buyume: both partners grow to 1 + this by the end of the merge.")]
        public float Grow;
        [Tooltip("yukselme: cells toward the camera both partners rise by the end of the merge (B).")]
        public float Rise;
        [Tooltip("parlama: their glow (BlockView.Flash) at the end of the merge.")]
        public float Glow;
        [Tooltip("kameraYaklas: the camera's push toward the centre over the merge + itmeSure (fraction of the way, sin in and out; 0 = none).")]
        public float CameraPush;
        [Tooltip("hitStop: real seconds of the ×HitStopTapScale dip at the fire.")]
        public float HitStop;
        [Tooltip("yavas: slow-motion scale (1 = none); yavasSure: its real seconds, AFTER the hit-stop (R-10: sequential, never overlapping).")]
        public float SlowScale, SlowTime;
        [Tooltip("flas: the white flash's size at the fire (cells; flasGoster(…, flas, 3, 4, 0.18)).")]
        public float Flash;
        [Tooltip("halka: the toon ring's size (cells; vfx-ring-toon, 16 frames in 0.4 s).")]
        public float Ring;
        [Tooltip("sars: the board shake at the fire.")]
        public float Shake;
        [Tooltip("itme, itmeSure: the camera kick toward the centre at the fire (fraction of the way, real s).")]
        public float Kick, KickTime;
        [Tooltip("parcaBlok: chunks per burst block of the combo step (vfx-pillow-shatter pieces in the block's colour).")]
        public int ChunksPerBlock;
        [Tooltip("parcaHiz: the chunks' speed (cells/s, × 0.6–1.0).")]
        public float ChunkSpeed;
        [Tooltip("parcaBoy: a chunk's size × its natural size in the 0.9 block (× 0.7–1.2).")]
        public float ChunkSize;
        [Tooltip("yaziSure: the lettering's life (s).")]
        public float LetterTime;

        /// <summary>OZEL_AN.kombo.a "Hızlı-sert".</summary>
        public static ComboTuning A() => new ComboTuning
        {
            MergeTime = 0.15f, Grow = 0.18f, Rise = 0f, Glow = 0.8f, CameraPush = 0f, HitStop = 0.06f, SlowScale = 1f, SlowTime = 0f,
            Flash = 2.4f, Ring = 2.6f, Shake = 0.18f, Kick = 0.06f, KickTime = 0.2f, ChunksPerBlock = 2, ChunkSpeed = 4.2f, ChunkSize = 0.8f,
            LetterTime = 0.55f,
        };

        /// <summary>OZEL_AN.kombo.b "Sinematik".</summary>
        public static ComboTuning B() => new ComboTuning
        {
            MergeTime = 0.35f, Grow = 0.3f, Rise = 0.45f, Glow = 1f, CameraPush = 0.06f, HitStop = 0.12f, SlowScale = 0.5f, SlowTime = 0.3f,
            Flash = 3.2f, Ring = 3.4f, Shake = 0.26f, Kick = 0.1f, KickTime = 0.3f, ChunksPerBlock = 4, ChunkSpeed = 5.5f, ChunkSize = 1f,
            LetterTime = 0.7f,
        };
    }

    /// <summary>The combo numbers both choreographies share (OZEL_AN.kombo.yaziEn, devBomba, tasima, pasYelpaze, yaslanma, parca).</summary>
    [Serializable]
    public class ComboShared
    {
        [Tooltip("yaziEn: lettering widths (cells) — CROSSOVER!, SHOWTIME!, MEGA SLAM!, ALLEY-OOP!, FAST BREAK! (1024×512 stills, 2:1 quads).")]
        public float CrossoverCells = 5.6f, ShowtimeCells = 5.4f, MegaSlamCells = 5.6f, AlleyOopCells = 5.4f, FastBreakCells = 5.6f;
        [Tooltip("devBomba: the big bomb's dunk — ball × olcek, floor crack × catlak, dust ring × toz.")]
        public float BigBombScale = 1.5f, BigBombCrack = 1.8f, BigBombDust = 1.8f;
        [Tooltip("tasima: a carried special hangs this many cells under the pass ball, at this scale.")]
        public float CarryDown = 0.5f, CarryScale = 0.8f;
        [Tooltip("pasYelpaze: three passes leave the centre fanned out (cells; (j − 1) × this across).")]
        public float PassFan = 0.35f;
        [Tooltip("yaslanma: a pair that can combo leans this many cells toward each other, every LeanEvery s for LeanTime s (sin), while the board rests.")]
        public float LeanAmount = 0.06f, LeanEvery = 3.5f, LeanTime = 0.4f;
        [Tooltip("parca: a chunk's life (s), gravity (cells/s²), pool size; the last 0.25 s it shrinks away.")]
        public float ChunkLife = 1.1f, ChunkGravity = 14f;
        public int ChunkPool = 120;
    }
}
