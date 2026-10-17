using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给现有 Demo 场景挂上倒计时条（不改其他任何内容）。
    /// 供编辑器菜单和 batchmode -executeMethod 调用。
    /// </summary>
    public static class TimerScenePatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";

        [MenuItem("Tools/Roll-a-Ball/3 - 给场景添加倒计时条")]
        public static void AddTimerToDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<CountdownTimerUI>() != null)
            {
                Debug.Log("[TimerScenePatcher] 场景中已存在 CountdownTimerUI，跳过。");
                return;
            }

            new GameObject("CountdownTimerUI", typeof(CountdownTimerUI));

            // 场景里若没有 Canvas（理论上不会），运行时组件会自建；这里只保证物体存在
            if (Object.FindFirstObjectByType<Canvas>() == null)
                Debug.LogWarning("[TimerScenePatcher] 场景中未找到 Canvas，计时条将在运行时自建 Canvas。");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TimerScenePatcher] 已添加 CountdownTimerUI 并保存 {ScenePath}");
        }
    }
}
