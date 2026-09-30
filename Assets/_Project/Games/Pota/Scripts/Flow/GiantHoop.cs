using Project.Basket.Visual;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// The giant hoop around the Color Hunt board at Pota's size (sahaYerlesim.ts DEV_POTA, pota.ts): frosted glass backboard 8.8²
    /// (pano-a.png, #bfe3ff, opacity 0.32) 0.7 behind the board centre, orange #ff8a2a frame 0.35 thick × 0.9 deep (outer 9.5), rim
    /// R 1.6 at y −5.6 sticking out R + 0.6 toward the camera, a 6-ring net 1.8 deep (PotaFlow's ammo hopper: up to
    /// <see cref="MaxPile"/> balls rest in it) and a grey pole down to <see cref="GroundY"/>. Child of the board's game root, which is
    /// never scaled (the grid scales inside the glass); local space = the root's (the board faces −z).
    /// </summary>
    public class GiantHoop : MonoBehaviour, IBallReceiver
    {
        public const int MaxPile = 10;

        public float RimRadius = PotaLook.RimRadius;
        public float RimTube = 0.1f;
        public float RimY = PotaLook.RimY;
        [Tooltip("Rim centre in front of the board (pota.ts: R + 0.6).")]
        public float RimForward = PotaLook.RimRadius + 0.6f;
        public float NetDepth = PotaLook.NetDepth;
        public int NetRings = 6;
        public float GlassSize = PotaLook.PanoSize;
        public float GlassZ = 0.7f;
        public float FrameWidth = PotaLook.FrameWidth;
        public float FrameDepth = 0.9f;
        public float FrameZ = 0.35f;
        public float PoleZ = 1.2f;
        public float PoleDiameter = 0.55f;
        [Tooltip("World height of the ground the pole stands on (PotaFlow: the roof, 0).")]
        public float GroundY;
        public float BallRadius = 0.24f;
        public Texture2D Backboard;
        public Color FrameColor = PotaLook.Hex(0xff8a2a);
        public Color GlassColor = new Color(0.749f, 0.890f, 1f, 0.32f);   // #bfe3ff, opacity 0.32
        public Color NetColor = PotaLook.Hex(0xf4f4f4);
        public Color PoleColor = PotaLook.Hex(0x8c8c8c);

        public Transform Rim { get; private set; }
        public Transform Net { get; private set; }
        public Vector3 RimCenter => Rim.position;
        public int Pile { get; private set; }

        private Material _frameMat, _rimMat;
        private float _kick;

        public void Build()
        {
            float half = GlassSize * 0.5f, outer = half + FrameWidth;
            _frameMat = Emissive(FrameColor, 0.5f, 0.1f);
            _rimMat = Emissive(FrameColor, 0.5f, 0.1f);
            var netMat = RimMeshBuilder.UrpLit(NetColor, 0f);             // MeshStandard default roughness 1
            var poleMat = RimMeshBuilder.UrpLit(PoleColor, 0.4f);
            poleMat.SetFloat("_Metallic", 0.3f);                         // roughness 0.6, metalness 0.3
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            var glass = RimMeshBuilder.MeshObject("Glass", transform, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), Transparent(GlassColor, Backboard));
            glass.transform.localPosition = new Vector3(0f, 0f, GlassZ);
            glass.transform.localScale = new Vector3(GlassSize, GlassSize, 1f);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // pota.ts cubukGeo: plain boxes.
            float mid = half + FrameWidth * 0.5f;
            Bar("FrameTop", cube, new Vector3(0f, mid, FrameZ), new Vector3(GlassSize + 2f * FrameWidth, FrameWidth, FrameDepth));
            Bar("FrameBottom", cube, new Vector3(0f, -mid, FrameZ), new Vector3(GlassSize + 2f * FrameWidth, FrameWidth, FrameDepth));
            Bar("FrameLeft", cube, new Vector3(-mid, 0f, FrameZ), new Vector3(FrameWidth, GlassSize, FrameDepth));
            Bar("FrameRight", cube, new Vector3(mid, 0f, FrameZ), new Vector3(FrameWidth, GlassSize, FrameDepth));

            Rim = RimMeshBuilder.MeshObject("Rim", transform, RimMeshBuilder.Torus(RimRadius, RimTube, 48, 12), _rimMat).transform;
            Rim.localPosition = new Vector3(0f, RimY, -RimForward);
            // pota.ts file: six rings, R(1 − 0.1k), 0.3 apart, white; no strands.
            Net = new GameObject("Net").transform;
            Net.SetParent(Rim, false);
            for (int k = 1; k <= NetRings; k++)
            {
                var ring = RimMeshBuilder.MeshObject($"Ring{k}", Net, RimMeshBuilder.Torus(RimRadius * (1f - 0.1f * k), 0.035f, 30, 6), netMat);
                ring.transform.localPosition = new Vector3(0f, -k * NetDepth / NetRings, 0f);
            }

            // pota.ts direk: steel pole from the frame's lower edge to the roof, an arm from the board to the rim, an arm to the pole.
            float ground = GroundY - transform.position.y;           // the game root is never rotated or scaled
            float len = Mathf.Max(0.01f, -outer - ground);
            var pole = RimMeshBuilder.MeshObject("Pole", transform, cyl, poleMat);
            pole.transform.localPosition = new Vector3(0f, -outer - len * 0.5f, PoleZ);
            pole.transform.localScale = new Vector3(PoleDiameter, len * 0.5f, PoleDiameter);
            Box("RimArm", cube, poleMat, new Vector3(0f, RimY, -RimForward * 0.5f), new Vector3(0.3f, 0.3f, RimForward));
            Box("PoleArm", cube, poleMat, new Vector3(0f, -outer, 1.3f), new Vector3(0.3f, 0.3f, 1.2f));
            SetGlow(0f);
        }

        private void Bar(string name, Mesh mesh, Vector3 pos, Vector3 size)
        {
            var go = RimMeshBuilder.MeshObject(name, transform, mesh, _frameMat);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        /// <summary>World position of pile slot i (0 = bottom): 3 + 4 + 3 balls in three layers inside the net cone.</summary>
        public Vector3 SlotPosition(int i)
        {
            i = Mathf.Clamp(i, 0, MaxPile - 1);
            int layer = i < 3 ? 0 : i < 7 ? 1 : 2;
            int first = layer == 0 ? 0 : layer == 1 ? 3 : 7;
            int count = layer == 1 ? 4 : 3;
            float depth = NetDepth - BallRadius * (1.2f + 1.7f * layer);
            float netR = RimRadius * (1f - 0.1f * NetRings * depth / NetDepth);      // the rings' taper
            float r = Mathf.Max(0f, netR - BallRadius * 1.1f);
            float a = (i - first) * Mathf.PI * 2f / count + layer * 0.6f;
            return Rim.TransformPoint(new Vector3(Mathf.Cos(a) * r, -depth, Mathf.Sin(a) * r));
        }

        // IBallReceiver: balls drop through the rim into the net (kept for a net-filling variant; A uses the hero's basket).
        public Vector3 EntryPoint(int i)
        {
            float a = FlowHash.Get(i, 3) * Mathf.PI * 2f, r = FlowHash.Get(i, 4) * RimRadius * 0.35f;
            return RimCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        public Vector3 SinkPoint => RimCenter + Vector3.down * (NetDepth + BallRadius);
        public int RestingCapacity => MaxPile;
        public void AddBall() => AddToPile();

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

        /// <summary>Frame and rim glow 0 → 1 (HDR emission: blooms at the top). Pota's frame does not glow at rest.</summary>
        public void SetGlow(float g)
        {
            var c = FrameColor * (1.2f * Mathf.Clamp01(g));
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

        private static Material Emissive(Color c, float smoothness, float metallic)
        {
            var m = RimMeshBuilder.UrpLit(c, smoothness);
            m.SetFloat("_Metallic", metallic);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        /// <summary>pota.ts pano: MeshPhysical #bfe3ff, roughness 0.25, opacity 0.32, pano-a.png.</summary>
        private static Material Transparent(Color c, Texture2D map)
        {
            var m = RimMeshBuilder.UrpLit(c, 1f - 0.25f);
            if (map != null) m.SetTexture("_BaseMap", map);
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
