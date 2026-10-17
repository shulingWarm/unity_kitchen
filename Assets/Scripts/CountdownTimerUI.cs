using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 倒计时条（右下角，胡闹厨房风格）：深蓝面板 + 白色大号剩余时间 + 红色递减进度条 + 沙漏图标。
    /// UI 全部在运行时自建，场景中只需一个挂了本组件的空物体。
    /// 时间到后停在 00:00 并变红（结算逻辑可在 OnTimeUp 里扩展）。
    /// </summary>
    public class CountdownTimerUI : MonoBehaviour
    {
        [Tooltip("倒计时总秒数")]
        public float countdownSeconds = 120f;

        private Text _timeText;
        private Image _barFill;
        private float _remaining;
        private bool _finished;

        private static readonly Color PanelColor = new Color(0.10f, 0.17f, 0.36f, 0.92f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFillColor = new Color(0.86f, 0.15f, 0.16f);
        private static readonly Color UrgentColor = new Color(1f, 0.25f, 0.2f);

        private void Awake()
        {
            BuildUI();
            _remaining = countdownSeconds;
            _finished = countdownSeconds <= 0f;
        }

        private void Update()
        {
            if (_finished) return;

            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            Refresh();

            if (_remaining <= 0f)
            {
                _finished = true;
                OnTimeUp();
            }
        }

        /// <summary>时间到。后续可在这里接失败/结算逻辑。</summary>
        protected virtual void OnTimeUp()
        {
            Debug.Log("[CountdownTimerUI] 时间到！");
        }

        private void Refresh()
        {
            int left = Mathf.CeilToInt(_remaining);
            _timeText.text = $"{left / 60:00}:{left % 60:00}";
            _timeText.color = _remaining <= 10f ? UrgentColor : Color.white;
            float frac = countdownSeconds > 0f ? _remaining / countdownSeconds : 0f;
            _barFill.rectTransform.anchorMax = new Vector2(frac, 1f);
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

            // 面板：右下角
            var panel = new GameObject("TimerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var panelRt = (RectTransform)panel.transform;
            SetRect(panelRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(230f, 84f), new Vector2(-16f, 14f));
            panel.GetComponent<Image>().color = PanelColor;

            // 剩余时间：面板上部大字
            _timeText = CreateText(panel.transform, "TimeText", FormatTime(countdownSeconds), font, 38, FontStyle.Bold, Color.white);
            SetRect((RectTransform)_timeText.transform,
                new Vector2(0f, 0.42f), new Vector2(1f, 0.98f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            _timeText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            // 红色进度条：左下（背景 + 填充）
            var barBack = new GameObject("BarBack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barBack.transform.SetParent(panel.transform, false);
            SetRect((RectTransform)barBack.transform,
                new Vector2(0.07f, 0.14f), new Vector2(0.70f, 0.34f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            barBack.GetComponent<Image>().color = BarBackColor;

            var barFillGO = new GameObject("BarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barFillGO.transform.SetParent(barBack.transform, false);
            SetRect((RectTransform)barFillGO.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            _barFill = barFillGO.GetComponent<Image>();
            _barFill.color = BarFillColor;

            // 沙漏图标：面板右侧（程序化绘制，避免字体缺字形）
            var hourGO = new GameObject("Hourglass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hourGO.transform.SetParent(panel.transform, false);
            SetRect((RectTransform)hourGO.transform,
                new Vector2(0.73f, 0.10f), new Vector2(0.97f, 0.88f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var hourImg = hourGO.GetComponent<Image>();
            hourImg.sprite = CreateHourglassSprite();
            hourImg.preserveAspect = true;

            Refresh();
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

        private static string FormatTime(float seconds)
        {
            int left = Mathf.CeilToInt(seconds);
            return $"{left / 60:00}:{left % 60:00}";
        }

        // 程序化画一个白色沙漏：上下横杆 + 中间收腰的沙体，运行时生成，无需美术资源
        private static Sprite CreateHourglassSprite()
        {
            const int w = 48, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = Color.clear;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool on = false;
                    // 上下横杆
                    if (y < 6 || y >= h - 6) on = x >= 2 && x < w - 2;
                    // 中部沙体：宽度从 20px 收到 2px 再展开
                    else
                    {
                        float t = (y - 6) / (float)(h - 12);
                        float half = t < 0.5f ? Mathf.Lerp(20f, 2f, t * 2f) : Mathf.Lerp(2f, 20f, (t - 0.5f) * 2f);
                        on = Mathf.Abs(x - (w - 1) * 0.5f) <= half;
                    }
                    tex.SetPixel(x, y, on ? Color.white : clear);
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
