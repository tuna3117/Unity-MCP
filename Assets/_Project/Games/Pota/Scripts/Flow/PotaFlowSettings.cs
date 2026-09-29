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
        public Vector3 MidPos = new Vector3(9.5f, 2.6f, 9f);
        public Vector3 MidLook = new Vector3(0f, 1.6f, 6.6f);
        public float MidFov = 46f;
        public float FovPunch = 3f;
        public float SettleAmount = 0.35f;
        public float BoardFogStart = 60f;
        public float BoardFogEnd = 220f;

        [Header("Balls")]
        public float BallRadius = 0.24f;
        [Tooltip("Arc top above the higher end (cage mouth or basket).")]
        public float ApexAbove = 5f;
        [Tooltip("The hero's basket on the terrace (floor centre), beside him on the camera side.")]
        public Vector3 BinPosition = new Vector3(1.5f, 0f, 7.0f);
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);

    }
}
