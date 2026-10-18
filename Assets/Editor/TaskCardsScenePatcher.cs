using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给现有 Demo 场景挂上任务卡（注入 MapLoader 与 ItemRegistry 引用）、积分面板与关卡流程。
    /// 同时把 LevelRegistry 注入 MapLoader（关卡选择换图用）。
    /// 已存在 TaskCardsUI / ScoreUI / GameFlowUI（或旧版 OrderCardsUI 残留物）时销毁重建，保证最新逻辑与引用。
    /// 供编辑器菜单和 DemoSceneBuilder 调用。
    /// </summary>
    public static class TaskCardsScenePatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string RegistryPath = "Assets/GameInfo/ItemRegistry.asset";
        private const string LevelRegistryPath = "Assets/GameInfo/LevelRegistry.asset";
        private const string HouseFolder = "Assets/House/RestaurantBits";

        [MenuItem("Tools/Roll-a-Ball/5 - 给场景添加任务卡、积分与结算面板")]
        public static void AddTaskCardsToDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 旧版组件直接替换，保证始终是最新逻辑与引用
            foreach (var old in Object.FindObjectsByType<TaskCardsUI>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            foreach (var oldScore in Object.FindObjectsByType<ScoreUI>(FindObjectsSortMode.None))
                Object.DestroyImmediate(oldScore.gameObject);
            foreach (var oldFlow in Object.FindObjectsByType<GameFlowUI>(FindObjectsSortMode.None))
                Object.DestroyImmediate(oldFlow.gameObject);
            var legacy = GameObject.Find("OrderCardsUI");   // 旧订单卡（脚本已删除，只剩空壳）
            if (legacy != null) Object.DestroyImmediate(legacy);

            var loader = Object.FindFirstObjectByType<MapLoader>();
            if (loader == null)
            {
                Debug.LogError($"[TaskCardsScenePatcher] 场景里没有 MapLoader，请先构建/检查场景：{ScenePath}");
                return;
            }

            var ui = CreateTaskCards(loader);
            new GameObject("ScoreUI", typeof(ScoreUI));   // 左下角积分面板（无引用需要注入）
            CreateGameFlow(loader);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TaskCardsScenePatcher] 已重建 TaskCardsUI / ScoreUI / GameFlowUI 并保存 {ScenePath}" +
                      $"（registry 就绪：{ui.registry != null}，levels 就绪：{loader.levelRegistry != null}）");
        }

        /// <summary>创建并注入任务卡 UI，返回组件（DemoSceneBuilder 重建场景时也走这里）。</summary>
        public static TaskCardsUI CreateTaskCards(MapLoader loader)
        {
            var ui = new GameObject("TaskCardsUI", typeof(TaskCardsUI)).GetComponent<TaskCardsUI>();
            ui.mapLoader = loader;
            ui.registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryPath);
            if (ui.registry == null)
                Debug.LogWarning($"[TaskCardsScenePatcher] 未找到 {RegistryPath}，请先跑菜单 3 导入地图（会顺带烘焙物品注册表）");

            loader.levelRegistry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(LevelRegistryPath);
            if (loader.levelRegistry == null)
                Debug.LogWarning($"[TaskCardsScenePatcher] 未找到 {LevelRegistryPath}，请先跑菜单 3（会顺带烘焙关卡列表）");

            InjectHouseShell(loader);
            return ui;
        }

        /// <summary>注入房屋外墙模型与统一材质（KayKit OBJ；缺模型时 MapLoader 自动跳过外壳）。</summary>
        private static void InjectHouseShell(MapLoader loader)
        {
            loader.houseWallModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{HouseFolder}/wall.obj");
            loader.housePillarModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{HouseFolder}/pillar_A.obj");
            loader.houseMaterial = GetOrCreateHouseMaterial();
            if (loader.houseWallModel == null)
                Debug.LogWarning($"[TaskCardsScenePatcher] 未找到 {HouseFolder}/wall.obj，房屋外墙不生成" +
                                 "（把 KayKit Restaurant Bits 的 obj 与贴图拷到该目录）");
        }

        private static Material GetOrCreateHouseMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HouseFolder}/restaurantbits_texture.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{HouseFolder}/HouseWall.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, $"{HouseFolder}/HouseWall.mat");
            }
            if (tex != null) mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>创建并注入关卡流程组件（胜利/失败判定与结算面板）。</summary>
        public static GameFlowUI CreateGameFlow(MapLoader loader)
        {
            var flow = new GameObject("GameFlowUI", typeof(GameFlowUI)).GetComponent<GameFlowUI>();
            flow.mapLoader = loader;
            flow.levelRegistry = loader.levelRegistry;
            return flow;
        }
    }
}
