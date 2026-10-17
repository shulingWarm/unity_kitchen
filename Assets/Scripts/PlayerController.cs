using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 玩家（人形角色）：读取虚拟摇杆输入做相机相对移动，
    /// 模型朝移动方向转身，Animator 按移动状态切换 Idle/Walk。
    /// 抓取：点击抓取按钮时从最近的木箱拿出绿色小球，双手 IK 捧在身前。
    /// 胜利条件：暂无（收集玩法已移除）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 6f;
        public Camera followCamera;       // 用于把摇杆方向换算成世界方向
        public Animator animator;         // 场景生成器注入（挂在模型实例上）
        public Transform modelPivot;      // 模型父节点：只负责朝向，物理根不旋转
        public float turnSpeed = 12f;     // 转身平滑度

        [Header("抓取")]
        public MapLoader mapLoader;       // 木箱列表来源
        public float grabRange = 3f;      // 距离箱子多远以内可以拿
        public float itemRadius = 0.18f;  // 手持绿球半径

        private Rigidbody rb;
        private GameObject heldItem;      // 手上的物品（挂在胸部骨骼上，null = 空手）

        private static Material itemMaterial;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;   // 人形不翻滚
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

            // 动画：MoveSpeed 驱动站立/行走，IsCarry 驱动空手/捧物两个分支
            if (animator != null)
            {
                animator.SetFloat("MoveSpeed", moving ? 1f : 0f, 0.1f, Time.deltaTime);
                animator.SetBool("IsCarry", heldItem != null);
            }
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

        // ---------------- 抓取 ----------------

        /// <summary>抓取按钮回调。空手且附近有木箱时，取出绿球捧在双手里。</summary>
        public void TryGrab()
        {
            if (heldItem != null) return;                       // 手上已有物品：第一版不做任何处理
            if (mapLoader == null || animator == null) return;

            Transform box = FindNearestBox();
            if (box == null) return;                            // 附近没有木箱：不响应

            SpawnHeldItem();
        }

        private Transform FindNearestBox()
        {
            Transform nearest = null;
            float best = grabRange * grabRange;
            foreach (var box in mapLoader.Boxes)
            {
                if (box == null) continue;
                float dxz = Vector2.SqrMagnitude(new Vector2(
                    box.position.x - transform.position.x,
                    box.position.z - transform.position.z));
                if (dxz <= best) { best = dxz; nearest = box; }
            }
            return nearest;
        }

        private void SpawnHeldItem()
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) return;

            if (itemMaterial == null)
                itemMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.75f, 0.3f) };

            heldItem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heldItem.name = "HeldItem";
            Object.Destroy(heldItem.GetComponent<Collider>());   // 纯视觉挂件，不参与物理
            heldItem.GetComponent<Renderer>().sharedMaterial = itemMaterial;
            heldItem.transform.localScale = Vector3.one * itemRadius * 2f;
            heldItem.transform.position = chest.position
                                         + modelPivot.forward * 0.35f
                                         + Vector3.up * 0.1f;
            heldItem.transform.SetParent(chest, true);           // 跟随胸部骨骼
        }

        /// <summary>由模型实例上的 PlayerHandIK 在 OnAnimatorIK 中调用（IK 必须与 Animator 同节点）。</summary>
        public void ApplyHandIK(Animator anim)
        {
            if (heldItem == null)
            {
                anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }

            Vector3 forward = modelPivot != null ? modelPivot.forward : transform.forward;
            Vector3 right = modelPivot != null ? modelPivot.right : transform.right;
            Vector3 center = heldItem.transform.position;
            float grip = itemRadius * 0.9f;

            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, center - right * grip);
            anim.SetIKPosition(AvatarIKGoal.RightHand, center + right * grip);

            // 手掌朝向：顺着身体朝向轻扶球体
            Quaternion gripRot = Quaternion.LookRotation(forward, Vector3.up);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.7f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0.7f);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, gripRot);
            anim.SetIKRotation(AvatarIKGoal.RightHand, gripRot);
        }
    }
}
