using Project.Basket.Visual;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// The hero's ball basket on the terrace (PotaFlow A, Tuna 2026-09-30: "the balls should fill a basket next to the
    /// guy"): the tower cage's wire look at basket size. The balls from the cage drop in from above and pile up in three
    /// layers of 3 × 3; its fill is the ammo the board receives. Local origin = floor centre on the terrace (y = 0).
    /// 2026-10-02 (Tuna: "why are the balls so big?"): sized for real basketballs (r 0.12, the round's own), waist-high.
    /// </summary>
    public class BallBin : MonoBehaviour, IBallReceiver
    {
        public float Width = 0.92f;
        public float Height = 0.72f;
        public float Depth = 0.92f;
        public float FloorY = 0.1f;
        public float BallRadius = 0.12f;
        public Color WireColor = new Color(0.1f, 0.1f, 0.11f);
        public Color RimColor = new Color(1f, 0.416f, 0.102f);   // BasketSettings.RimColor

        public int Pile { get; private set; }
        public const int Layer = 3;
        public int RestingCapacity => Layer * Layer * 3;
        private Transform _body;
        private float _kick;

        /// <summary>Inside of the basket in world space (walls to walls, floor to a heaped top above the rim).</summary>
        public Bounds InnerBounds
        {
            get
            {
                var centre = transform.TransformPoint(new Vector3(0f, FloorY + Height * 0.5f + 0.15f, 0f));
                return new Bounds(centre, new Vector3(Width - 0.06f, Height + 0.3f, Depth - 0.06f));
            }
        }

        public void Build()
        {
            _body = new GameObject("BinBody").transform;
            _body.SetParent(transform, false);
            var wire = RimMeshBuilder.UrpLit(WireColor, 0.45f);
            var rim = RimMeshBuilder.UrpLit(RimColor, 0.6f);
            CageMeshBuilder.Build(_body, Width, Height, Depth, FloorY, wire, rim, 0.2f, 0.028f, 0.07f, 0.12f);
            var floor = RimMeshBuilder.MeshObject("BinFloor", _body, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), wire);
            floor.transform.localPosition = new Vector3(0f, FloorY - 0.01f, 0f);
            floor.transform.localScale = new Vector3(Width, 0.02f, Depth);
        }

        public Vector3 EntryPoint(int i)
        {
            float x = (FlowHash.Get(i, 11) - 0.5f) * (Width - 4f * BallRadius);
            float z = (FlowHash.Get(i, 12) - 0.5f) * (Depth - 4f * BallRadius);
            return transform.TransformPoint(new Vector3(x, FloorY + Height + 0.1f, z));
        }

        /// <summary>Three layers of 3 × 3, each layer shifted a little so the heap does not look like a grid.</summary>
        public Vector3 SlotPosition(int i)
        {
            i = Mathf.Clamp(i, 0, RestingCapacity - 1);
            int per = Layer * Layer, layer = i / per, col = i % Layer, row = (i / Layer) % Layer;
            float gap = BallRadius * 2.05f, mid = (Layer - 1) * 0.5f;
            float shift = layer == 1 ? 0.04f : layer == 2 ? -0.035f : 0f;
            var local = new Vector3((col - mid) * gap + shift, FloorY + BallRadius + layer * BallRadius * 1.9f, (row - mid) * gap - shift);
            return transform.TransformPoint(local);
        }

        public Vector3 SinkPoint => SlotPosition(RestingCapacity - 1) + Vector3.up * 0.05f;

        public void AddBall()
        {
            Pile = Mathf.Min(RestingCapacity, Pile + 1);
            _kick = 1f;
        }

        public void Clear()
        {
            Pile = 0;
            _kick = 0f;
            if (_body != null) _body.localScale = Vector3.one;
        }

        /// <summary>Small squash on every landing.</summary>
        public void Tick(float dt)
        {
            if (_body == null) return;
            _kick = Mathf.Max(0f, _kick - dt * 6f);
            _body.localScale = new Vector3(1f + 0.04f * _kick, 1f - 0.06f * _kick, 1f + 0.04f * _kick);
        }
    }
}
