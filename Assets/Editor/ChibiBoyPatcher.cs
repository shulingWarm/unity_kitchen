using System.IO;
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
            ApplyChibiBoyToScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChibiBoyPatcher] 已替换为豆豆男孩并保存 {ScenePath}");
        }

        /// <summary>
        /// 给当前已打开场景的玩家换上豆豆男孩（幂等）：
        /// 骨骼 Humanoid 导入、材质、四状态动画、手部 IK、身高贴地归一化。
        /// 不开场景不存盘，供菜单 8 与 DemoSceneBuilder 构建流程复用。
        /// </summary>
        public static void ApplyChibiBoyToScene()
        {
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

            Debug.Log($"[ChibiBoyPatcher] 玩家形象已替换为豆豆男孩（目标高度 {targetHeight:F2}m，四状态动画绑定）");
        }

        [MenuItem("Tools/Roll-a-Ball/9 - 修正行走动作的骨骼配置")]
        public static void MatchWalkAnimationAvatar()
        {
            const string walkPath = "Assets/Models/Walking.fbx";
            var importer = AssetImporter.GetAtPath(walkPath) as ModelImporter;
            if (importer == null) { Debug.LogError($"[ChibiBoyPatcher] 未找到 {walkPath}"); return; }

            // 不拷贝豆豆男孩的 rig：两边骨骼命名不同（Walking 是 mixamorig:*，ChibiBoy 是 Hip 等），
            // 拷贝必然报 "Transform hierarchy does not match"。
            // Humanoid 动画走肌肉空间自动重定向，Walking.fbx 用自己的自动映射即可在豆豆男孩上正常播放。
            if (importer.animationType != ModelImporterAnimationType.Human || importer.sourceAvatar != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.sourceAvatar = null;
                importer.SaveAndReimport();
                Debug.Log("[ChibiBoyPatcher] Walking.fbx 已重置为 Humanoid 自动映射（骨骼命名与豆豆男孩不同，不做 rig 拷贝）");
            }
            else
            {
                Debug.Log("[ChibiBoyPatcher] Walking.fbx 已是 Humanoid 自动映射，无需修正");
            }
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

            // 写到工程目录内（原硬编码路径 D:/Projects/github/TMP 在其他机器上不存在）
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string dumpPath = Path.Combine(projectRoot, "TMP", "avatar_mapping.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
            File.WriteAllLines(dumpPath, lines);
            Debug.Log($"[ChibiBoyPatcher] 腿部映射已导出（共 {human.Length} 项映射）→ {dumpPath}");
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
