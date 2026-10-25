using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>返回主菜单按钮：点击后加载主菜单场景（放弃本局进度）。</summary>
    [RequireComponent(typeof(Button))]
    public class BackToMenuButton : MonoBehaviour
    {
        [Tooltip("主菜单场景名")]
        public string menuSceneName = "MainMenuScene";

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(BackToMenu);
        }

        private void BackToMenu()
        {
            Debug.Log("[BackToMenuButton] 返回主菜单");
            Time.timeScale = 1f;   // 结算面板可能暂停过时间
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
