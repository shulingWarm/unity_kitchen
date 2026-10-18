using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 全局物品注册表：Assets/GameInfo/object.json 在编辑器导入地图时烘焙成此资产。
    /// ids/icons 按下标一一对应；任务卡等运行时系统按物品 id 查图标。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Roll-a-Ball/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        public string[] ids;
        public Texture2D[] icons;

        public int Count => ids != null ? ids.Length : 0;

        /// <summary>物品 id → 下标；不存在返回 -1。</summary>
        public int IndexOf(string id)
        {
            if (ids == null || id == null) return -1;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return i;
            return -1;
        }

        public Texture2D GetIcon(string id)
        {
            int i = IndexOf(id);
            return i >= 0 && icons != null && i < icons.Length ? icons[i] : null;
        }
    }
}
