using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RollABallEditorTools
{
    /// <summary>
    /// 在游戏 HUD 右上角 Restart 按钮左侧添加"返回主菜单"按钮。
    /// 幂等：已存在则重建。供菜单和 batchmode 调用。
    /// </summary>
    public static class GameHudPatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";

        [MenuItem("Tools/Roll-a-Ball/17 - 添加返回主菜单按钮")]
        public static void AddBackToMenuButton()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 找 Restart 按钮作为定位基准
            GameObject restartGO = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "RestartButton") { restartGO = t.gameObject; break; }
            }
            if (restartGO == null) { Debug.LogError("[GameHudPatcher] 场景里找不到 RestartButton"); return; }

            var parent = restartGO.transform.parent;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 幂等：清理旧按钮
            var existing = parent.Find("BackToMenuButton");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // 定位：与 Restart 同锚点（右上），放在其左侧
            var restartRt = (RectTransform)restartGO.transform;
            float restartW = restartRt.sizeDelta.x;
            float restartX = restartRt.anchoredPosition.x;
            float spacing = 12f;

            var go = new GameObject("BackToMenuButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(BackToMenuButton));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = restartRt.anchorMin;
            rt.anchorMax = restartRt.anchorMax;
            rt.pivot = restartRt.pivot;
            rt.sizeDelta = new Vector2(200f, restartRt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(restartX - restartW - spacing, restartRt.anchoredPosition.y);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.75f);   // 与 Restart 同底色

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.text = "返回主菜单";
            text.font = font;
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            go.GetComponent<BackToMenuButton>().menuSceneName = "MainMenuScene";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameHudPatcher] 返回主菜单按钮已添加到 Restart 左侧（{rt.sizeDelta} @ {rt.anchoredPosition}）");
        }
    }
}
