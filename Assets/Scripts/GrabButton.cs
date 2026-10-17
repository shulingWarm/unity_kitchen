using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 抓取按钮：点击触发玩家的抓取尝试。
    /// 手上有物品、或附近没有木箱时，玩家内部直接忽略，不做任何表现。
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
