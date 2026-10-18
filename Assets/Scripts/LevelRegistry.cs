using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 关卡列表：maps/*.txt 全部导入后按文件名排序烘焙（菜单 3 顺带完成）。
    /// 主菜单的关卡按钮与游戏内的"下一关"都按此顺序取关。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelRegistry", menuName = "Roll-a-Ball/Level Registry")]
    public class LevelRegistry : ScriptableObject
    {
        public MapData[] levels;

        /// <summary>是否为最后一关（没有"下一关"选项）。</summary>
        public bool IsLast(int index) => levels == null || index >= levels.Length - 1;
    }

    /// <summary>当前关卡选择：主菜单写入，游戏场景启动时读取（静态传递，场景重载自动沿用本局关卡）。</summary>
    public static class LevelSelection
    {
        /// <summary>LevelRegistry.levels 的下标；直接运行游戏场景时默认第 0 关。</summary>
        public static int Index = 0;
    }
}
