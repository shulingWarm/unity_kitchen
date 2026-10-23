using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 全局物品注册表：Assets/GameInfo/object.json 在编辑器导入地图时烘焙成此资产。
    /// ids/icons 按下标一一对应；任务卡等运行时系统按物品 id 查图标。
    /// 另存切配配方（json 里带 cut_from 字段的条目）：切菜交互按输入 id 查产物 id 与模型。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Roll-a-Ball/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        public string[] ids;
        public Texture2D[] icons;

        [Header("切配配方（数组按下标一一对应）")]
        public string[] cutFromIds;       // 输入物品 id（如 tomato）
        public string[] cutToIds;         // 切配产物 id（如 tomato_slice）
        public GameObject[] cutToModels;  // 产物 3D 模型（json model 字段的 fbx）

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

        /// <summary>查询某物品的切配产物；没有配方返回 false。</summary>
        public bool TryGetCutResult(string fromId, out string toId, out GameObject model)
        {
            toId = null;
            model = null;
            if (cutFromIds == null || fromId == null) return false;
            for (int i = 0; i < cutFromIds.Length; i++)
            {
                if (cutFromIds[i] != fromId) continue;
                toId = cutToIds != null && i < cutToIds.Length ? cutToIds[i] : null;
                model = cutToModels != null && i < cutToModels.Length ? cutToModels[i] : null;
                return toId != null;
            }
            return false;
        }
    }
}
