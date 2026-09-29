using UnityEngine;

namespace Project.Flow
{
    /// <summary>Every PotaFlow number in one Inspector-editable asset (spec 2026-09-29 §3–4; Pota sources in comments).</summary>
    [CreateAssetMenu(menuName = "Project/Pota/Flow Settings", fileName = "PotaFlowSettings")]
    public class PotaFlowSettings : ScriptableObject
    {
        [Header("Choreography")]
        public FlowCandidate Candidate = FlowCandidate.Stream;
        public FlowTiming Timing = new FlowTiming();
        [Tooltip("Basket level played before the board (20: median 158 baskets = Pota level 20 ammo).")]
        public int BasketLevel = 20;

        [Header("World")]
        [Tooltip("Board game root. Translation only: the board faces −z, toward the tower and the hero.")]
        public Vector3 BoardAnchor = new Vector3(0f, 8.5f, 30f);
        public Vector2 CourtSize = new Vector2(15f, 28f);
        public float CourtStartZ = 8.4f;
        public Color CourtColor = new Color(0.851f, 0.467f, 0.227f);   // Pota saha.ts zeminDokusu #d9773a
        public Color CourtLineColor = Color.white;

        [Header("Camera")]
        public float BoardFov = 34f;
        [Tooltip("Vista at the end of the first camera beat: beside the roof, on the hero with the balls rising behind him.")]
        public Vector3 MidPos = new Vector3(10f, 3.5f, 12f);
        public Vector3 MidLook = new Vector3(0f, 2.2f, 6.2f);
        public float MidFov = 50f;
        public float FovPunch = 3f;
        public float SettleAmount = 0.35f;
        public float BoardFogStart = 60f;
        public float BoardFogEnd = 220f;

        [Header("Balls")]
        public float BallRadius = 0.24f;
        public float ApexAbove = 9f;
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);

        [Header("Hero")]
        public float HeroTurnDegrees = 180f;
    }
}
