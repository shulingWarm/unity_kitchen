using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 传送带滚动：每帧平移顶面贴图的 UV 偏移，
    /// 让 ">" 箭头纹理沿指向方向循环滑动（贴图需 Repeat 包裹模式）。
    /// </summary>
    public class ConveyorScroller : MonoBehaviour
    {
        public float speed = 0.4f;   // UV/秒

        private Renderer rend;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
        }

        private void Update()
        {
            // 用 .material（实例副本）而非 sharedMaterial，避免污染材质资产
            var m = rend.material;
            m.mainTextureOffset -= new Vector2(speed * Time.deltaTime, 0f);
        }
    }
}
