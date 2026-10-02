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

        /// <summary>
        /// Cubic Hermite through every pose of <paramref name="pts"/> (eye and look alike), u split by the eye's segment lengths;
        /// FOV eases segment by segment. Tangents default to Catmull-Rom's ((next − previous) / 2, phantom ends mirror the
        /// neighbours); <paramref name="eyeTangents"/> overrides the eye's tangent at a point (candidate 2's rise through the rim
        /// is vertical at both ends, so it is a straight line and never bulges into the net). Exact at u = 0, u = 1 and every point.
        /// </summary>
        public static CamPose Path(CamPose[] pts, float u, Vector3[] eyeTangents = null)
        {
            if (pts == null || pts.Length == 0) return default;
            if (pts.Length == 1 || u <= 0f) return pts[0];
            int last = pts.Length - 1;
            if (u >= 1f) return pts[last];
            float total = 0f;
            for (int i = 0; i < last; i++) total += Vector3.Distance(pts[i].Pos, pts[i + 1].Pos);
            if (total <= 1e-5f) return pts[last];
            float target = u * total, acc = 0f;
            int seg = 0;
            for (; seg < last - 1; seg++)
            {
                float len = Vector3.Distance(pts[seg].Pos, pts[seg + 1].Pos);
                if (acc + len >= target) break;
                acc += len;
            }
            float segLen = Vector3.Distance(pts[seg].Pos, pts[seg + 1].Pos);
            float s = segLen > 1e-6f ? Mathf.Clamp01((target - acc) / segLen) : 1f;
            var a = pts[seg]; var b = pts[seg + 1];
            var pos = Hermite(a.Pos, EyeTangent(pts, seg, eyeTangents), b.Pos, EyeTangent(pts, seg + 1, eyeTangents), s);
            var look = Hermite(a.Look, LookTangent(pts, seg), b.Look, LookTangent(pts, seg + 1), s);
            return new CamPose(pos, look, Mathf.Lerp(a.Fov, b.Fov, Smooth(s)));
        }

        private static Vector3 EyeTangent(CamPose[] pts, int i, Vector3[] overrides)
        {
            if (overrides != null && i < overrides.Length && overrides[i].sqrMagnitude > 1e-8f) return overrides[i];
            int last = pts.Length - 1;
            var prev = i > 0 ? pts[i - 1].Pos : 2f * pts[0].Pos - pts[1].Pos;
            var next = i < last ? pts[i + 1].Pos : 2f * pts[last].Pos - pts[last - 1].Pos;
            return (next - prev) * 0.5f;
        }

        private static Vector3 LookTangent(CamPose[] pts, int i)
        {
            int last = pts.Length - 1;
            var prev = i > 0 ? pts[i - 1].Look : 2f * pts[0].Look - pts[1].Look;
            var next = i < last ? pts[i + 1].Look : 2f * pts[last].Look - pts[last - 1].Look;
            return (next - prev) * 0.5f;
        }

        /// <summary>Cubic Hermite from p0 (tangent m0) to p1 (tangent m1), s 0..1; with Catmull-Rom tangents it is Catmull-Rom.</summary>
        public static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float s)
        {
            float s2 = s * s, s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * p0 + (s3 - 2f * s2 + s) * m0 + (-2f * s3 + 3f * s2) * p1 + (s3 - s2) * m1;
        }

        /// <summary>The u of <see cref="Path"/> at point <paramref name="index"/> plus <paramref name="frac"/> of the next segment.</summary>
        public static float PathU(CamPose[] pts, int index, float frac = 0f)
        {
            if (pts == null || pts.Length < 2) return 0f;
            int last = pts.Length - 1;
            float total = 0f, acc = 0f;
            for (int i = 0; i < last; i++) total += Vector3.Distance(pts[i].Pos, pts[i + 1].Pos);
            if (total <= 1e-5f) return 0f;
            index = Mathf.Clamp(index, 0, last);
            for (int i = 0; i < index; i++) acc += Vector3.Distance(pts[i].Pos, pts[i + 1].Pos);
            if (index < last) acc += Mathf.Clamp01(frac) * Vector3.Distance(pts[index].Pos, pts[index + 1].Pos);
            return Mathf.Clamp01(acc / total);
        }

        /// <summary>
        /// The u on segment <paramref name="index"/> where the eye's height crosses <paramref name="y"/> (bisection; the segment's
        /// height must be monotonic, as the dive's rise is). Candidate 2 times its rim crossing with this.
        /// </summary>
        public static float PathUAtHeight(CamPose[] pts, int index, float y, Vector3[] eyeTangents = null)
        {
            if (pts == null || pts.Length < 2) return 0f;
            index = Mathf.Clamp(index, 0, pts.Length - 2);
            float lo = PathU(pts, index), hi = PathU(pts, index + 1);
            bool rising = pts[index + 1].Pos.y >= pts[index].Pos.y;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                bool above = Path(pts, mid, eyeTangents).Pos.y >= y;
                if (above == rising) hi = mid; else lo = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>
        /// Pota's kameraYonetmeni pozKaris: eye and look lerp with smoothstep applied by the caller's u, the eye rising
        /// sin(πu)·<paramref name="rise"/> in the middle, FOV lerped. Exact at the ends. Candidate 3's one sweep.
        /// </summary>
        public static CamPose Sweep(CamPose a, CamPose b, float u, float rise)
        {
            if (u <= 0f) return a;
            if (u >= 1f) return b;
            var pos = Vector3.Lerp(a.Pos, b.Pos, u);
            pos.y += Mathf.Sin(u * Mathf.PI) * rise;
            return new CamPose(pos, Vector3.Lerp(a.Look, b.Look, u), Mathf.Lerp(a.Fov, b.Fov, u));
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
