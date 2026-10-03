using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// F1 level intro (spec 2026-10-03): a navy-gold "BÖLÜM N" ribbon drops in, the goal chips pop under it, fly to their target rows
    /// (CollectFlight's bow and arrival) and the ribbon leaves; the game's block cascade starts at Feel.IntroBoardAt. Standalone
    /// (NewRound) plays the whole sequence on the game's IntroStarted; PotaFlow drives ShowRibbon / FlyGoals itself. Real time.
    /// </summary>
    public class LevelIntro : MonoBehaviour
    {
        private class Chip { public RectTransform T; public CanvasGroup Group; public int Color; public Vector2 From, To; public float Side; public bool Flying, Arrived; }

        private const float RibbonInTime = 0.3f, RibbonOutTime = 0.25f, ChipPopTime = 0.22f, ChipStagger = 0.06f, ChipPopAt = 0.2f;
        private const float ChipFlyTime = 0.35f, ChipFlyStagger = 0.05f, RibbonYCss = 170f, RibbonExitCss = 300f;

        private ColorHuntHud _hud;
        private RectTransform _ribbon;
        private CanvasGroup _ribbonGroup;
        private Text _title;
        private readonly List<Chip> _chips = new List<Chip>();
        private readonly Stack<Chip> _pool = new Stack<Chip>();
        private Sprite _face;
        private float _t = -1f, _speed = 1f, _flyAt = float.PositiveInfinity, _ribbonOutAt = float.PositiveInfinity, _ribbonOut = -1f;
        private bool _manual;

        public bool Done => _t < 0f;
        public bool RibbonVisible => _ribbon != null && _ribbon.gameObject.activeSelf;
        public string Title => _title != null ? _title.text : "";
        /// <summary>Goal chips that reached their row in the current or last intro.</summary>
        public int GoalsArrived { get; private set; }

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            float css = ColorHuntHud.Css;
            _face = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(18f * css), PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold,
                Mathf.RoundToInt(3f * css), 0.18f, Mathf.RoundToInt(2f * css));
            var go = new GameObject("LevelRibbon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            go.transform.SetSiblingIndex(1);                        // under the HUD panels (the tension edge is first)
            _ribbon = (RectTransform)go.transform;
            _ribbon.anchorMin = _ribbon.anchorMax = new Vector2(0.5f, 1f);
            _ribbon.pivot = new Vector2(0.5f, 0.5f);
            _ribbon.sizeDelta = new Vector2(260f, 56f) * css;
            _ribbonGroup = go.GetComponent<CanvasGroup>();
            _ribbonGroup.blocksRaycasts = false;
            _ribbonGroup.interactable = false;
            var img = go.GetComponent<Image>();
            img.sprite = _face;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(Outline));
            titleGo.transform.SetParent(_ribbon, false);
            _title = titleGo.GetComponent<Text>();
            _title.font = hud.HudFont;
            _title.fontSize = Mathf.RoundToInt(30f * css);
            _title.alignment = TextAnchor.MiddleCenter;
            _title.color = PotaLook.HudGold;
            _title.raycastTarget = false;
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleGo.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.35f);
            titleGo.GetComponent<Outline>().effectDistance = new Vector2(0f, -2f) * css;
            var trt = (RectTransform)titleGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            go.SetActive(false);
            if (hud.Game != null)
            {
                hud.Game.IntroStarted += Play;
                hud.Game.IntroSkipped += Skip;
                hud.Game.RoundStarted += Reset;
            }
        }

        private void OnDestroy()
        {
            if (_hud == null || _hud.Game == null) return;
            _hud.Game.IntroStarted -= Play;
            _hud.Game.IntroSkipped -= Skip;
            _hud.Game.RoundStarted -= Reset;
        }

        /// <summary>Standalone: ribbon, chips, flight at Feel.IntroGoalFlyAt, ribbon out IntroRibbonHold later.</summary>
        public void Play()
        {
            if (_hud == null || _hud.Game == null || !_hud.Game.Feel) return;
            var feel = _hud.Game.S.Feel;
            Begin(false);
            _flyAt = feel.IntroGoalFlyAt;
            _ribbonOutAt = feel.IntroGoalFlyAt + feel.IntroRibbonHold;
        }

        /// <summary>PotaFlow: the ribbon and chips only; they wait for <see cref="FlyGoals"/>.</summary>
        public void ShowRibbon()
        {
            if (_hud == null || _hud.Game == null || !_hud.Game.Feel) return;
            Begin(true);
        }

        /// <summary>PotaFlow: the chips leave for their rows now; the ribbon follows IntroRibbonHold later.</summary>
        public void FlyGoals()
        {
            if (_t < 0f) return;
            _flyAt = Mathf.Min(_flyAt, _t);
            _ribbonOutAt = Mathf.Min(_ribbonOutAt, _t + _hud.Game.S.Feel.IntroRibbonHold);
        }

        /// <summary>Compresses what is left into Feel.IntroSkipTime (the game compresses its cascade the same way).</summary>
        public void Skip()
        {
            if (_t < 0f) return;
            float end = EndTime();
            float remaining = Mathf.Max(0.01f, end - _t);
            _speed = Mathf.Max(_speed, remaining / Mathf.Max(0.01f, _hud.Game.S.Feel.IntroSkipTime));
        }

        /// <summary>Drops any running intro and shows the rows (a new round rebuilt them anyway).</summary>
        public void Reset()
        {
            _t = -1f;
            _speed = 1f;
            _ribbonOut = -1f;
            _flyAt = _ribbonOutAt = float.PositiveInfinity;
            foreach (var c in _chips) { c.T.gameObject.SetActive(false); _pool.Push(c); }
            _chips.Clear();
            if (_ribbon != null) _ribbon.gameObject.SetActive(false);
        }

        private void Begin(bool manual)
        {
            Reset();
            _manual = manual;
            var game = _hud.Game;
            _t = 0f;
            GoalsArrived = 0;
            _title.text = "BÖLÜM " + game.S.LevelNumber;
            float css = ColorHuntHud.Css;
            float top = Mathf.Max(PotaLook.HudTopPadCss, ColorHuntCamera.SafeTopCss()) * css;
            _ribbon.anchoredPosition = new Vector2(0f, -top - RibbonYCss * css);
            _ribbon.localScale = Vector3.zero;
            _ribbonGroup.alpha = 1f;
            _ribbon.gameObject.SetActive(true);
            if (game.Board == null) return;
            int count = game.Board.Targets.Length;
            float pitch = 84f * css, x0 = -(count - 1) * 0.5f * pitch;
            for (int i = 0; i < count; i++)
            {
                var t = game.Board.Targets[i];
                var chip = _pool.Count > 0 ? _pool.Pop() : NewChip();
                chip.Color = t.Color;
                chip.Flying = chip.Arrived = false;
                chip.From = new Vector2(x0 + i * pitch, _ribbon.anchoredPosition.y - 70f * css);
                chip.T.anchoredPosition = chip.From;
                chip.T.localScale = Vector3.zero;
                chip.Group.alpha = 1f;
                var icon = chip.T.Find("Icon").GetComponent<Image>();
                var tex = game.S.ItemIcon(t.Color);
                icon.sprite = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
                icon.enabled = icon.sprite != null;
                chip.T.Find("Count").GetComponent<Text>().text = game.ShownTarget(t.Color).ToString();
                chip.T.gameObject.SetActive(true);
                _chips.Add(chip);
                _hud.SetRowShown(t.Color, false);
            }
        }

        private Chip NewChip()
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject("GoalChip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(_hud.transform, false);
            go.transform.SetSiblingIndex(2);                        // under the HUD panels, over the ribbon
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 84f) * css;
            var bg = go.GetComponent<Image>();
            bg.sprite = _face;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
            iconGo.transform.SetParent(rt, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -8f * css);
            irt.sizeDelta = Vector2.one * (44f * css);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var sh = iconGo.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
            sh.effectDistance = new Vector2(0f, -2f * css);
            var countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(rt, false);
            var crt = (RectTransform)countGo.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 4f * css);
            crt.sizeDelta = new Vector2(64f, 26f) * css;
            var count = countGo.GetComponent<Text>();
            count.font = _hud.HudFont;
            count.fontSize = Mathf.RoundToInt(22f * css);
            count.alignment = TextAnchor.MiddleCenter;
            count.color = PotaLook.HudGold;
            count.raycastTarget = false;
            return new Chip { T = rt, Group = go.GetComponent<CanvasGroup>() };
        }

        private float EndTime()
        {
            float chips = _flyAt + ChipFlyStagger * Mathf.Max(0, _chips.Count - 1) + ChipFlyTime;
            float ribbon = _ribbonOutAt + RibbonOutTime;
            return Mathf.Max(chips, ribbon);
        }

        private void Update()
        {
            if (_t < 0f || _hud == null || _hud.Game == null) return;
            if (!_hud.Game.Feel) { Reset(); ShowAllRows(); return; }
            float dt = Time.unscaledDeltaTime * _speed;
            _t += dt;
            float css = ColorHuntHud.Css;
            // ribbon in, then out
            if (_t < _ribbonOutAt)
            {
                _ribbon.localScale = Vector3.one * FeelCurves.RibbonScale(_t / RibbonInTime);
            }
            else
            {
                float u = Mathf.Clamp01((_t - _ribbonOutAt) / RibbonOutTime);
                float k = u * u;
                _ribbon.localScale = Vector3.one;
                float top = Mathf.Max(PotaLook.HudTopPadCss, ColorHuntCamera.SafeTopCss()) * css;
                _ribbon.anchoredPosition = new Vector2(0f, -top - RibbonYCss * css + RibbonExitCss * css * k);
                _ribbonGroup.alpha = 1f - u;
                if (u >= 1f && _ribbon.gameObject.activeSelf) _ribbon.gameObject.SetActive(false);
            }
            // chips pop, then fly
            for (int i = 0; i < _chips.Count; i++)
            {
                var c = _chips[i];
                if (c.Arrived) continue;
                float flyStart = _flyAt + i * ChipFlyStagger;
                if (_t < flyStart)
                {
                    float pu = (_t - ChipPopAt - i * ChipStagger) / ChipPopTime;
                    c.T.localScale = Vector3.one * FeelCurves.RibbonScale(pu);
                    continue;
                }
                if (!c.Flying)
                {
                    c.Flying = true;
                    if (_hud.TryTarget(c.Color, out var icon, out _, out _)) c.To = _hud.CanvasPointOf(icon.rectTransform);
                    else c.To = c.From;
                    c.Side = StudioCurves.FitSide(c.From, c.To, StudioCurves.Side(i), ((RectTransform)_hud.transform).rect.size * 0.5f, 60f * css);
                }
                float fu = Mathf.Clamp01((_t - flyStart) / ChipFlyTime);
                c.T.anchoredPosition = StudioCurves.FlightPoint(c.From, c.To, c.Side, fu);
                c.T.localScale = Vector3.one * Mathf.Lerp(1f, StudioCurves.ArrivalScale, fu);
                c.Group.alpha = 1f - 0.5f * fu;
                if (fu >= 1f) Arrive(c);
            }
            if (_t >= EndTime()) Finish();
        }

        private void Arrive(Chip c)
        {
            c.Arrived = true;
            GoalsArrived++;
            c.T.gameObject.SetActive(false);
            _hud.SetRowShown(c.Color, true);
            _hud.PopRow(c.Color);
            _hud.Collect?.ArrivalRing(c.Color);
        }

        private void Finish()
        {
            ShowAllRows();
            foreach (var c in _chips) { c.T.gameObject.SetActive(false); _pool.Push(c); }
            _chips.Clear();
            _ribbon.gameObject.SetActive(false);
            _t = -1f;
            _speed = 1f;
        }

        private void ShowAllRows()
        {
            if (_hud.Game.Board == null) return;
            foreach (var t in _hud.Game.Board.Targets) _hud.SetRowShown(t.Color, true);
        }
    }
}
