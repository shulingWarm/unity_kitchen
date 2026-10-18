using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>投掷按钮：点击把手里的物品沿玩家朝向抛出（空手时玩家内部直接忽略）。</summary>
    [RequireComponent(typeof(Button))]
    public class ThrowButton : MonoBehaviour
    {
        public PlayerController player;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            if (player != null) player.Throw();
        }
    }
}
