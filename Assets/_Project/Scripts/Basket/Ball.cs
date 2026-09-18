using System;
using UnityEngine;

namespace Project.Basket
{
    public enum BallPhase { Flight, Drop, Piled, Frozen }

    /// <summary>
    /// One pooled basketball. Gravity is applied manually so flight and drop can use different values.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public BallPhase Phase { get; private set; }
        public int Id;
        public bool Counted;              // entered the cage (counts once)
        public float LastPassTime = -10f;
        public Hoop LastHoop;
        public float StuckTimer;
        public float SlowTimer;
        public float LowestY;             // lowest y reached so far (progress tracking)

        public event Action<Ball, Collision> RimContact;

        private BasketSettings _s;
        private int _layerFlight, _layerDrop;
        private TrailRenderer _trail;

        public void Configure(BasketSettings settings, int flightLayer, int dropLayer)
        {
            _s = settings;
            _layerFlight = flightLayer;
            _layerDrop = dropLayer;
            Body = GetComponent<Rigidbody>();
            Body.mass = _s.BallMass;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.05f;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 50f;
            var col = GetComponent<SphereCollider>();
            col.sharedMaterial = _s.BallMaterial;
            _trail = GetComponent<TrailRenderer>();
        }

        public void Launch(Vector3 position, Vector3 velocity)
        {
            ResetState(position);
            Phase = BallPhase.Flight;
            gameObject.layer = _layerFlight;
            Body.linearVelocity = velocity;
            Body.angularVelocity = new Vector3(-_s.Backspin, 0f, 0f);
            if (_trail != null) { _trail.Clear(); _trail.emitting = true; }
        }

        public void Drop(Vector3 position, Vector3 velocity)
        {
            ResetState(position);
            Phase = BallPhase.Drop;
            gameObject.layer = _layerDrop;
            Body.linearVelocity = velocity;
            if (_trail != null) _trail.emitting = false;
        }

        /// <summary>Flight → Drop when the ball reaches the column slab.</summary>
        public void EnterSlab()
        {
            if (Phase != BallPhase.Flight) return;
            Phase = BallPhase.Drop;
            gameObject.layer = _layerDrop;
            var v = Body.linearVelocity;
            v.z *= _s.EntryForwardDamping;
            Body.linearVelocity = v;
            if (_trail != null) _trail.emitting = false;
        }

        public void MarkPiled()
        {
            Counted = true;
            if (Phase != BallPhase.Frozen) Phase = BallPhase.Piled;
        }

        public void Freeze()
        {
            Phase = BallPhase.Frozen;
            Body.isKinematic = true;
        }

        public void Teleport(Vector3 position, Vector3 velocity)
        {
            Body.position = position;
            transform.position = position;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            StuckTimer = 0f;
            SlowTimer = 0f;
            LowestY = position.y;
        }

        private void ResetState(Vector3 position)
        {
            Body.isKinematic = false;
            Body.position = position;
            transform.position = position;
            Body.angularVelocity = Vector3.zero;
            Counted = false;
            LastPassTime = -10f;
            LastHoop = null;
            StuckTimer = 0f;
            SlowTimer = 0f;
            LowestY = position.y;
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (Phase == BallPhase.Frozen || Body == null || Body.isKinematic || _s == null) return;
            float g = Phase == BallPhase.Flight ? _s.FlightGravity : _s.DropGravity;
            Body.AddForce(Vector3.down * g, ForceMode.Acceleration);
            if (Phase == BallPhase.Flight && Body.position.z < _s.SlabEntryZ) EnterSlab();
        }

        private void OnCollisionEnter(Collision collision)
        {
            var seg = collision.collider.GetComponent<RimSegment>();
            if (seg != null && seg.Hoop != null)
            {
                seg.Hoop.OnRimContact(this, collision);
                RimContact?.Invoke(this, collision);
            }
        }
    }
}
