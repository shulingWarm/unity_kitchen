using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 挂在模型实例（与 Animator 同节点）上：Unity 只会对 Animator 同节点的
    /// 脚本回调 OnAnimatorIK，所以由它转发给玩家控制器执行双手 IK。
    /// </summary>
    public class PlayerHandIK : MonoBehaviour
    {
        public PlayerController player;
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (player == null || animator == null) return;
            player.ApplyHandIK(animator);
        }
    }
}
