using System.Linq;
using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 用可爱猫模型（Tripo FBX，内含完整人形骨架 45 骨）替换玩家的人形形象：
    /// - FBX 导入切到 Humanoid（骨骼按 Mixamo 惯例自动映射），生成 Avatar
    /// - 挂项目现成的 PlayerAnimator 控制器：站立/行走/抱物站立/抱物行走 四状态真动画
    /// - 挂 PlayerHandIK 恢复双手捧球 IK；高度对齐旧模型、脚底贴地、XZ 对中旋转轴
    /// 幂等：重复执行先清后建。供菜单和 batchmode 调用。
    /// </summary>
    public static class CatModelPatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string FbxPath = "Assets/Models/Cat/CuteCat.fbx";
        private const string TexturePath = "Assets/Models/Cat/cute_cat_basecolor.png";
        private const string MaterialPath = "Assets/Models/Cat/CatMat.mat";
        private const string AnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        [MenuItem("Tools/Roll-a-Ball/7 - 用猫咪模型替换玩家形象")]
        public static void ReplacePlayerModelWithCat()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureHumanoidImport();

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[CatModelPatcher] 场景中没有 PlayerController"); return; }
            var pivot = pc.modelPivot != null ? pc.modelPivot : pc.transform.Find("Model");
            if (pivot == null) { Debug.LogError("[CatModelPatcher] 玩家身上没有 Model 节点"); return; }

            // 目标高度 = 现有模型包围盒高度（保持与物理胶囊一致的视觉尺寸）
            float targetHeight = 2.12f;
            var oldBounds = ComputeRendererBounds(pivot.gameObject);
            if (oldBounds.size.y > 0.01f) targetHeight = oldBounds.size.y;

            // 清空旧模型
            for (int i = pivot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(pivot.GetChild(i).gameObject);

            // 实例化猫咪 + 材质
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError($"[CatModelPatcher] 未找到模型 {FbxPath}"); return; }
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot);
            cat.name = "CatModel";
            var catMat = GetOrCreateCatMaterial();
            foreach (var r in cat.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = catMat;

            // 人形 Avatar + 项目现成的四状态动画控制器
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null) { Debug.LogError("[CatModelPatcher] FBX 里没有 Humanoid Avatar（骨骼映射失败）"); return; }
            Debug.Log($"[CatModelPatcher] Avatar 有效: {avatar.isValid}");

            var anim = cat.GetComponent<Animator>() ?? cat.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            anim.applyRootMotion = false;   // 位置由物理根驱动

            // 手部 IK 恢复：捧球时双手合拢
            var handIK = cat.AddComponent<PlayerHandIK>();
            handIK.player = pc;

            pc.animator = anim;
            pc.mouthAnchor = null;          // 人形有手了，不再叼球

            // 归一化：高度对齐 + 脚底贴地 + XZ 对中旋转轴
            var bounds = ComputeRendererBounds(cat);
            if (bounds.size.y > 0.0001f)
            {
                float scale = targetHeight / bounds.size.y;
                cat.transform.localScale = Vector3.one * scale;
                bounds = ComputeRendererBounds(cat);
            }
            var center = bounds.center;
            cat.transform.position += new Vector3(
                pivot.position.x - center.x,
                pivot.position.y - bounds.min.y,
                pivot.position.z - center.z);

            // 不再需要程序化小跳（真动画接管）
            var oldProcedural = cat.GetComponent<CatPlayerModel>();
            if (oldProcedural != null) Object.DestroyImmediate(oldProcedural);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CatModelPatcher] 玩家形象已替换为人形猫咪（目标高度 {targetHeight:F2}m，四状态动画绑定）并保存 {ScenePath}");
        }

        // FBX 导入切到 Humanoid：骨骼名符合 Mixamo 惯例，Unity 自动映射
        private static void EnsureHumanoidImport()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) return;
            if (importer.animationType == ModelImporterAnimationType.Human) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
            Debug.Log("[CatModelPatcher] FBX 已切换为 Humanoid 导入（自动骨骼映射）");
        }

        private static Material GetOrCreateCatMaterial()
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
