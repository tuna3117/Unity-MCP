using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// What a look draws at the swell peaks. The game keeps the rhythm (swell, peak, chain steps, drops), the sounds, the dips and the
    /// shake; a style only draws. Positions are board-local (BlockView.Pos space); tones are palette colours (sRGB).
    /// </summary>
    public interface IBurstStyle
    {
        /// <summary>Its assets are in place. A style that is not ready is replaced by Pota's burst, with one warning.</summary>
        bool Ready { get; }
        /// <summary>Pota's "×d" chain popup (ColorHuntJuice.Chain) shows under this look.</summary>
        bool ShowsChainNumber { get; }
        /// <summary>Live particles and pieces this style owns (BurstBudget).</summary>
        int LiveCount { get; }
        void Build(ColorHuntGame game);
        void Activate(bool on);
        /// <summary>The tapped block's burst at its swell peak.</summary>
        void TapBurst(BlockView v, Color tone);
        /// <summary>A chain block's burst at its swell peak, <paramref name="depth"/> ≥ 1.</summary>
        void StepBurst(BlockView v, Color tone, int depth);
        /// <summary>Once per chain step at its first peak: the step's board-local centroid and its first block's tone.</summary>
        void ChainPeak(int depth, Vector2 at, Color tone);
        void Clear();
    }
}
