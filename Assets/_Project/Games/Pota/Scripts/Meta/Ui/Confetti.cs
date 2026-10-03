using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>Pota's konfeti: 70 pieces (10×16 css, six colours) falling from the top over 1.6–2.8 s, swaying and spinning. Part of Pota, so it plays with the feel layer off too. Real time.</summary>
    public class Confetti : MonoBehaviour
    {
        private class Piece { public RectTransform T; public Image I; public float X, Speed, Sway, Spin, Life, Age; public bool Live; }

        private static readonly Color[] Colours =
        {
            ColorHunt.PotaLook.Hex(0xff7a1a), ColorHunt.PotaLook.Hex(0xffc83d), ColorHunt.PotaLook.Hex(0x2fb36b),
            ColorHunt.PotaLook.Hex(0x3a8dff), ColorHunt.PotaLook.Hex(0xff4d6d), Color.white,
        };

        private readonly List<Piece> _pieces = new List<Piece>();
        private RectTransform _rt;
        public int Live { get; private set; }

        public static Confetti Create(Transform canvasRoot)
        {
            var go = new GameObject("Confetti", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var c = go.AddComponent<Confetti>();
            c._rt = rt;
            return c;
        }

        public void Burst(int count = 70, int seed = 7)
        {
            var rng = new System.Random(seed);
            float css = MetaStyle.Css;
            float w = _rt.rect.width, h = _rt.rect.height;
            transform.SetAsLastSibling();
            for (int i = 0; i < count; i++)
            {
                var p = i < _pieces.Count ? _pieces[i] : New();
                p.X = ((float)rng.NextDouble() - 0.5f) * w;
                p.Life = Mathf.Lerp(1.6f, 2.8f, (float)rng.NextDouble());
                p.Speed = (h + 60f * css) / p.Life;
                p.Sway = Mathf.Lerp(10f, 30f, (float)rng.NextDouble()) * css;
                p.Spin = Mathf.Lerp(-540f, 540f, (float)rng.NextDouble());
                p.Age = -(float)rng.NextDouble() * 0.4f;
                p.Live = true;
                p.I.color = Colours[rng.Next(Colours.Length)];
                p.T.anchoredPosition = new Vector2(p.X, h * 0.5f + 20f * css);
                p.T.gameObject.SetActive(true);
            }
            for (int i = count; i < _pieces.Count; i++) { _pieces[i].Live = false; _pieces[i].T.gameObject.SetActive(false); }
        }

        private Piece New()
        {
            var go = new GameObject("Piece", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rt, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10f, 16f) * MetaStyle.Css;
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
            var p = new Piece { T = rt, I = img };
            _pieces.Add(p);
            return p;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime, h = _rt.rect.height, css = MetaStyle.Css;
            Live = 0;
            foreach (var p in _pieces)
            {
                if (!p.Live) continue;
                p.Age += dt;
                if (p.Age < 0f) { Live++; continue; }
                if (p.Age >= p.Life) { p.Live = false; p.T.gameObject.SetActive(false); continue; }
                Live++;
                float y = h * 0.5f + 20f * css - p.Speed * p.Age;
                p.T.anchoredPosition = new Vector2(p.X + Mathf.Sin(p.Age * 3f) * p.Sway, y);
                p.T.localRotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin);
                p.T.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p.Age * 5f)) * 0.8f + 0.2f, 1f, 1f);
            }
        }
    }
}
