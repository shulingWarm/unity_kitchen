using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 左上角订单卡（胡闹厨房风格）：番茄、玉米两道菜，各带 30 秒倒计时进度条。
    /// 收集物按数量均分给两道菜；一道菜集满（完成）或超时后，卡片从菜单消失。
    /// UI 全部运行时自建；菜品图片使用项目贴图（cube_images/tomato.png、corn.png），
    /// 由编辑器侧补丁通过 Inspector 字段注入，字段为空时回退到程序化图形。
    /// </summary>
    public class OrderCardsUI : MonoBehaviour
    {
        [Tooltip("每道菜的限时（秒）")]
        public float orderTimeSeconds = 30f;

        [Tooltip("番茄图片（Assets/cube_images/tomato.png）")]
        public Texture2D tomatoTexture;

        [Tooltip("玉米图片（Assets/cube_images/corn.png）")]
        public Texture2D cornTexture;

        private static readonly Color CardColor = new Color(1f, 1f, 1f, 0.96f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFillColor = new Color(0.30f, 0.75f, 0.30f);
        private static readonly Color BarUrgentColor = new Color(0.90f, 0.18f, 0.15f);
        private static readonly Color TextColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color[] TapeColors =
        {
            new Color(0.86f, 0.20f, 0.18f), // 番茄红
            new Color(0.95f, 0.78f, 0.15f), // 玉米黄
        };

        private class Order
        {
            public GameObject root;
            public Image barFill;
            public Text count;
            public float timeLeft;
            public int quota;
            public int filled;
            public bool finished;
            public Vector2 targetPos; // 当前应处的卡槽位置（完成后自动向前顺位）
        }

        private readonly Order[] _orders = new Order[2];
        private int _total;
        private int _collected;

        private void Awake()
        {
            BuildUI();
            StartCoroutine(TrackPickups());
        }

        private void Update()
        {
            bool anyExpired = false;
            foreach (var o in _orders)
            {
                if (o.finished) continue;
                o.timeLeft = Mathf.Max(0f, o.timeLeft - Time.deltaTime);
                if (o.timeLeft <= 0f)
                {
                    o.finished = true; // 超时，订单作废
                    anyExpired = true;
                }
            }
            if (anyExpired) Refresh();

            // 未完成的卡片平滑滑动到自己的卡槽（前面的卡消失后自动向前顺位）
            foreach (var o in _orders)
            {
                if (o.finished || !o.root.activeSelf) continue;
                var rt = (RectTransform)o.root.transform;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, o.targetPos, Time.deltaTime * 10f);
            }
        }

        // 每 0.25 秒轮询 Pickup 数量（MapLoader 可能晚于本组件生成收集物）
        private IEnumerator TrackPickups()
        {
            var wait = new WaitForSeconds(0.25f);
            while (true)
            {
                int current = GameObject.FindGameObjectsWithTag("Pickup").Length;
                if (current + _collected > _total) _total = current + _collected;
                int done = Mathf.Clamp(_total - current, 0, _total);
                if (done > _collected) _collected = done;
                Refresh();
                yield return wait;
            }
        }

        private void Refresh()
        {
            if (_total <= 0) return;

            // 番茄优先多拿余数：总量 15 → 8 + 7
            int quota1 = _total - _total / 2;
            int quota2 = _total / 2;

            TryComplete(_orders[0], quota1, _collected);
            TryComplete(_orders[1], quota2, _collected - _orders[0].filled);

            // 重新分配卡槽：未完成的卡按剩余顺序排到前面
            int visible = 0;
            foreach (var o in _orders)
            {
                if (o.finished) continue;
                o.targetPos = new Vector2(20f + visible * 134f, -8f);
                visible++;
            }

            foreach (var o in _orders)
            {
                if (o.finished) continue;
                o.count.text = $"{o.filled}/{o.quota}";
                float frac = orderTimeSeconds > 0f ? o.timeLeft / orderTimeSeconds : 0f;
                o.barFill.rectTransform.anchorMax = new Vector2(frac, 1f);
                o.barFill.color = o.timeLeft <= 10f ? BarUrgentColor : BarFillColor;
            }
        }

        private void TryComplete(Order o, int quota, int collected)
        {
            if (o.finished) return;
            o.quota = quota;
            o.filled = Mathf.Clamp(collected, 0, quota);
            if (quota > 0 && o.filled >= quota)
            {
                o.finished = true; // 完成，卡片消失
                o.root.SetActive(false);
            }
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
            Sprite[] icons = { MakeIcon(tomatoTexture, new Color(0.88f, 0.18f, 0.15f)), MakeIcon(cornTexture, new Color(0.95f, 0.78f, 0.15f)) };

            for (int i = 0; i < 2; i++)
            {
                var o = new Order();
                _orders[i] = o;

                var card = new GameObject($"OrderCard_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                card.transform.SetParent(canvas.transform, false);
                var rt = (RectTransform)card.transform;
                SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(120f, 160f), new Vector2(20f + i * 134f, -8f));
                o.targetPos = rt.anchoredPosition;
                var img = card.GetComponent<Image>();
                img.color = CardColor;
                img.gameObject.AddComponent<Outline>().effectColor = ShadowColor;

                // 卡顶色带
                var tape = new GameObject("Tape", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tape.transform.SetParent(card.transform, false);
                SetRect((RectTransform)tape.transform,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-20f, 12f), new Vector2(0f, 6f));
                tape.GetComponent<Image>().color = TapeColors[i];

                // 菜品图片
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGO.transform.SetParent(card.transform, false);
                SetRect((RectTransform)iconGO.transform,
                    new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.86f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                var icon = iconGO.GetComponent<Image>();
                icon.sprite = icons[i];
                icon.preserveAspect = true;

                // 份数文字
                var countGO = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                countGO.transform.SetParent(card.transform, false);
                SetRect((RectTransform)countGO.transform,
                    new Vector2(0f, 0.26f), new Vector2(1f, 0.44f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                var t = countGO.GetComponent<Text>();
                t.text = "…";
                t.font = font;
                t.fontSize = 24;
                t.fontStyle = FontStyle.Bold;
                t.color = TextColor;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;

                // 限时进度条
                var barBack = new GameObject("TimeBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barBack.transform.SetParent(card.transform, false);
                SetRect((RectTransform)barBack.transform,
                    new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.20f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                barBack.GetComponent<Image>().color = BarBackColor;

                var barFillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barFillGO.transform.SetParent(barBack.transform, false);
                SetRect((RectTransform)barFillGO.transform,
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);

                o.root = card;
                o.barFill = barFillGO.GetComponent<Image>();
                o.count = t;
                o.timeLeft = orderTimeSeconds;
                o.barFill.color = BarFillColor;
            }
        }

        // 项目贴图 → Sprite；贴图未注入时回退为纯色圆形
        private static Sprite MakeIcon(Texture2D tex, Color fallback)
        {
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x - 32) / 26f, dy = (y - 32) / 26f;
                t.SetPixel(x, y, dx * dx + dy * dy <= 1f ? fallback : Color.clear);
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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
