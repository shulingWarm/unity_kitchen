using System.Collections.Generic;
using System.Linq;
using RollABall;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给玩家接入切菜动作 Assets/Models/chop.fbx（纯骨骼、无蒙皮）：
    /// 1) 导入为 Humanoid，只保留 ChopFirstFrame~ChopLastFrame 帧并循环，自动映射失败时用显式骨骼表兜底；
    /// 2) 在 PlayerAnimator 里加 IsChopping 参数、Chop 状态与进出转换（幂等）；
    /// 3) 顺带刷新 Demo 场景交互按钮的环形进度引用。
    /// 菜单：Tools/Roll-a-Ball/11
    /// </summary>
    public static class ChopAnimationPatcher
    {
        public const string FbxPath = "Assets/Models/chop.fbx";
        private const string ControllerPath = "Assets/Animations/PlayerAnimator.controller";
        private const string ScenePath = "Assets/Scenes/DemoScene.unity";

        // 只保留源动画的该帧区间作为切菜动作（20~60 帧是有效切菜段）
        private const float ChopFirstFrame = 20f;
        private const float ChopLastFrame = 60f;

        [MenuItem("Tools/Roll-a-Ball/11 - 给玩家接入切菜动画")]
        public static void Apply()
        {
            AnimationClip clip = ConfigureImport();
            if (clip == null)
            {
                EditorUtility.DisplayDialog("切菜动画",
                    $"没能从 {FbxPath} 读到 AnimationClip，详情见 Console。", "好的");
                return;
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                EditorUtility.DisplayDialog("切菜动画",
                    $"找不到 {ControllerPath}，请先跑菜单 1 生成场景与状态机。", "好的");
                return;
            }

            DemoSceneBuilder.EnsureChopState(ctrl);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            RefreshSceneButton();

            Debug.Log($"[ChopAnimationPatcher] 切菜动画已接入：clip '{clip.name}' 取帧 {ChopFirstFrame:0}~{ChopLastFrame:0}，" +
                      $"时长 {clip.length:0.###}s，路径 {FbxPath}");
            EditorUtility.DisplayDialog("切菜动画",
                "已接入 chop.fbx：\n- Humanoid 导入 + 循环\n- 状态机新增 Chop 状态（IsChopping 驱动）\n" +
                "- 交互按钮补上环形进度\n\n进 Play 模式按住交互键 3 秒即可切菜。", "好的");
        }

        // ---------------- 导入配置 ----------------

        /// <summary>强制导入 → Humanoid → 剪辑循环 → 校验 Avatar（无效时显式映射）。</summary>
        private static AnimationClip ConfigureImport()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[ChopAnimationPatcher] 拿不到 ModelImporter：{FbxPath}（文件是否存在？）");
                return null;
            }

            // 第一阶段：切到 Humanoid 并落地重导入（刚切换类型时剪辑列表还没生成，需先重导入再取）
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
            }

            // 第二阶段：每个剪辑只保留 ChopFirstFrame~ChopLastFrame 帧并设为循环（按住期间反复播放）
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                bool changed = false;
                foreach (var c in clips)
                {
                    if (c.firstFrame != ChopFirstFrame) { c.firstFrame = ChopFirstFrame; changed = true; }
                    if (c.lastFrame != ChopLastFrame) { c.lastFrame = ChopLastFrame; changed = true; }
                    if (!c.loopTime) { c.loopTime = true; changed = true; }
                }
                if (changed)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
            else
            {
                Debug.LogWarning("[ChopAnimationPatcher] chop.fbx 里没有导入出任何动画剪辑（AnimationStack 为空？）");
            }

            Avatar avatar = LoadAvatar();
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogWarning("[ChopAnimationPatcher] Humanoid 自动映射不完整，改用显式骨骼表重映射");
                var hd = importer.humanDescription;
                hd.human = BuildHumanBones();
                if (hd.skeleton == null || hd.skeleton.Length == 0)
                    hd.skeleton = BuildSkeleton();
                importer.humanDescription = hd;
                importer.SaveAndReimport();

                avatar = LoadAvatar();
            }

            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError("[ChopAnimationPatcher] chop.fbx 的 Humanoid Avatar 仍无效，" +
                               "请到 Rig 页签手动检查骨骼映射（Hips/Spine/Head/四肢必需）");
            }
            else
            {
                LogMappedBones(avatar);
            }

            return LoadClip();
        }

        private static Avatar LoadAvatar()
            => AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();

        private static AnimationClip LoadClip()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            Debug.LogError($"[ChopAnimationPatcher] {FbxPath} 里没有可用的 AnimationClip");
            return null;
        }

        /// <summary>
        /// 纯骨骼 fbx 的骨骼命名不是 Mixamo 风格，自动映射常缺腿/颈；这里显式给出对应关系。
        /// 腿只有 3 段（LeftLeg → LeftFoot → LeftToeBase），依次映射上/下腿与脚，脚趾留空。
        /// </summary>
        private static readonly (HumanBodyBones bone, string source)[] ExplicitBones =
        {
            (HumanBodyBones.Hips, "Hips"),
            (HumanBodyBones.Spine, "Spine1"),
            (HumanBodyBones.Chest, "Spine2"),
            (HumanBodyBones.UpperChest, "Chest"),
            (HumanBodyBones.Neck, "Neck1"),
            (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.LeftShoulder, "LeftShoulder"),
            (HumanBodyBones.RightShoulder, "RightShoulder"),
            (HumanBodyBones.LeftUpperArm, "LeftArm"),
            (HumanBodyBones.RightUpperArm, "RightArm"),
            (HumanBodyBones.LeftLowerArm, "LeftForeArm"),
            (HumanBodyBones.RightLowerArm, "RightForeArm"),
            (HumanBodyBones.LeftHand, "LeftHand"),
            (HumanBodyBones.RightHand, "RightHand"),
            (HumanBodyBones.LeftUpperLeg, "LeftLeg"),
            (HumanBodyBones.RightUpperLeg, "RightLeg"),
            (HumanBodyBones.LeftLowerLeg, "LeftFoot"),
            (HumanBodyBones.RightLowerLeg, "RightFoot"),
            (HumanBodyBones.LeftFoot, "LeftToeBase"),
            (HumanBodyBones.RightFoot, "RightToeBase"),
        };

        private static HumanBone[] BuildHumanBones()
        {
            var human = new HumanBone[(int)HumanBodyBones.LastBone];
            foreach (var (bone, source) in ExplicitBones)
                SetBone(human, bone, source);

            AddFinger(human, "Left", "Thumb", HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal);
            AddFinger(human, "Right", "Thumb", HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal);
            AddFinger(human, "Left", "Index", HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
            AddFinger(human, "Right", "Index", HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
            AddFinger(human, "Left", "Middle", HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal);
            AddFinger(human, "Right", "Middle", HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal);
            AddFinger(human, "Left", "Ring", HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal);
            AddFinger(human, "Right", "Ring", HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal);
            AddFinger(human, "Left", "Pinky", HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
            AddFinger(human, "Right", "Pinky", HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
            return human;
        }

        private static void AddFinger(HumanBone[] human, string side, string finger,
            HumanBodyBones proximal, HumanBodyBones intermediate, HumanBodyBones distal)
        {
            SetBone(human, proximal, $"{side}Hand{finger}1");
            SetBone(human, intermediate, $"{side}Hand{finger}2");
            SetBone(human, distal, $"{side}Hand{finger}3");
        }

        private static void SetBone(HumanBone[] human, HumanBodyBones bone, string sourceBone)
        {
            human[(int)bone] = new HumanBone
            {
                humanName = bone.ToString(),
                boneName = sourceBone,
                limit = new HumanLimit { useDefaultValues = true },
            };
        }

        /// <summary>按导入后的层级生成骨架表（仅在自动映射没有留下 skeleton 时兜底）。</summary>
        private static SkeletonBone[] BuildSkeleton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (prefab == null) return null;

            var list = new List<SkeletonBone>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                list.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                });
            }
            return list.ToArray();
        }

        private static void LogMappedBones(Avatar avatar)
        {
            var missing = new List<string>();
            foreach (var h in avatar.humanDescription.human)
                if (!string.IsNullOrEmpty(h.humanName) && string.IsNullOrEmpty(h.boneName))
                    missing.Add(h.humanName);
            if (missing.Count > 0)
                Debug.LogWarning($"[ChopAnimationPatcher] 仍未映射的可选骨骼：{string.Join(", ", missing)}");
        }

        // ---------------- 场景刷新 ----------------

        /// <summary>打开 Demo 场景，把进度环和物品注册表引用补到交互按钮/玩家上。</summary>
        private static void RefreshSceneButton()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var loader = Object.FindFirstObjectByType<MapLoader>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var pc = loader != null && loader.player != null
                ? loader.player.GetComponent<PlayerController>()
                : Object.FindFirstObjectByType<PlayerController>();
            if (pc == null || canvas == null)
            {
                Debug.LogWarning("[ChopAnimationPatcher] 场景里缺 PlayerController 或 Canvas，交互按钮未刷新");
                return;
            }

            pc.itemRegistry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(TaskCardsScenePatcher.RegistryPath);
            TaskCardsScenePatcher.CreateInteractButton(canvas, pc);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChopAnimationPatcher] 已刷新交互按钮进度环并保存 {ScenePath}");
        }
    }
}
