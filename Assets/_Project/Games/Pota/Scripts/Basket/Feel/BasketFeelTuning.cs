using System;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>The basket round's feel layer (spec 2026-10-03 §2, F10–F13). Off = today's behaviour exactly.</summary>
    [Serializable]
    public class BasketFeelTuning
    {
        [Tooltip("Master switch: slingshot lean + hand ball + hoop glow + camera pull, miss feel, clean-pass SWISH!, rolling count and end stamp.")]
        public bool Enabled = true;

        [Header("F10 slingshot")]
        [Tooltip("Spine lean at full pull, degrees backwards.")]
        public float LeanDeg = 12f;
        [Tooltip("Hip drop at full pull, metres.")]
        public float CrouchM = 0.06f;
        [Tooltip("Camera pull-back at full pull, metres.")]
        public float CamBackM = 0.4f;
        [Tooltip("Camera pitch down at full pull, degrees.")]
        public float CamDownDeg = 2f;
        [Tooltip("Rim glow fade time on the aimed hoop.")]
        public float HoopGlowTime = 0.2f;
        [Tooltip("Trajectory line width at zero and full pull.")]
        public float LineWidthMin = 0.06f, LineWidthMax = 0.11f;

        [Header("F11 miss")]
        public float MissTime = 1f;
        [Tooltip("Camera sag, metres.")]
        public float MissCamSag = 0.2f;
        public float RimBlinkTime = 0.3f;
        [Tooltip("Hero slump forward, degrees.")]
        public float SlumpDeg = 8f;
        public float SlumpTime = 0.4f;

        [Header("F13 count stamp")]
        public float StampPeak = 2.2f;
        public float StampRest = 1.8f;
        public float StampTime = 0.35f;
        public float CageGlowTime = 0.3f;
    }
}
