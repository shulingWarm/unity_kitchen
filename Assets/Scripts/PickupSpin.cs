using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 收集物：自转 + 上下浮动，纯视觉效果。
    /// </summary>
    public class PickupSpin : MonoBehaviour
    {
        public float spinSpeed = 90f;
        public float bobHeight = 0.15f;
        public float bobSpeed = 2f;

        private Vector3 basePos;

        private void Start() => basePos = transform.position;

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobHeight;
        }
    }
}
