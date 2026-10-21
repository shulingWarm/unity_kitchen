using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 被世界化（放下/摆上台面）的物品：记录物品 id（拾起后原样恢复到手里），
    /// 并注册进全局列表 All 供玩家就近查询。物理落地的版本带 Rigidbody；
    /// 摆上置物台 的版本无物理、纯视觉，并带 surfaceKey 标记所在台面。
    /// 场景重载时物品随之销毁、列表自动清空。
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        /// <summary>当前所有地面/台面物品。</summary>
        public static readonly List<WorldItem> All = new List<WorldItem>();

        [Tooltip("物品 id（object.json）")]
        public string objectId;

        [Tooltip("所在台面的占用键（格子坐标 r,c）；空 = 未上台面（物理落地）")]
        public string surfaceKey;

        /// <summary>某台面（格子键）是否已被物品占用（一台一物）。</summary>
        public static bool KeyOccupied(string key)
        {
            foreach (var item in All)
                if (item.surfaceKey == key) return true;
            return false;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);
    }
}
