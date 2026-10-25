using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 主菜单 + 关卡选择（启动场景的界面）：
    /// 主菜单只有「开始游戏 / 关闭游戏」两个按钮；开始游戏进入关卡选择，
    /// 每关一个按钮（全部开放，无解锁机制），选中后记录 LevelSelection 并加载游戏场景。
    /// 关闭游戏 = 退出应用（编辑器下仅打日志）。UI 运行时自建。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("关卡列表（菜单 3 烘焙的 LevelRegistry）")]
        public LevelRegistry registry;

        [Tooltip("游戏场景名")]
        public string gameSceneName = "DemoScene";

        private GameObject _menuPanel;
        private GameObject _levelPanel;
        private GameObject _characterPanel;
        private CharacterPreviewStage _previewStage;

        [Tooltip("候选形象（菜单 12 注入）：顺序对应预览台三个按钮")]
        public GameObject[] characterPrefabs;
        [Tooltip("候选形象贴图（菜单 12 注入）")]
        public Texture2D chibiTexture;
        public Texture2D catTexture;
        public Material catMaterial;

        private static readonly Color BgColor = new Color(0.12f, 0.15f, 0.20f, 1f);
        private static readonly Color TitleColor = new Color(0.95f, 0.85f, 0.55f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color StartColor = new Color(0.25f, 0.60f, 0.30f);
        private static readonly Color QuitColor = new Color(0.60f, 0.28f, 0.25f);
        private static readonly Color LevelColor = new Color(0.25f, 0.45f, 0.65f);
        private static readonly Color BackColor = new Color(0.45f, 0.47f, 0.50f);
        private static readonly Color SelectColor = new Color(0.25f, 0.45f, 0.75f);   // 选择角色（蓝）

        private void Start()
        {
            Time.timeScale = 1f;   // 从游戏结算面板回来时确保恢复时间流速
            BuildUI();
            ShowMenu();
        }

        // ---------------- 面板切换 ----------------

        private void ShowMenu()
        {
            _menuPanel.SetActive(true);
            _levelPanel.SetActive(false);
            if (_characterPanel != null) _characterPanel.SetActive(false);
            if (_previewStage != null) _previewStage.SetVisible(false);
        }

        private void ShowCharacterSelect()
        {
            _menuPanel.SetActive(false);
            if (_characterPanel != null) Destroy(_characterPanel);
            BuildCharacterPanel();
            _characterPanel.SetActive(true);
            if (_previewStage != null) _previewStage.SetVisible(true);
        }

        // 角色列表：三个人形形象各自独占方形展位（独立拖拽旋转），下方按钮选择
        private void BuildCharacterPanel()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _characterPanel = BuildPanel("CharacterPanel", canvas);

            Text title = CreateText(_characterPanel.transform, "Title", "选择角色", font, 52, FontStyle.Bold, TextColor);
            SetRect((RectTransform)title.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(600f, 80f), new Vector2(0f, -45f));

            // 三个方形展位横排（独立相机渲染 + 独立拖拽旋转）
            if (_previewStage == null)
            {
                var stageGO = new GameObject("CharacterPreviewStage");
                _previewStage = stageGO.AddComponent<CharacterPreviewStage>();
                _previewStage.prefabs = characterPrefabs;
                _previewStage.chibiTexture = chibiTexture;
                _previewStage.catTexture = catTexture;
                _previewStage.catMaterial = catMaterial;
            }
            var raws = _previewStage.BuildCells(_characterPanel.transform, new Vector2(380f, 380f));
            float[] xs = { -425f, 0f, 425f };
            for (int i = 0; i < raws.Length && i < 3; i++)
                SetRect((RectTransform)raws[i].transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(380f, 380f), new Vector2(xs[i], -45f));

            // 选择按钮对齐各展位下方（当前项打勾）
            string current = CharacterSelection.Get();
            AddCharacterButton(font, "豆豆男孩", CharacterSelection.ChibiBoy, current, new Vector2(-425f, -322f));
            AddCharacterButton(font, "小猫", CharacterSelection.Cat, current, new Vector2(0f, -322f));
            AddCharacterButton(font, "默认模型", CharacterSelection.Default, current, new Vector2(425f, -322f));

            AddButton(_characterPanel.transform, font, "返回主菜单", BackColor, new Vector2(500f, 310f), ShowMenu);
        }

        private void AddCharacterButton(Font font, string label, string id, string current, Vector2 position)
        {
            bool selected = current == id;
            string text = selected ? $"{label}（当前）" : label;
            AddButton(_characterPanel.transform, font, text,
                selected ? StartColor : LevelColor, position,
                () => { CharacterSelection.Set(id); ShowCharacterSelect(); });   // 重建面板刷新"当前"标记
        }

        private void ShowLevelSelect()
        {
            _menuPanel.SetActive(false);
            _levelPanel.SetActive(true);
        }

        private void PlayLevel(int index)
        {
            LevelSelection.Index = index;
            SceneManager.LoadScene(gameSceneName);
        }

        private void QuitGame()
        {
            Debug.Log("[MainMenuUI] 退出游戏");
            Application.Quit();   // 编辑器 Play 模式下不会真正退出
        }

        // ---------------- UI 自建 ----------------

        private void BuildUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
                canvas = canvasGO.GetComponent<Canvas>();
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _menuPanel = BuildPanel("MenuPanel", canvas);
            Text title = CreateText(_menuPanel.transform, "Title", "胡闹厨房", font, 72, FontStyle.Bold, TitleColor);
            SetRect((RectTransform)title.transform,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f),
                new Vector2(600f, 100f), Vector2.zero);
            AddButton(_menuPanel.transform, font, "开始游戏", StartColor, new Vector2(0f, 20f), ShowLevelSelect);
            AddButton(_menuPanel.transform, font, "关闭游戏", QuitColor, new Vector2(0f, -60f), QuitGame);
            AddButton(_menuPanel.transform, font, "选择角色", SelectColor, new Vector2(0f, -140f), ShowCharacterSelect);

            _levelPanel = BuildPanel("LevelPanel", canvas);
            Text levelTitle = CreateText(_levelPanel.transform, "Title", "选择关卡", font, 52, FontStyle.Bold, TextColor);
            SetRect((RectTransform)levelTitle.transform,
                new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f),
                new Vector2(600f, 80f), Vector2.zero);

            if (registry == null || registry.levels == null || registry.levels.Length == 0)
            {
                Text missing = CreateText(_levelPanel.transform, "Missing", "未找到关卡列表（先跑菜单 3 导入地图）", font, 24, FontStyle.Normal, TextColor);
                SetRect((RectTransform)missing.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(700f, 40f), Vector2.zero);
            }
            else
            {
                // 关卡按钮：竖排居中，随关卡数量往下排
                for (int i = 0; i < registry.levels.Length; i++)
                {
                    int index = i;   // 闭包捕获
                    string label = $"关卡 {i + 1}";
                    AddButton(_levelPanel.transform, font, label, LevelColor,
                        new Vector2(0f, 130f - i * 84f), () => PlayLevel(index));
                }
            }

            AddButton(_levelPanel.transform, font, "返回主菜单", BackColor, new Vector2(0f, -170f), ShowMenu);
        }

        private GameObject BuildPanel(string name, Canvas canvas)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = BgColor;
            return panel;
        }

        private void AddButton(Transform parent, Font font, string label, Color color, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280f, 64f);
            rt.anchoredPosition = position;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            Text text = CreateText(go.transform, "Text", label, font, 30, FontStyle.Bold, Color.white);
            SetRect((RectTransform)text.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
    }
}
