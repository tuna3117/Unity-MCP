using UnityEngine;
using UnityEngine.UI;

namespace Project.Basket
{
    /// <summary>Counters, hint, "Kaçtı" label and the result panel (legacy uGUI Text, no TMP setup needed).</summary>
    public class BasketHud : MonoBehaviour
    {
        public Text BallsText;
        public Text BasketText;
        public Text HintText;
        public Text MissedText;
        public Text ResultText;
        public GameObject ResultPanel;
        public Button RetryButton;

        private BasketRound _round;
        private int _lastBasket;
        private float _pop;

        public void Bind(BasketRound round)
        {
            _round = round;
            round.StateChanged += OnState;
            round.RoundEnded += OnEnded;
            round.MissedDeclared += () => { if (MissedText != null) MissedText.enabled = true; };
            if (RetryButton != null) RetryButton.onClick.AddListener(() => { ResultPanel.SetActive(false); _round.ResetRound(); });
            OnState(round.Current);
        }

        private void Start()
        {
            if (_round == null)
            {
                var round = FindFirstObjectByType<BasketRound>();
                if (round != null) Bind(round);
            }
        }

        private void OnState(BasketRound.State s)
        {
            if (HintText != null) HintText.enabled = s == BasketRound.State.Aim;
            if (MissedText != null && s == BasketRound.State.Aim) MissedText.enabled = false;
            if (ResultPanel != null && s == BasketRound.State.Aim) ResultPanel.SetActive(false);
            if (s == BasketRound.State.Aim) _lastBasket = 0;
        }

        private void OnEnded(int count)
        {
            if (ResultText != null) ResultText.text = $"Sepet doldu!\n{count} top";
            if (ResultPanel != null) ResultPanel.SetActive(true);
        }

        private void Update()
        {
            if (_round == null || _round.Layout == null) return;
            if (BallsText != null)
                BallsText.text = (_round.Current == BasketRound.State.Aim ? _round.Layout.BallCount : _round.BallsToThrow).ToString();
            if (_round.BasketCount != _lastBasket) { _lastBasket = _round.BasketCount; _pop = 1f; }
            if (BasketText != null)
            {
                BasketText.text = _lastBasket.ToString();
                _pop = Mathf.Max(0f, _pop - Time.unscaledDeltaTime * 4f);
                BasketText.transform.localScale = Vector3.one * (1f + 0.25f * _pop);
            }
        }

        /// <summary>Builds the whole HUD hierarchy under a new canvas (used by the scene tool).</summary>
        public static BasketHud Create(Transform parent)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("BasketHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<BasketHud>();

            hud.BallsText = MakeText(canvasGo.transform, "Balls", font, 48, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(300f, 70f), TextAnchor.UpperLeft, new Color(1f, 0.78f, 0.24f));
            var ballsLabel = MakeText(canvasGo.transform, "BallsLabel", font, 22, new Vector2(0f, 1f), new Vector2(40f, -14f), new Vector2(300f, 30f), TextAnchor.UpperLeft, Color.white);
            ballsLabel.text = "TOP";
            hud.BasketText = MakeText(canvasGo.transform, "Basket", font, 48, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 70f), TextAnchor.UpperRight, new Color(1f, 0.78f, 0.24f));
            var basketLabel = MakeText(canvasGo.transform, "BasketLabel", font, 22, new Vector2(1f, 1f), new Vector2(-40f, -14f), new Vector2(300f, 30f), TextAnchor.UpperRight, Color.white);
            basketLabel.text = "SEPET";
            hud.HintText = MakeText(canvasGo.transform, "Hint", font, 26, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(900f, 40f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f));
            hud.HintText.text = "Kaydır: sola/sağa pota seç, uzunluk güç";
            hud.MissedText = MakeText(canvasGo.transform, "Missed", font, 64, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(600f, 90f), TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.37f));
            hud.MissedText.text = "Kaçtı";
            hud.MissedText.enabled = false;

            var panel = new GameObject("ResultPanel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            hud.ResultText = MakeText(panel.transform, "Result", font, 56, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(800f, 160f), TextAnchor.MiddleCenter, Color.white);
            var btnGo = new GameObject("Retry", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panel.transform, false);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, -80f);
            brt.sizeDelta = new Vector2(320f, 90f);
            btnGo.GetComponent<Image>().color = new Color(0.18f, 0.7f, 0.42f);
            var btnText = MakeText(btnGo.transform, "Label", font, 36, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 90f), TextAnchor.MiddleCenter, Color.white);
            btnText.text = "Tekrar";
            hud.RetryButton = btnGo.GetComponent<Button>();
            hud.ResultPanel = panel;
            panel.SetActive(false);
            return hud;
        }

        private static Text MakeText(Transform parent, string name, Font font, int size, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
