using UnityEngine;

namespace Project.Juice
{
    /// <summary>
    /// Secondary motion for a bone chain (a hair tuft, an ear, a cap tip, a ponytail): each bone's tip follows its animated
    /// position through a damped spring with gravity, and the bone turns to point at the simulated tip. Runs in LateUpdate,
    /// after the Animator, so animation stays the base and only the lag is added.
    /// </summary>
    public class SpringBone : MonoBehaviour
    {
        [Tooltip("First bone of the chain; every child down the first-child line follows. Default: this transform.")]
        [SerializeField] private Transform root;
        [Tooltip("Pull back to the animated pose (higher = stiffer).")]
        [SerializeField, Range(0f, 1f)] private float stiffness = 0.25f;
        [Tooltip("Velocity kept per step (lower = more drag).")]
        [SerializeField, Range(0f, 1f)] private float keep = 0.8f;
        [SerializeField] private Vector3 gravity = new Vector3(0f, -2f, 0f);
        [Tooltip("Largest bend from the animated direction, degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxAngle = 45f;
        [Tooltip("Length of the last bone's tip when it has no child (m).")]
        [SerializeField, Min(0.001f)] private float endLength = 0.05f;
        [SerializeField] private bool useUnscaledTime;

        private Transform[] _bones;
        private Vector3[] _tip, _prevTip, _restDir;
        private Quaternion[] _animated, _written;
        private float[] _length;

        public int BoneCount { get { Init(); return _bones.Length; } }

        private void Init()
        {
            if (_bones != null) return;
            var start = root != null ? root : transform;
            var list = new System.Collections.Generic.List<Transform>();
            for (var t = start; t != null; t = t.childCount > 0 ? t.GetChild(0) : null) list.Add(t);
            _bones = list.ToArray();
            int n = _bones.Length;
            _tip = new Vector3[n];
            _prevTip = new Vector3[n];
            _restDir = new Vector3[n];
            _length = new float[n];
            _animated = new Quaternion[n];
            _written = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                var b = _bones[i];
                _animated[i] = _written[i] = b.localRotation;
                Vector3 local = i + 1 < n ? _bones[i + 1].localPosition : Vector3.up * endLength / Mathf.Max(1e-4f, b.lossyScale.y);
                if (local.sqrMagnitude < 1e-10f) local = Vector3.up * endLength;
                _restDir[i] = local.normalized;   // in the bone's own space
                _length[i] = b.TransformVector(local).magnitude;
                _tip[i] = _prevTip[i] = b.TransformPoint(local);
            }
        }

        /// <summary>Restarts the simulation from the current animated pose (after a teleport or a pool reuse).</summary>
        public void ResetChain()
        {
            _bones = null;
            Init();
        }

        public void Step(float dt)
        {
            Init();
            if (dt <= 0f) return;
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                // The Animator rewrites the pose every frame; a bone nobody rewrote still holds last frame's bend, so take
                // that off first, or the bend would feed back into the "animated" pose and drift.
                if (Quaternion.Angle(b.localRotation, _written[i]) < 0.01f) b.localRotation = _animated[i];
                _animated[i] = b.localRotation;
                Vector3 animatedTip = b.position + b.rotation * _restDir[i] * _length[i];
                Vector3 velocity = (_tip[i] - _prevTip[i]) * keep;
                Vector3 next = _tip[i] + velocity + gravity * dt * dt + (animatedTip - _tip[i]) * stiffness;
                // Keep the bone length and the bend limit.
                Vector3 dir = next - b.position;
                Vector3 animatedDir = animatedTip - b.position;
                if (dir.sqrMagnitude < 1e-10f) dir = animatedDir;
                float angle = Vector3.Angle(animatedDir, dir);
                if (angle > maxAngle) dir = Vector3.Slerp(animatedDir.normalized, dir.normalized, maxAngle / angle);
                next = b.position + dir.normalized * _length[i];
                _prevTip[i] = _tip[i];
                _tip[i] = next;
                b.rotation = Quaternion.FromToRotation(animatedDir, next - b.position) * b.rotation;
                _written[i] = b.localRotation;
            }
        }

        private void LateUpdate() => Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }
}
