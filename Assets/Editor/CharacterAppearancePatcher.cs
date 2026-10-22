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
        private const string CatFbxPath = "Assets/Models/Cat/CuteCat.fbx";
        private const string AnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        [MenuItem("Tools/Roll-a-Ball/12 - 注入角色选择外观组件")]
        public static void AddPlayerAppearance()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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
                menuUi.characterPrefabs = new[]
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>(ChibiFbxPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(CatFbxPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFbxPath),
                };
                menuUi.chibiTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ChibiBoy/chibi_boy_basecolor.png");
                menuUi.catTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Cat/cute_cat_basecolor.png");
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
            appearance.catPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatFbxPath);
            appearance.chibiTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ChibiBoy/chibi_boy_basecolor.png");
            appearance.catTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Cat/cute_cat_basecolor.png");
            appearance.controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            appearance.modelPivot = pc.modelPivot;
            appearance.player = pc;
            appearance.defaultModel = pc.modelPivot != null && pc.modelPivot.childCount > 0
                ? pc.modelPivot.GetChild(0).gameObject
                : null;
        }
    }
}
