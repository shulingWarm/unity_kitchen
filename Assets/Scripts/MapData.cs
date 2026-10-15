using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 文本地图的 Unity 标准存储格式。
    /// 源文件是工程根 maps/*.txt，由编辑器菜单"导入地图"解析生成 .asset；
    /// 游戏运行时只读这个 asset，不碰 txt。
    /// 网格字符含义（导入后 p 已归一化为 0，出生点单存 spawnCell）：
    ///   0 = 地面   1 = 墙   x = 可吃小球
    /// </summary>
    [CreateAssetMenu(fileName = "MapData", menuName = "Roll-a-Ball/Map Data")]
    public class MapData : ScriptableObject
    {
        public int rows;
        public int cols;
        public float cellSize = 2f;          // 每格边长（世界单位）
        [TextArea] public string[] grid;     // 每个元素一整行
        public Vector2Int spawnCell;         // 玩家出生格 (row, col)

        public bool InRange(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

        public char Get(int r, int c)
        {
            if (!InRange(r, c) || grid == null || c >= grid[r].Length) return '1';   // 越界按墙处理
            return grid[r][c];
        }

        public bool IsWall(int r, int c) => Get(r, c) == '1';

        public int CountPickups()
        {
            int n = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (Get(r, c) == 'x') n++;
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
