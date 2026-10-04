using UnityEngine;

namespace Project.VFX
{
    /// <summary>
    /// Plays every effect of the scene's VfxService library in turn, through the one-line API, and names it on screen:
    /// a quick way to look at a game's (or the studio's) effects together. Dev/showcase scenes only.
    /// </summary>
    public class VfxShowcase : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float interval = 1.4f;
        [Tooltip("Optional: only ids starting with this.")]
        [SerializeField] private string prefix = "";

        private int _index = -1;
        private float _next;
        private string _current = "";

        public string Current => _current;

        private void Update()
        {
            var service = VfxService.Instance;
            if (service == null || service.Library == null || Time.time < _next) return;
            var effects = service.Library.effects;
            for (int tries = 0; tries < effects.Count; tries++)
            {
                _index = (_index + 1) % effects.Count;
                var def = effects[_index];
                if (def == null || !def.id.StartsWith(prefix) || !service.Has(def.id)) continue;
                _current = def.id;
                VfxService.Play(def.id, transform.position, transform.rotation);
                break;
            }
            _next = Time.time + interval;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_current)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.035f), fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 0.8f, 0.25f);
            GUI.Label(new Rect(Screen.width * 0.05f, Screen.height * 0.04f, Screen.width * 0.9f, Screen.height * 0.08f), _current, style);
        }
    }
}
