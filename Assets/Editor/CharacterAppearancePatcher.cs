using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给游戏场景的 Player 注入 PlayerAppearance（角色选择外观组件），
    /// 并接上三个形象的 FBX 引用与动画控制器。幂等：先销毁旧组件再建。
    /// 主菜单的"选择角色"按钮由 MainMenuUI 运行时自建（无需改主菜单场景）。
    /// </summary>
    public static class CharacterAppearancePatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string DefaultFbxPath = "Assets/Models/X Bot.fbx";
        private const string ChibiFbxPath = "Assets/Models/ChibiBoy/ChibiBoy.fbx";
        private const string CatRiggedPath = "Assets/Models/Cat/CatRigged2.fbx";
        private const string GirlFbxPath = "Assets/Models/Girl/Girl.fbx";
        private const string AnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        [MenuItem("Tools/Roll-a-Ball/12 - 注入角色选择外观组件")]
        public static void AddPlayerAppearance()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 所有可选形象都必须是 Humanoid 导入（否则没有 Avatar → 运行时无 Animator → 不播动画）
            EnsureHumanoidImport(GirlFbxPath);

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[CharacterAppearancePatcher] 场景中没有 PlayerController"); return; }

            foreach (var old in pc.GetComponentsInChildren<PlayerAppearance>())
                Object.DestroyImmediate(old);

            var appearance = pc.gameObject.AddComponent<PlayerAppearance>();
            AssignReferences(appearance, pc);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CharacterAppearancePatcher] PlayerAppearance 已注入并保存 {ScenePath}");

            // 主菜单场景：注入候选形象引用（3D 预览台用）
            var menuScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            var menuUi = Object.FindFirstObjectByType<MainMenuUI>();
            if (menuUi != null)
            {
                var chibiMatPath = EnsureChibiMaterial();
                var catMatPath = "Assets/Models/Cat/CatRiggedMat.mat";
                var girlMatPath = EnsureGirlMaterial();
                menuUi.characterPrefabs = new[]
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>(ChibiFbxPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(CatRiggedPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(GirlFbxPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFbxPath),
                };
                menuUi.chibiTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ChibiBoy/chibi_boy_basecolor.png");
                menuUi.catMaterial = AssetDatabase.LoadAssetAtPath<Material>(catMatPath);
                menuUi.cellMaterials = new[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>(chibiMatPath),
                    AssetDatabase.LoadAssetAtPath<Material>(catMatPath),
                    AssetDatabase.LoadAssetAtPath<Material>(girlMatPath),
                    null,   // X Bot 用原生材质
                };
                menuUi.cellYaw = new[] { 180f, 0f, 0f, 180f };   // 各模型原生朝向校正
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
                Debug.Log("[CharacterAppearancePatcher] 主菜单 characterPrefabs 已注入");
            }
            else
            {
                Debug.LogWarning("[CharacterAppearancePatcher] 主菜单场景没有 MainMenuUI 组件");
            }

            // 收尾切回游戏场景
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>注入引用（DemoSceneBuilder 重建场景后也调用）。</summary>
        public static void AssignReferences(PlayerAppearance appearance, PlayerController pc)
        {
            appearance.defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFbxPath);
            appearance.chibiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChibiFbxPath);
            appearance.catPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatRiggedPath);
            appearance.girlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GirlFbxPath);
            appearance.chibiTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ChibiBoy/chibi_boy_basecolor.png");
            appearance.catMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Cat/CatRiggedMat.mat");
            appearance.girlMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnsureGirlMaterial());
            appearance.controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            appearance.modelPivot = pc.modelPivot;
            appearance.player = pc;
            appearance.defaultModel = pc.modelPivot != null && pc.modelPivot.childCount > 0
                ? pc.modelPivot.GetChild(0).gameObject
                : null;
        }

        /// <summary>把 FBX 切成 Humanoid 导入（Mixamo 骨骼自动映射，生成 Avatar 才能用人形动画）。</summary>
        private static void EnsureHumanoidImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            if (importer.animationType == ModelImporterAnimationType.Human) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAppearancePatcher] {fbxPath} → Humanoid 导入");
        }

        /// <summary>小女孩成品材质（贴图从 FBX 内嵌提取：Assets/Textures/girl_tex0.png）。</summary>
        private static string EnsureGirlMaterial()
        {
            const string path = "Assets/Models/Girl/GirlMat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/girl_tex0.png");
            if (tex != null) mat.mainTexture = tex;
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterAppearancePatcher] 小女孩材质贴图: {(tex != null ? tex.name : "无")}");
            return path;
        }

        /// <summary>豆豆男孩成品材质（预览台用，与运行时一致）。</summary>
        private static string EnsureChibiMaterial()
        {
            const string path = "Assets/Models/ChibiBoy/ChibiMat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ChibiBoy/chibi_boy_basecolor.png");
            if (tex != null) mat.mainTexture = tex;
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return path;
        }
    }
}
