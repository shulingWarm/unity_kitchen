using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 单个物品的切菜累计进度（秒）。挂在物品 GameObject 上，
    /// 随拾取/放下/换板保留（这些操作复用同一个物体），物品交付或销毁时一并消失。
    /// </summary>
    public class CutProgress : MonoBehaviour
    {
        public float seconds;
    }
}
