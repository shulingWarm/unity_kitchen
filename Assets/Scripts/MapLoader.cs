using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 游戏启动（Awake）时按 MapData 实例化关卡：
    /// 整块地面 + 逐格墙体 + 可吃小球，并把玩家放到出生点、让相机取景适配地图大小。
    /// 场景里只挂这一个组件，不再硬编码任何关卡内容。
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        public MapData map;
        public Transform player;
        public Camera followCamera;
        public Material groundMaterial;
        public Material wallMaterial;
        public Material pickupMaterial;

        public float wallHeight = 1f;
        public float pickupScale = 0.6f;

        private void Awake()
        {
            if (map == null || player == null)
            {
                Debug.LogError("[MapLoader] 未配置 map 或 player 引用，跳过关卡生成");
                return;
            }

            BuildGround();
            BuildWalls();
            BuildPickups();
            PlacePlayer();
            FrameCamera();

            Debug.Log($"[MapLoader] 地图加载完成：{map.rows}x{map.cols}，小球 {map.CountPickups()} 个，出生点 (r{map.spawnCell.x}, c{map.spawnCell.y})");
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // Unity 原生 Plane 是 10x10，按格子尺寸换算缩放
            ground.transform.localScale = new Vector3(map.cols * map.cellSize / 10f, 1f, map.rows * map.cellSize / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        }

        private void BuildWalls()
        {
            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(transform, false);

            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    if (!map.IsWall(r, c)) continue;

                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = $"Wall_{r}_{c}";
                    wall.transform.SetParent(wallsRoot.transform, false);
                    wall.transform.localScale = new Vector3(map.cellSize, wallHeight, map.cellSize);
                    wall.transform.position = map.CellToWorld(r, c) + Vector3.up * (wallHeight * 0.5f);
                    wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                }
            }
        }

        private void BuildPickups()
        {
            var pickupsRoot = new GameObject("Pickups");
            pickupsRoot.transform.SetParent(transform, false);

            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    if (map.Get(r, c) != 'x') continue;

                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"Pickup_{r}_{c}";
                    cube.tag = "Pickup";
                    cube.transform.SetParent(pickupsRoot.transform, false);
                    cube.transform.localScale = Vector3.one * pickupScale;
                    cube.transform.position = map.CellToWorld(r, c) + Vector3.up * 0.5f;
                    cube.GetComponent<Renderer>().sharedMaterial = pickupMaterial;
                    cube.GetComponent<BoxCollider>().isTrigger = true;
                    cube.AddComponent<PickupSpin>();
                }
            }
        }

        private void PlacePlayer()
        {
            player.transform.position = map.CellToWorld(map.spawnCell.x, map.spawnCell.y) + Vector3.up * 0.5f;
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
