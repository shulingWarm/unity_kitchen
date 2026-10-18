using RollABall;
using UnityEditor;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给地板材质（Assets/Materials/M_Ground.mat）铺上厨房地板贴图。
    /// 供编辑器菜单和 batchmode -executeMethod 调用；MapLoader 运行时会按格子数设置平铺。
    /// </summary>
    public static class FloorTexturePatcher
    {
        private const string TexturePath = "Assets/Textures/Floor_Kitchen.jpg";
        private const string GroundMatPath = "Assets/Materials/M_Ground.mat";

        [MenuItem("Tools/Roll-a-Ball/6 - 给地板应用厨房贴图")]
        public static void ApplyFloorTexture()
        {
            ApplyFloorTextureToMaterial();
        }

        // 供 DemoSceneBuilder 重建场景后调用
        public static bool ApplyFloorTextureToMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex == null)
            {
                Debug.LogWarning($"[FloorTexturePatcher] 未找到地板贴图 {TexturePath}");
                return false;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
            if (mat == null)
            {
                Debug.LogWarning($"[FloorTexturePatcher] 未找到地板材质 {GroundMatPath}");
                return false;
            }

            mat.mainTexture = tex;
            mat.color = Color.white; // 去掉旧材质的绿色染色，还原贴图本色
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FloorTexturePatcher] 已将厨房地板贴图应用到 {GroundMatPath}（平铺由 MapLoader 按格子数设置）");
            return true;
        }
    }
}
