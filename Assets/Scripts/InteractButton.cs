using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 场景交互按钮：通用的"使用/交互"动作入口。
    /// 目前接的是切菜板交互（面朝 cut 格把板上物品切成注册的产物）；
    /// 后续新交互类型在 PlayerController.TryInteract 里按面朝对象分流。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class InteractButton : MonoBehaviour
    {
        public PlayerController player;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            if (player == null) return;
            player.TryInteract();
        }
    }
}
