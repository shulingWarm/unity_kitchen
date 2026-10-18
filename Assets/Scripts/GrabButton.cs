using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 抓取按钮：点击触发玩家的抓取/交付尝试。
    /// 空手在木箱旁 → 拿取；手持物品在出餐口旁 → 交付；其余情况玩家内部直接忽略，不做任何表现。
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
