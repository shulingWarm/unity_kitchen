using System.Linq;
using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 豆豆男孩形象补丁器 + 行走动作骨骼配置对齐。
    /// </summary>
    public static class ChibiBoyPatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string FbxPath = "Assets/Models/ChibiBoy/ChibiBoy.fbx";
        private const string TexturePath = "Assets/Models/ChibiBoy/chibi_boy_basecolor.png";
        private const string MaterialPath = "Assets/Models/ChibiBoy/BoyMat.mat";
        private const string AnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        [MenuItem("Tools/Roll-a-Ball/8 - 用豆豆男孩模型替换玩家形象")]
        public static void ReplacePlayerModelWithChibiBoy()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureHumanoidImport();

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[ChibiBoyPatcher] 场景中没有 PlayerController"); return; }
            var pivot = pc.modelPivot != null ? pc.modelPivot : pc.transform.Find("Model");
            if (pivot == null) { Debug.LogError("[ChibiBoyPatcher] 玩家身上没有 Model 节点"); return; }

            float targetHeight = 2.12f;
            var oldBounds = ComputeRendererBounds(pivot.gameObject);
            if (oldBounds.size.y > 0.01f) targetHeight = oldBounds.size.y;

            for (int i = pivot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(pivot.GetChild(i).gameObject);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError($"[ChibiBoyPatcher] 未找到模型 {FbxPath}"); return; }
            var boy = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot);
            boy.name = "ChibiBoyModel";
            var boyMat = GetOrCreateBoyMaterial();
            foreach (var r in boy.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = boyMat;

            var avatar = LoadChibiAvatar();
            var anim = boy.GetComponent<Animator>() ?? boy.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            anim.applyRootMotion = false;

            var handIK = boy.AddComponent<PlayerHandIK>();
            handIK.player = pc;

            pc.animator = anim;
            pc.mouthAnchor = null;

            var bounds = ComputeRendererBounds(boy);
            if (bounds.size.y > 0.0001f)
            {
                float scale = targetHeight / bounds.size.y;
                boy.transform.localScale = Vector3.one * scale;
                bounds = ComputeRendererBounds(boy);
            }
            var center = bounds.center;
            boy.transform.position += new Vector3(
                pivot.position.x - center.x,
                pivot.position.y - bounds.min.y,
                pivot.position.z - center.z);

            var oldProcedural = boy.GetComponent<CatPlayerModel>();
            if (oldProcedural != null) Object.DestroyImmediate(oldProcedural);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChibiBoyPatcher] 玩家形象已替换为豆豆男孩（目标高度 {targetHeight:F2}m，四状态动画绑定）并保存 {ScenePath}");
        }

        [MenuItem("Tools/Roll-a-Ball/9 - 修正行走动作的骨骼配置")]
        public static void MatchWalkAnimationAvatar()
        {
            var avatar = LoadChibiAvatar();
            DumpAvatarMapping();

            const string walkPath = "Assets/Models/Walking.fbx";
            var importer = AssetImporter.GetAtPath(walkPath) as ModelImporter;
            if (importer == null) { Debug.LogError($"[ChibiBoyPatcher] 未找到 {walkPath}"); return; }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.sourceAvatar = avatar;   // 动作按豆豆男孩的骨骼长度重定向
            importer.SaveAndReimport();
            Debug.Log("[ChibiBoyPatcher] Walking.fbx 的 Avatar 配置已对齐豆豆男孩");
        }

        /// <summary>导出 Avatar 的骨骼映射表到 TMP/avatar_mapping.txt（诊断左脚滑步用）。</summary>
        [MenuItem("Tools/Roll-a-Ball/10 - 导出豆豆男孩骨骼映射")]
        public static void DumpAvatarMapping()
        {
            var avatar = LoadChibiAvatar();
            var human = avatar.humanDescription.human;
            var lines = human
                .Where(h => h.humanName.Contains("Leg") || h.humanName.Contains("Foot") || h.humanName.Contains("Toe"))
                .Select(h => $"{h.humanName}  <=  {h.boneName}");
            System.IO.File.WriteAllLines("D:/Projects/github/TMP/avatar_mapping.txt", lines);
            Debug.Log($"[ChibiBoyPatcher] 腿部映射已导出（共 {human.Length} 项映射）");
        }

        private static Avatar LoadChibiAvatar()
        {
            return AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
        }

        private static void EnsureHumanoidImport()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) return;
            if (importer.animationType == ModelImporterAnimationType.Human) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
            Debug.Log("[ChibiBoyPatcher] FBX 已切换为 Humanoid 导入（自动骨骼映射）");
        }

        private static Material GetOrCreateBoyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            mat = new Material(Shader.Find("Standard"));
            if (tex != null) mat.mainTexture = tex;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static Bounds ComputeRendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
