using System.IO;
using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给 Demo 场景注入全局音效播放器（SfxPlayer，跨场景常驻）：
    /// 按 Assets/Audio/Sfx/<名称>.ogg 的约定加载音效填进 clips（下标 = SfxKey）。
    /// 缺文件只告警不阻塞（SfxPlayer 对空位静默跳过）。幂等，可重复执行。
    /// 菜单：Tools/Roll-a-Ball/12
    /// </summary>
    public static class SfxScenePatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string SfxFolder = "Assets/Audio/Sfx";

        /// <summary>SfxKey 枚举顺序对应的源文件名（不含扩展名）。</summary>
        private static readonly string[] ClipNames =
        {
            "grab",         // Grab
            "place",        // Place
            "throw",        // Throw
            "chop",         // Chop
            "chop_done",    // ChopDone
            "serve_hit",    // ServeHit
            "serve_miss",   // ServeMiss
            "task_expired", // TaskExpired
            "tick",         // Tick
            "win",          // Win
            "lose",         // Lose
        };

        [MenuItem("Tools/Roll-a-Ball/12 - 给场景注入音效")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindFirstObjectByType<SfxPlayer>(FindObjectsInactive.Include);
            if (player == null)
            {
                var go = new GameObject("SfxPlayer", typeof(SfxPlayer));
                player = go.GetComponent<SfxPlayer>();
            }

            int found = 0;
            var missing = new System.Collections.Generic.List<string>();
            var clips = new AudioClip[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                string path = $"{SfxFolder}/{ClipNames[i]}.ogg";
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[i] != null) found++;
                else missing.Add(ClipNames[i]);
            }

            player.clips = clips;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (missing.Count > 0)
                Debug.LogWarning($"[SfxScenePatcher] 缺少 {missing.Count} 个音效（静默跳过播放）：{string.Join(", ", missing)}");
            Debug.Log($"[SfxScenePatcher] 音效注入完成：{found}/{ClipNames.Length}，场景已保存 {ScenePath}");
            EditorUtility.DisplayDialog("注入音效",
                found == ClipNames.Length
                    ? $"已注入全部 {found} 个音效并保存场景。"
                    : $"已注入 {found}/{ClipNames.Length} 个音效（缺：{string.Join("、", missing)}）。", "好的");
        }
    }
}
