using System.Collections.Generic;
using Project.Basket.Visual;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// yapbozSahne.ts gecisGoster: the golden ball's gates, glowing rings on the backboard's bottom edge under their column. Pota keeps a
    /// pool of four (one TorusGeometry(0.42, 0.07, 10, 28), one MeshBasicMaterial #ffb03a at opacity 0.95) in its frame group
    /// (cerceveGrup: scaled by the grid scale, never turned), so the rings neither turn nor shrink with a rotating board; they breathe
    /// 1 + 0.08·sin(5t) (ilerlet). This component sits on the child "GateRings" of the game root, at its origin: every position here is
    /// in game-root units (BoardRoot's unrotated placement: origin, scale GridScale(n)).
    /// </summary>
    public class GateRings : MonoBehaviour
    {
        public const int Pool = 4;
        public const float Radius = 0.42f, Tube = 0.07f;
        public const int Tubular = 28, Radial = 10;
        public const float Opacity = 0.95f;
        public const float Breath = 0.08f, BreathSpeed = 5f;
        /// <summary>altinlariIlerlet: flasGoster(bx, halkaY, #ffe27a, 0.9, 2.0, no light) as a golden ball passes its ring.</summary>
        public const float FlashSize = 0.9f, FlashRing = 2f;
        public static readonly Color Tint = PotaLook.Hex(0xffb03a);
        public static readonly Color FlashTint = PotaLook.Hex(0xffe27a);

        private ColorHuntGame _game;
        private readonly List<Transform> _rings = new List<Transform>();
        private readonly List<int> _shown = new List<int>();
        private float _t;

        /// <summary>The columns whose rings are visible, in pool order (at most <see cref="Pool"/>).</summary>
        public IReadOnlyList<int> Shown => _shown;

        public void Build(ColorHuntGame game)
        {
            _game = game;
            if (_rings.Count > 0) return;
            var mesh = RimMeshBuilder.Torus(Radius, Tube, Tubular, Radial);
            var mat = GateMaterial(game != null && game.S != null ? game.S.SelectionTemplate : null);
            for (int i = 0; i < Pool; i++)
            {
                var go = RimMeshBuilder.MeshObject("Gate" + i, transform, mesh, mat);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // RimMeshBuilder's torus lies in xz; three's faces the camera
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                _rings.Add(go.transform);
            }
        }

        /// <summary>The selection ring's URP Unlit transparent template (SelectionTemplate) retinted #ffb03a at 0.95: no new shader or template.</summary>
        public static Material GateMaterial(Material template)
        {
            var m = BlockView.RingMaterial(template);
            m.name = "PotaGate";
            var c = Tint;
            c.a = Opacity;
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>gecisGoster: ring i goes under column cols[i]; the rest of the pool hides. Null or empty hides them all.</summary>
        public void Show(IReadOnlyList<int> cols)
        {
            _shown.Clear();
            if (cols != null)
                for (int i = 0; i < cols.Count && _shown.Count < _rings.Count; i++) _shown.Add(cols[i]);
            for (int i = 0; i < _rings.Count; i++)
            {
                bool on = i < _shown.Count;
                _rings[i].gameObject.SetActive(on);
                if (on) _rings[i].localPosition = RingLocal(_shown[i]);
            }
            Breathe();
        }

        /// <summary>altinlariIlerlet: the golden ball passes the ring — ColorHuntFx's glow (no light) at the ring, board-local.</summary>
        public void Flash(int col)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            int n = Size;
            float x = BoardBuilder.CellPosition(n, n - 1, col).x;
            fx.Glow(new Vector2(x, BottomEdge(n)), FlashTint.linear, FlashSize, FlashRing, 0f, 0f);
        }

        /// <summary>The ring centre of <paramref name="col"/> for the board on the table, in game-root units.</summary>
        public Vector3 RingLocal(int col) => RingAt(Size, col);

        /// <summary>gecisGoster (c − (n−1)/2, panoAltKenar + 0.05, Z_ON + 0.1) in the frame group, times its grid scale s; Unity's front is −z.</summary>
        public static Vector3 RingAt(int n, int col)
        {
            float s = PotaLook.GridScale(n);
            return new Vector3(BoardBuilder.CellPosition(n, n - 1, col).x * s, BottomEdge(n) * s, -(PotaLook.FrontZ + 0.1f) * s);
        }

        /// <summary>yapbozSahne.ts panoAltKenar + 0.05 in board units (kasasız: −(panoBoy/2 + cerceve)/s): Pota's halkaY.</summary>
        public static float BottomEdge(int n) => -(PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth) / PotaLook.GridScale(n) + 0.05f;

        private int Size => _game != null && _game.Board != null ? _game.Board.Size : _game != null && _game.S != null ? _game.S.Size : 7;

        private void Update()
        {
            _t += Mathf.Min(Time.deltaTime, 0.05f);
            Breathe();
        }

        /// <summary>ilerlet: nabiz = 1 + 0.08·sin(5·gecisZaman) on each visible ring, on top of the frame group's grid scale.</summary>
        private void Breathe()
        {
            float k = PotaLook.GridScale(Size) * (1f + Breath * Mathf.Sin(_t * BreathSpeed));
            for (int i = 0; i < _shown.Count; i++) _rings[i].localScale = Vector3.one * k;
        }
    }
}
