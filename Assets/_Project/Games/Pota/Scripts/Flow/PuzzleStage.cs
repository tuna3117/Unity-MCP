using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// Pota's puzzle surroundings (saha.ts field): the giant hoop around the board, the court under it and the baked room reflection
    /// (yapbozSahne.ts ortamUret, envMapIntensity 0.45) as a custom probe over the board. Defaults = Pota's rooftop (sahaYerlesim.ts
    /// YERLESIM, saha.ts): the hoop 8.5 m above the roof and 10 m beyond the court's centre, so the 15 × 14 court ends 3 m before it
    /// and the building (17 × 16, #cbbfae) 2 m before it, with rails on three edges. The Color Hunt scene builds it at Start;
    /// PotaFlow keeps its own longer court (BuildRoof off).
    /// </summary>
    public class PuzzleStage : MonoBehaviour
    {
        public ColorHuntGame Game;
        public bool BuildOnStart = true;
        [Tooltip("Ground below the board centre (PotaFlow: BoardAnchor.y above the roof).")]
        public float GroundBelow = 8.5f;
        /// <summary>How far the roof's top sits under the court quad (never in its plane: z-fighting).</summary>
        public const float RoofDrop = 0.01f;
        public Vector2 CourtSize = new Vector2(15f, 14f);           // YERLESIM.saha
        [Tooltip("How far the court reaches from the board toward the camera (Pota: 10 + 7; PotaFlow: BoardAnchor.z − CourtStartZ).")]
        public float CourtNear = 17f;
        [Tooltip("saha.ts: the building under the court (1 m wider on every side, 40 m tall) and rails on three edges.")]
        public bool BuildRoof = true;
        public Color RoofColor = PotaLook.Hex(0xcbbfae);
        public Color RailColor = PotaLook.Hex(0x9aa5ad);
        public bool BuildCourt = true;
        public Color CourtColor = new Color(0.851f, 0.467f, 0.227f);   // saha.ts zeminDokusu #d9773a
        public Color CourtLineColor = Color.white;
        public float ReflectionIntensity = 0.45f;
        public Vector3 ReflectionBox = new Vector3(26f, 26f, 26f);
        public float BallRadius = 0.19f;

        public GiantHoop Hoop { get; private set; }
        public ReflectionProbe Reflection { get; private set; }
        public GameObject Court { get; private set; }
        public GameObject Roof { get; private set; }

        private void Start()
        {
            if (BuildOnStart) Build();
        }

        /// <summary>saha.ts: govde BoxGeometry(en + 2, 40, boy + 2) under the roof (y = 0), rails 0.9 high on the far, near and left edges.</summary>
        private void BuildBuilding(Vector3 courtCentre)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var roofMat = Project.Basket.Visual.RimMeshBuilder.UrpLit(RoofColor, 0.1f);        // roughness 0.9
            Roof = Project.Basket.Visual.RimMeshBuilder.MeshObject("Roof", transform, cube, roofMat);
            // 1 cm under the court: a top face in the court's plane z-fought with it, and the beige roof flickered over the court
            // whenever the camera shook (2026-10-01 specials recordings)
            Roof.transform.position = courtCentre + new Vector3(0f, -20f - RoofDrop, 0f);
            Roof.transform.localScale = new Vector3(CourtSize.x + 2f, 40f, CourtSize.y + 2f);
            var railMat = Project.Basket.Visual.RimMeshBuilder.UrpLit(RailColor, 0.4f);
            railMat.SetFloat("_Metallic", 0.3f);
            float hx = CourtSize.x * 0.5f, hz = CourtSize.y * 0.5f;
            Rail("RailFar", cube, railMat, courtCentre + new Vector3(0f, 0.45f, hz), new Vector3(CourtSize.x + 2f, 0.9f, 0.08f));
            Rail("RailNear", cube, railMat, courtCentre + new Vector3(0f, 0.45f, -hz), new Vector3(CourtSize.x + 2f, 0.9f, 0.08f));
            Rail("RailLeft", cube, railMat, courtCentre + new Vector3(-hx, 0.45f, 0f), new Vector3(0.08f, 0.9f, CourtSize.y + 2f));
        }

        private void Rail(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 size)
        {
            var go = Project.Basket.Visual.RimMeshBuilder.MeshObject(name, transform, mesh, mat);
            go.transform.position = pos;
            go.transform.localScale = size;
        }

        public void Build()
        {
            if (Hoop != null || Game == null) return;
            var root = Game.transform;
            var hoopGo = new GameObject("GiantHoop");
            hoopGo.transform.SetParent(root, false);
            Hoop = hoopGo.AddComponent<GiantHoop>();
            Hoop.GroundY = root.position.y - GroundBelow;
            Hoop.BallRadius = BallRadius;
            Hoop.Backboard = Game.S.Backboard;
            Hoop.Build();
            var courtCentre = root.position + new Vector3(0f, -GroundBelow, CourtSize.y * 0.5f - CourtNear);
            if (BuildCourt) Court = CourtBuilder.Build(transform, CourtSize, courtCentre, CourtColor, CourtLineColor);
            if (BuildRoof) BuildBuilding(courtCentre);
            if (Game.Feel)
                SkyLife.Ensure(transform, new SkyLife.Props { Clouds = Game.S.Clouds, Birds = Game.S.Birds, BirdFlap = Game.S.BirdFlap,
                    CityBand = Game.S.CityBand, ToonTemplate = Game.S.ToonTemplate }, root.position, Hoop.GroundY, true);
            if (Game.S.RoomReflection == null) { Debug.LogWarning("[PuzzleStage] no RoomReflection: run ai_potalook_import"); return; }
            var go = new GameObject("RoomReflection");
            go.transform.SetParent(transform, false);
            go.transform.position = root.position;
            Reflection = go.AddComponent<ReflectionProbe>();
            Reflection.mode = ReflectionProbeMode.Custom;
            Reflection.customBakedTexture = Game.S.RoomReflection;
            Reflection.intensity = ReflectionIntensity;
            Reflection.size = ReflectionBox;
            Reflection.boxProjection = false;
            Reflection.importance = 10;
        }
    }
}
