using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RollABallEditorTools
{
    /// <summary>
    /// 生成/重建主菜单场景（MainMenuScene）：相机 + EventSystem + Canvas + MainMenuUI（注入 LevelRegistry）。
    /// 并把构建列表设为 [主菜单, DemoScene]，让游戏从主菜单启动。
    /// </summary>
    public static class MainMenuScenePatcher
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string GameScenePath = "Assets/Scenes/DemoScene.unity";
        private const string LevelRegistryPath = "Assets/GameInfo/LevelRegistry.asset";

        [MenuItem("Tools/Roll-a-Ball/6 - 生成主菜单场景并设为启动场景")]
        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);

            var ui = new GameObject("MainMenuUI", typeof(MainMenuUI)).GetComponent<MainMenuUI>();
            ui.registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(LevelRegistryPath);
            if (ui.registry == null)
                Debug.LogWarning($"[MainMenuScenePatcher] 未找到 {LevelRegistryPath}，请先跑菜单 3 导入地图（会顺带烘焙关卡列表）");

            // 构建列表：主菜单在前（启动场景），游戏场景在后
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log($"[MainMenuScenePatcher] 已生成 {MenuScenePath} 并把构建列表设为 [主菜单, DemoScene]");
        }
    }
}
