using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 左下角积分面板：显示当前积分。分值由地图的任务定义决定（完成 +success，超时 -fail），
    /// TaskCardsUI 调 Add(delta) 记账，允许扣到负分；白交（未命中任务）不计分。
    /// 当前分数通过 Score 属性暴露（GameFlowUI 做胜利判定用）；
    /// 每局重新开始（场景重载）分数自动归零。UI 运行时自建。
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        /// <summary>当前积分（可为负）。</summary>
        public int Score => _score;

        private Text _label;
        private int _score;

        private void Start()
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
            BuildPanel(canvas);
            Refresh();
        }

        /// <summary>记一笔分数变化（正=完成任务的奖励，负=超时惩罚）。</summary>
        public void Add(int delta)
        {
            _score += delta;
            Refresh();
        }

        private void Refresh()
        {
            if (_label != null) _label.text = $"积分: {_score}";
        }

        // ---------------- UI 自建 ----------------

        private void BuildPanel(Canvas canvas)
        {
            // 面板底（左下角，深色半透明，和任务卡同为 20 边距）
            var panel = new GameObject("ScorePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
            rt.sizeDelta = new Vector2(170f, 56f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // 积分文字
            var textGO = new GameObject("Score", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _label = textGO.GetComponent<Text>();
            _label.text = "积分: 0";
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 30;
            _label.fontStyle = FontStyle.Bold;
            _label.color = Color.white;
            _label.alignment = TextAnchor.MiddleCenter;
        }
    }
}
