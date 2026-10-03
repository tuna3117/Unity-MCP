using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// F2 ammo tension (spec 2026-10-03): while the ammo is short of twice the cheapest block and the round is still being played, a red
    /// breath at the screen edge (1 Hz, peak Feel.TensionEdgeAlpha) joins the counter's pulse. Clears the frame the feel layer goes off
    /// or the round ends. Real time.
    /// </summary>
    public class AmmoTension : MonoBehaviour
    {
        private ColorHuntHud _hud;
        private Image _edge;

        public bool Active { get; private set; }
        public float EdgeAlpha => _edge != null && _edge.enabled ? _edge.color.a : 0f;

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var go = new GameObject("TensionEdge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            go.transform.SetAsFirstSibling();                       // under the HUD panels
            _edge = go.GetComponent<Image>();
            _edge.sprite = StudioTextures.EdgeGlowSprite();
            _edge.raycastTarget = false;
            _edge.enabled = false;
            var rt = _edge.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (_hud == null || _hud.Game == null || _edge == null) return;
            var game = _hud.Game;
            Active = game.Feel && game.Board != null && game.Current == ColorHuntGame.State.Playing
                     && StudioCurves.LowAmmo(game.DisplayedAmmo, ColorHuntRules.Cheapest(game.Board));
            if (!Active) { if (_edge.enabled) _edge.enabled = false; return; }
            var c = PotaLook.HudRed;
            c.a = game.S.Feel.TensionEdgeAlpha * FeelCurves.Breath(Time.unscaledTime, StudioCurves.PulseHz);
            _edge.color = c;
            _edge.enabled = true;
        }
    }
}
