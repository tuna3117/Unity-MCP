using Project.Basket.Visual;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// The giant hoop around the Color Hunt board (Pota pota.ts proportions, board cell = 1 m): frosted glass backboard,
    /// orange frame, rim under the board sticking out toward the camera, a 6-ring net that is the ammo hopper (up to
    /// <see cref="MaxPile"/> balls rest in it, it bulges as they land) and a pole down to the rooftop (world y = 0).
    /// Built at runtime as a child of the board's game root; local space = board space (the board faces −z).
    /// </summary>
    public class GiantHoop : MonoBehaviour
    {
        public const int MaxPile = 10;

        public float RimRadius = 1.6f;
        public float RimTube = 0.075f;
        [Tooltip("Rim centre below the board's outer edge (Pota: rim y −5.6 on an 8.8 m board).")]
        public float RimDrop = 1.7f;
        [Tooltip("Rim centre in front of the board (Pota: R + 0.6).")]
        public float RimForward = 2.2f;
        public float NetDepth = 1.6f;
        public int NetRings = 6;
        public float GlassMargin = 0.75f;
        public float FrameWidth = 0.35f;
        public float BallRadius = 0.24f;
        public Color FrameColor = new Color(1f, 0.54f, 0.16f);
        public Color GlassColor = new Color(0.85f, 0.93f, 1f, 0.28f);
        public Color NetColor = new Color(0.97f, 0.97f, 0.95f);
        public Color PoleColor = new Color(0.16f, 0.2f, 0.3f);

        public Transform Rim { get; private set; }
        public Transform Net { get; private set; }
        public Vector3 RimCenter => Rim.position;
        public int Pile { get; private set; }

        private Material _frameMat, _rimMat;
        private float _kick;

        public void Build(int boardSize)
        {
            float edge = boardSize * 0.5f + BoardBuilder.Margin;
            float half = edge + GlassMargin;
            _frameMat = Emissive(FrameColor, 0.5f);
            _rimMat = Emissive(FrameColor, 0.6f);
            var glassMat = Transparent(GlassColor);
            var netMat = RimMeshBuilder.UrpLit(NetColor, 0.2f);
            var poleMat = RimMeshBuilder.UrpLit(PoleColor, 0.4f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            Box("Glass", cube, glassMat, new Vector3(0f, 0f, 0.95f), new Vector3(half * 2f, half * 2f, 0.1f));
            float fw = FrameWidth;
            Box("FrameTop", cube, _frameMat, new Vector3(0f, half, 0.9f), new Vector3(half * 2f + fw, fw, 0.3f));
            Box("FrameBottom", cube, _frameMat, new Vector3(0f, -half, 0.9f), new Vector3(half * 2f + fw, fw, 0.3f));
            Box("FrameLeft", cube, _frameMat, new Vector3(-half, 0f, 0.9f), new Vector3(fw, half * 2f + fw, 0.3f));
            Box("FrameRight", cube, _frameMat, new Vector3(half, 0f, 0.9f), new Vector3(fw, half * 2f + fw, 0.3f));

            float rimY = -edge - RimDrop;
            Rim = RimMeshBuilder.MeshObject("Rim", transform, RimMeshBuilder.Torus(RimRadius, RimTube, 48, 12), _rimMat).transform;
            Rim.localPosition = new Vector3(0f, rimY, -RimForward);
            float backZ = -RimForward + RimRadius;
            Box("RimStrut", cube, _rimMat, new Vector3(0f, (rimY - half) * 0.5f, 0.9f), new Vector3(0.3f, Mathf.Abs(rimY + half) + 0.12f, 0.2f));
            Box("RimBracket", cube, _rimMat, new Vector3(0f, rimY, (backZ + 0.9f) * 0.5f), new Vector3(0.4f, 0.12f, 0.9f - backZ));

            Net = new GameObject("Net").transform;
            Net.SetParent(Rim, false);
            NetBuilder.Build(Net, RimRadius * 0.98f, netMat, NetRings, 16, NetDepth / NetRings, 0.03f, 0.022f);

            // Pole from the roof (world y = 0; the board root is never rotated or scaled) up to the glass centre, plus an arm.
            float ground = -transform.position.y;
            float len = -ground;
            var pole = RimMeshBuilder.MeshObject("Pole", transform, cyl, poleMat);
            pole.transform.localPosition = new Vector3(0f, ground + len * 0.5f, 1.9f);
            pole.transform.localScale = new Vector3(0.35f, len * 0.5f, 0.35f);
            Box("PoleArm", cube, poleMat, new Vector3(0f, 0f, 1.42f), new Vector3(0.25f, 0.25f, 0.95f));
            SetGlow(0f);
        }

        /// <summary>World position of pile slot i (0 = bottom): 3 + 4 + 3 balls in three layers inside the net cone.</summary>
        public Vector3 SlotPosition(int i)
        {
            i = Mathf.Clamp(i, 0, MaxPile - 1);
            int layer = i < 3 ? 0 : i < 7 ? 1 : 2;
            int first = layer == 0 ? 0 : layer == 1 ? 3 : 7;
            int count = layer == 1 ? 4 : 3;
            float depth = NetDepth - BallRadius * (1.2f + 1.7f * layer);
            float netR = RimRadius * (1f - 0.11f * NetRings * depth / NetDepth);     // NetBuilder's taper
            float r = Mathf.Max(0f, netR - BallRadius * 1.1f);
            float a = (i - first) * Mathf.PI * 2f / count + layer * 0.6f;
            return Rim.TransformPoint(new Vector3(Mathf.Cos(a) * r, -depth, Mathf.Sin(a) * r));
        }

        public void AddToPile()
        {
            Pile = Mathf.Min(MaxPile, Pile + 1);
            _kick = 1f;
        }

        public void ClearPile()
        {
            Pile = 0;
            _kick = 0f;
            if (Net != null) Net.localScale = Vector3.one;
        }

        /// <summary>Net shape: a fuller net is wider; every landing adds a short kick that decays.</summary>
        public void Tick(float dt)
        {
            _kick = Mathf.Max(0f, _kick - dt * 5f);
            float fill = Pile / (float)MaxPile;
            float w = 1f + 0.10f * fill + 0.08f * _kick;
            Net.localScale = new Vector3(w, 1f + 0.06f * fill + 0.05f * _kick, w);
        }

        /// <summary>Frame and rim glow 0 → 1 (HDR emission: blooms at the top).</summary>
        public void SetGlow(float g)
        {
            var c = FrameColor * (0.15f + 1.2f * Mathf.Clamp01(g));
            _frameMat.SetColor("_EmissionColor", c);
            _rimMat.SetColor("_EmissionColor", c);
        }

        private GameObject Box(string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 size)
        {
            var go = RimMeshBuilder.MeshObject(name, transform, mesh, mat);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            return go;
        }

        private static Material Emissive(Color c, float smoothness)
        {
            var m = RimMeshBuilder.UrpLit(c, smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        private static Material Transparent(Color c)
        {
            var m = RimMeshBuilder.UrpLit(c, 0.9f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }
    }
}
