using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 左下角积分面板：显示当前积分。计分规则：成功送出一餐 +deliveryScore，
    /// 任务卡超时作废 -timeoutPenalty，允许扣到负分。白交（未命中任务）不计分。
    /// 分数状态由本组件持有，TaskCardsUI 在交付命中/超时作废时调用 AddDelivery / AddTimeout；
    /// 每局重新开始（场景重载）分数自动归零。UI 运行时自建。
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [Tooltip("每成功送出一餐的得分")]
        public int deliveryScore = 10;

        [Tooltip("每有一餐超时的扣分")]
        public int timeoutPenalty = 5;

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

        /// <summary>成功送出一餐：加分。</summary>
        public void AddDelivery()
        {
            _score += deliveryScore;
            Refresh();
        }

        /// <summary>一张任务卡超时作废：扣分。</summary>
        public void AddTimeout()
        {
            _score -= timeoutPenalty;
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
