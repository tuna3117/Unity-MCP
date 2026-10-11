using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// The special-balls showcase (Scenes/Specials.unity, spec §1.3): a row of scenario buttons (Doğuş, Çizgi, Bomba, Zincir, Altın,
    /// Serbest, Bekleme, Pas, Pas→Bomba), a row of look buttons (Pota | 1 | 2) with "Tekrar", a row of idle buttons (Bekleme: Görünüm · A ·
    /// B · C · D · E, spec 2026-10-07), a pass row (Pas: A Pas · B Pervane, spec 2026-10-11) and a label (look · scenario · idle · pass ·
    /// golden balls in the hoop), bottom-left,
    /// clear of the rotate arrows. Play stages the scenario's preset and, when auto, taps its script one move at a time, each after the
    /// board has settled plus <see cref="Beat"/> (game time, so the frame recorder can step it). Built in code at runtime in ColorHuntHud's
    /// style: listeners added in Edit Mode are not saved with a scene.
    /// </summary>
    public class SpecialsDemo : MonoBehaviour
    {
        public ColorHuntGame Game;
        [Tooltip("The scenario staged when the scene starts.")]
        public SpecialScenario StartScenario = SpecialScenario.Birth;
        [Tooltip("Play the start scenario's taps by itself.")]
        public bool AutoPlayOnStart = true;
        [Tooltip("Game seconds between a settled board and the next scripted tap.")]
        public float Beat = 0.45f;

        public SpecialScenario Current { get; private set; }
        public bool AutoPlaying { get; private set; }
        /// <summary>The script has been played and the board has settled (no job, no golden ball, no sticker in the air).</summary>
        public bool Finished => _finished;
        public string LabelText => _label != null ? _label.text : "";
        public event Action ScenarioFinished;
        /// <summary>A scripted tap was made (its cell), whatever Tap returned — the recorder's "tap" marker.</summary>
        public event Action<int> Tapped;
        /// <summary>Whether the demo's rows and label show (the recorder hides them; the HUD stays).</summary>
        public bool UiVisible => _showUi;

        private int[] _taps = new int[0];
        private int _next;
        private float _wait;
        private bool _finished = true, _pending, _shownAny;
        private int _shownLanded;
        private SpecialLook _shownLook;
        private SpecialIdle _shownIdle;
        private SpecialScenario _shownScenario;
        private Text _label;
        private Canvas _canvas;
        private bool _showUi = true;
        private Sprite _face, _faceOn;
        private readonly Dictionary<SpecialScenario, Image> _scenarioButtons = new Dictionary<SpecialScenario, Image>();
        private readonly Dictionary<SpecialLook, Image> _lookButtons = new Dictionary<SpecialLook, Image>();
        private readonly Dictionary<SpecialIdle, Image> _idleButtons = new Dictionary<SpecialIdle, Image>();
        private readonly Dictionary<PassLook, Image> _passButtons = new Dictionary<PassLook, Image>();
        private PassLook _shownPass;

        /// <summary>A scenario button's face: the title, "Pas→B" for Pas→Bomba (the row is tight).</summary>
        public static string ScenarioButton(SpecialScenario s) => s == SpecialScenario.PassBomb ? "Pas→B" : SpecialScenarios.Title(s);

        public static string LookTitle(SpecialLook look) => look == SpecialLook.Royal ? "1 · Royal" : look == SpecialLook.Court ? "2 · Basketbol" : "Pota";

        /// <summary>The idle row's names (spec 2026-10-07); Look = the special look's own idle.</summary>
        public static string IdleTitle(SpecialIdle idle)
        {
            switch (idle)
            {
                case SpecialIdle.Breath: return "A Sakin nefes";
                case SpecialIdle.Glint: return "B Parıltı süpürmesi";
                case SpecialIdle.Rim: return "C Kenar nabzı";
                case SpecialIdle.Twinkle: return "D Tek yıldızcık";
                case SpecialIdle.Sway: return "E Hafif sallantı";
                default: return "Görünüm";
            }
        }

        /// <summary>The pass row's names (Pota spec 2026-10-11 §3).</summary>
        public static string PassTitle(PassLook look) => look == PassLook.B ? "B Pervane" : "A Pas";

        /// <summary>The idle buttons' faces: "Görünüm", then the letters.</summary>
        public static string IdleButton(SpecialIdle idle) => idle == SpecialIdle.Look ? "Görünüm" : IdleTitle(idle).Substring(0, 1);

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<ColorHuntGame>();
            BuildUi();
            Play(StartScenario, AutoPlayOnStart);
        }

        /// <summary>Stages <paramref name="s"/>'s board (now, or once the game has started) and, if <paramref name="auto"/>, plays its taps.</summary>
        public void Play(SpecialScenario s, bool auto)
        {
            Current = s;
            _taps = SpecialScenarios.Taps(s);
            _next = 0;
            _wait = SpecialScenarios.Beat(s, Beat);
            AutoPlaying = auto && _taps.Length > 0;
            _finished = false;
            _pending = true;
            if (Game != null && Game.BoardRoot != null) StageScenario();
            _shownAny = false;
            RefreshLabel();
        }

        public void SetLook(SpecialLook look)
        {
            if (Game != null) Game.SetSpecialLook(look);
            _shownAny = false;
            RefreshLabel();
        }

        /// <summary>The idle row (spec 2026-10-07): how the resting specials idle, whatever the scenario.</summary>
        public void SetIdle(SpecialIdle idle)
        {
            if (Game != null) Game.SetSpecialIdle(idle);
            _shownAny = false;
            RefreshLabel();
        }

        /// <summary>The pass row (spec 2026-10-11): how a pass ball flies, whatever the scenario.</summary>
        public void SetPassLook(PassLook look)
        {
            if (Game != null) Game.SetPassLook(look);
            _shownAny = false;
            RefreshLabel();
        }

        /// <summary>Shows or hides the demo's buttons and label (before the UI is built, the choice waits for it).</summary>
        public void ShowUi(bool on)
        {
            _showUi = on;
            if (_canvas != null) _canvas.enabled = on;
        }

        private void StageScenario()
        {
            _pending = false;
            Game.Stage(ColorHuntRules.FromPreset(SpecialScenarios.Board(Current)));
            Game.Begin();
        }

        private void Update()
        {
            if (Game == null) return;
            if (_pending)
            {
                if (Game.BoardRoot == null) return;            // the game's Start has not run yet
                StageScenario();
            }
            RefreshLabel();
            if (_finished) return;
            if (Game.Current == ColorHuntGame.State.Ended) { Finish(); return; }
            if (Game.Current != ColorHuntGame.State.Playing || Game.Busy || Game.ItemsInFlight > 0) { _wait = SpecialScenarios.Beat(Current, Beat); return; }
            if (AutoPlaying && _next < _taps.Length)
            {
                _wait -= Time.deltaTime;
                if (_wait > 0f) return;
                int cell = _taps[_next++];
                string r = Game.Tap(cell);
                if (r != "broken") Debug.LogWarning($"[SpecialsDemo] {Current}: the scripted tap on cell {cell} returned \"{r}\".");
                try { Tapped?.Invoke(cell); }
                catch (Exception e) { Debug.LogException(e); }
                _wait = SpecialScenarios.Beat(Current, Beat);
                return;
            }
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            AutoPlaying = false;
            try { ScenarioFinished?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void RefreshLabel()
        {
            if (Game == null || _label == null) return;
            if (_shownAny && _shownLanded == Game.GoldenLanded && _shownLook == Game.SpecialLook && _shownScenario == Current
                && _shownIdle == Game.SpecialIdle && _shownPass == Game.PassLook) return;
            _shownAny = true;
            _shownLanded = Game.GoldenLanded;
            _shownLook = Game.SpecialLook;
            _shownScenario = Current;
            _shownIdle = Game.SpecialIdle;
            _shownPass = Game.PassLook;
            _label.text = $"{LookTitle(_shownLook)}  ·  {SpecialScenarios.Title(Current)}  ·  bekleme {IdleTitle(_shownIdle)}  ·  pas {PassTitle(_shownPass)}  ·  sepette {_shownLanded}";
            foreach (var kv in _scenarioButtons) kv.Value.sprite = kv.Key == Current ? _faceOn : _face;
            foreach (var kv in _lookButtons) kv.Value.sprite = kv.Key == _shownLook ? _faceOn : _face;
            foreach (var kv in _idleButtons) kv.Value.sprite = kv.Key == _shownIdle ? _faceOn : _face;
            foreach (var kv in _passButtons) kv.Value.sprite = kv.Key == _shownPass ? _faceOn : _face;
        }

        // ---------- UI (Pota CSS px on a 390-px-wide phone, ColorHuntHud.Css canvas units each) ----------

        private void BuildUi()
        {
            var font = Game != null && Game.S != null && Game.S.Font != null ? Game.S.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("SpecialsDemoUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;                                         // over the HUD (0) and the popups (5)
            _canvas = canvas;
            canvas.enabled = _showUi;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;                                   // width-bound like the HUD
            float css = ColorHuntHud.Css;
            // .dondur's navy face with the gold border; the current choice in the pressed orange.
            _face = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(12f * css), PotaLook.HudButtonTop, PotaLook.HudNavy2, PotaLook.HudGold,
                Mathf.RoundToInt(2f * css), 0.18f, Mathf.RoundToInt(2f * css));
            _faceOn = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(12f * css), PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold,
                Mathf.RoundToInt(2f * css), 0f, 0);
            var root = (RectTransform)canvasGo.transform;
            var scenarios = (SpecialScenario[])Enum.GetValues(typeof(SpecialScenario));
            // Nine scenarios on a 390-px phone: 38 wide at a 40 pitch (the pass pair's titles are the long ones: "Pas→B" on the face)
            for (int i = 0; i < scenarios.Length; i++)
            {
                var s = scenarios[i];
                _scenarioButtons[s] = MakeButton(root, "Scenario" + s, ScenarioButton(s), new Vector2(14f + i * 40f, 14f), new Vector2(38f, 34f), font,
                    () => Play(s, true));
            }
            var looks = new[] { SpecialLook.Pota, SpecialLook.Royal, SpecialLook.Court };
            var names = new[] { "Pota", "1", "2" };
            for (int i = 0; i < looks.Length; i++)
            {
                var l = looks[i];
                _lookButtons[l] = MakeButton(root, "Look" + l, names[i], new Vector2(14f + i * 50f, 54f), new Vector2(46f, 34f), font, () => SetLook(l));
            }
            MakeButton(root, "Replay", "Tekrar", new Vector2(14f + 3 * 50f + 8f, 54f), new Vector2(78f, 34f), font, () => Play(Current, true));
            // spec 2026-10-07: "Bekleme: Görünüm · A · B · C · D · E", above the look row in the same style
            var idles = (SpecialIdle[])Enum.GetValues(typeof(SpecialIdle));
            float x = 14f;
            foreach (var idle in idles)
            {
                var i = idle;
                float w = i == SpecialIdle.Look ? 78f : 38f;
                _idleButtons[i] = MakeButton(root, "Idle" + i, IdleButton(i), new Vector2(x, 94f), new Vector2(w, 34f), font, () => SetIdle(i));
                x += w + 4f;
            }
            // spec 2026-10-11: "Pas: A Pas · B Pervane", above the idle row
            MakeText(root, "PassCaption", new Vector2(14f, 134f), new Vector2(34f, 34f), font, TextAnchor.MiddleLeft).text = "Pas:";
            float px = 50f;
            foreach (var look in new[] { PassLook.A, PassLook.B })
            {
                var l = look;
                float w = l == PassLook.A ? 62f : 90f;
                _passButtons[l] = MakeButton(root, "Pass" + l, PassTitle(l), new Vector2(px, 134f), new Vector2(w, 34f), font, () => SetPassLook(l));
                px += w + 4f;
            }
            _label = MakeText(root, "Label", new Vector2(14f, 176f), new Vector2(300f, 20f), font, TextAnchor.MiddleLeft);
        }

        private Image MakeButton(RectTransform root, string name, string text, Vector2 posCss, Vector2 sizeCss, Font font, UnityAction onClick)
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = posCss * css;
            rt.sizeDelta = sizeCss * css;
            var img = go.GetComponent<Image>();
            img.sprite = _face;
            img.type = Image.Type.Sliced;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var label = MakeText(rt, "Label", Vector2.zero, sizeCss, font, TextAnchor.MiddleCenter);
            label.text = text;
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 posCss, Vector2 sizeCss, Font font, TextAnchor align)
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = posCss * css;
            rt.sizeDelta = sizeCss * css;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = Mathf.RoundToInt(12f * css);
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.16f, 0.31f, 0.9f);       // PopupText's navy edge
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
