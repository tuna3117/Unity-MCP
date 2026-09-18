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
        [Tooltip("The flight targets this much above the aim point so the ball clears the front of the rim and drops in (3D-only correction).")] public float ArrivalLift = 0f;

        [Header("Drop")]
        [Tooltip("Gravity while falling through the column; lower than flight so the eye can follow.")] public float DropGravity = 5.0f;
        [Tooltip("Ball z below which a flying ball is inside the column slab.")] public float SlabEntryZ = 0f;
        [Tooltip("Forward (z) velocity multiplier when entering the slab; stands in for the original 2D hand-off.")] public float EntryForwardDamping = 0.1f;
        public float SlabHalfDepth = 0.5f;
        [Tooltip("Sideways speed given to a ball that arrives more than 1 m above the rim (original: \"over the backboard\" → falls beside the hoop).")] public float OverPowerSideKick = 1.5f;
        public float OverPowerAbove = 1.0f;
        [Tooltip("A ball arriving this much above the rim \"hits the backboard\": its lateral speed is scaled by BackboardLateralDamping (original rule).")] public float BackboardHitAbove = 0.45f;
        public float BackboardLateralDamping = 0.3f;

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

        [Header("Art (assigned by the scene tool)")]
        public Texture2D FacadeTexture;     // kule-cam.jpg, tiled 3.2 x 5.7 m
        public Texture2D WingTexture;       // kule-renkli.jpg
        public Texture2D CityTexture;       // cephe.jpg
        public Texture2D SkyTexture;        // sehir.jpg
        public Texture2D BackboardTexture;  // pano.png
        public GameObject PlayerPrefab;     // oyuncu.glb
        public Color SkyColor = new Color(0.557f, 0.788f, 0.949f);
        public Color FogColor = new Color(0.62f, 0.827f, 0.961f);
        public float FogStart = 26f, FogEnd = 115f;
        public Color RimColor = new Color(1f, 0.416f, 0.102f);
        public Color GoldColor = new Color(1f, 0.702f, 0.102f);
        public Color GreenColor = new Color(0.184f, 0.702f, 0.42f);
        public Color NavyColor = new Color(0.078f, 0.157f, 0.314f);
        public Color TerraceColor = new Color(0.247f, 0.42f, 0.31f);
        public Color GlassColor = new Color(0.16f, 0.3f, 0.48f);
        public bool BuildEnvironment = true;

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
