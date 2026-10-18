using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 抓取按钮：情境交互键，点击触发玩家的交互尝试，由玩家内部按状态分流——
    /// 手持 → 出餐口旁交付 / 就地放下；空手 → 捡起地面物品 / 从木箱拿取；其余忽略。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GrabButton : MonoBehaviour
    {
        public PlayerController player;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            if (player != null) player.TryGrab();
        }
    }
}
