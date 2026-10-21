using UnityEngine;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 场景交互按钮：通用的"使用/交互"动作入口（切菜、开机关等后续接入）。
    /// 目前仅完成按钮框架与引用注入，具体交互逻辑由后续版本在此接入。
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
            // TODO 交互逻辑接入点：查找面朝格子上的可交互对象（IInteractable）并触发其动作。
            if (player == null) return;
        }
    }
}
