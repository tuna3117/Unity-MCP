using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Dev
{
    /// <summary>
    /// Development device builds only: a small button at the top of the screen that loads the next scene in the build,
    /// so one install can try every scene of a game. Not created in the editor or in release builds.
    /// </summary>
    public class DevSceneSwitcher : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private RectTransform panel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || !Debug.isDebugBuild || SceneManager.sceneCountInBuildSettings < 2) return;
            Create();
        }

        public static DevSceneSwitcher Create()
        {
            var root = new GameObject(nameof(DevSceneSwitcher), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;

            var panel = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);

            var button = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = button.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(260f, 60f);
            rect.anchoredPosition = new Vector2(0f, -230f); // under the top HUD row (hints, ammo/target panels)
            button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            var switcher = root.AddComponent<DevSceneSwitcher>();
            switcher.label = text;
            switcher.panel = panel;
            button.GetComponent<Button>().onClick.AddListener(switcher.LoadNext);
            return switcher;
        }

        private Rect _safeArea;

        public int NextIndex => (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;

        public void LoadNext() => SceneManager.LoadScene(NextIndex);

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void Start() => RefreshLabel();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshLabel();

        private void RefreshLabel() =>
            label.text = "→ " + Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(NextIndex));

        private void Update()
        {
            // Keep clear of the notch: the panel spans Screen.safeArea.
            var safe = Screen.safeArea;
            if (safe == _safeArea) return;
            _safeArea = safe;
            panel.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            panel.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }
    }
}
