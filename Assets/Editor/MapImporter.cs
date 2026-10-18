using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RollABall;
using UnityEditor;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 把工程根 maps/*.txt 解析校验后转成 Assets/Maps/*.asset（MapData）。
    /// 网格行：单元格用空白（tab/空格）分隔，token 为 0/1/p/wN/s1-s4；
    /// 兼容旧格式：整行无分隔符且长度等于列数时按单字符解析。
    /// 图例区：每个木箱类型三行——「wN:」单独一行，随后两个非空行依次是
    /// 图片路径（烘焙顶面材质）、fbx 模型路径（取出物品模型），均相对 Assets；
    /// 模型缺贴图时的兜底材质优先用其同目录的 colormap.png / 同名 png，否则用图例图片。
    /// 资产已存在时覆盖字段（保留 GUID，场景引用不失效）。
    /// </summary>
    public static class MapImporter
    {
        private const string AssetFolder = "Assets/Maps";
        private static readonly Regex BoxToken = new Regex(@"^w\d+$");
        private static readonly Regex LegendHeader = new Regex(@"^\s*(\S+)\s*:\s*$");

        [MenuItem("Tools/Roll-a-Ball/3 - 导入 maps/ 下的地图文件")]
        public static void ImportMenu()
        {
            ImportAll(out int ok, out int fail);
            EditorUtility.DisplayDialog("导入地图",
                fail == 0 ? $"成功导入 {ok} 张地图。" : $"成功 {ok} 张，失败 {fail} 张，详情见 Console。", "好的");
        }

        /// <summary>供场景生成器调用：导入全部地图并返回 map1.asset（不存在时为 null）。</summary>
        public static MapData ImportAllAndGetDefault()
        {
            ImportAll(out _, out _);
            return AssetDatabase.LoadAssetAtPath<MapData>(AssetFolder + "/map1.asset");
        }

        public static void ImportAll(out int okCount, out int failCount)
        {
            okCount = 0;
            failCount = 0;

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string srcDir = Path.Combine(projectRoot, "maps");
            if (!Directory.Exists(srcDir))
            {
                Debug.LogError($"[MapImporter] 找不到地图源文件夹：{srcDir}");
                return;
            }
            EnsureFolder(AssetFolder);

            foreach (string txtPath in Directory.GetFiles(srcDir, "*.txt"))
            {
                try
                {
                    MapData parsed = ParseFile(txtPath);
                    string assetPath = $"{AssetFolder}/{Path.GetFileNameWithoutExtension(txtPath)}.asset";
                    ApplyToAsset(parsed, assetPath);

                    okCount++;
                    Debug.Log($"[MapImporter] {assetPath} ← {Path.GetFileName(txtPath)}" +
                              $"（{parsed.rows}x{parsed.cols}，出生点 r{parsed.spawnCell.x} c{parsed.spawnCell.y}，" +
                              $"木箱类型 {parsed.blockTypes.Length} 种）");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"[MapImporter] {Path.GetFileName(txtPath)} 导入失败：{e.Message}");
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------- 解析 ----------------

        private static MapData ParseFile(string path)
        {
            var content = new List<string>(File.ReadAllLines(path));
            for (int i = 0; i < content.Count; i++) content[i] = content[i].TrimEnd().TrimEnd('\r');
            while (content.Count > 0 && content[^1].Length == 0) content.RemoveAt(content.Count - 1);
            if (content.Count == 0) throw new System.Exception("文件为空");

            // 第一行：行数 列数（容忍任意空白分隔）
            string[] head = content[0].Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (head.Length != 2 || !int.TryParse(head[0], out int rows) || !int.TryParse(head[1], out int cols))
                throw new System.Exception("第一行必须是「行数 列数」两个整数");
            if (rows <= 0 || cols <= 0) throw new System.Exception("行列数必须为正整数");

            // 网格行 / 图例行分界：网格恰好 rows 行，其后所有非空行都是图例
            if (content.Count - 1 < rows)
                throw new System.Exception($"标称 {rows} 行，但文件只有 {content.Count - 1} 行地图数据");

            var gridRows = new string[rows];
            var usedBoxTokens = new HashSet<string>();
            int pCount = 0;
            var spawn = new Vector2Int(-1, -1);

            for (int r = 0; r < rows; r++)
            {
                string line = content[r + 1];
                string[] tokens = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    throw new System.Exception($"第 {r + 2} 行是空行，网格行数不足 {rows}");

                // 旧格式兼容：无分隔符且每字符恰是一格
                if (tokens.Length == 1 && tokens[0].Length == cols)
                    tokens = tokens[0].Select(ch => ch.ToString()).ToArray();
                else if (tokens.Length != cols)
                    throw new System.Exception(
                        $"第 {r + 2} 行拆出 {tokens.Length} 个单元格，与标称列数 {cols} 不符（分隔符是否遗漏？）");

                var sb = new System.Text.StringBuilder();
                for (int c = 0; c < cols; c++)
                {
                    string t = tokens[c].ToLowerInvariant();
                    switch (t)
                    {
                        case "0":
                        case "1":
                        case "s":    // 出餐口简写 = s1（朝上）
                        case "s1":   // 出餐口朝上
                        case "s2":   // 出餐口朝下
                        case "s3":   // 出餐口朝左
                        case "s4":   // 出餐口朝右
                            break;
                        case "p":
                            pCount++;
                            spawn = new Vector2Int(r, c);
                            t = "0";   // 出生点站在地面格上，归一化存储
                            break;
                        case "x":
                            throw new System.Exception(
                                $"第 {r + 2} 行第 {c + 1} 格的 'x'（收集物）已随胜利条件移除，请改为 0");
                        default:
                            if (!BoxToken.IsMatch(t))
                                throw new System.Exception(
                                    $"第 {r + 2} 行第 {c + 1} 格 '{tokens[c]}' 非法（只允许 0/1/p/wN/s1-s4）");
                            usedBoxTokens.Add(t);
                            break;
                    }
                    if (c > 0) sb.Append(' ');
                    sb.Append(t);
                }
                gridRows[r] = sb.ToString();
            }

            if (pCount > 1) throw new System.Exception($"p 出现了 {pCount} 次，最多只能有 1 个");
            if (pCount == 0)
            {
                for (int r = 0; r < rows && spawn.x < 0; r++)
                {
                    string[] toks = gridRows[r].Split(' ');
                    for (int c = 0; c < cols; c++)
                        if (toks[c] == "0") { spawn = new Vector2Int(r, c); break; }
                }
                if (spawn.x < 0) throw new System.Exception("地图里没有可站立的地面格（至少需要一个 0 或 p）");
            }

            // ---------------- 图例区 ----------------
            // 新格式：每个类型三行——「wN:」单独一行，随后两个非空行依次是 图片路径、模型路径
            var types = new List<string>();
            var images = new List<string>();
            var materials = new List<Material>();
            var models = new List<GameObject>();
            var itemMaterials = new List<Material>();

            // 所有木箱共用的侧面材质（纯色木棕）
            Material sideMat = GetOrCreatePlainMaterial($"{AssetFolder}/BoxSide.mat", new Color(0.55f, 0.36f, 0.20f));

            for (int i = rows + 1; i < content.Count; )
            {
                string line = content[i];
                if (line.Length == 0) { i++; continue; }

                Match m = LegendHeader.Match(line);
                if (!m.Success)
                    throw new System.Exception(
                        $"图例区第 {i + 1} 行 '{line}' 应为「wN:」（单独一行，后面跟图片路径、模型路径两行）。" +
                        "旧的单行「wN: 图片路径」格式已不再支持");

                string token = m.Groups[1].Value.ToLowerInvariant();
                if (!BoxToken.IsMatch(token))
                    throw new System.Exception($"图例 token '{token}' 非法（应为 wN 形式，如 w1）");

                // 收集本图例的两个字段（容忍空行）：字段 1 = 图片，字段 2 = 模型
                var fields = new string[2];
                int found = 0;
                i++;
                while (i < content.Count && found < 2)
                {
                    string field = content[i].Trim();
                    if (field.Length == 0) { i++; continue; }
                    if (LegendHeader.IsMatch(field))
                        throw new System.Exception(
                            $"图例 {token} 字段不全：第 {i + 1} 行出现了下一个「wN:」，缺少图片/模型路径");
                    fields[found++] = field;
                    i++;
                }
                if (found < 2)
                    throw new System.Exception($"图例 {token} 字段不全：需要「wN:」+ 图片路径 + 模型路径 共 3 行");

                // 字段 1：图片 → 顶面材质（相对 Assets 解析）
                string texPath = "Assets/" + fields[0].Replace('\\', '/');
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                    throw new System.Exception($"图例 {token} 的图片不存在：{texPath}");

                // 字段 2：模型 → 取出物品的 prefab
                string modelPath = "Assets/" + fields[1].Replace('\\', '/');
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                    throw new System.Exception(
                        $"图例 {token} 的模型不存在或尚未被 Unity 导入：{modelPath}（fbx 需放在 Assets 下）");

                types.Add(token);
                images.Add(fields[0]);
                materials.Add(GetOrCreateTopMaterial($"{AssetFolder}/BoxTop_{token}.mat", tex));
                models.Add(model);
                itemMaterials.Add(GetOrCreateTopMaterial(
                    $"{AssetFolder}/BoxItem_{token}.mat", FindItemTexture(modelPath, texPath)));
            }

            foreach (string used in usedBoxTokens)
                if (!types.Contains(used))
                    throw new System.Exception($"网格使用了 {used}，但图例区没有它的定义（wN: + 图片路径 + 模型路径）");

            var asset = ScriptableObject.CreateInstance<MapData>();
            asset.rows = rows;
            asset.cols = cols;
            asset.gridRows = gridRows;
            asset.spawnCell = spawn;
            asset.blockTypes = types.ToArray();
            asset.blockImages = images.ToArray();
            asset.blockMaterials = materials.ToArray();
            asset.blockModels = models.ToArray();
            asset.blockItemMaterials = itemMaterials.ToArray();
            asset.blockSideMaterial = sideMat;
            return asset;
        }

        // ---------------- 材质烘焙 ----------------

        /// <summary>
        /// 食材模型缺贴图时的兜底贴图：优先模型同目录的 colormap.png（低模资源包的惯用命名），
        /// 其次与模型同名的 png，最后才回退图例图片（箱顶贴图）。
        /// </summary>
        private static Texture2D FindItemTexture(string modelPath, string legendTexPath)
        {
            string modelDir = Path.GetDirectoryName(modelPath)!.Replace('\\', '/');
            string modelBase = Path.GetFileNameWithoutExtension(modelPath);
            var candidates = new[]
            {
                $"{modelDir}/colormap.png",
                $"{modelDir}/{modelBase}.png",
            };
            foreach (string path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(legendTexPath);
        }

        private static Material GetOrCreateTopMaterial(string matPath, Texture2D topTexture)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = topTexture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetOrCreatePlainMaterial(string matPath, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { color = color };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        // ---------------- 资产写入 ----------------

        private static void ApplyToAsset(MapData parsed, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<MapData>(assetPath);
            if (existing != null)
            {
                existing.rows = parsed.rows;
                existing.cols = parsed.cols;
                existing.gridRows = parsed.gridRows;
                existing.spawnCell = parsed.spawnCell;
                existing.blockTypes = parsed.blockTypes;
                existing.blockImages = parsed.blockImages;
                existing.blockMaterials = parsed.blockMaterials;
                existing.blockModels = parsed.blockModels;
                existing.blockItemMaterials = parsed.blockItemMaterials;
                existing.blockSideMaterial = parsed.blockSideMaterial;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(parsed, assetPath);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
