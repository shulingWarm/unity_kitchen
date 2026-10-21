using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 游戏启动（Awake）时按 MapData 实例化关卡：
    /// 整块地面 + 逐格墙体 + 木箱（wN：木色立方体 + 顶面图片 + 取出物品模型），
    /// 并把玩家放到出生点、让相机取景适配地图大小。
    /// 场景里只挂这一个组件，不再硬编码任何关卡内容。
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        /// <summary>木箱槽位：box = 箱体；objectId = 对应物品 id；itemModel = 取出的食材模型；fallbackMaterial = 模型缺贴图时的兜底材质。</summary>
        public struct BoxSlot
        {
            public Transform box;
            public string objectId;
            public GameObject itemModel;
            public Material fallbackMaterial;
        }

        public MapData map;
        [Tooltip("关卡列表（菜单 3 烘焙）；主菜单选择的关卡会覆盖上面序列化的默认地图")]
        public LevelRegistry levelRegistry;
        public Transform player;
        public Camera followCamera;
        public Material groundMaterial;
        public Material wallMaterial;
        public Material servingTopMaterial;    // 出餐口顶面（chevron 滚动纹理）
        public Material servingSideMaterial;   // 出餐口侧面（深灰）

        public float wallHeight = 1f;
        public float pickupScale = 0.6f;
        public float boxSize = 1.6f;    // 木箱底面边长（高度=wallHeight，与内墙一致）；格子 2.0 留 0.4 缝，碰撞用箱子自身即可

        [Header("房屋外壳（KayKit 模型，编辑器菜单 5 注入；缺模型时自动跳过）")]
        public GameObject houseWallModel;       // 直墙段
        public GameObject housePillarModel;     // 墙角柱（可选）
        public Material houseMaterial;          // 统一贴图材质（空 = 用模型自带材质）
        public bool buildHouseShell = true;     // 北/西/东三面墙；南侧留空只加隐形挡板

        [Header("切菜板工作站（cut 格，编辑器菜单 5 注入；缺模型时只生成底座）")]
        public GameObject cutBoardModel;        // 菜板
        public GameObject cutKnifeModel;        // 菜板上的刀

        /// 运行时生成的所有切菜板（后续切菜交互的就近查询用）
        public List<Transform> CutStations { get; } = new List<Transform>();

        /// 运行时生成的所有木箱（抓取功能的距离查询用）
        public List<BoxSlot> Boxes { get; } = new List<BoxSlot>();

        /// 运行时生成的所有出餐口（交付任务的距离查询用）
        public List<Transform> ServingStations { get; } = new List<Transform>();

        private string[,] cells;         // 预拆分的 token 网格
        private Dictionary<string, Material> boxTopMaterials;
        private Dictionary<string, GameObject> boxItemModels;
        private Dictionary<string, Material> boxItemFallbackMaterials;
        private Dictionary<string, string> boxObjectIds;

        private void Awake()
        {
            // 关卡机制：主菜单选中的关卡覆盖场景里序列化的默认地图（直接运行游戏场景 = 第 0 关）
            if (levelRegistry != null && levelRegistry.levels != null &&
                LevelSelection.Index >= 0 && LevelSelection.Index < levelRegistry.levels.Length &&
                levelRegistry.levels[LevelSelection.Index] != null)
            {
                map = levelRegistry.levels[LevelSelection.Index];
            }

            if (map == null || player == null)
            {
                Debug.LogError("[MapLoader] 未配置 map 或 player 引用，跳过关卡生成");
                return;
            }

            ParseCells();
            BuildBoxMaterialLookup();

            BuildGround();
            BuildObstacles();
            BuildHouseShell();
            PlacePlayer();
            FrameCamera();
            SetupGameTimer();

            Debug.Log($"[MapLoader] 地图加载完成：{map.rows}x{map.cols}，" +
                      $"出生点 (r{map.spawnCell.x}, c{map.spawnCell.y})，目标 {map.goal} 分/{map.totalTime:0} 秒");
        }

        /// <summary>把地图的 total_time 注入右下角全局倒计时（存在时）。</summary>
        private void SetupGameTimer()
        {
            var timer = FindFirstObjectByType<CountdownTimerUI>();
            if (timer != null) timer.Setup(map.totalTime);
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
            boxItemModels = new Dictionary<string, GameObject>();
            boxItemFallbackMaterials = new Dictionary<string, Material>();
            boxObjectIds = new Dictionary<string, string>();
            if (map.blockTypes == null) return;

            for (int i = 0; i < map.blockTypes.Length; i++)
            {
                string token = map.blockTypes[i];
                if (map.blockMaterials != null && i < map.blockMaterials.Length)
                    boxTopMaterials[token] = map.blockMaterials[i];
                if (map.blockModels != null && i < map.blockModels.Length)
                    boxItemModels[token] = map.blockModels[i];
                if (map.blockItemMaterials != null && i < map.blockItemMaterials.Length)
                    boxItemFallbackMaterials[token] = map.blockItemMaterials[i];
                if (map.blockObjectIds != null && i < map.blockObjectIds.Length)
                    boxObjectIds[token] = map.blockObjectIds[i];
            }
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
            var cutRoot = new GameObject("CutStations");
            cutRoot.transform.SetParent(transform, false);

            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    string t = cells[r, c];
                    if (t == "1") BuildWallCell(wallsRoot.transform, r, c);
                    else if (t.Length > 1 && t[0] == 'w') BuildBox(boxesRoot.transform, r, c, t);
                    else if (t[0] == 's') BuildServingCell(servingRoot.transform, r, c, t);
                    else if (t == "cut") BuildCutStation(cutRoot.transform, r, c);
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
            box.transform.localScale = new Vector3(boxSize, wallHeight, boxSize);   // 高度与内墙一致
            box.transform.position = map.CellToWorld(r, c) + Vector3.up * (wallHeight * 0.5f);
            box.GetComponent<Renderer>().sharedMaterial =
                map.blockSideMaterial != null ? map.blockSideMaterial : wallMaterial;

            // 取出物品模型（图例 model 字段）；缺失时记 null，抓取端回退绿色小球
            GameObject itemModel;
            if (!boxItemModels.TryGetValue(token, out itemModel) || itemModel == null)
                Debug.LogWarning($"[MapLoader] {token} 缺少取出物品模型，抓取时将回退绿色小球");
            boxObjectIds.TryGetValue(token, out string objectId);
            Material fallbackMat;
            boxItemFallbackMaterials.TryGetValue(token, out fallbackMat);
            Boxes.Add(new BoxSlot
            {
                box = box.transform,
                objectId = objectId,
                itemModel = itemModel,
                fallbackMaterial = fallbackMat,
            });

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
            ServingStations.Add(counter.transform);
        }

        // ---------------- 置物台查询 ----------------

        /// <summary>该 token 是否可作为置物台（内墙 / 木箱 / 切菜板底座，均为 wallHeight 高的静态块）。</summary>
        public bool IsSurfaceToken(string t)
            => t == "1" || t == "cut" || (t.Length > 1 && t[0] == 'w');

        /// <summary>世界坐标 → 格子下标（CellToWorld 的逆变换）。</summary>
        public bool WorldToCell(Vector3 world, out int r, out int c)
        {
            c = Mathf.RoundToInt(world.x / map.cellSize + (map.cols - 1) * 0.5f);
            r = Mathf.RoundToInt((map.rows - 1) * 0.5f - world.z / map.cellSize);
            return map.InRange(r, c);
        }

        public string GetCellToken(int r, int c) => map.InRange(r, c) ? cells[r, c] : "1";

        /// <summary>
        /// 沿 facing 找 origin 面前的置物台格子（内墙/木箱/切菜板）。
        /// 命中返回台面顶面中心与占用键（格子坐标 r,c）；斜向朝向时先查主轴格，再查侧轴格。
        /// </summary>
        public bool TryGetPlaceSurface(Vector3 origin, Vector3 facing, out Vector3 topCenter, out string surfaceKey)
        {
            topCenter = default;
            surfaceKey = default;

            if (!WorldToCell(origin, out int r0, out int c0)) return false;
            var f = new Vector2(facing.x, facing.z);
            if (f.sqrMagnitude < 1e-6f) return false;
            f.Normalize();

            var candidates = new List<(int dr, int dc)>(2);
            if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
            {
                candidates.Add((0, f.x > 0 ? 1 : -1));
                if (Mathf.Abs(f.y) > 0.01f) candidates.Add((f.y > 0 ? 1 : -1, 0));
            }
            else
            {
                candidates.Add((f.y > 0 ? 1 : -1, 0));
                if (Mathf.Abs(f.x) > 0.01f) candidates.Add((0, f.x > 0 ? 1 : -1));
            }

            foreach (var (dr, dc) in candidates)
            {
                int r = r0 + dr;
                int c = c0 + dc;
                if (!map.InRange(r, c)) continue;
                if (!IsSurfaceToken(GetCellToken(r, c))) continue;

                topCenter = map.CellToWorld(r, c) + Vector3.up * wallHeight;
                surfaceKey = $"{r},{c}";
                return true;
            }
            return false;
        }

        // ---------------- 切菜板工作站 ----------------

        /// <summary>
        /// 切菜板工作站（cut 格）：墙高底座（与墙同款材质、带碰撞）+ 顶面菜板 + 板上刀。
        /// 模型按包围盒自动归一化：菜板宽 ≈ 格子的 70%，刀沿用同一缩放比例平放板面中央。
        /// </summary>
        private void BuildCutStation(Transform parent, int r, int c)
        {
            var baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBlock.name = $"CutStation_{r}_{c}";
            baseBlock.transform.SetParent(parent, false);
            baseBlock.transform.localScale = new Vector3(map.cellSize, wallHeight, map.cellSize);
            baseBlock.transform.position = map.CellToWorld(r, c) + Vector3.up * (wallHeight * 0.5f);
            baseBlock.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            CutStations.Add(baseBlock.transform);

            if (cutBoardModel == null)
            {
                Debug.LogWarning($"[MapLoader] cut ({r},{c}) 缺少菜板模型，只生成底座（跑菜单 5 注入）");
                return;
            }

            // 菜板：宽度归一化到格子的 70%，底面贴住底座顶面
            Vector3 topCenter = map.CellToWorld(r, c) + Vector3.up * wallHeight;
            var board = Instantiate(cutBoardModel, parent);
            board.name = "CutBoard";
            board.isStatic = true;
            Bounds boardBounds = ComputeWorldBounds(board);
            float boardWidth = Mathf.Max(boardBounds.size.x, boardBounds.size.z);
            float scale = (map.cellSize * 0.7f) / Mathf.Max(boardWidth, 1e-4f);
            PlaceStationModel(board, topCenter, scale);

            // 刀：沿用菜板的缩放比例，平放板面中央（轻微下压避免悬空）
            if (cutKnifeModel != null)
            {
                var knife = Instantiate(cutKnifeModel, parent);
                knife.name = "CutKnife";
                knife.isStatic = true;
                Bounds boardTop = ComputeWorldBounds(board);
                var knifeAnchor = new Vector3(topCenter.x, boardTop.max.y - boardTop.size.y * 0.02f, topCenter.z);
                PlaceStationModel(knife, knifeAnchor, scale);
            }
        }

        /// <summary>合并 go 所有渲染器的世界包围盒。</summary>
        private static Bounds ComputeWorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>按世界缩放倍率缩放模型，并把包围盒底面中心对齐到 anchor。</summary>
        private static void PlaceStationModel(GameObject model, Vector3 anchor, float scale)
        {
            model.transform.localScale = Vector3.one * scale;
            Bounds b = ComputeWorldBounds(model);
            model.transform.position += new Vector3(
                anchor.x - b.center.x,
                anchor.y - b.min.y,
                anchor.z - b.center.z);
        }

        // ---------------- 房屋外壳 ----------------

        /// <summary>
        /// 房屋外壳：北/西/东三面墙（模型段铺设 + 整条碰撞体），南侧留空只放隐形挡板防掉落。
        /// 墙体铺在网格外圈线上，不占用任何格子；地板由合作开发者负责，这里不碰。
        /// </summary>
        private void BuildHouseShell()
        {
            if (!buildHouseShell || houseWallModel == null) return;

            var shell = new GameObject("HouseShell");
            shell.transform.SetParent(transform, false);
            shell.isStatic = true;   // 静态几何，利于合批

            // 墙段走向与缩放：按模型包围盒自动对齐格子宽度（KayKit 一段 ≈ 1 米模块）
            Bounds wallBounds = ComputeLocalBounds(houseWallModel);
            float runLength = Mathf.Max(wallBounds.size.x, wallBounds.size.z);
            float scale = map.cellSize / Mathf.Max(runLength, 1e-4f);
            bool runsAlongX = wallBounds.size.x >= wallBounds.size.z;   // 模型自身的走向轴
            float wallTop = Mathf.Max(wallBounds.size.y * scale, 1f);
            float baseY = -wallBounds.min.y * scale;                    // 模型轴心不在脚底时贴地校正

            float westX = map.CellToWorld(0, 0).x - map.cellSize * 0.5f;
            float eastX = map.CellToWorld(0, map.cols - 1).x + map.cellSize * 0.5f;
            float northZ = map.CellToWorld(0, 0).z + map.cellSize * 0.5f;
            float southZ = map.CellToWorld(map.rows - 1, 0).z - map.cellSize * 0.5f;

            // 北墙：一整排
            for (int c = 0; c < map.cols; c++)
                PlaceWallSegment(shell.transform, houseWallModel, houseMaterial,
                    new Vector3(map.CellToWorld(0, c).x, baseY, northZ), runsAlongX ? 0f : 90f, scale, $"North_{c}");
            // 西墙 / 东墙：两列
            for (int r = 0; r < map.rows; r++)
            {
                float yaw = runsAlongX ? 90f : 0f;
                PlaceWallSegment(shell.transform, houseWallModel, houseMaterial,
                    new Vector3(westX, baseY, map.CellToWorld(r, 0).z), yaw, scale, $"West_{r}");
                PlaceWallSegment(shell.transform, houseWallModel, houseMaterial,
                    new Vector3(eastX, baseY, map.CellToWorld(r, 0).z), yaw, scale, $"East_{r}");
            }
            // 四角柱（南侧两根只为收边好看）
            if (housePillarModel != null)
            {
                Bounds pillarBounds = ComputeLocalBounds(housePillarModel);
                float pillarBaseY = -pillarBounds.min.y * scale;
                Vector3[] corners =
                {
                    new Vector3(westX, pillarBaseY, northZ),
                    new Vector3(eastX, pillarBaseY, northZ),
                    new Vector3(westX, pillarBaseY, southZ),
                    new Vector3(eastX, pillarBaseY, southZ),
                };
                for (int i = 0; i < corners.Length; i++)
                    PlaceWallSegment(shell.transform, housePillarModel, houseMaterial, corners[i], 0f, scale, $"Pillar_{i}");
            }

            // 碰撞：三条整墙 + 南侧隐形挡板（各自一个盒子，比逐段加省且稳定）
            AddShellCollider(shell, new Vector3(0f, wallTop * 0.5f, northZ),
                new Vector3(map.cols * map.cellSize + map.cellSize, wallTop, map.cellSize * 0.5f));
            AddShellCollider(shell, new Vector3(westX, wallTop * 0.5f, 0f),
                new Vector3(map.cellSize * 0.5f, wallTop, map.rows * map.cellSize + map.cellSize));
            AddShellCollider(shell, new Vector3(eastX, wallTop * 0.5f, 0f),
                new Vector3(map.cellSize * 0.5f, wallTop, map.rows * map.cellSize + map.cellSize));
            AddShellCollider(shell, new Vector3(0f, wallTop * 0.5f, southZ),
                new Vector3(map.cols * map.cellSize + map.cellSize, wallTop, map.cellSize * 0.5f));

            Debug.Log($"[MapLoader] 房屋外壳完成：北 {map.cols} 段 + 东西各 {map.rows} 段，墙高 {wallTop:0.00}m");
        }

        private void PlaceWallSegment(Transform parent, GameObject model, Material material,
            Vector3 position, float yaw, float scale, string name)
        {
            var seg = Instantiate(model, parent);
            seg.name = name;
            seg.isStatic = true;
            seg.transform.localPosition = position;
            seg.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            seg.transform.localScale = Vector3.one * scale;
            if (material != null)
                foreach (var rend in seg.GetComponentsInChildren<Renderer>())
                    rend.sharedMaterial = material;   // 整包共用一张色板图，单材质即可
        }

        private static void AddShellCollider(GameObject shell, Vector3 center, Vector3 size)
        {
            var go = new GameObject("ShellCollider");
            go.transform.SetParent(shell.transform, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>计算模型 prefab 相对根节点的本地包围盒（未实例化时用）。</summary>
        private static Bounds ComputeLocalBounds(GameObject prefab)
        {
            bool hasAny = false;
            var bounds = new Bounds();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var matrix = Matrix4x4.identity;
                var t = mf.transform;
                while (t != null && t != prefab.transform)
                {
                    matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
                    t = t.parent;
                }
                var b = TransformLocalBounds(matrix, mf.sharedMesh.bounds);
                if (!hasAny) { bounds = b; hasAny = true; }
                else bounds.Encapsulate(b);
            }
            return hasAny ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        /// <summary>AABB 经矩阵（可含旋转/非均匀缩放）变换：变换 8 个角点后重新包络。</summary>
        private static Bounds TransformLocalBounds(Matrix4x4 m, Bounds b)
        {
            Vector3 center = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            var ext = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
            return new Bounds(center, ext * 2f);
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
            if (follow != null)
            {
                follow.offset = offset;
                follow.baseOffset = offset;   // 缩放基准：缩放是该偏移的相对倍率
                follow.zoom = 1f;             // 每关开始重置缩放
            }
        }
    }
}
