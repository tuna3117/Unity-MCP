using System;
using System.Collections.Generic;
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Basket
{
    public class CageTrigger : MonoBehaviour
    {
        public BasketCage Cage;
        private void OnTriggerEnter(Collider other) => Cage?.HandleTrigger(other);
    }

    /// <summary>The wire basket at the bottom: colliders, entry counting and pile metrics.</summary>
    public class BasketCage : MonoBehaviour
    {
        public event Action<Ball> Entered;
        public int EnteredCount { get; private set; }
        public float PileTopY { get; private set; }
        public float MaxSpeed { get; private set; }
        public float LastEntryTime { get; private set; } = -100f;
        public float FloorY, TopY, Width, Depth;

        public static BasketCage Build(Transform parent, HoopLayout layout, BasketSettings s)
        {
            var go = new GameObject("BasketCage");
            go.transform.SetParent(parent, false);
            var cage = go.AddComponent<BasketCage>();
            cage.FloorY = layout.BasketFloorY;
            cage.TopY = layout.BasketTopY;
            cage.Width = layout.BasketWidth;
            cage.Depth = layout.BasketDepth;
            cage.PileTopY = cage.FloorY;
            float h = cage.TopY - cage.FloorY;
            float midY = (cage.TopY + cage.FloorY) * 0.5f;

            Wall(go.transform, "Floor", new Vector3(0f, cage.FloorY - 0.1f, 0f), new Vector3(cage.Width + 0.4f, 0.2f, cage.Depth + 0.4f), s.CageMaterial, null, false);
            Wall(go.transform, "Left", new Vector3(-cage.Width * 0.5f - 0.1f, midY, 0f), new Vector3(0.2f, h + 0.5f, cage.Depth + 0.4f), s.CageMaterial, null, false);
            Wall(go.transform, "Right", new Vector3(cage.Width * 0.5f + 0.1f, midY, 0f), new Vector3(0.2f, h + 0.5f, cage.Depth + 0.4f), s.CageMaterial, null, false);
            Wall(go.transform, "Back", new Vector3(0f, midY, -cage.Depth * 0.5f - 0.1f), new Vector3(cage.Width + 0.4f, h + 0.5f, 0.2f), s.CageMaterial, null, false);
            Wall(go.transform, "Front", new Vector3(0f, midY, cage.Depth * 0.5f + 0.1f), new Vector3(cage.Width + 0.4f, h + 0.5f, 0.2f), s.CageMaterial, null, false);
            var wire = RimMeshBuilder.UrpLit(new Color(0.1f, 0.1f, 0.11f), 0.45f);
            var rail = RimMeshBuilder.UrpLit(s.RimColor, 0.6f);
            CageMeshBuilder.Build(go.transform, cage.Width, h, cage.Depth, cage.FloorY, wire, rail);

            var trig = new GameObject("EntryTrigger");
            trig.transform.SetParent(go.transform, false);
            trig.transform.localPosition = new Vector3(0f, cage.TopY - 0.15f, 0f);
            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(cage.Width, 0.3f, cage.Depth);
            trig.AddComponent<CageTrigger>().Cage = cage;
            return cage;
        }

        private static void Wall(Transform parent, string name, Vector3 pos, Vector3 size, PhysicsMaterial pm, Material visual, bool visible)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localScale = size;
            w.GetComponent<BoxCollider>().sharedMaterial = pm;
            var mr = w.GetComponent<MeshRenderer>();
            if (visible && visual != null) mr.sharedMaterial = visual; else mr.enabled = false;
        }

        internal void HandleTrigger(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null) return;
            var ball = body.GetComponent<Ball>();
            if (ball == null || ball.Counted) return;
            ball.MarkPiled();
            EnteredCount++;
            LastEntryTime = Time.time;
            Entered?.Invoke(ball);
        }

        /// <summary>Call once per FixedUpdate with the pool's active list.</summary>
        public void Track(IReadOnlyList<Ball> balls)
        {
            float top = FloorY;
            float maxSpeed = 0f;
            for (int i = 0; i < balls.Count; i++)
            {
                var b = balls[i];
                if (!b.Counted) continue;
                float y = b.transform.position.y + 0.12f;
                if (y > top) top = y;
                if (b.Phase != BallPhase.Frozen)
                {
                    float sp = b.Body.linearVelocity.magnitude;
                    if (sp > maxSpeed) maxSpeed = sp;
                }
            }
            PileTopY = top;
            MaxSpeed = maxSpeed;
        }
    }
}
