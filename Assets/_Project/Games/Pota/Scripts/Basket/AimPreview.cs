using UnityEngine;

namespace Project.Basket
{
    /// <summary>Dashed-looking arc plus a landing ring shown while dragging; red when over-powered.</summary>
    public class AimPreview : MonoBehaviour
    {
        public int Points = 27;
        private LineRenderer _line;
        private Transform _marker;
        private MeshRenderer _markerRenderer;
        private Material _lineMat;
        private static readonly Color White = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color Red = new Color(1f, 0.35f, 0.3f, 0.95f);
        private float _power, _widthMin = 0.06f, _widthMax = 0.11f;
        private bool _over;

        /// <summary>The line's current start width (F10: thickens with the pull).</summary>
        public float Width => _line != null ? _line.startWidth : 0f;

        /// <summary>F10: pull 0..1 → the line thickens and brightens; at ≥ 0.98 the landing ring pulses.</summary>
        public void SetPower(float pull)
        {
            _power = Mathf.Clamp01(pull);
            if (_line == null) return;
            float w = Mathf.Lerp(_widthMin, _widthMax, _power);
            _line.startWidth = w;
            _line.endWidth = w * 0.65f;
            var c = _over ? Red : White;
            c.a = Mathf.Lerp(0.6f, 1f, _power);
            _lineMat.color = c;
        }

        private void Update()
        {
            if (_marker == null || !_markerRenderer.enabled) return;
            float pulse = _power >= 0.98f ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI) : 1f;
            _marker.localScale = new Vector3(0.5f * pulse, 0.01f, 0.5f * pulse);
        }

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = Points;
            _line.startWidth = 0.06f;
            _line.endWidth = 0.04f;
            _line.useWorldSpace = true;
            _line.numCapVertices = 4;
            _lineMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _lineMat.color = White;
            _line.material = _lineMat;

            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m.name = "LandingMarker";
            Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(transform, false);
            m.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            _markerRenderer = m.GetComponent<MeshRenderer>();
            _markerRenderer.sharedMaterial = _lineMat;
            _marker = m.transform;
            Hide();
        }

        public void Show(AimPoint aim, BasketSettings s)
        {
            var target = new Vector3(aim.X, aim.Y + s.ArrivalLift, 0f);
            var v = TrajectorySolver.LaunchVelocity(s.LaunchStart, target, aim.FlightTime, s.FlightGravity);
            for (int i = 0; i < Points; i++)
            {
                float t = (float)i / (Points - 1) * aim.FlightTime * 1.1f;
                _line.SetPosition(i, TrajectorySolver.PositionAt(s.LaunchStart, v, s.FlightGravity, t));
            }
            _over = aim.OverPower;
            _lineMat.color = aim.OverPower ? Red : White;
            SetPower(_power);
            _marker.position = new Vector3(aim.X, aim.Y, 0f);
            _line.enabled = true;
            _markerRenderer.enabled = true;
        }

        public void Hide()
        {
            _line.enabled = false;
            _markerRenderer.enabled = false;
        }
    }
}
