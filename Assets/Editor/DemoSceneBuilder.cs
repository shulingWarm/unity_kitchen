using System.IO;
using RollABall;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RollABallEditorTools
{
    /// <summary>
    /// 一键构建滚球 Demo：场景搭建 + Android 打包配置。
    /// 菜单：Tools / Roll-a-Ball
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string CircleSpritePath = "Assets/UI/joystick_circle.png";

        // ---------------- 菜单 1：构建场景 ----------------
        [MenuItem("Tools/Roll-a-Ball/1 - 构建 Demo 场景")]
        public static void BuildScene()
        {
            EnsureTag("Pickup");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/UI");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---- 材质 ----
            var matGround = GetOrCreateMaterial("Assets/Materials/M_Ground.mat", new Color(0.55f, 0.75f, 0.55f));
            var matWall   = GetOrCreateMaterial("Assets/Materials/M_Wall.mat", new Color(0.65f, 0.65f, 0.65f));
            var matPickup = GetOrCreateMaterial("Assets/Materials/M_Pickup.mat", new Color(0.95f, 0.8f, 0.15f));

            // ---- 地面 / 墙 / 收集物：运行时由 MapLoader 按地图数据生成，场景里不再硬编码 ----

            // ---- 玩家（人形角色）：物理胶囊根节点 + X Bot 模型实例 ----
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            var cap = player.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.3f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            var rb = player.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            player.AddComponent<PlayerController>();

            // 模型子节点：只负责外观与朝向，物理根不旋转
            var modelPivot = new GameObject("Model");
            modelPivot.transform.SetParent(player.transform, false);
            EnsureMixamoImportSettings();
            Animator botAnimator = null;
            var botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/X Bot.fbx");
            if (botPrefab != null)
            {
                var bot = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab, modelPivot.transform);
                bot.transform.localPosition = Vector3.zero;
                bot.transform.localRotation = Quaternion.identity;
                CenterModelOnPivot(bot);   // 身体包围盒中心对齐旋转轴、脚底贴地，防转身公转
                botAnimator = bot.GetComponent<Animator>();
            }
            else
            {
                Debug.LogWarning("[DemoSceneBuilder] 找不到 Assets/Models/X Bot.fbx，玩家没有外观（物理胶囊仍在）");
            }
            var animatorCtrl = GetOrCreatePlayerAnimator();

            // ---- 相机 ----
            var camGO = Camera.main.gameObject;
            camGO.transform.position = new Vector3(0f, 10f, -8f);
            camGO.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            var follow = camGO.AddComponent<CameraFollow>();
            follow.target = player.transform;

            // ---- 玩家脚本引用 ----
            var pc = player.GetComponent<PlayerController>();
            pc.followCamera = camGO.GetComponent<Camera>();
            pc.moveSpeed = 6f;
            pc.animator = botAnimator;
            pc.modelPivot = modelPivot.transform;
            if (botAnimator != null)
            {
                botAnimator.runtimeAnimatorController = animatorCtrl;
                botAnimator.applyRootMotion = false;   // Mixamo 动画带位移曲线，位置必须由物理根驱动
            }

            // ---- 关卡数据（导入 maps/*.txt → Assets/Maps/*.asset，运行时由 MapLoader 生成）----
            var mapAsset = MapImporter.ImportAllAndGetDefault();
            if (mapAsset == null)
            {
                EditorUtility.DisplayDialog("Roll-a-Ball",
                    "maps/ 下没有可导入的地图（需要至少一个 map1.txt），关卡未生成。", "知道了");
                return;
            }
            var levelGO = new GameObject("Level");
            var loader = levelGO.AddComponent<MapLoader>();
            loader.map = mapAsset;
            loader.player = player.transform;
            loader.followCamera = camGO.GetComponent<Camera>();
            loader.groundMaterial = matGround;
            loader.wallMaterial = matWall;
            loader.pickupMaterial = matPickup;

            // ---- UI ----
            BuildUI(player);

            // ---- 灯光角度微调 ----
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ---- 保存场景并设为唯一构建场景 ----
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            EditorUtility.DisplayDialog("Roll-a-Ball",
                "场景已生成并保存到 Assets/Scenes/DemoScene.unity\n" +
                "已设为唯一构建场景。\n\n" +
                "下一步：菜单 Tools/Roll-a-Ball/2 应用 Android 打包设置。", "好的");
        }

        // ---------------- 菜单 2：Android 打包设置 ----------------
        [MenuItem("Tools/Roll-a-Ball/2 - 应用 Android 打包设置并切平台")]
        public static void ApplyAndroidSettings()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Roll-a-Ball",
                    "Android Build Support 模块尚未安装完成，请稍后再试。", "知道了");
                return;
            }

            PlayerSettings.companyName = "Demo";
            PlayerSettings.productName = "RollABall";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.demo.rollaball");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;   // Unity 6000.3 最低允许 25

            // 横屏
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            bool ok = EditorUtility.DisplayDialog("Roll-a-Ball",
                "已应用：IL2CPP / ARM64 / 包名 com.demo.rollaball / 横屏。\n\n" +
                "现在切换构建平台到 Android？会触发资源重导入（几分钟）。", "切换", "暂不");

            if (ok)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTarget.Android);
        }

        // ---------------- UI 构建 ----------------
        private static void BuildUI(GameObject player)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 分数（顶部居中）
            var scoreGO = CreateText(canvasGO.transform, "ScoreText", "Score: 0 / 8", font, 32, Color.white);
            SetRect(scoreGO.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(420f, 60f), new Vector2(0f, -15f));
            scoreGO.AddComponent<Outline>().effectColor = Color.black;

            // 胜利提示（居中，默认隐藏）
            var winGO = CreateText(canvasGO.transform, "WinText", "YOU WIN!", font, 56, new Color(1f, 0.85f, 0.1f));
            SetRect(winGO.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(600f, 100f), Vector2.zero);
            winGO.AddComponent<Outline>().effectColor = Color.black;
            winGO.SetActive(false);

            // 浮动摇杆热区（左下角 40% 宽 × 60% 高，全透明但接收触控；摇杆视觉由组件运行时自建）
            var zoneGO = new GameObject("JoystickZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VirtualJoystick));
            zoneGO.transform.SetParent(canvasGO.transform, false);
            var zoneRt = (RectTransform)zoneGO.transform;
            zoneRt.anchorMin = new Vector2(0f, 0f);
            zoneRt.anchorMax = new Vector2(0f, 0f);
            zoneRt.pivot = new Vector2(0f, 0f);
            zoneRt.sizeDelta = new Vector2(1280f * 0.4f, 720f * 0.6f);   // 基于参考分辨率 1280x720
            zoneRt.anchoredPosition = Vector2.zero;
            var zoneImg = zoneGO.GetComponent<Image>();
            zoneImg.color = Color.clear;
            zoneImg.raycastTarget = true;

            var joy = zoneGO.GetComponent<VirtualJoystick>();
            joy.circleSprite = GetOrCreateCircleSprite();
            joy.maxRadius = 90f;
            joy.idleAlpha = 0.28f;

            // 重新开始按钮（右上角）
            var restartGO = new GameObject("RestartButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(RestartButton));
            restartGO.transform.SetParent(canvasGO.transform, false);
            SetRect(restartGO.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(150f, 60f), new Vector2(-25f, -25f));
            restartGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.75f);

            var restartLabelGO = CreateText(restartGO.transform, "Label", "Restart", font, 28, Color.white);
            SetRect(restartLabelGO.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            // 抓取按钮（右下，与摇杆待机位同高；目前仅展示，无点击逻辑）
            var grabGO = new GameObject("GrabButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            grabGO.transform.SetParent(canvasGO.transform, false);
            SetRect(grabGO.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(160f, 160f), new Vector2(-160f, 185f));
            var grabImg = grabGO.GetComponent<Image>();
            grabImg.sprite = GetOrCreateRingSprite();
            grabImg.color = new Color(1f, 1f, 1f, 0.5f);   // 圆环 50% 透明
            grabImg.raycastTarget = true;

            // 中心图标（用户提供的 drag.png，50% 透明；缺失时只显示圆环不阻塞建场景）
            const string GrabIconPath = "Assets/UI/button_icon/drag.png";
            if (File.Exists(GrabIconPath))
            {
                ForceSpriteImport(GrabIconPath);
                var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrabIconPath);
                if (iconSprite != null)
                {
                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGO.transform.SetParent(grabGO.transform, false);
                    var iconRt = (RectTransform)iconGO.transform;
                    iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.sizeDelta = new Vector2(104f, 104f);   // 外径 160 的 65%
                    var iconImg = iconGO.GetComponent<Image>();
                    iconImg.sprite = iconSprite;
                    iconImg.color = new Color(1f, 1f, 1f, 0.5f);   // 图标 50% 透明
                    iconImg.raycastTarget = false;
                }
                else
                {
                    Debug.LogWarning($"[DemoSceneBuilder] {GrabIconPath} 导入后拿不到 Sprite，抓取按钮只有圆环");
                }
            }
            else
            {
                Debug.LogWarning($"[DemoSceneBuilder] 找不到 {GrabIconPath}，抓取按钮只有圆环");
            }

            // 玩家脚本挂 UI 引用
            var pc = player.GetComponent<PlayerController>();
            pc.scoreText = scoreGO.GetComponent<Text>();
            pc.winText = winGO.GetComponent<Text>();

            // 倒计时条（右下角，UI 由组件运行时自建）
            new GameObject("CountdownTimerUI", typeof(CountdownTimerUI));

            // 订单卡信息牌（左上角，UI 由组件运行时自建，并注入项目菜品图片）
            var orderUi = new GameObject("OrderCardsUI", typeof(OrderCardsUI)).GetComponent<OrderCardsUI>();
            OrderCardsScenePatcher.AssignIcons(orderUi);
        }

        private static GameObject CreateText(Transform parent, string name, string content, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return go;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // ---------------- 人形角色工具 ----------------

        /// <summary>
        /// 把模型可见身体对中到旋转轴上：渲染包围盒的 XZ 中心对齐模型根，
        /// 脚底（包围盒下沿）对齐根平面。否则转身时身体会绕轴心公转。
        /// </summary>
        private static void CenterModelOnPivot(GameObject modelRoot)
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            var rootPos = modelRoot.transform.position;
            var offsetXZ = bounds.center - rootPos;
            float footY = bounds.min.y - rootPos.y;

            var p = modelRoot.transform.localPosition;
            modelRoot.transform.localPosition =
                new Vector3(p.x - offsetXZ.x, p.y - footY, p.z - offsetXZ.z);
        }

        /// Mixamo FBX 导入设置：角色建 Humanoid Avatar；两个动画设 Humanoid 肌肉空间（同骨架自动重定向）
        private static void EnsureMixamoImportSettings()
        {
            const string botPath = "Assets/Models/X Bot.fbx";
            if (AssetImporter.GetAtPath(botPath) is ModelImporter bot &&
                bot.animationType != ModelImporterAnimationType.Human)
            {
                bot.animationType = ModelImporterAnimationType.Human;
                bot.SaveAndReimport();
            }

            foreach (string animPath in new[] { "Assets/Models/Idle.fbx", "Assets/Models/Walking.fbx" })
            {
                if (AssetImporter.GetAtPath(animPath) is ModelImporter imp)
                {
                    bool dirty = false;

                    if (imp.animationType != ModelImporterAnimationType.Human)
                    {
                        imp.animationType = ModelImporterAnimationType.Human;   // Humanoid 肌肉空间，同骨架自动重定向
                        dirty = true;
                    }

                    // 剪辑循环：Mixamo 导入默认 Loop Time 关闭，播完一遍会定格在末帧
                    // 注意：clipAnimations 在未被手动编辑过时返回空数组，必须以 defaultClipAnimations 为底稿
                    var clips = imp.clipAnimations;
                    bool seeded = false;
                    if (clips == null || clips.Length == 0)
                    {
                        clips = imp.defaultClipAnimations;
                        seeded = clips.Length > 0;
                    }

                    bool loopChanged = false;
                    foreach (var clip in clips)
                    {
                        if (!clip.loopTime) { clip.loopTime = true; loopChanged = true; }
                    }
                    if (loopChanged || seeded)
                    {
                        imp.clipAnimations = clips;
                        dirty = true;
                        Debug.Log($"[DemoSceneBuilder] {Path.GetFileName(animPath)} 已设置 {clips.Length} 个剪辑为循环播放");
                    }

                    if (dirty) imp.SaveAndReimport();
                }
            }
        }

        /// 程序化生成 Animator 状态机：MoveSpeed 参数驱动 Idle <-> Walk
        private static RuntimeAnimatorController GetOrCreatePlayerAnimator()
        {
            const string path = "Assets/Animations/PlayerAnimator.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            EnsureFolder("Assets/Animations");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            var root = ctrl.layers[0].stateMachine;

            var idle = root.AddState("Idle");
            idle.motion = LoadClipFromFbx("Assets/Models/Idle.fbx");
            var walk = root.AddState("Walk");
            walk.motion = LoadClipFromFbx("Assets/Models/Walking.fbx");
            root.defaultState = idle;

            var toWalk = idle.AddTransition(walk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");

            var toIdle = walk.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        /// 取 FBX 里的动画剪辑（Mixamo 的剪辑名通常是 "mixamo.com"）
        private static AnimationClip LoadClipFromFbx(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            Debug.LogWarning($"[DemoSceneBuilder] {fbxPath} 里没有可用的 AnimationClip");
            return null;
        }

        // ---------------- 资源工具 ----------------
        private static Material GetOrCreateMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        /// 抓取按钮的白色圆环（程序化生成，空心带抗锯齿）
        private static Sprite GetOrCreateRingSprite()
        {
            const string RingPath = "Assets/UI/button_ring.png";
            if (AssetDatabase.LoadAssetAtPath<Object>(RingPath) == null)
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float c = (size - 1) * 0.5f;
                const float outer = 126f;
                const float inner = 110f;   // 环厚 16 像素（显示在 160 按钮上约 10px）
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();

                Directory.CreateDirectory("Assets/UI");
                File.WriteAllBytes(RingPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(RingPath);
            }
            ForceSpriteImport(RingPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        /// 强制把贴图导入为 UI Sprite 模式（用户手放的 PNG 默认是 Texture 类型，UI Image 读不到）
        private static void ForceSpriteImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency || ti.mipmapEnabled))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (existing != null) return existing;

            // 程序化画一个抗锯齿白色圆
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            float r = c;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            Directory.CreateDirectory("Assets/UI");
            File.WriteAllBytes(CircleSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CircleSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(CircleSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        }

        private static void EnsureTag(string tag)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string leaf = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
