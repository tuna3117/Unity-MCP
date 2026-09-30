using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// Pota's puzzle surroundings (saha.ts field): the giant hoop around the board, the court under it and the baked room reflection
    /// (yapbozSahne.ts ortamUret, envMapIntensity 0.45) as a custom probe over the board. Defaults = PotaFlow's layout measured from the
    /// board (8.5 m above the roof, court from 21.6 m in front of the board to 6.4 m behind it). The Color Hunt scene builds it at Start.
    /// </summary>
    public class PuzzleStage : MonoBehaviour
    {
        public ColorHuntGame Game;
        public bool BuildOnStart = true;
        [Tooltip("Ground below the board centre (PotaFlow: BoardAnchor.y above the roof).")]
        public float GroundBelow = 8.5f;
        public Vector2 CourtSize = new Vector2(15f, 28f);
        [Tooltip("How far the court reaches from the board toward the camera (PotaFlow: BoardAnchor.z − CourtStartZ).")]
        public float CourtNear = 21.6f;
        public bool BuildCourt = true;
        public Color CourtColor = new Color(0.851f, 0.467f, 0.227f);   // saha.ts zeminDokusu #d9773a
        public Color CourtLineColor = Color.white;
        public float ReflectionIntensity = 0.45f;
        public Vector3 ReflectionBox = new Vector3(26f, 26f, 26f);
        public float BallRadius = 0.19f;

        public GiantHoop Hoop { get; private set; }
        public ReflectionProbe Reflection { get; private set; }
        public GameObject Court { get; private set; }

        private void Start()
        {
            if (BuildOnStart) Build();
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
            if (BuildCourt)
                Court = CourtBuilder.Build(transform, CourtSize, root.position + new Vector3(0f, -GroundBelow, CourtSize.y * 0.5f - CourtNear), CourtColor, CourtLineColor);
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
