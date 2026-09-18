using UnityEngine;

namespace Project.Basket
{
    /// <summary>Every tunable of the basket round. Defaults are the original game's numbers (see spec §2).</summary>
    [CreateAssetMenu(menuName = "Project/Basket/Basket Settings", fileName = "BasketSettings")]
    public class BasketSettings : ScriptableObject
    {
        [Header("Flight")]
        public Vector3 LaunchStart = new Vector3(0f, 1.9f, 6.2f);
        public float FlightGravity = 9.8f;
        [Tooltip("Seconds between consecutive balls of one shot.")] public float BallSpacing = 0.09f;
        [Tooltip("Per-ball lateral aim jitter in metres (±).")] public float AimJitter = 0.03f;

        [Header("Drop")]
        [Tooltip("Gravity while falling through the column; lower than flight so the eye can follow.")] public float DropGravity = 5.0f;
        [Tooltip("Ball z below which a flying ball is inside the column slab.")] public float SlabEntryZ = 0.3f;
        [Tooltip("Forward (z) velocity multiplier when entering the slab; stands in for the original 2D hand-off.")] public float EntryForwardDamping = 0.25f;
        public float SlabHalfDepth = 0.5f;

        [Header("Ball")]
        public float BallRadius = 0.12f;
        public float BallMass = 0.6f;
        [Tooltip("Backspin angular speed (rad/s) applied at launch.")] public float Backspin = 9f;
        [Tooltip("No downward progress for this long → teleported to the cage mouth.")] public float StuckSeconds = 3f;
        [Tooltip("Nudged with a small impulse after this many seconds without progress.")] public float NudgeSeconds = 1.5f;
        public float SlowSeconds = 0.6f;
        public float SlowSpeed = 0.15f;

        [Header("Rim")]
        public float RimTube = 0.03f;
        public int RimSegments = 12;
        public bool FriendlyRim = true;
        [Tooltip("Minimum inward speed when a ball touches the inner rim while dropping.")] public float RimAssistMinInward = 0.6f;
        [Tooltip("Cap on upward speed after a rim bounce.")] public float RimUpSpeedCap = 2.6f;

        [Header("Effects")]
        [Tooltip("Lateral velocity scatter (total width) for ×N clones.")] public float CloneScatter = 1.6f;
        public float AddDropSpeed = 3f;
        public int MaxBalls = 900;
        public int MaxSimulated = 400;

        [Header("Physics materials (created by the scene tool)")]
        public PhysicsMaterial BallMaterial;
        public PhysicsMaterial RimMaterial;
        public PhysicsMaterial BackboardMaterial;
        public PhysicsMaterial WallMaterial;
        public PhysicsMaterial CageMaterial;

        [Header("Visual materials (optional, otherwise generated)")]
        public Material BallVisualMaterial;
        public Material RimVisualMaterial;
        public Material BackboardVisualMaterial;
        public Material WallVisualMaterial;
        public Material NetVisualMaterial;

        [Header("End")]
        public float RoundTimeout = 30f;

        [Header("Juice")]
        public float SwishSlowMoScale = 0.35f;
        public float SwishSlowMoSeconds = 0.4f;
        public float SwishShake = 0.04f;
        public float ShakeDecay = 9f;
        public float HitStopSeconds = 0.03f;
        public float PopupSeconds = 0.9f;
        public float PopupMinInterval = 0.35f;

        [Header("Camera")]
        public Vector3 AimCamPos = new Vector3(0f, 2.6f, 11f);
        public Vector3 AimLookAt = new Vector3(0f, 3.0f, 0f);
        public float Fov = 60f;
        public float DropCamZ = 9.5f;
        public float BasketCamZ = 13.5f;
        public float AimSmoothing = 4f;
        public float DropSmoothing = 2.2f;
        public float BasketSmoothing = 2f;
    }
}
