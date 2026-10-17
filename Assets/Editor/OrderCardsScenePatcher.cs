using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给现有 Demo 场景挂上订单卡（并把项目里的菜品图片注入 Inspector 字段）。
    /// 已存在 OrderCardsUI 时会销毁重建（保证用最新逻辑与贴图引用）。
    /// 供编辑器菜单和 batchmode -executeMethod 调用。
    /// </summary>
    public static class OrderCardsScenePatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string TomatoPath = "Assets/cube_images/tomato.png";
        private const string CornPath = "Assets/cube_images/corn.png";

        [MenuItem("Tools/Roll-a-Ball/5 - 给场景添加订单卡信息牌")]
        public static void AddOrderCardsToDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 旧版组件直接替换，保证始终是最新逻辑 + 最新贴图引用
            foreach (var old in Object.FindObjectsByType<OrderCardsUI>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var ui = new GameObject("OrderCardsUI", typeof(OrderCardsUI)).GetComponent<OrderCardsUI>();
            AssignIcons(ui);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[OrderCardsScenePatcher] 已重建 OrderCardsUI 并保存 {ScenePath}");
        }

        // 注入项目贴图（DemoSceneBuilder 重建场景时也应调用）
        public static void AssignIcons(OrderCardsUI ui)
        {
            ui.tomatoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TomatoPath);
            ui.cornTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CornPath);
            if (ui.tomatoTexture == null) Debug.LogWarning($"[OrderCardsScenePatcher] 未找到 {TomatoPath}");
            if (ui.cornTexture == null) Debug.LogWarning($"[OrderCardsScenePatcher] 未找到 {CornPath}");
        }
    }
}
