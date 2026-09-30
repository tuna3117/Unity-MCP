using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>One piece of B's broken block in the game root's space (the hoop's: +y up, the glass at z 0.7 behind the board, the camera
    /// toward −z). Deterministic, hand-written motion (plan decision 7) so recordings replay the same.</summary>
    public struct Debris
    {
        public Vector3 P, V, W;   // position, velocity, angular velocity (degrees/s)
        public Quaternion R;
        public float Age, Life, Size;
    }

    public static class ShatterDebris
    {
        public const float Gravity = 12f, Restitution = 0.45f, Friction = 0.8f, SpinLoss = 0.7f;
        /// <summary>GiantHoop: the frame's inner opening is ±PanoSize/2 = ±4.4; the glass plane is z = 0.7 (game-root space).</summary>
        public const float InnerHalf = PotaLook.PanoSize * 0.5f, GlassZ = 0.7f;

        /// <summary>A piece leaves its block along its offset from the block's centre, a little up and toward the camera.</summary>
        public static Debris Launch(Vector3 blockCentre, Vector3 pieceOffset, float scale, System.Random rng)
        {
            Vector3 dir = pieceOffset.sqrMagnitude > 1e-8f ? pieceOffset.normalized : Vector3.up;
            float speed = (3.2f + 2.2f * (float)rng.NextDouble()) * scale;
            var v = new Vector3(dir.x, dir.y + 0.35f, 0f) * speed
                    + new Vector3(0f, 1.2f * scale, -(1.6f + 1.8f * (float)rng.NextDouble()) * scale);
            var w = new Vector3(Signed(rng), Signed(rng), Signed(rng)) * 540f;
            return new Debris
            {
                P = blockCentre + pieceOffset * scale, V = v, W = w, R = Quaternion.identity,
                Age = 0f, Life = 0.8f + 0.4f * (float)rng.NextDouble(), Size = scale,
            };
        }

        public static void Step(ref Debris d, float dt, float radius)
        {
            d.V.y -= Gravity * dt;
            d.P += d.V * dt;
            d.R = Quaternion.Euler(d.W * dt) * d.R;
            d.Age += dt;
            float lim = InnerHalf - radius;
            if (d.P.x < -lim) { d.P.x = -lim; d.V.x = -d.V.x * Restitution; Hit(ref d); }
            else if (d.P.x > lim) { d.P.x = lim; d.V.x = -d.V.x * Restitution; Hit(ref d); }
            if (d.P.y < -lim) { d.P.y = -lim; d.V.y = -d.V.y * Restitution; Hit(ref d); }
            else if (d.P.y > lim) { d.P.y = lim; d.V.y = -d.V.y * Restitution; Hit(ref d); }
            float back = GlassZ - radius;
            if (d.P.z > back) { d.P.z = back; d.V.z = -d.V.z * Restitution; Hit(ref d); }
        }

        /// <summary>Full size for 70 % of the life, then shrinks to nothing.</summary>
        public static float Shrink(float age, float life)
        {
            float u = age / Mathf.Max(1e-4f, life);
            return u < 0.7f ? 1f : Mathf.Max(0f, 1f - (u - 0.7f) / 0.3f);
        }

        private static void Hit(ref Debris d)
        {
            d.V *= Friction;
            d.W *= SpinLoss;
        }

        private static float Signed(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;
    }
}
