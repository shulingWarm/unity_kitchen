using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 文本地图的 Unity 标准存储格式。
    /// 源文件是工程根 maps/*.txt，由编辑器菜单"导入地图"解析生成 .asset；
    /// 游戏运行时只读这个 asset，不碰 txt。
    /// 网格按 token 存储每行一串空格分隔的单元格：
    ///   0 = 地面   1 = 墙   x = 可吃小球   p = 出生点（导入时归一化为 0，单存 spawnCell）
    ///   wN = 木箱（外观/顶图由 blockTypes/blockImages/blockMaterials 图例定义，碰撞等同墙）
    /// </summary>
    [CreateAssetMenu(fileName = "MapData", menuName = "Roll-a-Ball/Map Data")]
    public class MapData : ScriptableObject
    {
        public int rows;
        public int cols;
        public float cellSize = 2f;          // 每格边长（世界单位）
        [TextArea(3, 12)] public string[] gridRows;   // 每个元素 = 一行，token 间空格分隔

        [Header("木箱图例（三个数组按下标一一对应）")]
        public string[] blockTypes;          // 如 ["w1", "w2"]
        public string[] blockImages;         // 相对 Assets 的图片路径（存档可读用）
        public Material[] blockMaterials;    // 顶面材质（导入时烘焙，运行时直接用）
        public Material blockSideMaterial;   // 所有木箱共用的侧面材质

        public Vector2Int spawnCell;         // 玩家出生格 (row, col)

        public bool InRange(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

        /// 某一行的 token 数组（每行最多拆一次，调用方缓存）
        public string[] SplitRow(int r) => gridRows != null && r < gridRows.Length
            ? gridRows[r].Split(' ')
            : System.Array.Empty<string>();

        public int CountPickups()
        {
            int n = 0;
            for (int r = 0; r < rows; r++)
                foreach (string t in SplitRow(r))
                    if (t == "x") n++;
            return n;
        }

        /// <summary>
        /// 网格坐标 → 世界坐标。地图居中于原点，row 0 在北侧（远离默认相机）。
        /// </summary>
        public Vector3 CellToWorld(int r, int c)
        {
            float x = (c - (cols - 1) * 0.5f) * cellSize;
            float z = ((rows - 1) * 0.5f - r) * cellSize;
            return new Vector3(x, 0f, z);
        }
    }
}
