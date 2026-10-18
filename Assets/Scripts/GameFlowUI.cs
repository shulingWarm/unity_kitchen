using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 关卡流程：监控积分与地图 goal —— 达标立即胜利；全局倒计时耗尽未达标判负。
    /// 结束时冻结游戏（Time.timeScale = 0）并弹出结算面板：
    /// 胜利 = 重新开始本局 / 开始下一关（最后一关无此选项）/ 回到主菜单；
    /// 失败 = 重新开始本局 / 回到主菜单。
    /// 切换场景前统一复位 timeScale。面板 UI 运行时自建。
    /// </summary>
    public class GameFlowUI : MonoBehaviour
    {
        [Tooltip("地图加载器（goal 与关卡上下文来源）")]
        public MapLoader mapLoader;

        [Tooltip("关卡列表（菜单 3 烘焙；未注入时不出现「开始下一关」）")]
        public LevelRegistry levelRegistry;

        private const string MenuSceneName = "MainMenuScene";

        private ScoreUI _score;
        private bool _ended;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.96f);
        private static readonly Color TitleWinColor = new Color(0.20f, 0.62f, 0.25f);
        private static readonly Color TitleLoseColor = new Color(0.85f, 0.20f, 0.16f);
        private static readonly Color TextColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color RestartColor = new Color(0.25f, 0.50f, 0.85f);
        private static readonly Color NextColor = new Color(0.25f, 0.65f, 0.32f);
        private static readonly Color MenuColor = new Color(0.45f, 0.47f, 0.50f);

        private void Start()
        {
            _score = FindFirstObjectByType<ScoreUI>();
            var timer = FindFirstObjectByType<CountdownTimerUI>();
            if (timer != null) timer.TimeUp += Lose;   // 同场景同生共死，无需退订
        }

        private void Update()
        {
            if (_ended || _score == null || mapLoader == null || mapLoader.map == null) return;
            if (_score.Score >= mapLoader.map.goal) Win();   // 达到目标分立即胜利
        }

        private void Win()
        {
            if (_ended) return;
            _ended = true;
            Time.timeScale = 0f;   // 冻结计时/移动/任务卡
            bool hasNext = levelRegistry != null && !levelRegistry.IsLast(LevelSelection.Index);
            ShowResult("胜利！", $"恭喜！达到 {mapLoader.map.goal} 分，本关通过！", TitleWinColor, restart: true, next: hasNext);
        }

        private void Lose()
        {
            if (_ended) return;
            _ended = true;
            Time.timeScale = 0f;
            int goal = mapLoader != null && mapLoader.map != null ? mapLoader.map.goal : 0;
            ShowResult("失败", $"时间到！未达到目标 {goal} 分。", TitleLoseColor, restart: true, next: false);
        }

        // ---------------- 结算面板 ----------------

        private void ShowResult(string title, string subtitle, Color titleColor, bool restart, bool next)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[GameFlowUI] 场景里没有 Canvas，无法显示结算面板");
                return;
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 全屏暗幕（挡住下层点击）
            var dim = new GameObject("ResultDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            dim.transform.SetAsLastSibling();
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = DimColor;

            // 中央面板
            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(dim.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 88f + (2 + (restart ? 1 : 0) + (next ? 1 : 0) + 1) * 66f);
            panel.GetComponent<Image>().color = PanelColor;

            // 标题 + 副标题
            Text titleText = CreateText(panel.transform, "Title", title, font, 44, FontStyle.Bold, titleColor);
            SetRect((RectTransform)titleText.transform,
                new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text subText = CreateText(panel.transform, "Subtitle", subtitle, font, 20, FontStyle.Normal, TextColor);
            SetRect((RectTransform)subText.transform,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.86f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // 按钮（从上往下：重新开始 / 下一关 / 主菜单）
            float y = 0.60f;
            if (restart)
            {
                AddButton(panel.transform, font, "重新开始本局", RestartColor, y, RestartLevel);
                y -= 0.16f;
            }
            if (next)
            {
                AddButton(panel.transform, font, "开始下一关", NextColor, y, NextLevel);
                y -= 0.16f;
            }
            AddButton(panel.transform, font, "回到主菜单", MenuColor, y, ToMainMenu);
        }

        private void AddButton(Transform parent, Font font, string label, Color color, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.18f, anchorY - 0.055f);
            rt.anchorMax = new Vector2(0.82f, anchorY + 0.055f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            Text text = CreateText(go.transform, "Text", label, font, 26, FontStyle.Bold, Color.white);
            SetRect((RectTransform)text.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        // ---------------- 按钮 回调 ----------------

        private void RestartLevel()
        {
            Time.timeScale = 1f;   // 场景重载前必须复位，否则下一局永远暂停
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void NextLevel()
        {
            Time.timeScale = 1f;
            LevelSelection.Index++;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ToMainMenu()
        {
            Time.timeScale = 1f;
            LevelSelection.Index = 0;   // 回主菜单后若直接运行游戏场景，默认从第 1 关开始
            SceneManager.LoadScene(MenuSceneName);
        }

        // ---------------- UI 小工具 ----------------

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
