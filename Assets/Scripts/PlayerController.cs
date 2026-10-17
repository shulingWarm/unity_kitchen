using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 玩家（人形角色）：读取虚拟摇杆输入做相机相对移动，
    /// 模型朝移动方向转身，Animator 按移动状态切换 Idle/Walk。
    /// 并在触发器中收集方块、更新计分 UI。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 6f;
        public Camera followCamera;       // 用于把摇杆方向换算成世界方向
        public Animator animator;         // 场景生成器注入（挂在模型实例上）
        public Transform modelPivot;      // 模型父节点：只负责朝向，物理根不旋转
        public float turnSpeed = 12f;     // 转身平滑度
        public Text scoreText;
        public Text winText;

        private Rigidbody rb;
        private int score;
        private int totalPickups;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;   // 人形不翻滚
        }

        private void Start()
        {
            totalPickups = GameObject.FindGameObjectsWithTag("Pickup").Length;
            UpdateScoreUI();
            if (winText != null) winText.gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            Vector2 input = VirtualJoystick.InputVector;
            if (input.sqrMagnitude < 0.01f) return;

            Vector3 move = CameraRelativeDirection(input) * moveSpeed;
            rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
        }

        private void Update()
        {
            Vector2 input = VirtualJoystick.InputVector;
            bool moving = input.sqrMagnitude > 0.01f;

            // 朝向：有输入时模型转向移动方向
            if (moving && modelPivot != null)
            {
                Vector3 dir = CameraRelativeDirection(input);
                Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                modelPivot.rotation = Quaternion.Slerp(modelPivot.rotation, target, turnSpeed * Time.deltaTime);
            }

            // 动画：移动速度参数驱动 Idle <-> Walk
            if (animator != null)
                animator.SetFloat("MoveSpeed", moving ? 1f : 0f, 0.1f, Time.deltaTime);
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
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
            return (camRight * input.x + camForward * input.y).normalized;
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
