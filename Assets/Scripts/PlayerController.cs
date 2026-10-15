using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 玩家小球：读取虚拟摇杆输入做相机相对移动，并在触发器中收集方块、更新计分 UI。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 8f;
        public Camera followCamera;   // 用于把摇杆方向换算成世界方向
        public Text scoreText;
        public Text winText;

        private Rigidbody rb;
        private int score;
        private int totalPickups;
        private Vector3 lastPos;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Start()
        {
            totalPickups = GameObject.FindGameObjectsWithTag("Pickup").Length;
            lastPos = transform.position;
            UpdateScoreUI();
            if (winText != null) winText.gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            Vector2 input = VirtualJoystick.InputVector;
            if (input.sqrMagnitude < 0.01f) return;

            // 摇杆方向 → 相机相对的世界方向（只取水平分量），操作直觉与屏幕一致
            Vector3 camForward = Vector3.up;
            Vector3 camRight = Vector3.right;
            if (followCamera != null)
            {
                Vector3 f = followCamera.transform.forward;
                Vector3 r = followCamera.transform.right;
                f.y = 0f; r.y = 0f;
                if (f.sqrMagnitude > 0.001f) camForward = f.normalized;
                if (r.sqrMagnitude > 0.001f) camRight = r.normalized;
            }

            Vector3 move = (camRight * input.x + camForward * input.y) * moveSpeed;
            rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
        }

        private void Update()
        {
            RollVisual();
        }

        // MovePosition 驱动的位移不会触发物理滚动，这里按实际位移手动自转，让球看起来在滚
        private void RollVisual()
        {
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.000001f) return;

            float radius = 0.5f;
            float angle = delta.magnitude / radius * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(Vector3.up, delta.normalized);
            transform.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Pickup")) return;

            Destroy(other.gameObject);
            score++;
            UpdateScoreUI();

            if (score >= totalPickups && winText != null)
                winText.gameObject.SetActive(true);
        }

        private void UpdateScoreUI()
        {
            if (scoreText != null)
                scoreText.text = $"Score: {score} / {totalPickups}";
        }
    }
}
