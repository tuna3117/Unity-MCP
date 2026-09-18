using System;
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>Marker on each rim collider so balls can find their hoop.</summary>
    public class RimSegment : MonoBehaviour
    {
        public Hoop Hoop;
    }

    /// <summary>Forwards the pass-trigger event from the child collider to the hoop.</summary>
    public class HoopTrigger : MonoBehaviour
    {
        public Hoop Hoop;
        private void OnTriggerEnter(Collider other) => Hoop?.HandleTrigger(other);
        private void OnTriggerStay(Collider other) => Hoop?.HandleTrigger(other);
    }

    /// <summary>
    /// One hoop: rim (ring of capsule colliders), backboard, pass trigger under the rim plane,
    /// effect (×N / +N), net wobble and badge.
    /// </summary>
    public class Hoop : MonoBehaviour
    {
        public HoopSpec Spec { get; private set; }
        public Transform RimRoot { get; private set; }
        public Transform NetRoot { get; private set; }
        public Transform BadgeRoot { get; private set; }
        public Vector3 Center => transform.position;

        public event Action<Hoop, Ball> Passed;
        public event Action<Hoop, Ball> RimHit;

        private BasketSettings _s;
        private float _wobbleT = -1f;
        private Vector3 _netBasePos;
        private const float PassGate = 0.3f;

        public static Hoop Build(Transform parent, HoopSpec spec, BasketSettings s, int layerHoop)
        {
            var go = new GameObject($"Hoop_{spec.Kind}{spec.Amount}_x{spec.X:0.00}_y{spec.Y:0.00}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(spec.X, spec.Y, 0f);
            go.layer = layerHoop;
            var hoop = go.AddComponent<Hoop>();
            hoop.Spec = spec;
            hoop._s = s;
            hoop.BuildRim(layerHoop);
            hoop.BuildBackboard(layerHoop);
            hoop.BuildTrigger(layerHoop);
            hoop.BuildNet();
            hoop.BuildBadge();
            return hoop;
        }

        private void BuildRim(int layer)
        {
            RimRoot = new GameObject("Rim").transform;
            RimRoot.SetParent(transform, false);
            RimRoot.gameObject.layer = layer;
            float r = Spec.Radius;
            int n = Mathf.Max(6, _s.RimSegments);
            float chord = 2f * Mathf.PI * r / n;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                var seg = new GameObject($"RimSeg{i}");
                seg.transform.SetParent(RimRoot, false);
                seg.layer = layer;
                seg.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                seg.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f); // capsule X axis along the tangent
                var cap = seg.AddComponent<CapsuleCollider>();
                cap.direction = 0;
                cap.radius = _s.RimTube;
                cap.height = chord + _s.RimTube * 2f;
                cap.sharedMaterial = _s.RimMaterial;
                seg.AddComponent<RimSegment>().Hoop = this;
            }
            var mat = _s.RimVisualMaterial != null ? _s.RimVisualMaterial : RimMeshBuilder.UrpLit(_s.RimColor, 0.7f);
            RimMeshBuilder.MeshObject("RimMesh", RimRoot, RimMeshBuilder.Torus(r, _s.RimTube * 1.15f), mat);
        }

        private void BuildBackboard(int layer)
        {
            float r = Spec.Radius;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            board.layer = layer;
            board.transform.SetParent(transform, false);
            board.transform.localPosition = new Vector3(0f, 0.38f, -r - 0.06f);
            board.transform.localScale = new Vector3(1.05f, 0.78f, 0.05f);
            board.GetComponent<BoxCollider>().sharedMaterial = _s.BackboardMaterial;
            var boardRenderer = board.GetComponent<MeshRenderer>();
            if (_s.BackboardTexture != null)
            {
                boardRenderer.enabled = false;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "BackboardArt";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                quad.transform.localPosition = new Vector3(0f, 0.38f, -r - 0.03f);
                quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                quad.transform.localScale = new Vector3(1.05f, 0.82f, 1f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", _s.BackboardTexture);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Smoothness", 0.45f);
                mat.EnableKeyword("_ALPHATEST_ON");
                quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            else
            {
                boardRenderer.sharedMaterial = _s.BackboardVisualMaterial != null
                    ? _s.BackboardVisualMaterial : RimMeshBuilder.UrpLit(new Color(0.97f, 0.96f, 0.92f), 0.4f);
            }

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.layer = layer;
            Destroy(arm.GetComponent<Collider>());
            arm.transform.SetParent(transform, false);
            arm.transform.localPosition = new Vector3(0f, 0f, -r - 0.02f);
            arm.transform.localScale = new Vector3(0.08f, 0.08f, 0.16f);
            arm.GetComponent<MeshRenderer>().sharedMaterial = RimMeshBuilder.UrpLit(_s.RimColor * 0.8f, 0.5f);
        }

        private void BuildTrigger(int layer)
        {
            var trig = new GameObject("PassTrigger");
            trig.transform.SetParent(transform, false);
            trig.layer = layer;
            trig.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Spec.Radius * 2f, 0.40f, Spec.Radius * 2f);
            trig.AddComponent<HoopTrigger>().Hoop = this;
        }

        private void BuildNet()
        {
            NetRoot = new GameObject("Net").transform;
            NetRoot.SetParent(transform, false);
            _netBasePos = NetRoot.localPosition;
            var mat = _s.NetVisualMaterial != null ? _s.NetVisualMaterial : RimMeshBuilder.UrpLit(new Color(0.97f, 0.97f, 0.97f), 0.25f);
            NetBuilder.Build(NetRoot, Spec.Radius, mat);
        }

        private void BuildBadge()
        {
            BadgeRoot = new GameObject("Badge").transform;
            BadgeRoot.SetParent(transform, false);
            BadgeRoot.localPosition = new Vector3(0f, 1.05f, 0.2f);
            BadgeRoot.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera (which looks toward -z)
            bool multiply = Spec.Kind == HoopEffectKind.Multiply;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(BadgeRoot, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            plate.transform.localScale = new Vector3(0.9f, 0.45f, 0.06f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = RimMeshBuilder.UrpLit(multiply ? _s.GoldColor : _s.GreenColor, 0.6f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(BadgeRoot, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = multiply ? $"x{Spec.Amount}" : $"+{Spec.Amount}";
            tm.fontSize = 64;
            tm.characterSize = 0.075f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = multiply ? _s.NavyColor : Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                textGo.GetComponent<MeshRenderer>().sharedMaterial = DepthTextMaterial.Create(font);
            }
        }

        internal void HandleTrigger(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null) return;
            var ball = body.GetComponent<Ball>();
            if (ball == null || ball.Phase == BallPhase.Frozen) return;
            if (ball.Body.linearVelocity.y >= 0f) return;
            float y = ball.transform.position.y;
            // Only a ball that was above the rim plane before this physics step and is at/below it now counts
            // (clones and bonus balls are born below the plane, so they never re-pass their own hoop).
            if (!(ball.PrevY > Center.y && y <= Center.y)) return;
            var d = ball.transform.position - Center;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            if (horizontal >= Spec.Radius - _s.BallRadius * 0.5f) return;
            if (ball.LastHoop == this && Time.time - ball.LastPassTime < PassGate) return;
            ball.LastPassTime = Time.time;
            ball.LastHoop = this;
            Passed?.Invoke(this, ball);
        }

        /// <summary>Friendly rim: a ball touching the inner rim while dropping rolls inward instead of popping out.</summary>
        public void OnRimContact(Ball ball, Collision collision)
        {
            var v = ball.Body.linearVelocity;
            var d = ball.transform.position - Center;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            bool inside = _s.FriendlyRim && horizontal < Spec.Radius && v.y < 0f && ball.transform.position.y > Center.y - _s.BallRadius;
            if (inside)
            {
                var toCenter = new Vector3(-d.x, 0f, -d.z);
                toCenter = toCenter.sqrMagnitude > 1e-6f ? toCenter.normalized : Vector3.zero;
                float inward = Mathf.Max(new Vector2(v.x, v.z).magnitude * 0.5f, _s.RimAssistMinInward);
                v = new Vector3(toCenter.x * inward, Mathf.Min(v.y, 0f), toCenter.z * inward);
            }
            else if (v.y > _s.RimUpSpeedCap)
            {
                v.y = _s.RimUpSpeedCap;
            }
            ball.Body.linearVelocity = v;
            RimHit?.Invoke(this, ball);
        }

        public void Wobble() => _wobbleT = 0f;

        private void Update()
        {
            if (_wobbleT < 0f || NetRoot == null) return;
            _wobbleT += Time.deltaTime;
            float fade = 1f - _wobbleT / 0.55f;
            if (fade <= 0f)
            {
                NetRoot.localScale = Vector3.one;
                NetRoot.localPosition = _netBasePos;
                _wobbleT = -1f;
                return;
            }
            float t = _wobbleT;
            NetRoot.localScale = new Vector3(1f + Mathf.Sin(t * 28f) * 0.12f * fade, 1f + Mathf.Sin(t * 22f) * 0.25f * fade, 1f + Mathf.Sin(t * 28f) * 0.12f * fade);
            NetRoot.localPosition = _netBasePos + Vector3.down * (Mathf.Abs(Mathf.Sin(t * 18f)) * 0.06f * fade);
        }
    }
}
