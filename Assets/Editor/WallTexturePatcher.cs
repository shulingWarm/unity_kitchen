using UnityEditor;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 给墙块桌子（地图矩阵 '1' 的位置，共用 M_Wall 材质）铺上木纹桌布贴图，
    /// 并把材质的灰色染色重置为白色以还原贴图本色。幂等可反复执行。
    /// </summary>
    public static class WallTexturePatcher
    {
        private const string TexturePath = "Assets/Textures/Table_Wood.png";
        private const string WallMatPath = "Assets/Materials/M_Wall.mat";

        [MenuItem("Tools/Roll-a-Ball/11 - 给墙块桌子铺桌布贴图")]
        public static void ApplyTableTextureToWalls()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex == null) { Debug.LogError($"[WallTexturePatcher] 未找到桌布贴图 {TexturePath}"); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
            if (mat == null) { Debug.LogError($"[WallTexturePatcher] 未找到墙块材质 {WallMatPath}"); return; }

            mat.mainTexture = tex;
            mat.color = Color.white;   // 去掉灰色染色，还原贴图本色
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WallTexturePatcher] 已将桌布贴图应用到 {WallMatPath}（染色重置为白色）");
        }
    }
}
