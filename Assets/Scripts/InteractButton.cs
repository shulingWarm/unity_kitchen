using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 场景交互按钮（按住语义）：按下开始切菜，抬起/失效即暂停（进度保留），
    /// 切菜期间在按钮上画环形进度。具体判定与累计在 PlayerController.UpdateChopping 里。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class InteractButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public PlayerController player;

        [Tooltip("环形进度（Radial360 填充）；由场景补丁创建并注入")]
        public Image progressRing;

        private void Awake()
        {
            // 视觉沿用 Button 组件，但触发改为按住/抬起，不再用 onClick
            GetComponent<Button>().onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (progressRing == null || player == null) return;
            bool chopping = player.IsChopping;
            if (progressRing.gameObject.activeSelf != chopping)
                progressRing.gameObject.SetActive(chopping);
            if (chopping) progressRing.fillAmount = player.ChopProgress01;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (player != null) player.SetInteractHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (player != null) player.SetInteractHeld(false);
        }

        private void OnDisable()
        {
            if (player != null) player.SetInteractHeld(false);   // 按钮被隐藏/禁用时兜底松开
        }
    }
}
