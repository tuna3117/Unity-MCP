using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Studio ammo counter (spec §3.2, §3.5): while a block is pressed its cost floats right of the counter as "−12" (red when the ammo
    /// does not cover it); during the salvo the number rolls down step by step; with the ammo short of two of the cheapest block the
    /// counter pulses red at 1 Hz (plan decision 5) and stays red while a lost round waits for its card. Off under the Pota look.
    /// </summary>
    public class AmmoCounter : MonoBehaviour
    {
        private ColorHuntHud _hud;
        private Text _old, _ghost;
        private RectMask2D _window;
        private CanvasGroup _chip;
        private Vector2 _home;
        private int _last = -1;
        private float _roll = -1f, _ghostAlpha, _ghostTarget;
        private bool _wasStudio, _wasPulsing;

        public bool Rolling => _roll >= 0f;
        public bool Pulsing { get; private set; }
        public bool GhostShort { get; private set; }
        public string GhostText => _ghost != null && _ghostTarget > 0f ? _ghost.text : "";
        /// <summary>The rolling number is clipped to its slot (studio looks only).</summary>
        public bool Clipped => _window != null && _window.enabled;
        public float ChipAlpha => _chip != null ? _chip.alpha : 0f;

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var game = hud.Game;
            var ammo = hud.AmmoText;
            if (game == null || ammo == null) return;
            game.AmmoChanged += OnAmmo;
            game.SelectionChanged += OnSelection;
            game.RoundStarted += () => { _last = -1; StopRoll(); };
            _window = RollWindow.Wrap(ammo, 12f);
            _window.enabled = game.Polish;
            _home = ammo.rectTransform.anchoredPosition;
            _old = Instantiate(ammo, ammo.transform.parent);
            _old.name = "AmmoOld";
            _old.enabled = false;
            // the cost rides a small navy chip right of the panel: gold on the bare sky did not read
            float css = ColorHuntHud.Css;
            var chipGo = new GameObject("AmmoCost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var crt = (RectTransform)chipGo.transform;
            crt.SetParent(hud.AmmoPanel, false);
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(8f * css, -4f * css);
            crt.sizeDelta = new Vector2(58f, 34f) * css;
            var bg = chipGo.GetComponent<Image>();
            bg.sprite = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(14f * css), PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold,
                Mathf.RoundToInt(2f * css), 0.18f, Mathf.RoundToInt(2f * css));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            _chip = chipGo.GetComponent<CanvasGroup>();
            _chip.alpha = 0f;
            _chip.blocksRaycasts = false;
            _chip.interactable = false;
            _ghost = Instantiate(ammo, crt);
            _ghost.name = "Cost";
            var rt = _ghost.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            _ghost.alignment = TextAnchor.MiddleCenter;
            _ghost.fontSize = Mathf.RoundToInt(22f * css);
            _ghost.text = "";
        }

        private void OnAmmo()
        {
            var game = _hud.Game;
            int v = game.DisplayedAmmo;
            if (game.Polish && _last >= 0 && v < _last)
            {
                _old.text = _last.ToString();
                _old.enabled = true;
                _roll = 0f;
            }
            _last = v;
        }

        private void OnSelection()
        {
            var game = _hud.Game;
            int sel = game.Selected;
            if (game.Studio && game.Board != null && sel >= 0 && sel < game.Board.Cells.Length)
            {
                int hp = game.Board.Cells[sel].Hp;
                GhostShort = hp > game.Board.Ammo;
                _ghost.text = "−" + hp;
                _ghost.color = GhostShort ? PotaLook.HudRed : PotaLook.HudGold;
                _ghostTarget = 1f;
            }
            else _ghostTarget = 0f;
        }

        private void Update()
        {
            if (_hud == null || _hud.Game == null || _ghost == null || _window == null) return;
            var game = _hud.Game;
            var ammo = _hud.AmmoText;
            float dt = Time.unscaledDeltaTime;
            bool studio = game.Polish;
            if (!studio && _wasStudio) { StopRoll(); ammo.color = PotaLook.HudGold; ammo.transform.localScale = Vector3.one; _ghostTarget = 0f; }
            _wasStudio = studio;
            _window.enabled = studio;
            _ghostAlpha = Mathf.MoveTowards(_ghostAlpha, _ghostTarget, dt / (_ghostTarget > _ghostAlpha ? 0.08f : 0.12f));
            _chip.alpha = _ghostAlpha;
            _chip.gameObject.SetActive(_ghostAlpha > 0.001f);
            if (!studio) { Pulsing = _wasPulsing = false; return; }
            if (_roll >= 0f)
            {
                _roll += dt / StudioCurves.RollTime;
                float u = Mathf.Min(1f, _roll);
                var r = StudioCurves.CounterRoll(u);
                float h = ammo.rectTransform.rect.height;          // the window clips the slide
                ammo.rectTransform.anchoredPosition = _home + new Vector2(0f, r.y * h);
                _old.rectTransform.anchoredPosition = _home + new Vector2(0f, r.x * h);
                var oc = _old.color;
                oc.a = 1f - u;
                _old.color = oc;
                if (_roll >= 1f) StopRoll();
            }
            int cheapest = game.Board != null ? ColorHuntRules.Cheapest(game.Board) : 0;
            Pulsing = game.Board != null
                      && ((game.Current == ColorHuntGame.State.Playing && StudioCurves.LowAmmo(game.DisplayedAmmo, cheapest))
                          || (game.Current == ColorHuntGame.State.Ended && !game.Won));
            if (Pulsing)
            {
                float pulse = StudioCurves.Pulse(Time.unscaledTime);
                ammo.color = Color.Lerp(PotaLook.HudGold, PotaLook.HudRed, 0.35f + 0.65f * pulse);
                ammo.transform.localScale = Vector3.one * (1f + 0.06f * pulse);
            }
            else if (_wasPulsing)
            {
                ammo.color = PotaLook.HudGold;
                ammo.transform.localScale = Vector3.one;
            }
            _wasPulsing = Pulsing;
        }

        private void StopRoll()
        {
            _roll = -1f;
            if (_old != null) _old.enabled = false;
            if (_hud != null && _hud.AmmoText != null) _hud.AmmoText.rectTransform.anchoredPosition = _home;
        }
    }
}
