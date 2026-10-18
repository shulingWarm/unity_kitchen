using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
// 新旧输入系统都有 Touch 类型，用别名固定指向 EnhancedTouch 的结构体
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RollABall
{
    /// <summary>
    /// 相机跟随：保持基准偏移平滑跟随玩家；支持缩放——
    /// 移动端双指捏合（张开 = 拉近放大，捏合 = 拉远缩小），电脑端滚轮（前推 = 拉近）。
    /// 缩放是相对倍率：1× = 每张地图的自动取景偏移（MapLoader.FrameCamera 注入 baseOffset）；
    /// 始终以玩家为中心，只改距离不改俯角。倍率带平滑与上下限钳制，每关开始重置为 1。
    /// 项目为纯新 Input System：捏合走 EnhancedTouch，滚轮走 Mouse.current。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 10f, -8f);   // 基准偏移（FrameCamera 注入；未注入时用默认）
        public float smooth = 5f;

        [Header("缩放")]
        public float zoom = 1f;                 // 当前目标倍率（1 = 自动取景）
        public float minZoom = 0.3f;            // 拉近下限
        public float maxZoom = 2.0f;            // 拉远上限
        public float zoomSmooth = 8f;           // 倍率平滑
        public float pinchGain = 1.2f;          // 捏合灵敏度（两指间距每变化 100% 对应的倍率变化）
        public float wheelSensitivity = 0.12f;  // 每单位滚轮增量对应的倍率变化

        /// <summary>缩放基准偏移；由 MapLoader.FrameCamera 注入，未注入时首帧回退用 offset。</summary>
        public Vector3 baseOffset;

        private Vector3 _appliedBase;       // 实际生效的基准偏移（懒初始化）
        private float _appliedZoom = 1f;    // 平滑后的当前倍率
        private float _lastPinchDist;       // 上一帧两指间距（<=0 表示当前没有有效捏合）

        private void Awake()
        {
            EnhancedTouchSupport.Enable();
        }

        private void Update()
        {
            float delta = 0f;

            // 双指捏合：两指间距增大（张开）= 拉近（倍率变小）
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                float dist = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (_lastPinchDist > 1f)
                    delta = (_lastPinchDist - dist) / _lastPinchDist * pinchGain;
                _lastPinchDist = dist;
            }
            else
            {
                _lastPinchDist = 0f;   // 任一手指抬起即结束本次捏合，防止下次跳变
            }

            // 滚轮：向前推 = 拉近
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                delta -= mouse.scroll.ReadValue().y * wheelSensitivity;

            if (delta != 0f)
                zoom = Mathf.Clamp(zoom + delta, minZoom, maxZoom);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 基准偏移懒初始化：直接运行场景、没人注入 baseOffset 时回退用 offset
            if (_appliedBase == Vector3.zero)
            {
                _appliedBase = baseOffset != Vector3.zero ? baseOffset : offset;
                _appliedZoom = zoom;
            }

            _appliedZoom = Mathf.Lerp(_appliedZoom, zoom, zoomSmooth * Time.deltaTime);
            Vector3 desired = target.position + _appliedBase * _appliedZoom;
            transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
            transform.LookAt(target.position);
        }
    }
}
