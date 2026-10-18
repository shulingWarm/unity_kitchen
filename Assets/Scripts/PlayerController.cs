using System.Collections.Generic;
using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 玩家（人形角色）：读取虚拟摇杆输入做相机相对移动，
    /// 模型朝移动方向转身，Animator 按移动状态切换 Idle/Walk。
    /// 抓取/放置/拾取/交付共用一个按钮，按状态自动分流：
    /// 手持物品 → 出餐口旁交付（物品必消耗，命中任务卡才算完成）；否则原地放下（物理自由落体）。
    /// 空手 → 先捡起近处地面物品（WorldItem），没有才从最近的木箱拿对应食材模型。
    /// 手持物品统一挂在胸口骨骼上、双手 IK 捧住。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 6f;
        public Camera followCamera;       // 用于把摇杆方向换算成世界方向
        public Animator animator;         // 场景生成器注入（挂在模型实例上；猫咪模型时为 null）
        public Transform modelPivot;      // 模型父节点：只负责朝向，物理根不旋转
        public Transform mouthAnchor;     // 嘴部锚点：无人形骨骼的模型（如猫咪）捧物挂这里
        public float turnSpeed = 12f;     // 转身平滑度

        /// <summary>是否叼着/抱着物品（猫咪外观动画读取此状态）。</summary>
        public bool IsCarrying => heldItem != null;

        [Header("抓取")]
        public MapLoader mapLoader;       // 木箱/出餐口列表来源
        public float grabRange = 3f;      // 距离箱子/出餐口多远以内可以交互
        public float pickupRange = 1.5f;  // 拾取地面物品的距离（要贴近才捡）
        public float itemRadius = 0.18f;  // 手持物品目标半径：模型最大边长归一化到 itemRadius*2，IK 抓握距离也按它算

        private Rigidbody rb;
        private GameObject heldItem;      // 手上的物品（挂在胸部骨骼上，null = 空手）
        private string heldObjectId;      // 手持物品的物品 id（object.json，交付时与任务卡匹配）
        private TaskCardsUI taskCards;    // 交付时延迟查找（场景重启后自动重找）

        private static Material itemMaterial;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;   // 人形不翻滚
        }

        private void FixedUpdate()
        {
            Vector2 input = VirtualJoystick.InputVector;
            if (input.sqrMagnitude < 0.01f) return;

            Vector3 move = CameraRelativeDirection(input) * moveSpeed;
            rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
        }

        private void Update()
        {
            Vector2 input = VirtualJoystick.InputVector;
            bool moving = input.sqrMagnitude > 0.01f;

            // 朝向：有输入时模型转向移动方向
            if (moving && modelPivot != null)
            {
                Vector3 dir = CameraRelativeDirection(input);
                Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                modelPivot.rotation = Quaternion.Slerp(modelPivot.rotation, target, turnSpeed * Time.deltaTime);
            }

            // 动画：MoveSpeed 驱动站立/行走，IsCarry 驱动空手/捧物两个分支
            if (animator != null)
            {
                animator.SetFloat("MoveSpeed", moving ? 1f : 0f, 0.1f, Time.deltaTime);
                animator.SetBool("IsCarry", heldItem != null);
            }
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            Vector3 camForward = Vector3.up;
            Vector3 camRight = Vector3.right;
            if (followCamera != null)
            {
                Vector3 f = followCamera.transform.forward;
                Vector3 r = followCamera.transform.right;
                f.y = 0f; r.y = 0f;
                if (f.sqrMagnitude > 0.001f) camForward = f.normalized;
                if (r.sqrMagnitude > 0.001f) camRight = r.normalized;
            }
            return (camRight * input.x + camForward * input.y).normalized;
        }

        // ---------------- 抓取 / 放置 / 拾取 / 交付 ----------------

        /// <summary>
        /// 交互按钮回调：按状态分流。
        /// 手持 → 出餐口旁交付，否则原地放下；空手 → 捡起地面物品（优先），没有才从木箱拿。
        /// </summary>
        public void TryGrab()
        {
            if (heldItem != null)
            {
                if (mapLoader == null) return;
                if (FindNearestServingStation() != null) { TryServe(); return; }   // 出餐优先
                DropHeldItem();                                                    // 就地放下
                return;
            }

            if (mapLoader == null) return;   // 不判空 animator：猫咪路径只有嘴部锚点

            WorldItem ground = FindNearestGroundItem();
            if (ground != null) { PickUp(ground); return; }    // 地上物品优先于木箱

            MapLoader.BoxSlot slot = FindNearestBox();
            if (slot.box == null) return;                       // 附近没有木箱：不响应

            if (mouthAnchor != null) SpawnHeldItemOnAnchor(slot, mouthAnchor);
            else if (animator != null) SpawnHeldItemOnChest(slot);
        }

        private MapLoader.BoxSlot FindNearestBox()
        {
            var nearest = new MapLoader.BoxSlot();
            float best = grabRange * grabRange;
            foreach (var slot in mapLoader.Boxes)
            {
                if (slot.box == null) continue;
                float dxz = Vector2.SqrMagnitude(new Vector2(
                    slot.box.position.x - transform.position.x,
                    slot.box.position.z - transform.position.z));
                if (dxz <= best) { best = dxz; nearest = slot; }
            }
            return nearest;
        }

        // 猫咪等无骨骼模型：食材模型挂嘴部锚点
        private void SpawnHeldItemOnAnchor(MapLoader.BoxSlot slot, Transform anchor)
        {
            BuildHeldItem(slot);
            heldItem.transform.SetParent(anchor, false);
            heldItem.transform.localPosition = Vector3.zero;
        }

        // 人形模型：食材模型挂在胸部骨骼前（世界位置保持，随骨骼摆动）
        private void SpawnHeldItemOnChest(MapLoader.BoxSlot slot)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) return;

            BuildHeldItem(slot);
            heldItem.transform.position = chest.position
                                         + modelPivot.forward * 0.35f
                                         + Vector3.up * 0.1f;
            heldItem.transform.SetParent(chest, true);           // 跟随胸部骨骼
        }

        private void BuildHeldItem(MapLoader.BoxSlot slot)
        {
            heldObjectId = slot.objectId;                       // 记录拿到的物品 id（交付时与任务卡匹配）
            heldItem = slot.itemModel != null
                ? Instantiate(slot.itemModel)                   // 图例 model 字段指定的食材模型
                : CreateFallbackSphere();                       // 模型缺失时的兜底（导入器通常已保证存在）

            heldItem.name = "HeldItem";
            foreach (var col in heldItem.GetComponentsInChildren<Collider>())
                Object.Destroy(col);                            // 纯视觉挂件，不参与物理

            FixMissingTextures(heldItem, slot.fallbackMaterial);
            NormalizeSize(heldItem, itemRadius * 2f);           // 不管 fbx 原始多大，统一缩到手持尺寸
        }

        private Transform FindNearestServingStation()
        {
            Transform nearest = null;
            float best = grabRange * grabRange;
            foreach (var station in mapLoader.ServingStations)
            {
                if (station == null) continue;
                float dxz = Vector2.SqrMagnitude(new Vector2(
                    station.position.x - transform.position.x,
                    station.position.z - transform.position.z));
                if (dxz <= best) { best = dxz; nearest = station; }
            }
            return nearest;
        }

        // ---------------- 放置 / 拾取 ----------------

        /// <summary>就近找地面物品（WorldItem.All 登记，pickupRange 内）。</summary>
        private WorldItem FindNearestGroundItem()
        {
            WorldItem nearest = null;
            float best = pickupRange * pickupRange;
            foreach (var item in WorldItem.All)
            {
                if (item == null) continue;
                float dxz = Vector2.SqrMagnitude(new Vector2(
                    item.transform.position.x - transform.position.x,
                    item.transform.position.z - transform.position.z));
                if (dxz <= best) { best = dxz; nearest = item; }
            }
            return nearest;
        }

        /// <summary>
        /// 把手里的物品原地放下：脱离骨骼并物理化（包围盒碰撞体 + 刚体），
        /// 自由落体允许翻滚，落地后成为 WorldItem 可再拾取。物品 id 原样保留。
        /// </summary>
        private void DropHeldItem()
        {
            GameObject item = heldItem;
            string objectId = heldObjectId;
            heldItem = null;
            heldObjectId = null;

            item.transform.SetParent(null, true);   // 脱离骨骼，保留世界位姿

            // 稍微向前送出，避免落在玩家碰撞体内被挤开
            Vector3 forward = modelPivot != null ? modelPivot.forward : transform.forward;
            item.transform.position += forward * 0.4f;

            var col = item.AddComponent<BoxCollider>();
            Bounds local = ComputeRoughLocalBounds(item);
            col.center = local.center;
            col.size = local.size;

            var rb = item.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.AddForce((forward + Vector3.up * 0.5f) * 1.2f, ForceMode.VelocityChange);   // 一点抛掷感
            rb.AddTorque(new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), Random.Range(-2f, 2f)),
                ForceMode.VelocityChange);                                                  // 自由翻滚

            var worldItem = item.AddComponent<WorldItem>();
            worldItem.objectId = objectId;
            item.name = "GroundItem";
        }

        /// <summary>拾取地上的物品：清掉物理组件挂回胸口骨骼，恢复手持状态（含物品 id）。</summary>
        private void PickUp(WorldItem ground)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) return;

            GameObject item = ground.gameObject;
            string objectId = ground.objectId;
            Destroy(ground);                                    // 先移出地面物品登记

            foreach (var col in item.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in item.GetComponentsInChildren<Rigidbody>()) Destroy(rb);

            item.name = "HeldItem";
            heldObjectId = objectId;
            heldItem = item;
            item.transform.position = chest.position
                                     + modelPivot.forward * 0.35f
                                     + Vector3.up * 0.1f;
            item.transform.SetParent(chest, true);
            item.transform.localRotation = Quaternion.identity; // 恢复直立持握，和从箱子拿一致
        }

        /// <summary>所有子网格本地包围盒的粗略合并（放置时的物理近似，不做矩阵精确变换）。</summary>
        private static Bounds ComputeRoughLocalBounds(GameObject root)
        {
            bool hasAny = false;
            var bounds = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                if (!hasAny) { bounds = mf.sharedMesh.bounds; hasAny = true; }
                else bounds.Encapsulate(mf.sharedMesh.bounds);
            }
            return hasAny ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.2f);
        }

        /// <summary>
        /// 手持物品在出餐口交付：物品必定被收走；命中任务卡才算完成，未命中就是白交。
        /// </summary>
        private void TryServe()
        {
            if (mapLoader == null) return;
            Transform station = FindNearestServingStation();
            if (station == null) return;                        // 附近没有出餐口：无处安放，忽略

            string objectId = heldObjectId;
            ClearHeldItem();                                    // 物品被收走（无论是否命中任务）

            if (taskCards == null) taskCards = FindFirstObjectByType<TaskCardsUI>();
            bool served = taskCards != null && taskCards.TryServe(objectId);
            Debug.Log(served
                ? $"[Player] 交付 {objectId}：任务完成"
                : $"[Player] 交付 {objectId}：没有任务需要它，白交了");
        }

        private void ClearHeldItem()
        {
            if (heldItem != null) Destroy(heldItem);
            heldItem = null;
            heldObjectId = null;
        }

        /// <summary>图例缺模型时的兜底：原来的绿色小球。</summary>
        private GameObject CreateFallbackSphere()
        {
            if (itemMaterial == null)
                itemMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.75f, 0.3f) };

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().sharedMaterial = itemMaterial;
            sphere.transform.localScale = Vector3.one * itemRadius * 2f;
            return sphere;
        }

        /// <summary>
        /// fbx 引用的外部贴图缺失时（导入后材质没有贴图）渲染会发白，
        /// 用导入器按图例图片烘焙的兜底材质补上；自带贴图的模型不受影响。
        /// </summary>
        private static void FixMissingTextures(GameObject root, Material fallback)
        {
            if (fallback == null) return;
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                var mats = rend.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].mainTexture != null) continue;
                    mats[i] = fallback;
                    changed = true;
                }
                if (changed) rend.sharedMaterials = mats;
            }
        }

        /// <summary>把 root 的整体包围盒最大边长等比缩放到 targetSize（不改朝向和比例）。</summary>
        private static void NormalizeSize(GameObject root, float targetSize)
        {
            Bounds bounds = ComputeBounds(root);
            float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxDim > 1e-4f)
                root.transform.localScale *= targetSize / maxDim;
        }

        /// 合并所有子网格的包围盒（世界空间）。fbx 根节点常带缩放/旋转偏移，须走矩阵变换而非直接取 size。
        private static Bounds ComputeBounds(GameObject root)
        {
            bool hasAny = false;
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var b = TransformBounds(mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
                if (!hasAny) { bounds = b; hasAny = true; }
                else bounds.Encapsulate(b);
            }
            return hasAny ? bounds : new Bounds(root.transform.position, new Vector3(0.2f, 0.2f, 0.2f));
        }

        /// AABB 经矩阵（可含旋转/非均匀缩放）变换：变换 8 个角点后重新包络。
        private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            Vector3 center = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            var ext = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
            return new Bounds(center, ext * 2f);
        }

        /// <summary>由模型实例上的 PlayerHandIK 在 OnAnimatorIK 中调用（IK 必须与 Animator 同节点）。</summary>
        public void ApplyHandIK(Animator anim)
        {
            if (heldItem == null)
            {
                anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }

            Vector3 forward = modelPivot != null ? modelPivot.forward : transform.forward;
            Vector3 right = modelPivot != null ? modelPivot.right : transform.right;
            Vector3 center = heldItem.transform.position;
            float grip = itemRadius * 0.9f;

            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, center - right * grip);
            anim.SetIKPosition(AvatarIKGoal.RightHand, center + right * grip);

            // 手掌朝向：顺着身体朝向轻扶球体
            Quaternion gripRot = Quaternion.LookRotation(forward, Vector3.up);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.7f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0.7f);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, gripRot);
            anim.SetIKRotation(AvatarIKGoal.RightHand, gripRot);
        }
    }
}
