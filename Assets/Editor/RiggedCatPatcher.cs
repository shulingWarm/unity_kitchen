using System.Linq;
using RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 用绑骨猫咪（Mixamo 人形骨架）替换玩家形象：
    /// Humanoid 导入自动映射骨骼 → 复用 PlayerAnimator 四状态控制器
    /// （站立/行走/抱物站立/抱物行走）+ PlayerHandIK（拿菜/捧球手部 IK）。
    /// 幂等：先清 modelPivot 再建。菜单 13 / batchmode 可执行。
    /// </summary>
    public static class RiggedCatPatcher
    {
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";
        private const string FbxPath = "Assets/Models/Cat/CatRigged2.fbx";
        private const string AnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        [MenuItem("Tools/Roll-a-Ball/13 - 用绑骨猫咪替换玩家形象")]
        public static void ReplacePlayerModelWithRiggedCat()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureHumanoidImport();

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[RiggedCatPatcher] 场景中没有 PlayerController"); return; }
            var pivot = pc.modelPivot != null ? pc.modelPivot : pc.transform.Find("Model");
            if (pivot == null) { Debug.LogError("[RiggedCatPatcher] 玩家身上没有 Model 节点"); return; }

            // 目标高度 = 现有模型包围盒高度（保持与物理胶囊一致）
            float targetHeight = 1.8f;
            var oldBounds = ComputeRendererBounds(pivot.gameObject);
            if (oldBounds.size.y > 0.01f) targetHeight = oldBounds.size.y;

            for (int i = pivot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(pivot.GetChild(i).gameObject);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError($"[RiggedCatPatcher] 未找到模型 {FbxPath}"); return; }
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pivot);
            cat.name = "CatModel";

            // 材质接线：优先保留 FBX 自带材质（内嵌贴图与模型 UV 匹配）；
            // 只有自带材质缺失或无贴图时才回退到自定义材质
            var catMat = GetOrCreateRiggedCatMaterial();
            foreach (var r in cat.GetComponentsInChildren<Renderer>())
            {
                var builtin = r.sharedMaterial;
                if (builtin != null && builtin.mainTexture != null)
                {
                    Debug.Log($"[RiggedCatPatcher] 保留 FBX 自带材质: {builtin.name}, 贴图: {builtin.mainTexture.name}");
                    continue;
                }
                r.sharedMaterial = catMat;
                Debug.Log($"[RiggedCatPatcher] 回退自定义材质（自带材质: {(builtin != null ? builtin.name : "null")}）");
            }

            // 人形 Avatar + 项目四状态动画控制器
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null) { Debug.LogError("[RiggedCatPatcher] FBX 里没有 Humanoid Avatar（Mixamo 骨骼映射失败）"); return; }
            Debug.Log($"[RiggedCatPatcher] Avatar 有效: {avatar.isValid}");

            var anim = cat.GetComponent<Animator>() ?? cat.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            anim.applyRootMotion = false;   // 位置由物理根驱动
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 防止误剔除导致骨骼冻结（T-pose 假死）

            // 手部 IK：拿菜/捧球时双手合拢
            var handIK = cat.AddComponent<PlayerHandIK>();
            handIK.player = pc;

            pc.animator = anim;
            pc.mouthAnchor = null;   // 人形用手捧，不再叼球

            // 归一化：高度对齐旧模型 + 脚底贴地 + XZ 对中旋转轴
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

            // 清掉可能残留的程序化动画组件（真动画接管）
            var oldProcedural = cat.GetComponent<CatPlayerModel>();
            if (oldProcedural != null) Object.DestroyImmediate(oldProcedural);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RiggedCatPatcher] 玩家形象已替换为绑骨猫咪（目标高度 {targetHeight:F2}m，四状态动画绑定）并保存 {ScenePath}");
        }

        /// <summary>导出绑骨猫 Avatar 的人形骨骼映射表到 工程内 TMP/cat_mapping.txt（诊断动画不生效用）。</summary>
        [MenuItem("Tools/Roll-a-Ball/14 - 导出绑骨猫骨骼映射")]
        public static void DumpCatAvatarMapping()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null) { Debug.LogError("[RiggedCatPatcher] 未找到 Avatar"); return; }

            var human = avatar.humanDescription.human;
            var lines = human.Select(h => $"{h.humanName}  <=  {h.boneName}").ToList();

            try
            {
                System.IO.Directory.CreateDirectory("TMP");
                System.IO.File.WriteAllLines("TMP/cat_mapping.txt", lines);
                Debug.Log($"[RiggedCatPatcher] 已导出 {lines.Count} 项映射到 TMP/cat_mapping.txt");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RiggedCatPatcher] 写文件失败（不影响诊断）: {e.Message}");
            }

            // 日志兜底：批处理环境下直接从日志读
            foreach (var line in lines)
                Debug.Log("[RiggedCatPatcher] " + line);
        }

        private static Material GetOrCreateRiggedCatMaterial()
        {
            const string matPath = "Assets/Models/Cat/CatRiggedMat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            // 贴图：v2 FBX 内嵌贴图（必须用模型自己的 UV 对应贴图，GLB 的 UV 不匹配）
            var tex = FindEmbeddedTexture();
            if (tex == null)
                Debug.LogWarning("[RiggedCatPatcher] 未找到 FBX 内嵌贴图，模型可能显示为白色");
            if (tex != null) mat.mainTexture = tex;

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RiggedCatPatcher] 猫材质贴图: {(tex != null ? tex.name + " (" + tex.width + "x" + tex.height + ")" : "无")}");
            return mat;
        }

        /// <summary>找 FBX 内嵌贴图：先查 FBX 子资产，再查同目录 .fbm 文件夹里的贴图。</summary>
        private static Texture2D FindEmbeddedTexture()
        {
            // 0) 从 FBX 二进制直接提取的内嵌贴图（UV 与模型匹配，最可靠）
            var direct = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/catrig2_tex0_embedded.png");
            if (direct != null) { Debug.Log($"[RiggedCatPatcher] 贴图来自 FBX 内嵌提取: {direct.name} ({direct.width}x{direct.height})"); return direct; }

            // 1) FBX 子资产（materialImportMode=2 时贴图作为子资产存在）
            var tex = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<Texture2D>()
                .OrderByDescending(t => t.width * t.height)
                .FirstOrDefault();
            if (tex != null) { Debug.Log($"[RiggedCatPatcher] 贴图来自 FBX 子资产: {tex.name}"); return tex; }

            // 2) .fbm 文件夹（materialImportMode=1 提取材质时生成）
            string dir = System.IO.Path.GetDirectoryName(FbxPath);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(FbxPath);
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"{dir}/{baseName}.fbm" });
            foreach (var g in guids)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null) { Debug.Log($"[RiggedCatPatcher] 贴图来自 .fbm: {t.name}"); return t; }
            }

            // 3) 兜底：同目录下名字含 basecolor/BaseColor 的贴图
            guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var name = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                if (name.Contains("basecolor") || name.Contains("color") || name.Contains("diffuse"))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (t != null) { Debug.Log($"[RiggedCatPatcher] 贴图来自同目录: {t.name}"); return t; }
                }
            }
            return null;
        }

        private static void EnsureHumanoidImport()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
                Debug.Log("[RiggedCatPatcher] FBX 已切换为 Humanoid 导入（Mixamo 骨骼自动映射）");
            }
            // ImportStandard：把内嵌贴图/材质提取到 .fbm 文件夹，白模的关键修复
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                changed = true;
                Debug.Log("[RiggedCatPatcher] 材质导入模式 → ImportStandard（提取内嵌贴图）");
            }
            if (changed) importer.SaveAndReimport();
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
