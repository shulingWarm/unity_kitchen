using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 相机跟随：保持固定偏移平滑跟随小球。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 10f, -8f);
        public float smooth = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            Vector3 pos = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
            transform.position = pos;
            transform.LookAt(target.position);
        }
    }
}
