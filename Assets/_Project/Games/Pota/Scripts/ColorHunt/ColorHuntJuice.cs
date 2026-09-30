using Project.Juice;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Feel of Pota's puzzle (renkAviFaz.ts HIS): additive shake (sars), game-time dips (zamanDusur), Pota's sounds, the "×d" and
    /// "Yetmiyor" popups. The game calls these at Pota's moments (the swell peaks, ball arrivals, landings).
    /// </summary>
    public class ColorHuntJuice : MonoBehaviour
    {
        public ColorHuntSettings S;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public PotaSounds Sounds;

        /// <summary>secimiSur: a block comes under the finger.</summary>
        public void Select() => Sounds?.Select();

        /// <summary>salvoBaslat: kuleAt.</summary>
        public void SalvoStart() => Sounds?.Salvo();

        /// <summary>topVardi: carpma (guc 0.8).</summary>
        public void BallHit() => Sounds?.Hit();

        /// <summary>salvoBitir kirilma (at the tapped block's swell peak): shake 0.08, ×0.12 for 0.05 s, kirildi.</summary>
        public void Break()
        {
            Shake?.Add(S.ShakeTap);
            TimeCtl?.Dip(S.HitStopTapScale, S.HitStopTap);
            Sounds?.Break();
        }

        /// <summary>adimBaslat adimTepe (a chain step's first peak): shake, ×0.06 dip, kombo(d), "×d" from depth 2.</summary>
        public void Chain(int depth, Vector3 at)
        {
            Shake?.Add(S.ShakeChain + (depth - 1) * S.ShakeChainPerDepth);
            TimeCtl?.Dip(S.HitStopChainScale, Mathf.Min(S.HitStopChainMax, S.HitStopChainBase + depth * S.HitStopChainPerDepth));
            Sounds?.Combo(depth);
            // index.html .patlama.kombo: gold 44 px; ≥ 4: .kombo.buyuk 62 px #ff7a3d.
            if (depth >= 2) Popups?.Show(at, $"×{depth}", depth >= 4 ? PotaLook.Hex(0xff7a3d) : PotaLook.HudGold, depth >= 4 ? 96 : 72, false, 0.9f);
        }

        /// <summary>gorselleriIlerlet: dustu from a fall of one cell or more, louder with the distance.</summary>
        public void Landed(float cells)
        {
            if (cells >= 1f) Sounds?.Drop(Mathf.Min(1f, 0.3f + cells * 0.2f));
        }

        public void Rotate()
        {
            Shake?.Add(S.ShakeRotate);
            Sounds?.Rotate();
        }

        /// <summary>dokunmaReddi: eksi and "Yetmiyor · n top" (red, rising).</summary>
        public void Insufficient(Vector3 at, int cost)
        {
            Sounds?.Minus();
            Popups?.Show(at, $"Yetmiyor · {cost} top", PotaLook.HudRed, 44);
        }

        public void Result(bool won)
        {
            if (won) { Shake?.Add(S.ShakeWin); Sounds?.Win(); }
            else Sounds?.Lose();
        }
    }
}
