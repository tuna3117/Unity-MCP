using UnityEngine;

namespace Project.Flow
{
    /// <summary>A camera pose: eye position, look-at point and vertical field of view.</summary>
    public struct CamPose
    {
        public Vector3 Pos;
        public Vector3 Look;
        public float Fov;

        public CamPose(Vector3 pos, Vector3 look, float fov)
        {
            Pos = pos;
            Look = look;
            Fov = fov;
        }

        public Vector3 Forward => (Look - Pos).normalized;

        /// <summary>Heading in degrees, atan2(x, z): 0 looks +z, ±180 looks −z.</summary>
        public float Yaw
        {
            get
            {
                var d = Look - Pos;
                return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
        }
    }

    /// <summary>
    /// The transition's camera path: Catmull-Rom through a → mid → b for the eye and, with the same timing, for the look-at
    /// point (phantom end points mirror the neighbours). Time is split by segment length so the speed has no kink at mid.
    /// FOV eases a → mid → b. Exact at u = 0 and u = 1.
    /// </summary>
    public static class CameraFlight
    {
        /// <param name="lookLead">
        /// After mid, the look point runs ahead of the eye (ease-out with this exponent): the camera turns toward b early
        /// instead of sweeping empty sky. 0 = look and eye move together.
        /// </param>
        public static CamPose Evaluate(CamPose a, CamPose mid, CamPose b, float u, float lookLead = 0f)
        {
            if (u <= 0f) return a;
            if (u >= 1f) return b;
            float split = Split(a, mid, b);
            var pos = Through(a.Pos, mid.Pos, b.Pos, u, split);
            float uLook = u;
            if (lookLead > 0f && u > split)
            {
                float s2 = (u - split) / (1f - split);
                uLook = split + (1f - split) * (1f - Mathf.Pow(1f - s2, 1f + lookLead));
            }
            var look = Through(a.Look, mid.Look, b.Look, uLook, split);
            float fov = u < split
                ? Mathf.Lerp(a.Fov, mid.Fov, Smooth(u / split))
                : Mathf.Lerp(mid.Fov, b.Fov, Smooth((u - split) / (1f - split)));
            return new CamPose(pos, look, fov);
        }

        /// <summary>The u at which the path passes mid (share of the eye's path length before mid).</summary>
        public static float Split(CamPose a, CamPose mid, CamPose b)
        {
            float la = Vector3.Distance(a.Pos, mid.Pos), lb = Vector3.Distance(mid.Pos, b.Pos);
            return la + lb > 1e-4f ? la / (la + lb) : 0.5f;
        }

        /// <summary>After arrival: push along the view by amount·sin(πs) and come back (s 0 → 1).</summary>
        public static CamPose Settle(CamPose b, float s, float amount)
        {
            var push = b.Forward * (amount * Mathf.Sin(Mathf.Clamp01(s) * Mathf.PI));
            return new CamPose(b.Pos + push, b.Look + push, b.Fov);
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float s)
        {
            float s2 = s * s, s3 = s2 * s;
            return 0.5f * (2f * p1 + (p2 - p0) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s2 + (3f * p1 - p0 - 3f * p2 + p3) * s3);
        }

        private static Vector3 Through(Vector3 a, Vector3 m, Vector3 b, float u, float split)
        {
            var before = 2f * a - m;
            var after = 2f * b - m;
            return u < split
                ? CatmullRom(before, a, m, b, u / split)
                : CatmullRom(a, m, b, after, (u - split) / (1f - split));
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);
    }
}
