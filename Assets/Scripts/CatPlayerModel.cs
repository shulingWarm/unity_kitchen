using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 猫咪角色外观动画（无人形骨骼，纯程序化）：
    /// 对应人形的四个状态 ——
    ///   站立：轻微呼吸（缩放起伏）
    ///   行走：蹦跳 + 左右摇摆
    ///   抱物站立：仰头坐姿 + 快速小呼吸（开心）
    ///   抱物行走：小碎步 + 身体后仰护住嘴里的球
    /// 状态由 PlayerController.IsCarrying 与移动速度决定，姿态间平滑过渡。
    /// </summary>
    public class CatPlayerModel : MonoBehaviour
    {
        private Rigidbody playerRb;
        private PlayerController player;
        private Vector3 baseLocalPos;
        private Vector3 baseScale;
        private float t;

        // 平滑过渡中的姿态参数
        private float pitch;     // 身体后仰角（抱物时仰头护球）
        private float hop;       // 跳跃幅度
        private float sway;      // 左右摇摆幅度
        private float breathe;   // 呼吸缩放比例

        private void Start()
        {
            playerRb = GetComponentInParent<Rigidbody>();
            player = GetComponentInParent<PlayerController>();
            baseLocalPos = transform.localPosition;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            float speed = playerRb != null ? playerRb.linearVelocity.magnitude : 0f;
            bool moving = speed > 0.1f;
            bool carrying = player != null && player.IsCarrying;

            t += Time.deltaTime * (moving ? Mathf.Clamp(speed * 3f, 6f, 14f) : 2.5f);

            // 各状态目标姿态
            float targetPitch = 0f, targetHop = 0f, targetSway = 0f, targetBreathe = 0.03f;
            if (carrying && moving)
            {
                targetHop = 0.05f;     // 小碎步
                targetSway = 4f;
                targetPitch = -10f;    // 身体后仰护球
            }
            else if (carrying)
            {
                targetPitch = -14f;    // 仰头坐姿，展示嘴里的球
                targetBreathe = 0.05f; // 开心的快速呼吸
            }
            else if (moving)
            {
                targetHop = 0.08f;     // 蹦跳
                targetSway = 6f;
            }

            pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * 6f);
            hop = Mathf.Lerp(hop, targetHop, Time.deltaTime * 6f);
            sway = Mathf.Lerp(sway, targetSway, Time.deltaTime * 6f);
            breathe = Mathf.Lerp(breathe, targetBreathe, Time.deltaTime * 4f);

            float hopWave = Mathf.Abs(Mathf.Sin(t)) * hop;
            transform.localPosition = baseLocalPos + Vector3.up * hopWave;
            transform.localRotation = Quaternion.Euler(pitch, 0f, moving ? Mathf.Sin(t) * sway : 0f);
            transform.localScale = baseScale * (1f + Mathf.Sin(t * 0.5f) * breathe);
        }
    }
}
