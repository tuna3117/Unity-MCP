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

        private Camera _cam;
        private CameraShake _shake;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        private void LateUpdate()
        {
            int n = S != null ? S.Size : 7;
            float halfW = n * 0.5f + BoardBuilder.Margin + SidePad;
            float top = n * 0.5f + BoardBuilder.Margin + TopBand;
            float bottom = -(n * 0.5f + BoardBuilder.Margin + BottomBand);
            float halfH = (top - bottom) * 0.5f;
            float centerY = (top + bottom) * 0.5f;
            float tan = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max(halfW / (tan * _cam.aspect), halfH / tan) + 0.6f;
            var pos = new Vector3(0f, centerY, -dist);
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.rotation = Quaternion.identity;
        }
    }
}
