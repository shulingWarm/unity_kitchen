using System.IO;
using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
        public const string RegistryPath = "Assets/GameInfo/ItemRegistry.asset";
        private const string LevelRegistryPath = "Assets/GameInfo/LevelRegistry.asset";
        private const string HouseFolder = "Assets/House/RestaurantBits";
        private const string ThrowIconPath = "Assets/UI/button_icon/throw.png";
        private const string InteractIconPath = "Assets/UI/button_icon/interact.png";

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

            // 投掷按钮（已存在时只重新注入引用）
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var pc = loader.player != null ? loader.player.GetComponent<PlayerController>() : null;
            if (pc != null)
            {
                pc.itemRegistry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryPath);
                if (pc.itemRegistry == null)
                    Debug.LogWarning($"[TaskCardsScenePatcher] 未找到 {RegistryPath}，切菜交互不可用（先跑菜单 3 导入地图）");
            }
            if (canvas != null && pc != null)
            {
                CreateThrowButton(canvas, pc);
                CreateInteractButton(canvas, pc);
            }
            else Debug.LogWarning("[TaskCardsScenePatcher] 场景缺少 Canvas 或 player 引用，投掷/交互按钮未创建");

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

            // 切菜板工作站模型（cut 格渲染用；缺失时 MapLoader 只生成底座并告警）
            loader.cutBoardModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cube_images/cutting-board.fbx");
            loader.cutKnifeModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cube_images/cooking-knife-chopping.fbx");
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

        /// <summary>
        /// 创建场景交互按钮（投掷按钮左侧，同款圆环 + interact.png 图标）。
        /// 已存在时仅重新注入玩家引用；图标缺失时只有圆环不阻塞。交互逻辑由后续版本接入。
        /// </summary>
        public static GameObject CreateInteractButton(Canvas canvas, PlayerController player)
        {
            var existing = Object.FindFirstObjectByType<InteractButton>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.player = player;
                existing.progressRing = EnsureProgressRing(existing.gameObject);
                return existing.gameObject;
            }

            var go = new GameObject("InteractButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(InteractButton));
            var interact = go.GetComponent<InteractButton>();
            interact.player = player;
            interact.progressRing = EnsureProgressRing(go);
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 150f);
            rt.anchoredPosition = new Vector2(-500f, 150f);   // 投掷按钮（-330,150）左侧，三键一排
            var img = go.GetComponent<Image>();
            img.sprite = DemoSceneBuilder.GetOrCreateRingSprite();
            img.color = new Color(1f, 1f, 1f, 0.5f);

            if (File.Exists(InteractIconPath))
            {
                DemoSceneBuilder.ForceSpriteImport(InteractIconPath);
                var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(InteractIconPath);
                if (iconSprite != null)
                {
                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGO.transform.SetParent(go.transform, false);
                    var iconRt = (RectTransform)iconGO.transform;
                    iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.sizeDelta = new Vector2(98f, 98f);
                    var iconImg = iconGO.GetComponent<Image>();
                    iconImg.sprite = iconSprite;
                    iconImg.color = new Color(1f, 1f, 1f, 0.5f);
                    iconImg.raycastTarget = false;
                }
                else
                {
                    Debug.LogWarning($"[TaskCardsScenePatcher] {InteractIconPath} 导入后拿不到 Sprite，交互按钮只有圆环");
                }
            }
            else
            {
                Debug.LogWarning($"[TaskCardsScenePatcher] 找不到 {InteractIconPath}，交互按钮只有圆环");
            }
            return go;
        }

        /// <summary>
        /// 给交互按钮补一个环形进度子物体（Radial360 填充，排在图标下层，初始隐藏）。
        /// 已存在时只刷新配置，保证补丁可重复执行。
        /// </summary>
        private static Image EnsureProgressRing(GameObject buttonGO)
        {
            var existing = buttonGO.transform.Find("Progress");
            var ring = existing != null ? existing.GetComponent<Image>() : null;
            if (ring == null)
            {
                var go = new GameObject("Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(buttonGO.transform, false);
                go.transform.SetAsFirstSibling();   // 垫在图标下面
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(150f, 150f);   // 与外圈圆环同尺寸
                ring = go.GetComponent<Image>();
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                ring.raycastTarget = false;
                ring.sprite = DemoSceneBuilder.GetOrCreateRingSprite();
                ring.color = new Color(0.30f, 0.75f, 0.30f, 0.9f);
            }
            ring.fillAmount = 0f;
            ring.gameObject.SetActive(false);
            return ring;
        }

        /// <summary>
        /// 创建投掷按钮（抓取按钮左侧，同款圆环 + throw.png 图标）。
        /// 已存在时仅重新注入玩家引用；图标缺失时只有圆环不阻塞。
        /// </summary>
        public static GameObject CreateThrowButton(Canvas canvas, PlayerController player)
        {
            var existing = Object.FindFirstObjectByType<ThrowButton>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.player = player;
                return existing.gameObject;
            }

            var go = new GameObject("ThrowButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ThrowButton));
            go.GetComponent<ThrowButton>().player = player;
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 150f);
            rt.anchoredPosition = new Vector2(-330f, 150f);   // 抓取按钮（-160,185）左侧错位
            var img = go.GetComponent<Image>();
            img.sprite = DemoSceneBuilder.GetOrCreateRingSprite();
            img.color = new Color(1f, 1f, 1f, 0.5f);

            if (File.Exists(ThrowIconPath))
            {
                DemoSceneBuilder.ForceSpriteImport(ThrowIconPath);
                var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThrowIconPath);
                if (iconSprite != null)
                {
                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGO.transform.SetParent(go.transform, false);
                    var iconRt = (RectTransform)iconGO.transform;
                    iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.sizeDelta = new Vector2(98f, 98f);   // 外径 150 的 65%
                    var iconImg = iconGO.GetComponent<Image>();
                    iconImg.sprite = iconSprite;
                    iconImg.color = new Color(1f, 1f, 1f, 0.5f);
                    iconImg.raycastTarget = false;
                }
                else
                {
                    Debug.LogWarning($"[TaskCardsScenePatcher] {ThrowIconPath} 导入后拿不到 Sprite，投掷按钮只有圆环");
                }
            }
            else
            {
                Debug.LogWarning($"[TaskCardsScenePatcher] 找不到 {ThrowIconPath}，投掷按钮只有圆环");
            }
            return go;
        }
    }
}
