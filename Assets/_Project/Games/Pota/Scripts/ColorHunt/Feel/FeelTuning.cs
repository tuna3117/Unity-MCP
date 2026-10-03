using System;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The in-game feel layer's switch and numbers (spec 2026-10-03 §2, Renk Avı F1–F9). Off = today's behaviour exactly, for the
    /// Pota | before | after recordings. Times are real seconds unless noted.
    /// </summary>
    [Serializable]
    public class FeelTuning
    {
        [Tooltip("Master switch: level intro, ammo tension, leftover finale, chain words, idle hint, landing bounce, board clip, sky life, lose moment.")]
        public bool Enabled = true;

        [Header("F4 chain words (Süper! / Harika! / Muhteşem!)")]
        [Tooltip("First chain depth that shows a word under the Pota look (the ×d number stays).")]
        public int WordFromDepth = 3;

        [Header("F1 level intro")]
        [Tooltip("When the goal chips leave the ribbon for the target panel.")]
        public float IntroGoalFlyAt = 0.7f;
        [Tooltip("When the block cascade starts (rows bottom-up, 0.03 s apart).")]
        public float IntroBoardAt = 0.95f;
        [Tooltip("A tap during the intro compresses the remainder into this long.")]
        public float IntroSkipTime = 0.3f;
        [Tooltip("How long the ribbon stays before it leaves (after the goals fly).")]
        public float IntroRibbonHold = 0.25f;

        [Header("F2 ammo tension")]
        [Tooltip("Peak alpha of the red screen-edge breath (1 Hz).")]
        public float TensionEdgeAlpha = 0.18f;

        [Header("F3 leftover finale")]
        public int FinaleMinFlights = 6;
        public int FinaleMaxFlights = 14;
        [Tooltip("Seconds between two flights.")]
        public float FinaleGap = 0.08f;
        [Tooltip("Ball flight time panel → block.")]
        public float FinaleBallTime = 0.22f;
        [Tooltip("Coin flight time block → badge.")]
        public float FinaleCoinTime = 0.45f;
        [Tooltip("Hard cap on the whole finale.")]
        public float FinaleMax = 2f;

        [Header("F6 landing bounce")]
        public float LandBounceTime = 0.18f;

        [Header("F9 lose moment")]
        [Tooltip("Board brightness while the lose card waits (1 = untouched).")]
        public float LoseDim = 0.6f;
        public float LoseDimTime = 0.4f;
        [Tooltip("Camera pull-back in metres.")]
        public float LoseCamBack = 0.3f;
        [Tooltip("Total remaining target items at or under this → \"Kıl payı!\".")]
        public int NearMissMax = 3;
    }
}
