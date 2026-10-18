using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 左上角任务卡（胡闹厨房风格）：开局按地图 task_num 从任务池随机抽卡，
    /// 每张卡只要求 1 个物品；完成或超时作废后立刻随机补一张新卡，始终保持驻留数。
    /// 交付由 PlayerController 在出餐口触发 TryServe：命中同物品的任务卡即完成；
    /// 未命中时物品由交付方直接消耗（惩罚乱交），这里只负责回答"命中与否"。
    /// 物品图标查 ItemRegistry（object.json 导入时烘焙）；UI 全部运行时自建。
    /// </summary>
    public class TaskCardsUI : MonoBehaviour
    {
        [Tooltip("每张任务卡的限时（秒），超时作废并补新卡")]
        public float orderTimeSeconds = 30f;

        [Tooltip("地图加载器（任务池 taskIds / 驻留数 taskNum 来源）")]
        public MapLoader mapLoader;

        [Tooltip("物品注册表（object.json 烘焙资产）")]
        public ItemRegistry registry;

        private static readonly Color CardColor = new Color(1f, 1f, 1f, 0.96f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFillColor = new Color(0.30f, 0.75f, 0.30f);
        private static readonly Color BarUrgentColor = new Color(0.90f, 0.18f, 0.15f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color[] TapeColors =
        {
            new Color(0.86f, 0.20f, 0.18f), // 番茄红
            new Color(0.95f, 0.78f, 0.15f), // 玉米黄
            new Color(0.30f, 0.72f, 0.35f), // 蔬菜绿
            new Color(0.60f, 0.40f, 0.75f), // 茄子紫
            new Color(0.95f, 0.55f, 0.20f), // 胡萝卜橙
        };

        private class Task
        {
            public string objectId;
            public GameObject root;
            public Image barFill;
            public float timeLeft;
            public Vector2 targetPos;   // 当前应处的卡槽位置（前面的卡消失后自动向前顺位）
        }

        private readonly List<Task> _tasks = new List<Task>();
        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
        private Canvas _canvas;
        private float _urgentBelow = 10f;   // 剩余时间低于该值时进度条变红

        private void Start()
        {
            if (mapLoader == null || mapLoader.map == null || registry == null)
            {
                Debug.LogError("[TaskCardsUI] 未配置 mapLoader / registry，任务系统不启动");
                return;
            }
            var map = mapLoader.map;
            if (map.taskIds == null || map.taskIds.Length == 0 || map.taskNum <= 0)
            {
                Debug.Log("[TaskCardsUI] 本地图没有任务（taskIds 为空或 task_num <= 0），不显示任务卡");
                return;
            }

            _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
                _canvas = canvasGO.GetComponent<Canvas>();
            }

            for (int i = 0; i < map.taskNum; i++)
                DrawNewTask();
            ReflowSlots();
        }

        private void Update()
        {
            bool anyExpired = false;
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var t = _tasks[i];
                t.timeLeft -= Time.deltaTime;
                UpdateBar(t);
                if (t.timeLeft <= 0f)
                {
                    RemoveTask(t);      // 超时作废
                    DrawNewTask();      // 立刻补一张
                    anyExpired = true;
                }
            }
            if (anyExpired) ReflowSlots();

            // 未完成的卡片平滑滑动到自己的卡槽（前面的卡消失后自动向前顺位）
            foreach (var t in _tasks)
            {
                var rt = (RectTransform)t.root.transform;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, t.targetPos, Time.deltaTime * 10f);
            }
        }

        /// <summary>交付尝试：命中第一张要求该物品的任务卡 → 完成并补新卡；返回是否命中。</summary>
        public bool TryServe(string objectId)
        {
            var task = _tasks.Find(t => t.objectId == objectId);
            if (task == null) return false;
            RemoveTask(task);
            DrawNewTask();
            ReflowSlots();
            return true;
        }

        // ---------------- 抽卡 / 卡片生命周期 ----------------

        private void DrawNewTask()
        {
            string[] pool = mapLoader.map.taskIds;
            string objectId = pool[Random.Range(0, pool.Length)];

            var task = new Task { objectId = objectId, timeLeft = orderTimeSeconds };
            task.root = BuildCard(task, _tasks.Count);
            _tasks.Add(task);
            UpdateBar(task);
        }

        private void RemoveTask(Task task)
        {
            _tasks.Remove(task);
            Destroy(task.root);
        }

        private void ReflowSlots()
        {
            for (int i = 0; i < _tasks.Count; i++)
                _tasks[i].targetPos = new Vector2(20f + i * 134f, -8f);
        }

        private void UpdateBar(Task task)
        {
            float frac = orderTimeSeconds > 0f ? Mathf.Clamp01(task.timeLeft / orderTimeSeconds) : 0f;
            task.barFill.rectTransform.anchorMax = new Vector2(frac, 1f);
            task.barFill.color = task.timeLeft <= _urgentBelow ? BarUrgentColor : BarFillColor;
        }

        // ---------------- UI 自建 ----------------

        private GameObject BuildCard(Task task, int cardIndex)
        {
            Color tapeColor = TapeColors[cardIndex % TapeColors.Length];

            // 先放在屏幕外，由 ReflowSlots 给出目标槽位后平滑滑入
            var card = new GameObject($"TaskCard_{task.objectId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_canvas.transform, false);
            var rt = (RectTransform)card.transform;
            SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, 140f), new Vector2(-200f, -8f));
            var img = card.GetComponent<Image>();
            img.color = CardColor;
            img.gameObject.AddComponent<Outline>().effectColor = ShadowColor;

            // 卡顶色带
            var tape = new GameObject("Tape", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tape.transform.SetParent(card.transform, false);
            SetRect((RectTransform)tape.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-20f, 12f), new Vector2(0f, 6f));
            tape.GetComponent<Image>().color = tapeColor;

            // 任务物品图片
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(card.transform, false);
            SetRect((RectTransform)iconGO.transform,
                new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.86f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var icon = iconGO.GetComponent<Image>();
            icon.sprite = MakeIcon(registry.GetIcon(task.objectId), tapeColor);
            icon.preserveAspect = true;

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
            task.barFill = barFillGO.GetComponent<Image>();

            return card;
        }

        // 注册表贴图 → Sprite（按 id 缓存）；贴图缺失时回退为纯色圆形
        private Sprite MakeIcon(Texture2D tex, Color fallback)
        {
            string key = tex != null ? tex.GetInstanceID().ToString() : "fallback:" + fallback;
            if (_iconCache.TryGetValue(key, out var cached)) return cached;

            Sprite sprite;
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            else
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 32) / 26f, dy = (y - 32) / 26f;
                    t.SetPixel(x, y, dx * dx + dy * dy <= 1f ? fallback : Color.clear);
                }
                t.Apply();
                sprite = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            _iconCache[key] = sprite;
            return sprite;
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
