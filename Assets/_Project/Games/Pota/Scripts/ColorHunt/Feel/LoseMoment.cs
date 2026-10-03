using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// F9 lose moment (spec 2026-10-03): with "Cephane bitti" the board fades to Feel.LoseDim over LoseDimTime, the camera pulls back
    /// LoseCamBack, every unfinished target row pulses red with an "N kaldı" label under it, and when the total left is at most
    /// Feel.NearMissMax a shaking "Kıl payı!" sits over the board. The result card follows after Feel.LoseHold. Real time.
    /// </summary>
    public class LoseMoment : MonoBehaviour
    {
        private class RowFx { public int Color; public Text Label; public Image Icon; public Text Count; public int Left; }

        private const float NearMissInTime = 0.25f;

        private ColorHuntHud _hud;
        private ColorHuntGame _game;
        private ColorHuntCamera _cam;
        private readonly List<RowFx> _rows = new List<RowFx>();
        private readonly Stack<Text> _labelPool = new Stack<Text>();
        private Text _nearMiss;
        private float _t = -1f;

        public bool Playing => _t >= 0f;
        public bool NearMiss { get; private set; }
        public string RemainingLabel(int color)
        {
            foreach (var r in _rows) if (r.Color == color) return r.Label.text;
            return null;
        }

        public void Init(ColorHuntHud hud, ColorHuntGame game)
        {
            _hud = hud;
            _game = game;
            float css = ColorHuntHud.Css;
            var go = new GameObject("NearMiss", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
            go.transform.SetParent(hud.transform, false);
            _nearMiss = go.GetComponent<Text>();
            _nearMiss.font = hud.HudFont;
            _nearMiss.fontSize = Mathf.RoundToInt(44f * css);
            _nearMiss.alignment = TextAnchor.MiddleCenter;
            _nearMiss.color = PotaLook.HudRed;
            _nearMiss.raycastTarget = false;
            _nearMiss.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nearMiss.text = "Kıl payı!";
            var outline = go.GetComponent<Outline>();
            outline.effectColor = PotaLook.HudNavy;
            outline.effectDistance = new Vector2(3f, -3f) * css;
            Shadow shadow = null;
            foreach (var sh in go.GetComponents<Shadow>()) if (sh.GetType() == typeof(Shadow)) shadow = sh;
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -5f) * css;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 120f) * css;
            rt.anchoredPosition = new Vector2(0f, 120f * css);
            go.SetActive(false);
            game.RoundStarted += Reset;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.RoundStarted -= Reset;
        }

        public void Play(string reason)
        {
            if (_game == null || !_game.Feel || _game.Board == null) return;
            Reset();
            _cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            _t = 0f;
            int total = 0;
            float css = ColorHuntHud.Css;
            foreach (var t in _game.Board.Targets)
            {
                int left = Mathf.Max(0, t.Remaining);
                total += left;
                if (left == 0 || !_hud.TryTarget(t.Color, out var icon, out var count, out _)) continue;
                var label = _labelPool.Count > 0 ? _labelPool.Pop() : NewLabel();
                label.text = left + " kaldı";
                var p = _hud.CanvasPointOf(icon.rectTransform);
                label.rectTransform.anchoredPosition = new Vector2(p.x + 18f * css, p.y - 36f * css);
                label.gameObject.SetActive(true);
                _rows.Add(new RowFx { Color = t.Color, Label = label, Icon = icon, Count = count, Left = left });
            }
            NearMiss = total > 0 && total <= _game.S.Feel.NearMissMax;
            if (NearMiss) { _nearMiss.gameObject.SetActive(true); _nearMiss.rectTransform.localScale = Vector3.zero; }
        }

        public void Reset()
        {
            bool was = _t >= 0f;
            _t = -1f;
            NearMiss = false;
            foreach (var r in _rows)
            {
                r.Label.gameObject.SetActive(false);
                _labelPool.Push(r.Label);
                if (r.Count != null) r.Count.color = PotaLook.HudGold;
                if (r.Icon != null) r.Icon.rectTransform.localScale = Vector3.one;
            }
            _rows.Clear();
            if (_nearMiss != null) _nearMiss.gameObject.SetActive(false);
            if (was) Restore();
        }

        private void Restore()
        {
            if (_cam != null) _cam.BackOffset = 0f;
            if (_game == null || _game.Board == null) return;
            for (int i = 0; i < _game.Board.Cells.Length; i++) { var v = _game.ViewAt(i); if (v != null) v.Fade = 1f; }
        }

        private Text NewLabel()
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject("Remaining", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(_hud.transform, false);
            var t = go.GetComponent<Text>();
            t.font = _hud.HudFont;
            t.fontSize = Mathf.RoundToInt(15f * css);
            t.alignment = TextAnchor.MiddleCenter;
            t.color = PotaLook.HudRed;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = PotaLook.HudNavy;
            outline.effectDistance = new Vector2(1.5f, -1.5f) * css;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 24f) * css;
            return t;
        }

        private void Update()
        {
            if (_t < 0f || _game == null) return;
            if (!_game.Feel) { Reset(); return; }
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            var feel = _game.S.Feel;
            float u = Mathf.Clamp01(_t / Mathf.Max(0.01f, feel.LoseDimTime));
            float k = PotaLook.Smoothstep(u);
            float fade = Mathf.Lerp(1f, feel.LoseDim, k);
            for (int i = 0; i < _game.Board.Cells.Length; i++) { var v = _game.ViewAt(i); if (v != null) v.Fade = fade; }
            if (_cam != null) _cam.BackOffset = feel.LoseCamBack * k;
            float pulse = FeelCurves.Breath(_t, StudioCurves.PulseHz);
            foreach (var r in _rows)
            {
                r.Count.color = Color.Lerp(PotaLook.HudGold, PotaLook.HudRed, 0.35f + 0.65f * pulse);
                r.Icon.rectTransform.localScale = Vector3.one * (1f + 0.08f * pulse);
                var c = r.Label.color; c.a = 0.7f + 0.3f * pulse; r.Label.color = c;
            }
            if (NearMiss)
            {
                float nu = Mathf.Clamp01(_t / NearMissInTime);
                _nearMiss.rectTransform.localScale = Vector3.one * FeelCurves.RibbonScale(nu);
                float shake = nu >= 1f ? Mathf.Sin(_t * 28f) * 3f * Mathf.Max(0f, 1f - (_t - NearMissInTime) / 0.5f) : 0f;
                _nearMiss.rectTransform.localRotation = Quaternion.Euler(0f, 0f, shake);
            }
        }
    }
}
