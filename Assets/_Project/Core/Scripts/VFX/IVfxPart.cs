namespace Project.VFX
{
    /// <summary>
    /// A non-particle piece of an effect (mesh pieces, a light flash…) that <see cref="VfxInstance"/> restarts on play,
    /// waits for before returning to the pool, and clears when the effect stops.
    /// </summary>
    public interface IVfxPart
    {
        void Restart();
        void Clear();
        bool IsAlive { get; }
    }
}
