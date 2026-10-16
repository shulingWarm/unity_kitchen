using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 王者荣耀式浮动摇杆：
    /// - 组件挂在控制热区（一块透明 Image）上，手指按在热区任意位置即在该点生成摇杆
    /// - 方向/幅度 = 当前指位相对按下起点的向量，最大半径处满速
    /// - 松手后摇杆回到默认提示位并变半透明；视觉元素在运行时自动创建
    /// </summary>
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public static Vector2 InputVector { get; private set; }

        [Header("配置")]
        public Sprite circleSprite;                       // 圆形贴图（场景生成器赋值）
        public float maxRadius = 90f;                     // 最大拖拽半径（Canvas 单位）
        [Range(0f, 1f)] public float idleAlpha = 0.28f;   // 待机提示透明度

        private const float BaseSize = 170f;
        private const float HandleSize = 70f;
        private const float ActiveBaseAlpha = 0.65f;
        private const float ActiveHandleAlpha = 0.95f;
        private static readonly Vector2 IdlePos = new Vector2(185f, 185f);   // 待机提示位（热区本地坐标）

        private RectTransform baseRect;
        private RectTransform handleRect;
        private Image baseImg;
        private Image handleImg;
        private Vector2 origin;          // 按下起点（热区本地坐标）
        private int activePointerId = -1;

        private void Awake()
        {
            BuildVisuals();
            ShowIdle();
        }

        private void BuildVisuals()
        {
            // 底座锚父级左下角：anchoredPosition = 父级本地坐标（绝对落点定位用）
            baseRect = CreateCircle("JoystickBase", BaseSize, transform, Vector2.zero);
            baseImg = baseRect.GetComponent<Image>();
            // 手柄锚父级（底座）中心：anchoredPosition = 相对底座中心的拖拽偏移
            handleRect = CreateCircle("JoystickHandle", HandleSize, baseRect, new Vector2(0.5f, 0.5f));
            handleImg = handleRect.GetComponent<Image>();
        }

        private RectTransform CreateCircle(string name, float size, Transform parent, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = circleSprite;
            img.raycastTarget = false;   // 视觉不参与点击，输入全部由热区接收
            return rt;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != -1) return;   // 已有手指在控制，忽略其余触点
            activePointerId = eventData.pointerId;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, eventData.position, eventData.pressEventCamera, out origin))
            {
                baseRect.anchoredPosition = origin;   // 摇杆在落指点生成
                SetVisuals(active: true);
                UpdateDrag(origin);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, eventData.position, eventData.pressEventCamera, out var local)) return;
            UpdateDrag(local);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            activePointerId = -1;
            InputVector = Vector2.zero;
            ShowIdle();
        }

        private void UpdateDrag(Vector2 local)
        {
            Vector2 offset = local - origin;
            float len = offset.magnitude;
            if (len > maxRadius)
                offset = offset / len * maxRadius;   // 超出半径只截断长度，方向保留

            handleRect.anchoredPosition = offset;
            InputVector = offset / maxRadius;
        }

        private void ShowIdle()
        {
            baseRect.anchoredPosition = IdlePos;
            handleRect.anchoredPosition = Vector2.zero;
            SetVisuals(active: false);
        }

        private void SetVisuals(bool active)
        {
            float ba = active ? ActiveBaseAlpha : idleAlpha;
            float ha = active ? ActiveHandleAlpha : Mathf.Clamp01(idleAlpha + 0.25f);
            var c = baseImg.color; c.a = ba; baseImg.color = c;
            c = handleImg.color; c.a = ha; handleImg.color = c;
        }

        private void OnDisable()
        {
            activePointerId = -1;
            InputVector = Vector2.zero;
            ShowIdle();
        }
    }
}
