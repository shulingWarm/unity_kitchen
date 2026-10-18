using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 掉落在地上的物品：记录物品 id（拾起后原样恢复到手里），
    /// 并注册进全局列表 All 供玩家就近查询。由 PlayerController 在放置时创建——
    /// 脱离手持骨骼 + Rigidbody 物理自由落体（允许翻滚）。
    /// 场景重载时物品随之销毁、列表自动清空。
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        /// <summary>当前所有地面物品。</summary>
        public static readonly List<WorldItem> All = new List<WorldItem>();

        [Tooltip("物品 id（object.json）")]
        public string objectId;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);
    }
}
