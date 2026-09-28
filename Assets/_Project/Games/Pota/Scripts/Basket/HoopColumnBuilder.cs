using System.Collections.Generic;
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>Builds hoops, walls, the slab and the cage for one <see cref="HoopLayout"/>.</summary>
    public static class HoopColumnBuilder
    {
        public sealed class Result
        {
            public Transform Root;
            public List<Hoop> Hoops = new List<Hoop>();
            public BasketCage Cage;
            public PlayerActor Player;
        }

        public const float TopMargin = 6f;

        public static Result Build(Transform parent, HoopLayout layout, BasketSettings s, int layerHoop, int layerSlab)
        {
            var result = new Result();
            var root = new GameObject($"Column_L{layout.Level}");
            root.transform.SetParent(parent, false);
            result.Root = root.transform;

            foreach (var spec in layout.Hoops)
                result.Hoops.Add(Hoop.Build(root.transform, spec, s, layerHoop));

            float top = layout.TopRimY + TopMargin;
            float bottom = layout.BasketFloorY;
            float midY = (top + bottom) * 0.5f;
            float height = top - bottom;
            float zMin = -s.SlabHalfDepth - 0.2f;
            float zMax = layout.BasketDepth * 0.5f + 0.2f;
            float depth = zMax - zMin;
            float zMid = (zMin + zMax) * 0.5f;

            bool grayBox = !s.BuildEnvironment;
            var wallVisual = s.WallVisualMaterial != null ? s.WallVisualMaterial : RimMeshBuilder.UrpLit(new Color(0.55f, 0.5f, 0.45f), 0.2f);
            Wall(root.transform, "WallLeft", new Vector3(-layout.WallX - 0.1f, midY, zMid), new Vector3(0.2f, height, depth), s.WallMaterial, wallVisual, grayBox, layerHoop);
            Wall(root.transform, "WallRight", new Vector3(layout.WallX + 0.1f, midY, zMid), new Vector3(0.2f, height, depth), s.WallMaterial, wallVisual, grayBox, layerHoop);

            // Facade (back wall of the slab) from the cage mouth up to the top.
            float backTop = top, backBottom = layout.BasketTopY;
            Wall(root.transform, "Facade", new Vector3(0f, (backTop + backBottom) * 0.5f, -s.SlabHalfDepth - 0.1f),
                new Vector3(layout.WallX * 2f + 0.4f, backTop - backBottom, 0.2f), s.WallMaterial, wallVisual, grayBox, layerHoop);

            // Invisible front wall of the slab; flying balls ignore it (layer), dropping balls are kept inside.
            float frontBottom = layout.BasketTopY + 0.3f;
            Wall(root.transform, "SlabFront", new Vector3(0f, (backTop + frontBottom) * 0.5f, s.SlabHalfDepth + 0.1f),
                new Vector3(layout.WallX * 2f + 0.4f, backTop - frontBottom, 0.2f), s.WallMaterial, null, false, layerSlab);

            result.Cage = BasketCage.Build(root.transform, layout, s);
            if (s.BuildEnvironment) result.Player = BasketEnvironmentBuilder.Build(root.transform, layout, s);
            return result;
        }

        private static void Wall(Transform parent, string name, Vector3 pos, Vector3 size, PhysicsMaterial pm, Material visual, bool visible, int layer)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.layer = layer;
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localScale = size;
            w.GetComponent<BoxCollider>().sharedMaterial = pm;
            var mr = w.GetComponent<MeshRenderer>();
            if (visible && visual != null) mr.sharedMaterial = visual; else mr.enabled = false;
        }
    }
}
