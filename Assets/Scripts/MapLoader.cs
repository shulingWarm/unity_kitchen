using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 游戏启动（Awake）时按 MapData 实例化关卡：
    /// 整块地面 + 逐格墙体 + 木箱（wN：木色立方体 + 顶面图片）+ 可吃小球，
    /// 并把玩家放到出生点、让相机取景适配地图大小。
    /// 场景里只挂这一个组件，不再硬编码任何关卡内容。
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        public MapData map;
        public Transform player;
        public Camera followCamera;
        public Material groundMaterial;
        public Material wallMaterial;
        public Material servingTopMaterial;    // 出餐口顶面（chevron 滚动纹理）
        public Material servingSideMaterial;   // 出餐口侧面（深灰）

        public float wallHeight = 1f;
        public float pickupScale = 0.6f;
        public float boxSize = 1.6f;    // 木箱边长；格子 2.0 留 0.4 缝，球直径 1.0 钻不进，碰撞用箱子自身即可

        /// 运行时生成的所有木箱（抓取功能的距离查询用）
        public List<Transform> Boxes { get; } = new List<Transform>();

        private string[,] cells;         // 预拆分的 token 网格
        private Dictionary<string, Material> boxTopMaterials;

        private void Awake()
        {
            if (map == null || player == null)
            {
                Debug.LogError("[MapLoader] 未配置 map 或 player 引用，跳过关卡生成");
                return;
            }

            ParseCells();
            BuildBoxMaterialLookup();

            BuildGround();
            BuildObstacles();
            PlacePlayer();
            FrameCamera();

            Debug.Log($"[MapLoader] 地图加载完成：{map.rows}x{map.cols}，" +
                      $"出生点 (r{map.spawnCell.x}, c{map.spawnCell.y})");
        }

        private void ParseCells()
        {
            cells = new string[map.rows, map.cols];
            for (int r = 0; r < map.rows; r++)
            {
                string[] toks = map.SplitRow(r);
                for (int c = 0; c < map.cols; c++)
                    cells[r, c] = c < toks.Length ? toks[c] : "1";
            }
        }

        private void BuildBoxMaterialLookup()
        {
            boxTopMaterials = new Dictionary<string, Material>();
            if (map.blockTypes == null || map.blockMaterials == null) return;

            int n = Mathf.Min(map.blockTypes.Length, map.blockMaterials.Length);
            for (int i = 0; i < n; i++)
                boxTopMaterials[map.blockTypes[i]] = map.blockMaterials[i];
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // Unity 原生 Plane 是 10x10，按格子尺寸换算缩放
            ground.transform.localScale = new Vector3(map.cols * map.cellSize / 10f, 1f, map.rows * map.cellSize / 10f);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = groundMaterial;
            // 地板贴图整场一张大图（UV 0..1 铺满整个 Plane，不做平铺）
            if (groundMaterial != null && groundMaterial.mainTexture != null)
                renderer.material.mainTextureScale = Vector2.one;
        }

        private void BuildObstacles()
        {
            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(transform, false);
            var boxesRoot = new GameObject("Boxes");
            boxesRoot.transform.SetParent(transform, false);
            var servingRoot = new GameObject("Serving");
            servingRoot.transform.SetParent(transform, false);

            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    string t = cells[r, c];
                    if (t == "1") BuildWallCell(wallsRoot.transform, r, c);
                    else if (t.Length > 1 && t[0] == 'w') BuildBox(boxesRoot.transform, r, c, t);
                    else if (t[0] == 's') BuildServingCell(servingRoot.transform, r, c, t);
                }
            }
        }

        private void BuildWallCell(Transform parent, int r, int c)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{r}_{c}";
            wall.transform.SetParent(parent, false);
            wall.transform.localScale = new Vector3(map.cellSize, wallHeight, map.cellSize);
            wall.transform.position = map.CellToWorld(r, c) + Vector3.up * (wallHeight * 0.5f);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        }

        private void BuildBox(Transform parent, int r, int c, string token)
        {
            // 木色箱体
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Box_{token}_{r}_{c}";
            box.transform.SetParent(parent, false);
            box.transform.localScale = Vector3.one * boxSize;
            box.transform.position = map.CellToWorld(r, c) + Vector3.up * (boxSize * 0.5f);
            box.GetComponent<Renderer>().sharedMaterial =
                map.blockSideMaterial != null ? map.blockSideMaterial : wallMaterial;
            Boxes.Add(box.transform);

            // 顶面图片：Quad 盖在顶面（抬高 0.002 防 Z-fighting），Quad 的碰撞体去掉
            Material topMat;
            if (!boxTopMaterials.TryGetValue(token, out topMat) || topMat == null)
            {
                Debug.LogWarning($"[MapLoader] {token} 缺少顶面材质，回退用侧面材质");
                topMat = map.blockSideMaterial;
            }
            var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(top.GetComponent<Collider>());
            top.GetComponent<Renderer>().sharedMaterial = topMat;
            top.transform.SetParent(box.transform, false);
            top.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Quad 默认朝 -Z，转成朝上
            top.transform.localPosition = new Vector3(0f, 0.5f + 0.002f, 0f);
        }

        /// <summary>
        /// 出餐口：柜台方块（障碍物），顶面是滚动的 ">" 箭头传送带。
        /// token：s（=s1）或 s1~s4，箭头分别朝 上/下/左/右。
        /// </summary>
        private void BuildServingCell(Transform parent, int r, int c, string token)
        {
            string dir = token == "s" ? "s1" : token;
            // 顶面绕竖直轴的 yaw：箭头纹理 +u 指向 → 转到对应的世界方向
            // （Unity yaw 正方向为俯视逆时针；北=+Z，东=+X）
            float yaw = dir switch
            {
                "s1" => 270f,   // 上（北，+Z）
                "s2" => 90f,    // 下（南，-Z）
                "s3" => 180f,   // 左（西，-X）
                _ => 0f,        // s4：右（东，+X）
            };

            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = $"Serving_{token}_{r}_{c}";
            counter.transform.SetParent(parent, false);
            counter.transform.localScale = new Vector3(map.cellSize, wallHeight, map.cellSize);
            counter.transform.position = map.CellToWorld(r, c) + Vector3.up * (wallHeight * 0.5f);
            counter.GetComponent<Renderer>().sharedMaterial =
                servingSideMaterial != null ? servingSideMaterial : wallMaterial;

            var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(top.GetComponent<Collider>());
            top.name = "Conveyor";
            top.GetComponent<Renderer>().sharedMaterial =
                servingTopMaterial != null ? servingTopMaterial : wallMaterial;
            top.transform.SetParent(counter.transform, false);
            top.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            top.transform.localPosition = new Vector3(0f, 0.5f + 0.002f, 0f);
            top.AddComponent<ConveyorScroller>();
        }

        private void PlacePlayer()
        {
            // 物理根在脚底（胶囊体从根向上伸展），略微抬离地面让物理自然落定
            player.transform.position = map.CellToWorld(map.spawnCell.x, map.spawnCell.y) + Vector3.up * 0.1f;
        }

        // 相机偏移随地图尺寸缩放：10x10 地图 ≈ 旧版 (0,10,-8) 的取景
        private void FrameCamera()
        {
            if (followCamera == null) return;

            float d = Mathf.Max(map.rows, map.cols) * map.cellSize;
            var offset = new Vector3(0f, d * 0.55f, -d * 0.45f);
            followCamera.transform.position = offset;
            followCamera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

            var follow = followCamera.GetComponent<CameraFollow>();
            if (follow != null) follow.offset = offset;
        }
    }
}
