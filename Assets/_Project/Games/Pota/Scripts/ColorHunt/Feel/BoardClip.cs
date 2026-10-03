using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// F7 board clip (spec 2026-10-03): the blocks and their labels live on the <see cref="LayerName"/> layer, which the renderer's
    /// default passes skip; two RenderObjects features (installed by the editor's BoardClipInstaller) draw that layer only where the
    /// stencil is <see cref="StencilRef"/>, written by an invisible mask quad over the frame's inside (and the run below it, so golden
    /// balls dropping to the hoop stay visible). Refill blocks above the frame are clipped at its top edge, in every look.
    /// </summary>
    public class BoardClip : MonoBehaviour
    {
        public const string LayerName = "PotaBoard";
        public const int StencilRef = 1;
        /// <summary>How far below the frame the mask reaches, in pano metres (the gate run to the hoop is ~7 m).</summary>
        public const float RunBelow = 20f;

        private ColorHuntGame _game;
        private Material _mat;

        /// <summary>The mask quad under the board root; null until the board exists.</summary>
        public Transform Mask { get; private set; }
        /// <summary>The board layer's index, −1 when the project has no such layer (then nothing is clipped).</summary>
        public int Layer { get; private set; } = -1;
        /// <summary>Clipping is on: the feel layer is on and the project has the board layer. Off = today's draw path (Default layer).</summary>
        public bool Active => Layer >= 0 && _game != null && _game.Feel;
        private bool _applied;

        /// <summary>The project has the board layer (the installer adds it with the renderer features).</summary>
        public static bool Installed() => LayerMask.NameToLayer(LayerName) >= 0;

        /// <summary>Mask size in board-root units for an n × n board: the pano's inside width, the pano plus the run below in height.</summary>
        public static Vector2 MaskLocalSize(int n)
        {
            float scale = PotaLook.GridScale(n);
            return new Vector2(PotaLook.PanoSize / scale, (PotaLook.PanoSize + RunBelow) / scale);
        }

        /// <summary>Mask centre in board-root units: the top edge sits on the frame's top.</summary>
        public static Vector2 MaskLocalCentre(int n) => new Vector2(0f, -RunBelow * 0.5f / PotaLook.GridScale(n));

        /// <summary>The frame bars' front face (GiantHoop FrameZ 0.35 − FrameDepth 0.9 / 2), world metres.</summary>
        public const float FrameFrontZ = 0.35f - 0.45f;

        /// <summary>
        /// Mask depth in board-root units: the frame bars' front face. Blocks bulge in front of the bars, so a mask at their own depth
        /// would clip a perspective sliver above the bar's inner edge; at the bar's depth the clip line is that edge on screen.
        /// </summary>
        public static float MaskLocalZ(int n) => FrameFrontZ / PotaLook.GridScale(n);

        private void Awake()
        {
            _game = GetComponent<ColorHuntGame>();
            Layer = LayerMask.NameToLayer(LayerName);
            if (_game != null) _game.Clip = this;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Clip == this) _game.Clip = null;
        }

        private void LateUpdate()
        {
            if (_game == null || _game.BoardRoot == null || Layer < 0) return;
            if (Mask == null) Build();
            bool on = Active;
            if (on != _applied)
            {
                _applied = on;
                foreach (var v in _game.Views) Assign(v.gameObject);
                Mask.gameObject.SetActive(on);
            }
            if (!on) return;
            int n = _game.Board != null ? _game.Board.Size : _game.S.Size;
            var size = MaskLocalSize(n);
            Mask.localScale = new Vector3(size.x, size.y, 1f);
            Mask.localPosition = (Vector3)MaskLocalCentre(n) + new Vector3(0f, 0f, MaskLocalZ(n));
        }

        private void Build()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BoardClipMask";
            Destroy(go.GetComponent<Collider>());
            // Default layer, NOT the board layer: the renderer's own opaque pass writes the stencil; the board's passes (stencil Equal)
            // would otherwise test the mask against itself and never write it (the first recording showed an empty board).
            go.layer = 0;
            go.transform.SetParent(_game.BoardRoot, false);
            go.transform.localRotation = Quaternion.identity;
            var r = go.GetComponent<MeshRenderer>();
            _mat = RuntimeMaterial.From(_game.S != null ? _game.S.StencilMaskTemplate : null, "Project/PotaStencilMask", "BoardClipMask");
            r.sharedMaterial = _mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Mask = go.transform;
        }

        /// <summary>Puts <paramref name="root"/> and everything under it on the board layer (Default when clipping is off).</summary>
        public void Assign(GameObject root)
        {
            if (root == null) return;
            int layer = Active ? Layer : 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
