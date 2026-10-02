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
        public float BoardFov = 38f;   // yapbozSahne.ts GORUS_ACISI
        [Tooltip("Vista at the end of the first camera beat: beside the roof, on the hero with the balls rising behind him.")]
        public Vector3 MidPos = new Vector3(8.5f, 4f, 4.5f);
        public Vector3 MidLook = new Vector3(0.7f, 1f, 7.15f);
        public float MidFov = 46f;
        [Tooltip("After the vista the camera turns toward the board ahead of its move (0 = together).")]
        public float LookLead = 1f;
        public float FovPunch = 3f;
        public float SettleAmount = 0.35f;
        public float BoardFogStart = 30f;   // saha.ts fog 30–130
        public float BoardFogEnd = 130f;

        [Header("Balls")]
        public float BallRadius = 0.19f;   // Pota's salvo ball
        [Tooltip("Arc top above the higher end (cage mouth or basket).")]
        public float ApexAbove = 5f;
        [Tooltip("The hero's basket on the terrace (floor centre), beside him on the camera side.")]
        public Vector3 BinPosition = new Vector3(1.4f, 0f, 7.5f);
        [Tooltip("Balls come into the basket on this much of a slant (0 = straight down, a stacked column).")]
        public float BinEndLean = 2.5f;
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);

        [Header("Candidate 1: Büyük şut")]
        [Tooltip("Shot pose: behind and beside the hero on the terrace, the giant hoop ahead (Pota 'saha' pose adapted; must stay in front of the tower face).")]
        public Vector3 ShotPos = new Vector3(-1.4f, 5.0f, 0.9f);   // tower front slab ends at z 0.7, hero at (0, 0, 6.8)
        public Vector3 ShotLook = new Vector3(0f, 1.0f, 30f);
        public float ShotFov = 66f;
        [Tooltip("Kick of the balls leaving the hero's basket toward the hoop.")]
        public Vector3 NetLaunchKick = new Vector3(0f, 1.2f, 0.6f);
        public float NetApexAbove = 6f;

        [Header("Candidate 2: Çemberden dalış")]
        [Tooltip("Chase waypoint low over the court.")]
        public Vector3 DiveCourtPos = new Vector3(0f, 1.8f, 20f);
        [Tooltip("The rise axis sits this far in front of the rim centre (clear of the rim arm behind it).")]
        public float DiveRimForward = 0.3f;
        [Tooltip("Below the rim centre (under the whole net) where the rise starts.")]
        public float DiveUnderRim = 1.9f;
        [Tooltip("Above the rim centre where the pull-back starts.")]
        public float DiveAboveRim = 1.7f;
        public float DiveChaseFov = 62f, DiveRimFov = 70f, DiveAboveFov = 66f;
        public float SpeedLinesStrength = 0.9f;
        public float SwishLetterWidth = 3.6f;

        [Header("Candidate 3: Pano damgası")]
        [Tooltip("Puff and star size on the glass (m).")]
        public float StampPuffSize = 4.5f;
    }
}
