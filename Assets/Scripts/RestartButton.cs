using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 重新开始按钮：点击后重载当前场景。
    /// 被吃掉的方块重新出现，分数清零，小球回到出生点，胜利提示隐藏。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RestartButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Restart);
        }

        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
