using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// A counter whose old number slides out and the new one slides in (M3; AmmoCounter's roll generalised): wraps the Text in a
    /// clip window. Feel off (or roll=false) = the text is set at once. Real time.
    /// </summary>
    public class RollingNumber : MonoBehaviour
    {
        private MetaFeelTuning _feel;
        private Text _text, _old;
        private Vector2 _home;
        private float _roll = -1f;
        private int _value;
        private string _prefix = "", _suffix = "";

        public int Value => _value;
        public bool Rolling => _roll >= 0f;
        public Text Text => _text;

        public static RollingNumber Attach(Text text, MetaFeelTuning feel, float padXCss = 12f, string prefix = "", string suffix = "")
        {
            RollWindow.Wrap(text, padXCss);
            var r = text.gameObject.AddComponent<RollingNumber>();
            r._feel = feel;
            r._text = text;
            r._prefix = prefix; r._suffix = suffix;
            r._home = text.rectTransform.anchoredPosition;        // read after the wrap (BasketHud lesson)
            r._old = Instantiate(text, text.transform.parent);
            Destroy(r._old.GetComponent<RollingNumber>());
            r._old.name = text.name + "Old";
            r._old.enabled = false;
            int.TryParse(text.text, out r._value);
            return r;
        }

        private string Format(int v) => _prefix + v + _suffix;

        /// <summary>Shows <paramref name="value"/>; with the feel layer and roll=true the change rolls, else it snaps.</summary>
        public void Set(int value, bool roll = true)
        {
            if (value == _value && _text.text == Format(value)) return;
            bool animate = roll && _feel != null && _feel.Enabled && _feel.Roll > 0f && _old != null;
            if (animate)
            {
                _old.text = Format(_value);
                _old.enabled = true;
                _roll = 0f;
            }
            _value = value;
            _text.text = Format(value);
            if (!animate) Stop();
        }

        private void Stop()
        {
            _roll = -1f;
            if (_old != null) _old.enabled = false;
            if (_text != null) _text.rectTransform.anchoredPosition = _home;
        }

        private void Update()
        {
            if (_roll < 0f) return;
            _roll += Time.unscaledDeltaTime / Mathf.Max(0.01f, _feel.Roll);
            float u = Mathf.Min(1f, _roll);
            var r = StudioCurves.CounterRoll(u);
            float h = _text.rectTransform.rect.height;
            _text.rectTransform.anchoredPosition = _home + new Vector2(0f, r.y * h);
            _old.rectTransform.anchoredPosition = _home + new Vector2(0f, r.x * h);
            var oc = _old.color; oc.a = 1f - u; _old.color = oc;
            if (_roll >= 1f) Stop();
        }
    }
}
