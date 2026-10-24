using UnityEngine;

namespace RollABall
{
    /// <summary>
    /// 角色形象运行时替换：读取主菜单"选择角色"的存档（PlayerPrefs），
    /// 游戏场景加载时把 modelPivot 下的模型换成所选形象，并接上项目现成的
    /// 四状态动画控制器与手部 IK。三个形象均为人形骨架（动画通用）。
    /// 未选择时保留场景原样（builder 生成的形象）。
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [Header("三个可选形象")]
        public GameObject defaultPrefab;   // X Bot
        public GameObject chibiPrefab;     // 豆豆男孩
        public GameObject catPrefab;       // 小猫
        [Header("贴图（X Bot 用自带材质，无需贴图）")]
        public Texture2D chibiTexture;     // 豆豆男孩 basecolor
        public Texture2D catTexture;       // 小猫 basecolor
        public RuntimeAnimatorController controller;
        public Transform modelPivot;
        public PlayerController player;
        [Tooltip("builder 生成的默认模型实例（用于测量目标高度，可空）")]
        public GameObject defaultModel;

        private void Start()
        {
            string id = CharacterSelection.Get();
            if (string.IsNullOrEmpty(id)) return;   // 从未选择过：保留场景默认形象

            GameObject prefab = id == CharacterSelection.ChibiBoy ? chibiPrefab
                              : id == CharacterSelection.Cat ? catPrefab
                              : id == CharacterSelection.Default ? defaultPrefab
                              : null;
            if (prefab == null) { Debug.LogWarning($"[PlayerAppearance] 未知名形象 {id}，保留默认"); return; }
            SwapTo(prefab, id);
        }

        private void SwapTo(GameObject prefab, string id)
        {
            if (modelPivot == null || prefab == null) return;

            // 目标高度 = 当前模型包围盒高度（保持与物理胶囊一致）
            float targetHeight = 1.8f;
            var oldRenderers = modelPivot.GetComponentsInChildren<Renderer>();
            if (oldRenderers.Length > 0)
            {
                var ob = oldRenderers[0].bounds;
                for (int i = 1; i < oldRenderers.Length; i++) ob.Encapsulate(oldRenderers[i].bounds);
                if (ob.size.y > 0.01f) targetHeight = ob.size.y;
            }

            for (int i = modelPivot.childCount - 1; i >= 0; i--)
                Destroy(modelPivot.GetChild(i).gameObject);

            var model = Instantiate(prefab, modelPivot);
            model.name = prefab.name;

            // Animator 可能在根或子节点：用 GetComponentInChildren 找
            var anim = model.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                if (player != null) player.animator = anim;

                // 手部 IK 必须与 Animator 同节点
                if (anim.gameObject.GetComponent<PlayerHandIK>() == null)
                {
                    var handIK = anim.gameObject.AddComponent<PlayerHandIK>();
                    handIK.player = player;
                }
                Debug.Log($"[PlayerAppearance] 角色形象已切换为 {id}，动画已绑定");
            }
            else
            {
                // 静态模型（无骨骼，如 GLB 转换的猫咪）：程序化小跳动画 + 嘴部锚点叼球
                model.AddComponent<CatPlayerModel>();
                if (player != null) player.animator = null;
                Debug.Log($"[PlayerAppearance] 角色形象已切换为 {id}（静态模型：程序化动画）");
            }

            // 贴图接线：仅豆豆男孩需要（小猫 GLB/OBJ 自带 MTL 贴图，X Bot 用原生材质）
            if (id == CharacterSelection.ChibiBoy && chibiTexture != null)
            {
                var mat = new Material(Shader.Find("Standard")) { mainTexture = chibiTexture };
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = mat;
            }

            // 归一化：高度对齐 + 脚底贴地 + XZ 对中
            var renderers2 = model.GetComponentsInChildren<Renderer>();
            if (renderers2.Length > 0)
            {
                var b = renderers2[0].bounds;
                for (int i = 1; i < renderers2.Length; i++) b.Encapsulate(renderers2[i].bounds);
                if (b.size.y > 0.0001f)
                {
                    float scale = targetHeight / b.size.y;
                    model.transform.localScale = Vector3.one * scale;
                    b = renderers2[0].bounds;
                    for (int i = 1; i < renderers2.Length; i++) b.Encapsulate(renderers2[i].bounds);
                }
                var center = b.center;
                model.transform.position += new Vector3(
                    modelPivot.position.x - center.x,
                    modelPivot.position.y - b.min.y,
                    modelPivot.position.z - center.z);
            }

            // 静态模型：嘴部锚点（叼球用）
            if (anim == null && renderers2.Length > 0)
            {
                var tb = renderers2[0].bounds;
                for (int i = 1; i < renderers2.Length; i++) tb.Encapsulate(renderers2[i].bounds);
                var mouth = new GameObject("Mouth");
                mouth.transform.SetParent(model.transform, true);
                mouth.transform.position = new Vector3(tb.center.x, tb.min.y + tb.size.y * 0.72f, tb.max.z + 0.03f);
                if (player != null) player.mouthAnchor = mouth.transform;
            }
        }
    }
}
