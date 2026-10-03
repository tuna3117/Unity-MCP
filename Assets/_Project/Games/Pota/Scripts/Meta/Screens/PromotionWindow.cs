using System;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #terfi with M8: the old badge shrinks and turns away (0.25 s), the new one bursts in 0 → 1.25 → 1 (0.4 s) behind a
    /// flash of rays, the name stamps under the "Yeni lig!" ribbon, confetti (Pota's). Devam is orange. Feel off = Pota's still window.
    /// </summary>
    public class PromotionWindow : MonoBehaviour
    {
        public const float OldOut = 0.25f, NewAt = 0.2f, NewIn = 0.4f, Rays = 0.7f, NameAt = 0.5f, NameStamp = 0.25f;

        private MetaSettings _s;
        private Confetti _confetti;
        private Image _old, _new;
        private RectTransform _rays;
        private Text _name, _panel;
        private float _t = -1f;

        public MetaWindow Window { get; private set; }
        public MetaButton Continue { get; private set; }
        public float NewBadgeScale => _new.rectTransform.localScale.x;
        public bool OldBadgeVisible => _old.gameObject.activeSelf;
        public string LeagueName => _name.text;
        public event Action Done;

        public static PromotionWindow Create(Transform parent, MetaSettings s, Confetti confetti)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "PromotionWindow", "Yeni lig!", s.Lilita, s.Feel, false);
            var pw = w.gameObject.AddComponent<PromotionWindow>();
            pw._s = s; pw._confetti = confetti; pw.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var stage = MetaUiKit.Row(w.Content, "Stage", 160f);
            var rays = new GameObject("Rays", typeof(RectTransform));
            rays.transform.SetParent(stage, false);
            pw._rays = (RectTransform)rays.transform;
            pw._rays.anchorMin = pw._rays.anchorMax = pw._rays.pivot = new Vector2(0.5f, 0.5f);
            pw._rays.sizeDelta = Vector2.one * (200f * css);
            for (int i = 0; i < 12; i++)
            {
                var ray = new GameObject("Ray" + i, typeof(RectTransform), typeof(Image));
                ray.transform.SetParent(pw._rays, false);
                var rrt = (RectTransform)ray.transform;
                rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0f);
                rrt.sizeDelta = new Vector2(10f, 110f) * css; rrt.localRotation = Quaternion.Euler(0f, 0f, i * 30f);
                var rimg = ray.GetComponent<Image>(); rimg.color = new Color(1f, 0.85f, 0.35f, 0.6f); rimg.raycastTarget = false;
                rimg.sprite = MetaStyle.Panel(16, 1, new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f), Color.clear, 0, 0f, 0); rimg.type = Image.Type.Simple;
            }
            pw._old = MetaUiKit.Picture(stage, "OldBadge", s.LeagueBadges[0], 140f, new Vector2(0.5f, 0.5f), Vector2.zero);
            pw._new = MetaUiKit.Picture(stage, "NewBadge", s.LeagueBadges[1], 140f, new Vector2(0.5f, 0.5f), Vector2.zero);
            var nr = MetaUiKit.Row(w.Content, "Name", 52f);
            pw._name = MetaUiKit.GoldText(nr, "Text", s.Lilita, "", 36f);
            pw._panel = MetaUiKit.Panel(w.Content, "Panel", bold, "", 36f, 14f);
            pw.Continue = LevelWindow.Big(w.Content, "Continue", MetaButton.Tone.Orange, "Devam", s);
            pw.Continue.Clicked += () => pw.Done?.Invoke();
            w.Fit();
            return pw;
        }

        public void Open(int fromLeague, int toLeague)
        {
            _old.sprite = _s.LeagueBadges[Mathf.Clamp(fromLeague, 0, _s.LeagueBadges.Length - 1)];
            _new.sprite = _s.LeagueBadges[Mathf.Clamp(toLeague, 0, _s.LeagueBadges.Length - 1)];
            string name = _s.LeagueNames[Mathf.Clamp(toLeague, 0, _s.LeagueNames.Length - 1)];
            _name.text = name;
            _panel.text = name + "'ne yükseldin!";
            _t = 0f;
            Apply();
            Window.Show();
            _confetti?.Burst(70, 11);
        }

        private void Update()
        {
            if (_t < 0f) return;
            if (!Window.IsOpen) { _t = -1f; return; }
            _t += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (!_s.Feel.Enabled)
            {
                _old.gameObject.SetActive(false);
                _new.gameObject.SetActive(true); _new.rectTransform.localScale = Vector3.one;
                _rays.gameObject.SetActive(false);
                _name.rectTransform.localScale = Vector3.one; _name.gameObject.SetActive(true);
                return;
            }
            float uo = Mathf.Clamp01(_t / OldOut);
            _old.gameObject.SetActive(uo < 1f);
            _old.rectTransform.localScale = Vector3.one * (1f - StudioCurves.EaseOutCubic(uo));
            _old.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f * uo);
            float un = Mathf.Clamp01((_t - NewAt) / NewIn);
            _new.gameObject.SetActive(_t >= NewAt);
            _new.rectTransform.localScale = Vector3.one * (un < 0.6f ? Mathf.Lerp(0f, 1.25f, StudioCurves.EaseOutCubic(un / 0.6f)) : Mathf.Lerp(1.25f, 1f, PotaLook.Smoothstep((un - 0.6f) / 0.4f)));
            float ur = (_t - NewAt) / Rays;
            _rays.gameObject.SetActive(ur >= 0f && ur < 1f);
            if (ur >= 0f && ur < 1f)
            {
                _rays.localRotation = Quaternion.Euler(0f, 0f, ur * 40f);
                _rays.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, StudioCurves.EaseOutCubic(ur));
                foreach (Transform ray in _rays) ray.GetComponent<Image>().color = new Color(1f, 0.85f, 0.35f, 0.7f * (1f - ur));
            }
            float us = Mathf.Clamp01((_t - NameAt) / NameStamp);
            _name.gameObject.SetActive(_t >= NameAt);
            _name.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, StudioCurves.EaseOutCubic(us));
        }
    }
}
