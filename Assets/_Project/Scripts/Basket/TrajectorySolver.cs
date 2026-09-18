using UnityEngine;

namespace Project.Basket
{
    /// <summary>Fixed-flight-time parabola under constant gravity (y down).</summary>
    public static class TrajectorySolver
    {
        /// <summary>Velocity that carries a body from start to target in exactly flightTime seconds.</summary>
        public static Vector3 LaunchVelocity(Vector3 start, Vector3 target, float flightTime, float gravity)
        {
            float t = Mathf.Max(0.01f, flightTime);
            var v = (target - start) / t;
            v.y += 0.5f * gravity * t;
            return v;
        }

        public static Vector3 PositionAt(Vector3 start, Vector3 velocity, float gravity, float t)
        {
            var p = start + velocity * t;
            p.y -= 0.5f * gravity * t * t;
            return p;
        }

        public static float ApexHeight(Vector3 start, Vector3 velocity, float gravity)
        {
            if (velocity.y <= 0f || gravity <= 0f) return start.y;
            return start.y + velocity.y * velocity.y / (2f * gravity);
        }
    }
}
