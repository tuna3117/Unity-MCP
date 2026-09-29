using Project.Juice;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Fits the board (plus launcher strip and HUD bands) into the view whatever the aspect, and applies shake.</summary>
    [RequireComponent(typeof(Camera))]
    public class ColorHuntCamera : MonoBehaviour
    {
        public ColorHuntSettings S;
        public float TopBand = 1.25f;
        public float BottomBand = 1.55f;
        public float SidePad = 0.25f;
        [Tooltip("World position of the board's game root (PotaFlow places the board away from the origin).")]
        public Vector3 Anchor;

        private Camera _cam;
        private CameraShake _shake;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        private void LateUpdate()
        {
            var pos = FitFor(_cam.fieldOfView, _cam.aspect);
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.rotation = Quaternion.identity;
        }

        /// <summary>This camera's fit position with its own bands (valid while the component is disabled).</summary>
        public Vector3 FitFor(float fov, float aspect) =>
            FitPosition(S != null ? S.Size : 7, fov, aspect, Anchor, TopBand, BottomBand, SidePad);

        /// <summary>
        /// Camera position (looking +z) that fits an n×n board plus the launcher strip and HUD bands at this FOV and aspect,
        /// for a board whose game root sits at <paramref name="anchor"/>.
        /// </summary>
        public static Vector3 FitPosition(int n, float fov, float aspect, Vector3 anchor,
            float topBand = 1.25f, float bottomBand = 1.55f, float sidePad = 0.25f)
        {
            float halfW = n * 0.5f + BoardBuilder.Margin + sidePad;
            float top = n * 0.5f + BoardBuilder.Margin + topBand;
            float bottom = -(n * 0.5f + BoardBuilder.Margin + bottomBand);
            float halfH = (top - bottom) * 0.5f;
            float centerY = (top + bottom) * 0.5f;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max(halfW / (tan * aspect), halfH / tan) + 0.6f;
            return anchor + new Vector3(0f, centerY, -dist);
        }
    }
}
