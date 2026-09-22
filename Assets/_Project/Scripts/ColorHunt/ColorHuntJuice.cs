using Project.Basket;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Feel layer: camera shake, hit-stop, popups and synth sounds, reusing the basket round's helpers.</summary>
    public class ColorHuntJuice : MonoBehaviour
    {
        public ColorHuntSettings S;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public SynthSfx Sfx;

        public void BallHit()
        {
            Shake?.Kick(S.ShakeBallHit);
            Sfx?.Play("rim", Random.Range(1.3f, 1.7f), 0.35f);
        }

        public void Tap(Vector3 pos, int cost)
        {
            Shake?.Kick(S.ShakeTap);
            TimeCtl?.HitStop(S.HitStopTap);
            Sfx?.Play("shot", 1.1f, 0.9f);
            Popups?.Show(pos + Vector3.up * 0.3f, $"-{cost}", S.GoldColor, 52);
        }

        public void Chain(int depth, Vector3 pos)
        {
            Shake?.Kick(S.ShakeChain + (depth - 1) * S.ShakeChainPerDepth);
            TimeCtl?.HitStop(Mathf.Min(S.HitStopChainMax, S.HitStopChainBase + depth * S.HitStopChainPerDepth));
            Sfx?.Play("multiply", 1f + depth * 0.18f, 0.9f);
            Sfx?.Play("add", 1f + depth * 0.1f, 0.5f);
            if (depth >= 2) Popups?.Show(pos + Vector3.up * 0.5f, $"x{depth}", depth >= 4 ? S.RedColor : S.GoldColor, depth >= 4 ? 96 : 72, false, 1f);
        }

        public void Landed(float distance)
        {
            Sfx?.Play("land", Random.Range(0.9f, 1.1f), Mathf.Min(1f, 0.3f + distance * 0.2f));
        }

        public void Rotate()
        {
            Shake?.Kick(0.05f);
            Sfx?.Play("swish", 1.2f, 0.6f);
        }

        public void Insufficient(Vector3 pos, int cost)
        {
            Sfx?.Play("miss", 1.4f, 0.6f);
            Popups?.Show(pos + Vector3.up * 0.55f, $"{cost} top gerek", S.RedColor, 44);
        }

        public void Select()
        {
            Sfx?.Play("rim", 2.2f, 0.12f);
        }

        public void Result(bool won)
        {
            if (won) { Shake?.Kick(S.ShakeWin); Sfx?.Play("add", 1.2f, 1f); Sfx?.Play("swish", 1f, 1f); }
            else Sfx?.Play("miss", 0.8f, 1f);
        }
    }
}
