using UnityEngine;

namespace Project.Flow
{
    /// <summary>Where the transition's balls go: the hero's basket (A) or, in principle, the giant hoop's net.</summary>
    public interface IBallReceiver
    {
        /// <summary>World point where ball i's arc ends (it arrives from straight above).</summary>
        Vector3 EntryPoint(int i);

        /// <summary>World resting place of ball i, for i below <see cref="RestingCapacity"/>.</summary>
        Vector3 SlotPosition(int i);

        /// <summary>Where balls beyond the resting ones disappear into the pile.</summary>
        Vector3 SinkPoint { get; }

        int RestingCapacity { get; }

        /// <summary>A ball has arrived (pile count, bounce).</summary>
        void AddBall();
    }

    /// <summary>Deterministic 0..1 noise per (ball, channel), so every replay of a transition looks the same.</summary>
    public static class FlowHash
    {
        public static float Get(int i, int k)
        {
            unchecked
            {
                uint h = (uint)(i * 73856093) ^ (uint)(k * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }
    }
}
